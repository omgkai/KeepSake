#!/usr/bin/env python3
"""Add-item stacks and atomic editor-to-destination writes, using synthetic saves only."""
import json,pathlib,subprocess,sys
helper,saves,scratch=map(pathlib.Path,sys.argv[1:4]);scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);checks=0

def req(op,expected=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline())
 assert r['ok']==expected,(op,kw,r.get('error'));checks+=1;return r.get('data')
def count(data,pi,id):return sum(x['count'] for x in data['pouches'][pi]['items'] if x['item']==id)
def setslot(slot,box=0,party=False,edits=[],expected=True):return req('entityEdit',expected=expected,edits=edits,apply=True,destinationBox=box,destinationSlot=slot,destinationParty=party)
try:
 for game in ['B2','BD','PLA','SL','ZA']:
  req('demo',version=game);inv=req('inventory');pouch=next(p for p in inv['pouches'] if any(int(k)>0 and v>=10 for k,v in p['limits'].items()));pi=pouch['id'];id=next(int(k) for k,v in pouch['limits'].items() if int(k)>0 and v>=10)
  start=count(inv,pi,id)
  added=req('inventoryAdd',pouch=pi,item=id,count=3);assert count(added,pi,id)==start+3
  added=req('inventoryAdd',pouch=pi,item=id,count=2);assert count(added,pi,id)==start+5
  assert sum(x['item']==id for x in added['pouches'][pi]['items'])==1
  req('undo');assert count(req('inventory'),pi,id)==start+3
  req('redo');assert count(req('inventory'),pi,id)==start+5
  for amount in [0,-1,pouch['limits'][str(id)],2147483647]:req('inventoryAdd',pouch=pi,item=id,count=amount,expected=False)
  req('inventoryAdd',pouch=pi,item=0,count=1,expected=False);req('inventoryAdd',pouch=-1,item=id,count=1,expected=False)
  assert count(req('inventory'),pi,id)==start+5
  limit=pouch['limits'][str(id)];req('inventoryAdd',pouch=pi,item=id,count=limit-start-5);assert count(req('inventory'),pi,id)==limit
  req('inventoryAdd',pouch=pi,item=id,count=1,expected=False)
  print('PASS',game,'add, merge, undo, redo, bounds, persistence',flush=True)
 # Full pouch: adding a new kind must not replace an existing stack.
 req('demo',version='PLA');inv=req('inventory');pouch=next(p for p in inv['pouches'] if len(p['choices'])-1>len(p['items']) and len(p['items'])<50)
 ids=[int(c['value']) for c in pouch['choices'] if int(c['value'])>0];pi=pouch['id']
 for index,id in enumerate(ids[:len(pouch['items'])]):req('inventorySet',pouch=pi,slot=index,item=id,count=1)
 before=req('inventory');req('inventoryAdd',pouch=pi,item=ids[len(pouch['items'])],count=1,expected=False);assert req('inventory')==before
 # Per-item limits can be lower than the pouch-wide maximum (SV picnic accessories).
 req('demo',version='SL');inv=req('inventory');pouch=next(p for p in inv['pouches'] if any(v<p['max'] for v in p['limits'].values()));id=next(int(k) for k,v in pouch['limits'].items() if v<pouch['max'])
 req('inventoryAdd',pouch=pouch['id'],item=id,count=2,expected=False);assert count(req('inventory'),pouch['id'],id)==0
 req('inventoryAdd',pouch=pouch['id'],item=id,count=1)
 other=next(p for p in inv['pouches'] if str(id) not in p['limits']);req('inventoryAdd',pouch=other['id'],item=id,count=1,expected=False)
 for game in ['B2','BD']:
  source=saves/(game+'.sav');original=source.read_bytes();s=req('open',path=str(source));name=s['entityNickname']
  s=setslot(2,edits=[dict(field='Nickname',value='COPY')]);assert s['slot']==2 and s['entityNickname']=='COPY' and not s['pending']
  assert req('select',box=0,slot=0,party=False)['entityNickname']==name
  s=req('undo');assert s['slot']==0 and s['entityNickname']==name and s['slots'][2]['empty']
  req('redo');req('select',box=0,slot=2,party=False);s=setslot(0);assert s['slots'][0]['nickname']=='COPY' and s['slots'][2]['nickname']=='COPY'
  req('entityEdit',edits=[dict(field='Nickname',value='PENDING')]);s=setslot(3,box=1);assert s['box']==1 and s['entityNickname']=='PENDING'
  before=req('state');setslot(999,edits=[dict(field='Nickname',value='FAILED')],expected=False);assert req('state')==before
  setslot(0,box=-1,expected=False)
  setslot(5,party=True,expected=False)
  s=setslot(0,party=True);assert s['partySlots'][0]['nickname']=='PENDING'
  inv=req('inventory');pouch=inv['pouches'][0];id=next(int(k) for k,v in pouch['limits'].items() if int(k)>0 and v>=3);req('inventoryAdd',pouch=pouch['id'],item=id,count=3)
  out=scratch/(game+'-added.sav');req('exportSave',path=str(out));s=req('open',path=str(out));assert s['checksumValid'];assert count(req('inventory'),pouch['id'],id)==3
  assert req('select',box=1,slot=3,party=False)['entityNickname']=='PENDING';assert source.read_bytes()==original
 req('open',path=str(saves/'BD-protected.sav'));req('select',box=1,slot=0,party=False)
 req('entityEdit',edits=[dict(field='Species',value='133')]);before=req('state');setslot(0,box=0,edits=[dict(field='Nickname',value='FAILED')],expected=False);assert req('state')==before
 print('PASS',checks,'inventory / destination operations, save roundtrips, protected targets and rollback',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
