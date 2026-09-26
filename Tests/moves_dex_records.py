#!/usr/bin/env python3
"""Move suggestions, per-slot legality, game identity and detailed BDSP/SV dex records."""
import json,pathlib,subprocess,sys
helper,fixtures,saves,scratch=map(pathlib.Path,sys.argv[1:5]);scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);checks=0

def req(op,expected=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());checks+=1
 assert r['ok']==expected,(op,kw,r.get('error'));return r.get('data')
def fields(s):return {f['id']:f['value'] for f in s['fields']}
def moves(s):return [c for c in s['moveChecks'] if not c['relearn']]
try:
 req('suggestMoves',expected=False)
 for ext in ['pk1','pk3','pk6','pk8','pb8','pa8','pk9']:
  source=next(x for x in sorted(fixtures.rglob('*.'+ext)) if 'Legal' in x.parts and 'Egg' not in str(x) and 'egg' not in str(x))
  original=source.read_bytes();before=req('open',path=str(source));assert before['gameVersion']==''
  assert len(moves(before))==4 and all(c['status'] in ['legal','empty'] for c in moves(before)),(ext,source,before['moveChecks'])
  # A duplicate move must get a red/illegal result and an explanation.
  move=fields(before)['Move1'];bad=req('entityEdit',edits=[dict(field='Move2',value=move)],apply=False)
  assert any(c['status']=='illegal' and c['detail'] for c in moves(bad)),(ext,bad['moveChecks'])
  assert req('undo')['moveChecks']==before['moveChecks']
  suggested=req('suggestMoves');assert suggested['pending'] and all(c['status'] in ['legal','empty'] for c in moves(suggested)),(ext,suggested['moveChecks'])
  vals=fields(suggested);assert all(vals['Move'+str(i)+'_PPUps']=='0' for i in range(1,5))
  assert all(int(vals['Move'+str(i)+'_PP'])>0 for i in range(1,5) if vals['Move'+str(i)]!='0')
  assert req('undo')['moveChecks']==before['moveChecks'];assert req('redo')['moveChecks']==suggested['moveChecks']
  out=scratch/('suggested.'+ext);req('exportEntity',path=str(out));reopened=req('open',path=str(out));assert reopened['moveChecks']==suggested['moveChecks'] and source.read_bytes()==original
  if ext in ['pk6','pk8','pb8','pa8','pk9']:
   old=req('state');s=req('suggestMoves',mode='relearn');assert s['suggestionMessage'] and len(s['moveChecks'])==8
   if s['revision']!=old['revision']:req('undo')
  else:req('suggestMoves',mode='relearn',expected=False)
  req('suggestMoves',mode='invalid',expected=False)
  print('PASS',ext,'move status, suggestions, undo/redo and file roundtrip',flush=True)
 # Save identity comes from the save, never the selected Pokémon's origin.
 for game in ['BD','SL','VL','SW','PLA','ZA']:
  s=req('demo',version=game);assert s['gameVersion']==game
  req('entityEdit',edits=[dict(field='Version',value='24')],apply=False)
  assert req('state')['gameVersion']==game
  req('undo')
  before=req('state');s=req('suggestMoves');assert s['slots']==before['slots'];req('undo')
 # Detailed dex writes preserve other fields and species and support undo/redo.
 for game,sp in [('BD',201),('SL',25),('SL',1024)]:
  before=req('demo',version=game);data=req('dexRecord',species=sp);neighbor=req('dexRecord',species=sp-1)
  for field in data['fields']:
   original=req('dexRecord',species=sp);expected=fields(original)
   value=('false' if field['value']=='true' else 'true') if field['kind']=='bool' else next((c['value'] for c in field['choices'] if c['value']!=field['value']),field['value'])
   s=req('dexRecordSet',species=sp,field=field['id'],value=value);assert s['dirty'] and s['slots']==before['slots']
   expected[field['id']]=value;assert fields(req('dexRecord',species=sp))==expected,field
   # Dex unlocks are global; all other fields stay local to their species.
   if field['id'] not in ['regional','national']:assert req('dexRecord',species=sp-1)==neighbor
   req('undo');assert req('dexRecord',species=sp)==original
   req('redo');assert fields(req('dexRecord',species=sp))[field['id']]==value
   req('undo')
  for key,value in [('language.6','true'),('state','99'),('form.99.seen','true'),('gender.9','true'),('model.shiny','yes')]:
   req('dexRecordSet',species=sp,field=key,value=value,expected=False)
  for bad in [0,-1,9999]:req('dexRecord',species=bad,expected=False)
  print('PASS',game,sp,len(data['fields']),'independent dex fields',flush=True)
 for game in ['BD']:
  source=saves/(game+'.sav');original=source.read_bytes();req('open',path=str(source));req('dexRecordSet',species=25,field='language.5',value='true')
  before=req('dexRecord',species=25);out=scratch/(game+'-dex.sav');req('exportSave',path=str(out));assert req('open',path=str(out))['checksumValid'];assert req('dexRecord',species=25)==before;assert source.read_bytes()==original
 req('demo',version='PLA');req('dexRecord',species=25,expected=False)
 print('PASS',checks,'move/dex/game-identity protocol operations',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
