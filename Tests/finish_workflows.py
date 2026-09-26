#!/usr/bin/env python3
"""Focused checks for save resources, trainer details, catalog language, gift QR and research panels."""
import json,subprocess,tempfile,pathlib,sys,shutil,hashlib
with tempfile.TemporaryDirectory(prefix='keepsake-finish-') as tmp:
 root=pathlib.Path(tmp); settings=root/'settings.json'
 p=subprocess.Popen([sys.argv[1],sys.argv[2],str(settings)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
 count=0
 def call(op,ok=True,**kw):
  global count
  p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();reply=json.loads(p.stdout.readline());assert reply['ok']==ok,(op,reply);count+=1;return reply.get('data')
 try:
  source=root/'saves';source.mkdir();fixture=bytearray(pathlib.Path(sys.argv[3]).read_bytes());assert len(fixture)==0x65600;fixture[-0x1F0:-0x1EC]=b'FEEB';original=bytes(fixture);save=source/'test.sav';save.write_bytes(original)
  call('open',path=str(save));snapshots=call('saveBackups');assert len(snapshots)==1
  call('open',path=str(save));assert len(call('saveBackups'))==1
  restored=root/'restored.sav';call('saveBackupExport',id=snapshots[0]['id'],path=str(restored));assert restored.read_bytes()==original
  call('saveBackupExport',ok=False,id=snapshots[0]['id'],path=str(restored));call('saveBackupExport',ok=False,id='../settings',path=str(root/'bad'))
  discovery=call('saveDiscover',path=str(source),recursive=True);assert len(discovery['entries'])==1
  assert save.read_bytes()==original
  call('settingsSet',field='CatalogLanguage',value='ja');assert next(c for c in call('lookup',kind='species') if c['value']=='25')['label']=='ピカチュウ'
  call('settingsSet',ok=False,field='CatalogLanguage',value='invalid');assert json.loads(settings.read_text())['CatalogLanguage']=='ja'
  call('settingsSet',field='CatalogLanguage',value='en')
  for game in ['X','AS','US','GP','SW','BD','PLA','SL','ZA']:
   call('demo',version=game);details=call('trainerDetails');assert details,(game,details)
   date=next((f for f in details if f['kind']=='datetime'),None)
   if date:
    before=call('state');call('trainerDetailSet',field=date['id'],value='2025-04-09 12:34:00')
    changed=next(f for f in call('trainerDetails') if f['id']==date['id']);assert changed['value']=='2025-04-09 12:34:00',(game,changed)
    call('undo');assert next(f for f in call('trainerDetails') if f['id']==date['id'])['value']==date['value']
   before=call('state');call('trainerDetailSet',ok=False,field='PartyCount',value='6');after=call('state');assert after==before,(game,[(k,before[k],after[k]) for k in before if before[k]!=after[k]])
  call('demo',version='PLA');research=call('research',species=25)
  special=0
  for choice in research['choices']:
   data=call('research',species=int(choice['value']));assert all('reported' in t and 'editable' in t for t in data['tasks'])
   special+=sum(1 for t in data['tasks'] if not t['editable'])
  assert special>0
  gifts=call('gifts');gift=next(g for g in gifts if g['exportable'] and g['extension']=='wc6')
  qr=call('giftQR',id=gift['id']);imported=call('giftQRImport',payload=qr['payload'],format='wc6');assert len(imported)==len(gifts)+1
  assert len(call('giftQRImport',payload=qr['payload'],format='wc6'))==len(imported)
  call('giftQRImport',ok=False,payload=qr['payload'],format='wa9');call('giftQRImport',ok=False,payload='not base64',format='wc6')
  print(f'PASS: {count} requests; backups/discovery, localized catalogs, trainer dates/Undo, all Hisui task panels, gift QR')
 finally:
  p.stdin.close();p.wait(timeout=10)
