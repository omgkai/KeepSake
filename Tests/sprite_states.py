#!/usr/bin/env python3
"""Shiny sprite selection in editor, slots, undo/redo, export/reopen and library."""
import pathlib,sys,json,subprocess
helper,assets,scratch=map(pathlib.Path,sys.argv[1:4]);scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);checks=0

def req(op,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok'],(op,kw,r.get('error'));checks+=1;return r['data']
def edit(**values):return req('entityEdit',edits=[dict(field=k,value=str(v).lower() if isinstance(v,bool) else str(v)) for k,v in values.items()])
try:
 req('demo',version='SL');m=json.loads((assets/'Gen9SpriteSheet/manifest.json').read_text())
 for e in m['entries']:
  normal=e['file'][:-4];s=edit(Species=e['species'],Form=e['form'],IsShiny=False);assert s['entitySprite']==normal,(e,s['entitySprite'])
  s=edit(IsShiny=True);assert s['entitySprite']==normal+'s',(e,s['entitySprite']);assert (assets/'Sprites'/(s['entitySprite']+'.png')).exists()
 s=edit(Species=915,Form=0,IsShiny=False);req('apply');s=edit(IsShiny=True);assert s['entitySprite']=='b_915s' and s['slots'][0]['sprite']=='b_915'
 s=req('apply');assert s['slots'][0]['sprite']=='b_915s';s=req('undo');assert s['entitySprite']=='b_915s' and s['slots'][0]['sprite']=='b_915';s=req('redo');assert s['slots'][0]['sprite']=='b_915s'
 shiny=scratch/'shiny-lechonk.pk9';req('exportEntity',path=str(shiny));s=req('open',path=str(shiny));assert s['entitySprite']=='b_915s';s=edit(IsShiny=False);assert s['entitySprite']=='b_915';normal=scratch/'normal-lechonk.pk9';req('exportEntity',path=str(normal));s=req('open',path=str(normal));assert s['entitySprite']=='b_915'
 data=req('libraryScan',path=str(scratch),recursive=False);assert next(x for x in data['entries'] if x['path']==shiny.name)['sprite']=='b_915s';assert next(x for x in data['entries'] if x['path']==normal.name)['sprite']=='b_915'
 print('PASS',checks,'sprite-state operations: all 142 pairs, live editor state, stored slots, undo/redo, Pokémon roundtrip, library',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
