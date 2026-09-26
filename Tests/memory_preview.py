"""Exercise the packaged guided memory catalogs without changing an entity."""
import json, subprocess, sys
p=subprocess.Popen([sys.argv[1]],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
checks=0
def req(expected=True,**payload):
 global checks
 p.stdin.write(json.dumps(payload)+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==expected,r;checks+=1;return r.get('data')
try:
 for game in ['X','SN','SW','BD','PLA','SL','ZA']:
  state=req(op='demo',version=game);revision=state['revision']
  page=req(op='memoryInfo');ot=next(r for r in page['entries'] if r['id']=='OriginalTrainer')
  choices=next(f for f in ot['fields'] if f['id']=='OriginalTrainerMemory')['choices']
  for c in choices:
   assert '{' not in c['label'] and 'an intensity' not in c['label']
   preview=req(op='memoryInfo',revision=revision,entityKey=page['entityKey'],id='OriginalTrainer',edits=[{'field':'OriginalTrainerMemory','value':c['value']}])
   row=next(r for r in preview['entries'] if r['id']=='OriginalTrainer')
   assert '{' not in row['detail'] and ' that .' not in row['detail'],row['detail']
   assert all(x['label'].strip() for f in row['fields'] for x in f['choices'])
  assert req(op='memoryInfo')==page
  assert req(op='state')['revision']==revision
 # Selection does not increment the session revision: the separate entity key must guard it.
 state=req(op='demo',version='X');page=req(op='memoryInfo')
 req(op='select',box=0,slot=1,party=False)
 req(expected=False,op='memorySet',revision=state['revision'],entityKey=page['entityKey'],id='OriginalTrainer',edits=[{'field':'OriginalTrainerFriendship','value':'90'}])
 req(op='select',box=0,slot=0,party=False)
 req(op='entitySet',field='OriginalTrainerMemory',value='255')
 req(op='entitySet',field='Geo1_Country',value='255')
 page=req(op='memoryInfo');before=req(op='state')['entityData']
 assert 'Unknown stored memory 255' in page['entries'][0]['detail']
 req(op='memoryInfo',revision=page['revision'],entityKey=page['entityKey'],id='OriginalTrainer',edits=[{'field':'OriginalTrainerFriendship','value':'90'}])
 assert req(op='state')['entityData']==before
 print(f'PASS {checks} packaged memory preview requests, readable labels and revision/read purity')
finally:
 p.stdin.close();p.wait(timeout=15)
