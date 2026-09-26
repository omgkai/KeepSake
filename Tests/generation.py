#!/usr/bin/env python3
"""Exercise actual isolated Auto-Legality searches and atomic journal team workflows."""
import base64,json,pathlib,subprocess,sys,time
helper=pathlib.Path(sys.argv[1]).resolve();fixtures=pathlib.Path(sys.argv[2]).resolve();out=pathlib.Path(sys.argv[3]).resolve();out.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);checks=0

def req(op,ok=True,**args):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**args))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());checks+=1
 assert r['ok']==ok,(op,r)
 return r.get('data') if ok else r['error']
def page(species=25,shiny=False):return dict(species=species,shiny=shiny,name='Companion')
def snapshot(state):return dict(data=state['entityData'],extension=state['entityExtension'],name=state['entityName'])
def stripped(slots):return [{k:v for k,v in s.items() if k!='pokemonData'} for s in slots]
try:
 # Every generation plus distinct modern formats, alternate game versions and shininess.
 for game in ['RD','C','E','FR','Pt','HG','B2','X','AS','SN','US','GP','SW','BD','PLA','SL','VL','ZA']:
  start=req('demo',version=game)
  preview=req('teamPreview',members=[page()]);assert preview['ready'],(game,preview)
  assert req('state')==start
  placed=req('teamPlace',token=preview['token'],box=1);assert placed['dirty'] and placed['legality']=='valid'
  assert placed['entityExtension']==preview['entries'][0]['extension']
  raw=snapshot(placed)
  # Snapshot -> same game -> checked legal, preserving actual Pokémon identity.
  copy=req('teamPreview',members=[raw]);assert copy['ready'],(game,copy)
  assert req('state')==placed
  original=req('undo');assert stripped(original['slots'])==stripped(start['slots']) and original['dirty']==start['dirty']
  req('teamPlace',ok=False,token=copy['token'],box=2)
  shiny=req('teamPreview',members=[page(shiny=True)]);assert shiny['ready'],(game,shiny)
  assert shiny['entries'][0]['sprite'].endswith('s'),(game,shiny)
  print('PASS',game,'legal generation, snapshot reuse, shiny generation, preview purity and Undo',flush=True)
 # Real fixture semantics: export, reopen files, preserved originals, multiple teams and stale previews.
 source=pathlib.Path(sys.argv[4]).resolve() if len(sys.argv)>4 else fixtures/'X.sav';before=source.read_bytes();state=req('open',path=str(source))
 preview=req('teamPreview',members=[page(25),page(133,True),page(1)])
 assert preview['ready'],preview
 export=req('teamExport',token=preview['token'],path=str(out));folder=pathlib.Path(export['path']);files=sorted(folder.iterdir());assert len(files)==3
 assert req('state')==state and source.read_bytes()==before
 result=req('teamPlace',token=preview['token'],box=1);assert result['dirty'] and result['legality']=='valid';assert sum(not x['empty'] for x in result['slots'])==3
 req('teamPlace',ok=False,token=preview['token'],box=1)
 undone=req('undo');assert stripped(undone['slots'])==stripped(state['slots'])
 # Save bytes after Undo must match the same normalized export before mutation.
 req('exportSave',path=str(out/'undo.sav'))
 req('open',path=str(source));req('exportSave',path=str(out/'before.sav'))
 assert (out/'undo.sav').read_bytes()==(out/'before.sav').read_bytes()
 for f in files:
  got=req('open',path=str(f));assert got['legality']=='valid',got['report']
 # Auto-Legality repairs invalid encounter met location, with review, guarded apply and undo.
 got=req('open',path=str(files[0]));originalData=got['entityData']
 bad=req('entitySet',field='MetLocation',value='65535');assert bad['legality']=='invalid'
 fixed=req('legalityPreview');assert fixed['ready'] and fixed['entries'][0]['changes'];assert req('state')==bad
 applied=req('legalityApply',token=fixed['token']);assert applied['legality']=='valid' and applied['pending']
 req('exportEntity',ok=False,path=str(files[0]))
 restored=req('undo');assert restored['entityData']==bad['entityData']
 req('legalityApply',ok=False,token=fixed['token'])
 # Selection changes do not increment revision: fingerprint must still reject stale legality preview.
 req('open',path=str(source));v=req('teamPreview',members=[page()]);req('teamPlace',token=v['token'],box=1)
 v=req('legalityPreview');req('select',box=0,slot=0);req('legalityApply',ok=False,token=v['token'])
 # Failed preview discards previous candidates, never applies partial teams.
 start=req('demo',version='X');good=req('teamPreview',members=[page()]);bad=req('teamPreview',members=[page(),page(1025)])
 assert not bad['ready'] and req('state')==start
 req('teamPlace',ok=False,token=good['token'],box=1);req('teamPlace',ok=False,token=bad['token'],box=1)
 for members in [[],[page()]*7,[dict(data='bad',extension='pk9')]]:req('teamPreview',ok=bool(members) and len(members)<=6,members=members)
 # Full box rejection and preservation (six member teams, repeated preparation).
 start=req('demo',version='X');v=req('teamPreview',members=[page()]*6);assert v['ready']
 for i in range(5):
  if i:v=req('teamPreview',members=[page()]*6)
  req('teamPlace',token=v['token'],box=1)
 full=req('state');v=req('teamPreview',members=[page()]);req('teamPlace',ok=False,token=v['token'],box=1);assert req('state')==full
 assert source.read_bytes()==before
 print('PASS team file export/reopen, save-byte Undo roundtrip, encounter repair, stale tokens/selection, unavailable species, invalid inputs and full-box atomic rejection')
 print('PASS',checks,'protocol checks')
finally:p.stdin.close();p.wait(timeout=10)
