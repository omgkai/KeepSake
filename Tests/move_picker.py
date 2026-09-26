#!/usr/bin/env python3
"""Move choices, types across generations, same-set feedback and atomic draft+suggest."""
import json,pathlib,subprocess,sys
helper,fixtures,scratch=map(pathlib.Path,sys.argv[1:4]);scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);checks=0

def req(op,expected=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==expected,(op,kw,r.get('error'));checks+=1;return r.get('data')
def moves(s):return [m['move'] for m in s['moveChecks'] if not m['relearn']]
def fields(s):return {f['id']:f['value'] for f in s['fields']}
try:
 for game in ['RD','C','E','B2','X','SW','PLA','SL','ZA']:
  before=req('demo',version=game);catalog=req('moveChoices');assert req('state')==before
  rows={e['id']:e for e in catalog['entries']};assert rows[0]['status']=='empty' and any(e['status']=='learnable' for e in rows.values())
  assert rows[85]['typeName']=='Electric' and rows[85]['type']==12
  assert rows[44]['type']==(0 if game=='RD' else 16)
  if game not in ['RD','C','E']:assert rows[204]['type']==(0 if game=='B2' else 17)
  assert all(0<=e['type']<18 and e['pp']>=0 for e in rows.values())
  assert rows[85]['status']=='learnable' and rows[53]['status']=='unavailable' # Pikachu can learn Thunderbolt, not Flamethrower.
  assert catalog['entries'][0]['id']==0
  print('PASS',game,'move types and PKHeX learnability',flush=True)
 # Move lookup accounts for drafts but doesn't commit them.
 before=req('state');draft=req('moveChoices',edits=[dict(field='Species',value='6')]);assert draft['species']=='Charizard'
 assert next(e for e in draft['entries'] if e['id']==53)['status']=='learnable';assert req('state')==before
 req('moveChoices',edits=[dict(field='CurrentLevel',value='999')],expected=False);assert req('state')==before
 # Same legal set used to silently return unchanged on every click.
 source=next(x for x in sorted(fixtures.rglob('*.pa8')) if 'Legal' in x.parts)
 before=req('open',path=str(source));original=source.read_bytes()
 a=req('suggestMoves',mode='level');b=req('suggestMoves')
 assert sorted(moves(a))!=sorted(moves(b)),(moves(a),moves(b),b['suggestionMessage'])
 assert b['suggestionMessage'].startswith('Moves updated:') and all(c['status'] in ['legal','empty'] for c in b['moveChecks'] if not c['relearn'])
 assert req('undo')['moveChecks']==a['moveChecks'];assert req('redo')['moveChecks']==b['moveChecks']
 # Typed values and the generated set are one undoable edit.
 old=req('state');s=req('suggestMoves',edits=[dict(field='Nickname',value='TypedMove'),dict(field='CurrentLevel',value='75')])
 assert fields(s)['Nickname']=='TypedMove' and fields(s)['CurrentLevel']=='75' and s['suggestionMessage']
 assert req('undo')['fields']==old['fields'];req('redo')
 old=req('state');req('suggestMoves',edits=[dict(field='Nickname',value='Lost'),dict(field='CurrentLevel',value='999')],expected=False);assert req('state')==old
 # Unchanged relearn results are explicit and don't add an empty undo operation.
 req('suggestMoves',mode='relearn');before=req('state');same=req('suggestMoves',mode='relearn')
 assert same['suggestionMessage']=='The relearn list already matches this encounter.' and same['revision']==before['revision'] and same['fields']==before['fields']
 assert source.read_bytes()==original
 print('PASS',checks,'move-picker and suggestion regression operations',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
