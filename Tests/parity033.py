#!/usr/bin/env python3
import json,subprocess,sys,tempfile,pathlib,hashlib,base64
p=subprocess.Popen([sys.argv[1]],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);count=0
def call(op,ok=True,**kw):
 global count
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==ok,(op,r);count+=1;return r.get('data')
try:
 for version in ['RD','GD','E','HG','B2','X','AS','US','SW','BD','PLA','SL','ZA']:
  before=call('demo',version=version);qr=call('pokemonQR');assert qr['payload'] and qr['lines']
  call('pokemonQRImport',payload=qr['payload']);after=call('state');assert after['entityName']==before['entityName'];call('undo');assert call('state')['fields']==before['fields']
  call('pokemonQRImport',ok=False,payload=base64.b64encode(b'not a Pokemon').decode());assert call('state')['fields']==before['fields']
 print('PASS QR prepare, Undo and invalid data across 13 game formats',flush=True)
 call('demo',version='X');profile=call('encounterTrainer');assert profile['version']=='X'
 criteria=call('encounterCriteria');assert criteria['ivs']==[-1]*6 and criteria['nature']==25
 criteria['nature']=15;criteria['levelMax']=100 # upstream permits no minimum (0) with a specified maximum
 data=call('encounterSearch',species=25,version='X',category='Slot',moves=[])
 assert data['entries'];before=call('state')
 for invalid in [dict(criteria,ivs=[32]*6),dict(criteria,ability=3),dict(criteria,levelMin=99,levelMax=1),dict(criteria,mutations=16)]:
  call('encounterPrepare',ok=False,id=0,token=data['token'],criteria=invalid);assert call('state')==before
 badprofile=dict(profile,version='SL');call('encounterPrepare',ok=False,id=0,token=data['token'],trainer=badprofile);assert call('state')==before
 profile['name']='TEST';call('encounterPrepare',id=0,token=data['token'],criteria=criteria,trainer=profile)
 fields={f['id']:f['value'] for f in call('state')['fields']};assert fields['Nature']=='Modest' and fields['OriginalTrainerName']=='TEST',fields
 call('undo');assert call('state')['fields']==before['fields']
 print('PASS independent criteria, trainer selection, rejection purity and Undo',flush=True)
 with tempfile.TemporaryDirectory(prefix='keepsake033-') as d:
  root=pathlib.Path(d);source=root/'source';source.mkdir();target=root/'target';target.mkdir()
  call('demo',version='SL');file=source/'sample.pk9';call('exportEntity',path=str(file));original=file.read_bytes()
  profile=call('encounterTrainer',path=str(file));assert profile['name']=='Preview'
  library=call('libraryScan',paths=[str(source)],recursive=False);before=call('state')
  preview=call('folderBatchPreview',token=library['token'],ids=[0],text='=Species=25\n.CurrentLevel=50');assert preview['count']==1 and not preview['errors'];assert call('state')==before
  exported=call('folderBatchExport',token=preview['token'],path=str(target));files=list(pathlib.Path(exported['path']).iterdir());assert len(files)==1 and files[0].read_bytes()!=original and file.read_bytes()==original
  call('folderBatchExport',ok=False,token=preview['token'],path=str(target))
  updated=call('libraryScan',paths=[exported['path']],recursive=False);assert updated['entries'][0]['level']==50
  library=call('libraryScan',paths=[str(source)],recursive=False)
  preview=call('folderBatchPreview',token=library['token'],ids=[0],text='.CurrentLevel=55');file.write_bytes(original+b'x');call('folderBatchExport',ok=False,token=preview['token'],path=str(target))
  stale=call('folderBatchPreview',token=library['token'],ids=[0],text='.CurrentLevel=55');assert stale['errors'];assert len(list(target.iterdir()))==1
  file.write_bytes(original);library=call('libraryScan',paths=[str(source)],recursive=False)
  preview=call('folderBatchPreview',token=library['token'],ids=[0],text='.CurrentLevel=55');call('libraryScan',paths=[str(source)],recursive=False);call('folderBatchExport',ok=False,token=preview['token'],path=str(target))
 print('PASS folder preview/export, source preservation, stale-file/token rejection',flush=True)
 print('PASS',count,'protocol requests',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
