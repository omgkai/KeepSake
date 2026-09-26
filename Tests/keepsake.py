#!/usr/bin/env python3
"""KeepSake UI contracts, game-specific dex flags, bulk mutations, random stats and hover data."""
import json,pathlib,subprocess,sys
helper=pathlib.Path(sys.argv[1]);p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);checks=0
def req(op,ok=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==ok,(op,kw,r.get('error'));checks+=1;return r.get('data')
def fields(s):return {f['id']:f['value'] for f in s['fields']}
try:
 for version in ['X','SN','US','GP','SW','BD','PLA','SL','ZA']:
  s=req('demo',version=version);assert s['canFashion'],version
  fashion=req('fashion');assert fashion['supported'];assert fashion['canUnlock']
  if fashion['canUnlock']:
   after=req('fashionUnlock');assert after['canUndo'] and after['dirty'];req('undo');assert not req('state')['dirty']
  if version=='PLA':assert fashion['fields']
  print('PASS',version,'fashion action and Undo',flush=True)
 for version in ['C','E','B2']:assert not req('demo',version=version)['canFashion']
 for version in ['SL','VL','BD','SW','ZA','PLA']:
  s=req('demo',version=version);assert s['canDex'];before=req('dex');assert before['supported'] and before['entries']
  row=next(x for x in before['entries'] if x['id']==25)
  changed=req('dexSet',species=25,seen=True,caught=True);assert next(x for x in changed['entries'] if x['id']==25)['caught']
  req('undo');assert req('dex')['entries']==before['entries']
  req('dexGiveAll');assert all(x['caught'] and x['seen'] for x in req('dex')['entries']);req('undo');assert req('dex')['entries']==before['entries']
  req('dexSet',species=25,seen=False,caught=False);row=next(x for x in req('dex')['entries'] if x['id']==25);assert not row['seen'] and not row['caught']
  print('PASS',version,'Pokédex flags, Give All and Undo',flush=True)
 for version in ['C','E','X','SL']:
  before=req('demo',version=version);s=req('entityAction',action='randomIV');f=fields(s);assert all(0<=int(f['IV_'+k])<=(15 if version=='C' else 31) for k in ['HP','ATK','DEF','SPA','SPD','SPE'])
  req('undo');assert fields(req('state'))==fields(before)
  s=req('entityAction',action='randomEV');ev=[int(fields(s)['EV_'+k]) for k in ['HP','ATK','DEF','SPA','SPD','SPE']];assert all(0<=x<=(65535 if version=='C' else 252) for x in ev)
  if version!='C':assert sum(ev)==510
  req('undo');assert fields(req('state'))==fields(before)
  if version in ['X','SL']:assert s['characteristic']
 for version in ['PLA','GP']:
  req('demo',version=version);req('entityAction',ok=False,action='randomEV')
 before=req('demo',version='SL');preview=req('slotPreview',box=0,slot=0,party=False,revision=before['revision']);assert preview['species']=='Pikachu' and len(preview['moves'])==4 and preview['text'];assert req('state')==before
 req('slotPreview',ok=False,box=0,slot=0,revision=-1);req('slotPreview',ok=False,box=999,slot=0,revision=before['revision'])
 bag=req('inventory');assert all(x['icon'].startswith(('bitem_','paldea_','hisui_')) for b in bag['pouches'] for x in b['items'])
 pouch=next(b for b in bag['pouches'] if b['name']=='Medicine');req('inventoryGiveAll',pouch=pouch['id']);after=req('inventory');assert sum(x['count'] for b in after['pouches'] for x in b['items'])>sum(x['count'] for b in bag['pouches'] for x in b['items']);req('undo');assert req('inventory')['pouches']==bag['pouches']
 req('inventoryGiveAll',ok=False,pouch=-1)
 print('PASS',checks,'KeepSake operations',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
