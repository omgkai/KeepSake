#!/usr/bin/env python3
"""Journal catalog is complete across games and cannot mutate the workspace."""
import json,pathlib,subprocess,sys
helper=pathlib.Path(sys.argv[1]);out=pathlib.Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
def req(op,**args):
 p.stdin.write(json.dumps(dict(op=op,**args))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok'],r;return r['data']
try:
 empty=req('state');catalog=req('lookup',kind='journalSpecies');assert len(catalog)==1026 and catalog[25]['label']=='Pikachu' and catalog[1025]['label']=='Pecharunt';assert req('state')==empty
 for game in ['RD','C','E','Pt','B2','X','AS','SN','US','GP','SW','BD','PLA','SL','ZA']:
  state=req('demo',version=game)
  before=out/(game+'-before.pk');after=out/(game+'-after.pk')
  req('exportEntity',path=str(before));assert req('lookup',kind='journalSpecies')==catalog;assert req('state')==state
  req('exportEntity',path=str(after));assert before.read_bytes()==after.read_bytes()
 print('PASS all 1,025 journal species without a save and across 15 game variants; state and Pokémon bytes unchanged')
finally:p.stdin.close();p.wait(timeout=10)
