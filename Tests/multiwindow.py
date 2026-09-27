#!/usr/bin/env python3
"""Exercise independent engine sessions sharing only their preference file."""
import json,subprocess,sys,tempfile,pathlib,concurrent.futures
with tempfile.TemporaryDirectory(prefix='keepsake-windows-') as tmp:
 settings=str(pathlib.Path(tmp)/'settings.json')
 engines=[subprocess.Popen([sys.argv[1],settings],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True) for _ in range(2)]
 def call(i,op,ok=True,**kw):
  p=engines[i];p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==ok,(op,r);return r.get('data')
 try:
  a=call(0,'demo',version='PLA');b=call(1,'demo',version='X');assert a['dragSession']!=b['dragSession']
  assert any(f['id']=='IsNoble' for f in a['fields']) and not any(f['id']=='IsNoble' for f in b['fields'])
  old=call(1,'state');changed=call(0,'entitySet',field='IsNoble',value='true');assert next(f['value'] for f in changed['fields'] if f['id']=='IsNoble')=='true';assert 'Noble' in changed['report']
  assert call(1,'state')==old;assert call(0,'undo')['fields']==a['fields']
  # A foreign window's slot token must never move anything in this save.
  call(1,'slotSwap',ok=False,session=a['dragSession'],revision=a['revision'],fromBox=0,fromSlot=0,toBox=0,toSlot=1);assert call(1,'state')==old
  with concurrent.futures.ThreadPoolExecutor() as pool:
   list(pool.map(lambda i:call(i,'settingsSet',field=['EncounterResultLimit','BackupOnOpen'][i],value=['777','false'][i]),[0,1]))
  persisted=json.loads(pathlib.Path(settings).read_text());assert persisted['EncounterResultLimit']==777 and persisted['BackupOnOpen']==False
  call(0,'settingsSet',field='CatalogLanguage',value='fr');assert call(1,'state')==old
  # Closing one helper does not close or invalidate the other workspace.
  engines[0].stdin.close();engines[0].wait(timeout=10)
  assert call(1,'state')==old
  print('PASS: independent saves, distinct drag tokens, Noble format gating/edit/Undo, cross-window rejection, concurrent settings merge and independent shutdown')
 finally:
  for p in engines:
   if p.poll() is None:p.stdin.close();p.wait(timeout=10)
