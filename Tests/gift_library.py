#!/usr/bin/env python3
"""Gift metadata, external-library isolation and restricted-language preparation."""
import json,subprocess,sys,tempfile,pathlib,shutil
p=subprocess.Popen([sys.argv[1]],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
count=0
def call(op,ok=True,**args):
 global count
 p.stdin.write(json.dumps(dict(op=op,**args))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==ok,r;count+=1;return r.get('data')
try:
 gifts=call('gifts');assert len(gifts)>2500
 assert all(g['languageKnown']==bool(g['languages']) and len(g['moves'])<=4 for g in gifts)
 assert {g['generation'] for g in gifts}==set(range(4,10))
 with tempfile.TemporaryDirectory(prefix='keepsake-gifts-') as d:
  root=pathlib.Path(d);folder=root/'cards';folder.mkdir();nested=folder/'nested';nested.mkdir()
  picked=[next(g for g in gifts if g['generation']==n and g['exportable'] and g['entity']) for n in range(4,10)]
  for g in picked:call('giftExport',id=g['id'],path=str(nested/(str(g['id'])+'.'+g['extension'])))
  files=list(nested.iterdir());original={f:f.read_bytes() for f in files}
  shutil.copy2(files[0],folder/('duplicate'+files[0].suffix));(folder/'invalid.wc6').write_bytes(b'bad');(folder/'ignore.txt').write_text('not a gift')
  (folder/'linked').symlink_to(nested,target_is_directory=True)
  before=call('demo',version='SL')
  result=call('giftsLoadFolder',path=str(folder),recursive=True);assert result['added']==6 and result['skipped']==1 and result['duplicates']==1,result
  assert call('state')==before
  local=[g for g in result['entries'] if g['source']=='Folder'];ids={g['id'] for g in local};assert len(ids)==6
  again=call('giftsLoadFolder',path=str(folder),recursive=True);assert again['added']==0 and again['duplicates']==7
  card=next(g for g in local if g['file'] in [files[0].name,'duplicate'+files[0].suffix])
  call('giftExport',ok=False,id=card['id'],path=str(files[0]))
  copy=root/('copy.'+card['extension']);call('giftExport',id=card['id'],path=str(copy));assert copy.read_bytes()==files[0].read_bytes()
  call('giftsClearFolders');call('giftPrepare',ok=False,id=card['id'])
  new=call('giftsLoadFolder',path=str(folder),recursive=True);assert not ids.intersection(g['id'] for g in new['entries'] if g['source']=='Folder')
  assert all(f.read_bytes()==b for f,b in original.items())
 # Restricted bundled cards must keep their language when prepared using an English sample.
 for gen,version in [(5,'B2'),(6,'AS'),(7,'US')]:
  candidates=[g for g in gifts if g['generation']==gen and g['game']=='Gen'+str(gen) and g['entity'] and len(g['languages'])==1 and g['languages'][0]!=2]
  for gift in candidates[:12]:
   before=call('demo',version=version);after=call('giftPrepare',id=gift['id'])
   fields={f['id']:f['value'] for f in after['fields']};assert int(fields['Language'])==gift['languages'][0],(gift,fields['Language'])
   undo=call('undo');assert undo['fields']==before['fields']
 assert call('gifts')[:len(gifts)]==gifts,'Bundled metadata changed during preparation'
 print('PASS',len(gifts),'card records;',count,'requests; folder isolation, deduplication, stale IDs, restricted-language preparation and Undo')
finally:p.stdin.close();p.wait(timeout=10)
