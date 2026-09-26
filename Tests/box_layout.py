#!/usr/bin/env python3
"""Storage moves, metadata, reordering, stale-token protection and save roundtrips."""
import json,pathlib,subprocess,sys
helper,saves,scratch=map(pathlib.Path,sys.argv[1:4]);scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
checks=0
def req(op,expected=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline())
 assert r['ok']==expected,(op,kw,r.get('error'));checks+=1;return r.get('data')
def token(state):return dict(session=state['dragSession'],revision=state['revision'])
def move(state,a,b,expected=True):return req('slotSwap',expected=expected,**token(state),fromBox=a[0],fromSlot=a[1],toBox=b[0],toSlot=b[1])
def layout(mode,**kw):return req('boxLayoutSet',mode=mode,**kw)
def metadata(state):return [(e['name'],e['wallpaper']) for e in state['boxLayout']['entries']]
try:
 for version in ['B2','BD']:
  source=saves/(version+'.sav');original=source.read_bytes();s=req('open',path=str(source));assert s['boxLayout']['wallpapers']
  # Prepare two distinguishable Pokémon in separate boxes.
  req('entityEdit',edits=[{'field':'Nickname','value':'FIRST'}],apply=True)
  req('select',box=1,slot=0,party=False);s=req('entityEdit',edits=[{'field':'Species','value':'133'},{'field':'Nickname','value':'SECOND'}],apply=True)
  s=move(s,(0,0),(1,0));assert s['entityNickname']=='FIRST'
  assert req('select',box=0,slot=0,party=False)['entityNickname']=='SECOND'
  req('undo');assert req('select',box=0,slot=0,party=False)['entityNickname']=='FIRST';s=req('redo')
  s=move(s,(1,0),(2,3));assert s['box']==2 and s['slot']==3 and s['entityNickname']=='FIRST'
  assert req('select',box=1,slot=0,party=False)['entityName']=='Empty slot'
  s=req('state');move(s,(1,0),(0,0),expected=False)
  move(s,(0,0),(0,0),expected=False);move(s,(0,0),(-1,0),expected=False);move(s,(0,0),(0,999),expected=False)
  req('slotSwap',session='other-session',revision=s['revision'],fromBox=0,fromSlot=0,toBox=0,toSlot=1,expected=False)
  stale=s;layout('name',box=0,name='Eevee');move(stale,(0,0),(0,1),expected=False)
  req('select',box=0,slot=0,party=False);s=req('entityEdit',edits=[{'field':'Nickname','value':'PENDING'}]);move(s,(0,0),(0,1),expected=False)
  layout('move',box=0,destination=1,expected=False);req('undo')
  for i in range(3):layout('name',box=i,name='Box'+str(i));layout('wallpaper',box=i,wallpaper=i+1)
  before=req('state');initial=metadata(before)
  s=layout('move',box=0,destination=2);assert metadata(s)[:3]==[initial[1],initial[2],initial[0]]
  assert s['entityNickname']=='SECOND';assert req('select',box=1,slot=3,party=False)['entityNickname']=='FIRST'
  req('undo');assert metadata(req('state'))==initial
  req('redo');s=layout('move',box=2,destination=0);assert metadata(s)==initial and s['entityNickname']=='SECOND'
  s=layout('swap',box=0,destination=2);assert metadata(s)[:3]==[initial[2],initial[1],initial[0]]
  req('undo');assert metadata(req('state'))==initial
  before=req('state');layout('wallpaper',box=0,wallpaper=999,expected=False);layout('name',box=0,name='x'*17,expected=False);layout('name',box=0,name='bad\nname',expected=False)
  layout('move',box=-1,destination=0,expected=False);layout('swap',box=0,destination=999,expected=False);assert metadata(req('state'))==metadata(before)
  out=scratch/(version+'-boxes.sav');req('exportSave',path=str(out));s=req('open',path=str(out));assert s['checksumValid'] and metadata(s)==metadata(before)
  assert req('select',box=0,slot=0,party=False)['entityNickname']=='SECOND'
  assert req('select',box=2,slot=3,party=False)['entityNickname']=='FIRST';assert source.read_bytes()==original
  print('PASS',version,'slot move/swap, reordering both directions, metadata, undo, stale/session/pending guards and checksum-valid save roundtrip',flush=True)
 req('demo',version='PLA');s=req('state');assert not s['boxLayout']['wallpapers'] and s['boxLayout']['entries'][0]['sprite']=='box_wp01bdsp'
 layout('wallpaper',box=0,wallpaper=0,expected=False);s=move(req('state'),(0,0),(1,0));assert s['entityName']=='Pikachu'
 layout('name',box=1,name='Alpha friends');assert req('state')['boxNames'][1]=='Alpha friends'
 req('demo',version='SL');assert len(req('state')['boxLayout']['wallpapers'])==20
 req('demo',version='VL');assert req('state')['boxLayout']['wallpapers'][-1]['sprite'].endswith('_u')
 print('PASS Arceus pastures and in-memory slot moves, game-specific wallpaper choices',flush=True)
 protected=saves/'BD-protected.sav'
 assert protected.exists(), 'Regenerate fixtures to include the locked battle-team save'
 if protected.exists():
  s=req('open',path=str(protected));move(s,(0,0),(1,0),expected=False);layout('swap',box=0,destination=1,expected=False);layout('move',box=1,destination=0,expected=False)
  assert req('state')['entityName']=='Pikachu';print('PASS locked battle-team slot and whole-box protection',flush=True)
 print('PASS',checks,'box layout operations and assertions',flush=True)
finally:
 p.stdin.close();p.wait(timeout=10)
