#!/usr/bin/env python3
"""Supplied item artwork integrity and game-specific inventory selection on synthetic saves."""
import hashlib,json,pathlib,subprocess,sys
helper,assets=map(pathlib.Path,sys.argv[1:3]);checks=0
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
def req(op,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok'],(op,kw,r.get('error'));checks+=1;return r['data']
try:
 for folder,game,prefix in [('HisuiItems','PLA','hisui_'),('PaldeaItems','SL','paldea_')]:
  manifest=json.loads((assets/folder/'manifest.json').read_text())
  for entry in manifest['files']:assert hashlib.sha256((assets/folder/entry['file']).read_bytes()).hexdigest()==entry['sha256']
  req('demo',version=game);before=req('inventory');tested=set()
  if game=='SL':assert any(x['name']=='TM Materials' for x in before['pouches'])
  for item,file in manifest['items'].items():
   if file in tested:continue
   tested.add(file);pouch=next(x for x in before['pouches'] if item in x['limits'])
   bag=req('inventoryAdd',pouch=pouch['id'],item=int(item),count=1)
   row=next(x for x in bag['pouches'][pouch['id']]['items'] if x['item']==int(item));assert row['icon']==prefix+pathlib.Path(file).stem,(game,item,row)
   req('undo')
  assert req('inventory')['pouches']==before['pouches']
  print('PASS',folder,len(manifest['files']),'original hashes;',len(tested),'distinct item artwork selections with Undo',flush=True)
 # Shared potion IDs must retain the older game's artwork.
 req('demo',version='SW');bag=req('inventory');pouch=next(x for x in bag['pouches'] if '26' in x['limits'])
 bag=req('inventoryAdd',pouch=pouch['id'],item=26,count=1);row=next(x for x in bag['pouches'][pouch['id']]['items'] if x['item']==26);assert row['icon']=='bitem_26'
 print('PASS',checks,'item artwork operations',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
