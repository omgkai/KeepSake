#!/usr/bin/env python3
"""Local end-to-end bridge checks. Uses synthetic saves and public upstream PKM fixtures.
Usage: python3 Tests/integration.py APP_HELPER UPSTREAM_TESTS SCRATCH FIXTURE_SAVES
"""
import hashlib, json, pathlib, subprocess, sys
helper, fixtures, scratch, saves = map(pathlib.Path, sys.argv[1:5])
scratch.mkdir(parents=True,exist_ok=True)
settings_path = scratch/'preferences.json'
checks = 0

def start():
 return subprocess.Popen([str(helper),str(settings_path)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
p = start()
def send(op, expected=True, **kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n'); p.stdin.flush()
 line=p.stdout.readline()
 assert line, f'Engine exited during {op}'
 result=json.loads(line)
 assert result['ok']==expected, (op,result)
 checks+=1
 return result.get('data',result.get('error'))
def field(s,k): return next(x['value'] for x in s['fields'] if x['id']==k)
def savefield(s,k): return next(x['value'] for x in s['saveFields'] if x['id']==k)
try:
 send('state')
 for extension in ['pk1','pk2','pk3','pk4','pk5','pk6','pk7','pb7','pk8','pb8','pa8','pk9','pa9']:
  files=sorted(f for f in fixtures.rglob('*.'+extension) if 'Legal' in f.parts)
  if not files:
   print('SKIP no public fixture for',extension,flush=True); continue
  for source in files:
   s=send('open',path=str(source))
   values={f['id']:f['value'] for f in s['fields']}
   if values.get('IsEgg')=='false' and values.get('Language') in ('2','0'): break
  original=source.read_bytes()
  assert s['loaded'] and not s['hasSave']; assert s['entityExtension']==extension,(extension,s['entityExtension'])
  previous=field(s,'Nickname')
  s=send('entitySet',field='Nickname',value='SWIFT'); assert field(s,'Nickname')=='SWIFT'
  s=send('undo'); assert field(s,'Nickname')==previous
  s=send('redo'); assert field(s,'Nickname')=='SWIFT'
  send('entitySet',field='Species',value='65535',expected=False)
  assert field(send('state'),'Nickname')=='SWIFT'
  dest=scratch/('roundtrip.'+extension)
  send('exportEntity',path=str(dest))
  send('exportEntity',path=str(source),expected=False)
  s=send('open',path=str(dest)); assert field(s,'Nickname')=='SWIFT'
  assert source.read_bytes()==original
  print('PASS',extension,'edit, undo/redo, range rejection, export, reopen, original preservation',flush=True)
 for version in ['B2','BD']:
  source=saves/(version+'.sav'); before=source.read_bytes()
  s=send('open',path=str(source)); assert s['hasSave']
  old=savefield(s,'Money')
  s=send('saveSet',field='Money',value='12345'); assert savefield(s,'Money')=='12345'
  s=send('undo'); assert savefield(s,'Money')==old
  s=send('redo'); assert savefield(s,'Money')=='12345'
  send('saveSet',field='Money',value='4294967295',expected=False)
  s=send('entitySet',field='Nickname',value='MAC')
  send('exportSave',path=str(scratch/(version+'-pending.sav')),expected=False)
  send('apply'); send('exportSave',path=str(source),expected=False)
  dest=scratch/(version+'-edited.sav'); send('exportSave',path=str(dest))
  s=send('open',path=str(dest)); assert s['checksumValid']; assert savefield(s,'Money')=='12345'; assert field(s,'Nickname')=='MAC'
  inventory=send('inventory')
  pouch=next(x for x in inventory['pouches'] if x['items'] and len(x['choices'])>1)
  item=int(next(c['value'] for c in pouch['choices'] if c['value']!='0'))
  result=send('inventorySet',pouch=pouch['id'],slot=0,item=item,count=1)
  assert result['pouches'][pouch['id']]['items'][0]['count']==1
  send('inventorySet',pouch=pouch['id'],slot=0,item=item,count=-1,expected=False)
  d=send('dex')
  if d['supported']:
   d=send('dexSet',species=25,seen=True,caught=True); pikachu=next(e for e in d['entries'] if e['id']==25); assert pikachu['caught'] and pikachu['seen']
  second=scratch/(version+'-inventory.sav'); send('exportSave',path=str(second)); s=send('open',path=str(second)); assert s['checksumValid']
  assert send('inventory')['pouches'][pouch['id']]['items'][0]['count']==1
  assert source.read_bytes()==before
  print('PASS',version,'save roundtrip, trainer, inventory, dex, checksums, original preservation',flush=True)
 prefs=send('settings'); toggle=next(f for f in prefs if f['kind']=='bool')
 new='false' if toggle['value']=='true' else 'true'
 prefs=send('settingsSet',field=toggle['id'],value=new); assert next(f['value'] for f in prefs if f['id']==toggle['id'])==new
 p.stdin.close(); p.wait(timeout=10); p=start()
 assert next(f['value'] for f in send('settings') if f['id']==toggle['id'])==new
 print('PASS preferences persist across engine restarts',flush=True)
 send('demo'); send('exportSave',path=str(scratch/'sample.sav'),expected=False)
 invalid=scratch/'invalid.txt'; invalid.write_text('not a save')
 send('open',path=str(invalid),expected=False); assert send('state')['demo']
 print(f'PASS {checks} protocol operations and associated assertions',flush=True)
finally:
 if p.poll() is None: p.stdin.close(); p.wait(timeout=10)
