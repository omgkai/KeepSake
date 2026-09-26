#!/usr/bin/env python3
"""National Dex coverage, representative forms, and readback; renderer resolution is checked by Swift."""
import pathlib,sys,json,subprocess
helper,assets,scratch=map(pathlib.Path,sys.argv[1:4]);scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);checks=0;rows=[];editable=set()
def req(op,**kw):
 global checks,editable
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok'],(op,kw,r.get('error'));checks+=1
 if checks%200==0: print('Checked',checks,flush=True)
 if isinstance(r['data'],dict) and 'fields' in r['data']:editable={f['id'] for f in r['data']['fields'] if f['editable']}
 return r['data']
def edit(**values):return req('entityEdit',edits=[dict(field=k,value=str(v).lower()) for k,v in values.items() if k in editable or k=='IsShiny'])
def pair(species,form=0,gender=0,**kw):
 a=edit(Species=species,Form=form,Gender=gender,IsShiny=False,**kw)['entitySprite'];b=edit(IsShiny=True)['entitySprite']
 assert b==a+'s',(species,form,a,b);rows.append(dict(normal=a,shiny=b))
try:
 req('demo',version='SL')
 for sp in range(1,1026):pair(sp)
 for sp,form,gender in [(25,1,0),(128,1,0),(201,27,0),(386,3,0),(412,2,0),(479,5,0),(487,1,0),(492,1,0),(550,2,0),(666,19,0),(676,9,0),(710,3,0),(741,3,0),(745,2,0),(800,2,0),(849,1,0),(869,8,0),(888,1,0),(892,1,0),(905,1,0),(1017,3,0),(1024,2,0),(449,0,1),(450,0,1),(521,0,1),(592,0,1),(593,0,1),(668,0,1)]:pair(sp,form,gender)
 pair(869,0,0,FormArgument=6)
 req('demo',version='X');pair(25,1);pair(6,1);pair(6,2)
 req('demo',version='PLA')
 for sp in [59,101,549,713]:pair(sp,2)
 pair(900,1)
 # Earlier generations now update both the editor and stored slots.
 for game,sp in [('C',25),('E',282),('X',6),('US',722),('SW',831),('PLA',899)]:
  req('demo',version=game);pair(sp);s=req('apply');assert s['slots'][0]['sprite']==rows[-1]['shiny']
  before=req('state');edit(IsShiny=False);assert req('undo')['entitySprite']==before['entitySprite'];assert req('redo')['entitySprite']==rows[-1]['normal']
  req('undo');out=scratch/(game+'-shiny.'+before['entityExtension']);req('exportEntity',path=str(out));assert req('open',path=str(out))['entitySprite']==rows[-1]['shiny']
 (scratch/'sprite-pairs.json').write_text(json.dumps(rows))
 for entry in json.loads((assets/'shiny-sources.json').read_text()):assert (assets/'Sprites'/entry['file']).is_file()
 print('PASS',checks,'operations across all 1,025 species and',len(rows)-1025,'additional forms/genders/game contexts')
finally:p.stdin.close();p.wait(timeout=10)
