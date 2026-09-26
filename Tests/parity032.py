#!/usr/bin/env python3
"""Focused parity workflows against isolated synthetic workspaces."""
import json,subprocess,sys,tempfile,pathlib,csv
p=subprocess.Popen([sys.argv[1]],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);count=0

def call(op,ok=True,**kw):
 global count
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==ok,(op,r);count+=1;return r.get('data')
def args(info):return {k:info[k] for k in ['field','revision','entityKey','hex']}
try:
 with tempfile.TemporaryDirectory(prefix='keepsake032-') as d:
  root=pathlib.Path(d)
  gifts=call('gifts');ids=call('giftsFilter',filters='=Species=25');assert ids and all(g['species']==25 for g in gifts if g['id'] in ids)
  assert call('giftsFilter',filters='=Species=Pikachu')==ids
  for text in ['.Species=25','garbage','=Species','+Level=1']:call('giftsFilter',ok=False,filters=text)
  result=call('giftsExportSelection',ids=ids[:3],path=d);files=list(pathlib.Path(result['path']).iterdir());assert len(files)==3
  call('giftsExportSelection',ok=False,ids=[ids[0],-1],path=d);assert len(list(root.iterdir()))==1
  call('demo',version='SL');columns=call('reportColumns');assert any(c['value']=='pk.PID' for c in columns)
  report=root/'report.csv';call('storageReport',path=str(report),columns=['Nickname','pk.PID','Species']);rows=list(csv.reader(report.open()));assert len(rows[0])==3 and rows[0][0]=='Nickname'
  call('storageReport',ok=False,path=str(report),columns=['Nickname','Bogus'])
  storage=call('storageSearch',filters='=Species=25');assert all(x['species']==25 for x in storage)
  call('storageReport',path=str(report),columns=['Species'],ids=[]);assert len(list(csv.reader(report.open())))==1
  one=root/'one';two=root/'two';one.mkdir();two.mkdir()
  call('exportEntity',path=str(one/'sample.pk9'))
  import shutil
  shutil.copy2(one/'sample.pk9',two/'sample.pk9')
  library=call('libraryScan',paths=[str(one),str(two),str(one)],recursive=True);assert len(library['entries'])==2 and len(library['paths'])==2
  before=call('state');call('libraryScan',ok=False,paths=[d,str(root/'missing')],recursive=True);assert call('state')==before
  before=call('state');call('slotFileImport',path=str(one/'sample.pk9'),box=1,slot=3,revision=before['revision'],session=before['dragSession'])
  assert call('state')['slot']==3
  call('slotFileImport',ok=False,path=str(one/'sample.pk9'),box=1,slot=4,revision=before['revision'],session=before['dragSession'])
  call('undo');assert call('state')['fields']==before['fields']
  print('PASS gift expressions/bulk export, custom report columns and multiple library roots',flush=True)
  for version in ['RD','X','AS']:
   call('demo',version=version)
   for field in ['Nickname']+(['OriginalTrainerName'] if version in ['X','AS'] else []):
    info=call('fameNameBytesInfo',id='0:0',field=field)
    preview=call('fameNameBytesInfo',id='0:0',mode='text',text='AB',**args(info));assert preview['text']=='AB'
    assert call('fameNameBytesInfo',id='0:0',field=field)['hex']==info['hex']
    call('fameNameBytesSet',id='0:0',**args(preview));assert call('fameNameBytesInfo',id='0:0',field=field)['hex']==preview['hex']
    call('fameNameBytesSet',ok=False,id='0:0',**args(preview))
    call('undo');assert call('fameNameBytesInfo',id='0:0',field=field)['hex']==info['hex']
   call('fameNameBytesInfo',ok=False,id='999:0')
  print('PASS Hall of Fame staged bytes, exact changes, stale guards and Undo',flush=True)
  for version,species in [('HG',201),('B2',201),('X',201),('AS',201),('US',201),('GP',25),('SW',25),('BD',201),('SL',25),('ZA',25)]:
   s=call('demo',version=version);data=call('dexRecord',species=species)
   groups={f['group'] for f in data['fields'] if f['kind']=='bool' and f['editable'] and f['group']!='Overview' and not f['group'].startswith('Display') and 'unlock' not in f['group'].lower()}
   for group in sorted(groups):
    before=call('dexRecord',species=species);s=call('state');call('dexRecordGroup',species=species,group=group,value=True,revision=s['revision']);after=call('dexRecord',species=species)
    assert all(f['value']=='true' for f in after['fields'] if f['group']==group and f['kind']=='bool' and f['editable'])
    call('undo');assert call('dexRecord',species=species)==before
  print('PASS Pokédex group actions across ten game formats',flush=True)
  call('demo',version='ZA');info=call('donutRangeInfo');assert info['flavors']
  before=call('extraEntry',kind='donuts',id='0');outside=call('extraEntry',kind='donuts',id='3')
  call('donutRangeGenerate',revision=info['revision'],start=0,end=3,flavors=[info['flavors'][0]['value']]);assert call('extraEntry',kind='donuts',id='0')!=before;assert call('extraEntry',kind='donuts',id='3')==outside
  call('undo');assert call('extraEntry',kind='donuts',id='0')==before
  info=call('donutRangeInfo');call('donutRangeGenerate',ok=False,revision=info['revision'],start=0,end=1,flavors=['0']);assert call('extraEntry',kind='donuts',id='0')==before
  print('PASS custom donut range boundaries, invalid pool and Undo',flush=True)
  call('demo',version='SL');before=call('state');preview=call('showdownTeamPreview',text='Pikachu\nLevel: 50\n\nEevee\nLevel: 50');assert preview['ready'] and len(preview['entries'])==2,preview;assert call('state')==before
  call('teamPlace',token=preview['token'],box=1);call('undo');assert call('state')['fields']==before['fields']
  call('showdownTeamPreview',ok=False,text='Pikachu\nBogus line\n\nEevee');call('teamPlace',ok=False,token=preview['token'],box=1)
  data=call('encounterSearch',species=25,moves=[],filters='=Species=25');assert data['entries'];call('encounterPrepare',token=data['token'],id=data['entries'][0]['id'],useEditorCriteria=True)
  print('PASS staged multi-set import, placement/Undo and encounter expressions/editor criteria',flush=True)
 print('PASS',count,'protocol requests',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
