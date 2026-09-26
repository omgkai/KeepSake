#!/usr/bin/env python3
import json,subprocess,sys,tempfile,pathlib
with tempfile.TemporaryDirectory(prefix='keepsake-rc-trainers-') as scratch:
 p=subprocess.Popen([sys.argv[1],sys.argv[2],str(pathlib.Path(scratch)/'settings.json')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
 count=0
 def call(op,ok=True,**kw):
  global count
  p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==ok,(op,kw,r);count+=1;return r.get('data')
 try:
  for game in ['SW','SH']:
   call('demo',version=game);call('entityEdit',edits=[dict(field='Species',value='25')],apply=True,destinationBox=0,destinationSlot=0,destinationParty=True);page=call('extraPage',kind='trainerTeams');original=page['entries']
   for mode in ['cardParty','titleParty']:
    page=call('extraPage',kind='trainerTeams');call('extraSet',kind='trainerTeams',mode=mode,revision=page['revision']);assert call('extraPage',kind='trainerTeams')['entries']!=original
    call('undo');assert call('extraPage',kind='trainerTeams')['entries']==original
   page=call('extraPage',kind='trainerTeams');call('extraSet',kind='trainerTeams',id='card:0',revision=page['revision'],edits=[dict(field='Species',value='25')]);changed=call('extraPage',kind='trainerTeams');assert next(f['value'] for f in changed['entries'][0]['fields'] if f['id']=='Species')=='25'
   call('extraSet',ok=False,kind='trainerTeams',id='card:0',revision=page['revision'],edits=[dict(field='Species',value='1')]);call('undo');assert call('extraPage',kind='trainerTeams')['entries']==original
  for game in ['SW','PLA','SL','ZA']:
   call('demo',version=game);before=call('trainerDetails');field=next(f for f in before if f['id']=='FacingDegrees')
   call('trainerDetailSet',field='FacingDegrees',value='90');after=call('trainerDetails');assert abs(float(next(f['value'] for f in after if f['id']=='FacingDegrees'))-90)<0.001,(game,[(f['id'],f['value']) for f in after if f['id']=='FacingDegrees'])
   call('undo');assert call('trainerDetails')==before
   call('trainerDetailSet',ok=False,field='FacingDegrees',value='361');assert call('trainerDetails')==before
  for game in ['Pt','HG','B2','X','AS','SN','US']:
   call('demo',version=game);before=call('trainerDetails')
   call('trainerDetailSet',field='AdventureDate',value='2020-06-15 17:32:41');assert next(f['value'] for f in call('trainerDetails') if f['id']=='AdventureDate')=='2020-06-15 17:32:41'
   call('undo');assert call('trainerDetails')==before
  for game in ['SN','MN','US','UM']:
   call('demo',version=game);page=call('extraPage',kind='trainerAlola');original=page['entries']
   for mode in ['flyAll','mapAll']:
    page=call('extraPage',kind='trainerAlola');call('extraSet',kind='trainerAlola',mode=mode,revision=page['revision']);after=call('extraPage',kind='trainerAlola');prefix='fly:' if mode=='flyAll' else 'map:';assert all(row['fields'][0]['value']=='true' for row in after['entries'] if row['id'].startswith(prefix));call('undo');assert call('extraPage',kind='trainerAlola')['entries']==original
   page=call('extraPage',kind='trainerAlola');call('extraSet',kind='trainerAlola',id='tree:1:1:1',revision=page['revision'],edits=[dict(field='Streak',value='123')]);assert next(row for row in call('extraPage',kind='trainerAlola')['entries'] if row['id']=='tree:1:1:1')['fields'][0]['value']=='123';call('undo');assert call('extraPage',kind='trainerAlola')['entries']==original
  call('demo',version='X');call('entityEdit',edits=[dict(field='Species',value='25')],apply=True,destinationBox=0,destinationSlot=1,destinationParty=False)
  preview=call('batchPreview',text='=Box=1\n=Slot=2\n.HeldItem=1',scope='boxes');assert preview['count']==1 and not preview['errors'],preview
  preview=call('batchPreview',text='=Box=2\n.HeldItem=1',scope='boxes');assert preview['count']==0 and not preview['errors'],preview
  call('demo',version='ZA');text=call('donutClipboard',id=0);page=call('extraPage',kind='donuts')
  call('extraSet',ok=False,kind='donuts',mode='import',id='0',revision=page['revision'],hex='FF');assert call('donutClipboard',id=0)==text
  call('extraSet',kind='donuts',mode='import',id='0',revision=page['revision'],hex=text);assert call('donutClipboard',id=0)==text
  for game,kind in [('X','maison'),('AS','maison'),('SW','trainerProgress'),('PLA','trainerProgress')]:
   call('demo',version=game);page=call('extraPage',kind=kind);original=page['entries']
   if not original:continue # Sparse practice Switch saves omit optional numeric progress blocks.
   row=original[0];field=row['fields'][0]
   call('extraSet',kind=kind,id=row['id'],revision=page['revision'],edits=[dict(field=field['id'],value='12')]);assert call('extraPage',kind=kind)['entries'][0]['fields'][0]['value']=='12';call('undo');assert call('extraPage',kind=kind)['entries']==original
  call('demo',version='B2');page=call('extraPage',kind='medals');original=page['entries']
  call('medalsSetSelected',ids=['0','1'],revision=page['revision'],edits=[dict(field='State',value='2')]);after=call('extraPage',kind='medals');assert all(next(f['value'] for f in row['fields'] if f['id']=='State')=='2' for row in after['entries'] if row['id'] in ['0','1']);call('undo');assert call('extraPage',kind='medals')['entries']==original
  page=call('extraPage',kind='medals');call('medalsSetSelected',ok=False,ids=['0','-1'],revision=page['revision'],edits=[dict(field='State',value='2')]);assert call('extraPage',kind='medals')['entries']==original
  call('demo',version='SW');fields=call('trainerDetails');assert any(f['id']=='TrainerCard.Number' for f in fields)
  call('trainerDetailSet',field='TrainerCard.Number',value='123');fields=call('trainerDetails');assert all(f['value']=='123' for f in fields if f['id'] in ['TrainerCard.Number','MyStatus.Number']);call('undo')
  call('demo',version='X');before=call('state');options=call('saveOpenOptions');assert options['handlers'] and options['types']
  bad=pathlib.Path(scratch)/'invalid.sav';bad.write_bytes(b'not a save')
  edition=next(v for v in options['versions'] if v['value']=='X')
  call('open',ok=False,path=str(bad),handler=0,saveType=edition['type'],saveVersion='X',saveLanguage='English');assert call('state')==before
  report=call('storageReportPreview',columns=['Species','pk.PID']);assert len(report['columns'])==2 and report['rows']
  call('storageReportPreview',ok=False,columns=['BadColumn'])
  for game,edition in [('X','Y'),('AS','OR'),('US','UM'),('GP','GE'),('SW','SH'),('BD','SP'),('SL','VL')]:
   call('demo',version=game);before=call('trainerDetails');call('trainerDetailSet',field='GameEdition',value=edition);assert next(f['value'] for f in call('trainerDetails') if f['id']=='GameEdition')==edition;call('undo');assert call('trainerDetails')==before
  print('PASS',count,'requests: display teams, exact Undo, stale rejection, four-game rotations and Gen6/7 timestamps')
 finally:p.stdin.close();p.wait(timeout=10)
