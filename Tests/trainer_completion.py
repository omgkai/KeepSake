#!/usr/bin/env python3
import subprocess,json,sys,tempfile,pathlib
with tempfile.TemporaryDirectory(prefix='keepsake-trainer-') as temp:
 p=subprocess.Popen([sys.argv[1],sys.argv[2],str(pathlib.Path(temp)/'settings.json')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
 count=0
 def call(op,ok=True,**kw):
  global count
  p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==ok,(op,r);count+=1;return r.get('data')
 try:
  for game in ['RD','YW','GD','E','HG','B2','X','AS','US','GP','SW','BD','PLA','SL','ZA']:
   call('demo',version=game);assert call('trainerNameSupported');page=call('trainerNameInfo',field='OT');original=page['hex']
   request=dict(field='OT',revision=page['revision'],entityKey=page['entityKey'],hex=original)
   preview=call('trainerNameInfo',mode='text',text='KAI',**request);assert preview['text']=='KAI'
   assert call('trainerNameInfo',field='OT')['hex']==original
   request['hex']=preview['hex'];call('trainerNameSet',**request);assert call('trainerNameInfo',field='OT')['text']=='KAI'
   call('trainerNameSet',ok=False,**request);call('undo');assert call('trainerNameInfo',field='OT')['hex']==original,game
   tools=call('extraTools')
   if any(t['id']=='trainerRecords' for t in tools):
    records=call('extraPage',kind='trainerRecords');entry=records['entries'][0];value=entry['fields'][0]['value']
    call('extraSet',kind='trainerRecords',id=entry['id'],revision=records['revision'],edits=[dict(field='Value',value='1' if value!='1' else '2')])
    updated=call('extraPage',kind='trainerRecords');assert updated['entries'][0]['fields'][0]['value']!=value
    call('undo');assert call('extraPage',kind='trainerRecords')['entries'][0]['fields'][0]['value']==value
   details=call('trainerDetails');assert len({f['id'] for f in details})==len(details)
  call('demo',version='BD');treat=call('treats');original=treat['cookingCount']
  call('treatsSet',kind='poffins8',mode='cooking',revision=treat['revision'],edits=[dict(field='CookingCount',value='42')])
  assert call('treats')['cookingCount']==42
  call('treatsSet',ok=False,kind='poffins8',mode='cooking',revision=treat['revision'],edits=[dict(field='CookingCount',value='43')])
  call('undo');assert call('treats')['cookingCount']==original
  treat=call('treats');call('treatsSet',ok=False,kind='poffins8',mode='cooking',revision=treat['revision'],edits=[dict(field='CookingCount',value='-1')]);assert call('treats')['cookingCount']==original
  state=call('demo',version='X');album=call('giftAlbum')
  call('giftAlbumSet',ok=False,mode='delete',index=0,revision=state['revision']-1);assert call('giftAlbum')==album
  call('settingsSet',field='EncounterResultLimit',value='3');call('settingsSet',ok=False,field='EncounterResultLimit',value='0')
  call('demo',version='X');results=call('encounterSearch',species=25);assert len(results['entries'])==3 and results['truncated']
  call('settingsSet',field='BoxExport.FolderCreation',value='None');call('settingsSet',field='BoxExport.FileIndexPrefix',value='InAll')
  exported=call('boxExport',path=temp,all=False,empty=False);assert len(list(pathlib.Path(exported['path']).glob('*.pk6')))==1
  assert not any(x.is_dir() for x in pathlib.Path(exported['path']).iterdir())
  print('PASS',count,'requests: 15-game trainer text preview/apply/stale rejection/Undo, named records, trainer fields, export/search preferences')
 finally:p.stdin.close();p.wait(timeout=10)
