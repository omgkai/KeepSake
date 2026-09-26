#!/usr/bin/env python3
"""Curated Windows game editors: bounds, read purity, bulk behavior, files and Undo."""
import json,pathlib,subprocess,sys
helper,out=map(pathlib.Path,sys.argv[1:3]);out.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);checks=0
def req(op,ok=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();reply=json.loads(p.stdout.readline());checks+=1;assert reply['ok']==ok,(op,kw,reply.get('error'));return reply.get('data')
def rev():return req('state')['revision']
def page(kind):return req('extraPage',kind=kind)
def mutate(kind,**kw):return req('extraSet',kind=kind,revision=rev(),**kw)
def contents(kind):return page(kind)['entries']
def alternate(f):
 if f['kind']=='bool':return 'false' if f['value']=='true' else 'true'
 if f['kind']=='enum':return next((c['value'] for c in reversed(f['choices']) if c['value']!=f['value']),None)
 if f['kind']=='date':return None
 return '1' if f['value']!='1' else '2'
try:
 for game in ['RD','YW','C','GD','E','R','FR','HG','Pt','B2','B','X','AS','SN','US','GP','SW','BD','PLA','SL','ZA']:
  state=req('demo',version=game);tools=req('extraTools');assert req('state')==state
  for tool in tools:
   kind=tool['id']
   if kind in ['misc3','misc4','misc5','gear','unlocks8b','mail','fame','geonet','unity','berries','avenue','contacts','underground8','raids8','raids9','sevenstar','bases3','bases6','pokeathlon','festival','passes','link6','globallink5','dlc4','dlc5','underground4','chatter','training6']:continue # Dedicated Parity019Checks, Parity020Checks, Parity022Checks, Parity023Checks, Parity025Checks Parity028Checks, Mail028Checks and JoinAvenueChecks cover these adapters.
   before=page(kind);assert before['entries'];assert req('state')==state
   rows=before['entries'];candidates=rows if len(rows)<=4 else [rows[0],rows[1],rows[-1]]
   for listed in candidates:
    row=req('extraEntry',kind=kind,id=listed['id'])
    for f in row['fields']:
     value=alternate(f)
     if value is None:continue
     edits=[dict(field=f['id'],value=value)]
     if kind=='donuts' and f['id']!='MillisecondsSince1970':edits.append(dict(field='MillisecondsSince1970',value='1700000000123'))
     mutate(kind,id=row['id'],edits=edits)
     actual=req('extraEntry',kind=kind,id=row['id']);assert next(x['value'] for x in actual['fields'] if x['id']==f['id'])==value,(game,kind,f,actual)
     req('undo');assert contents(kind)==rows,(game,kind,'undo')
    if row['fields']:
     req('extraSet',ok=False,kind=kind,id=row['id'],revision=-1,edits=[])
     req('extraSet',ok=False,kind=kind,id=row['id'],revision=rev(),edits=[dict(field='InvalidField',value='0')])
     numeric=next((f for f in row['fields'] if f['kind']=='number'),None)
     if numeric:req('extraSet',ok=False,kind=kind,id=row['id'],revision=rev(),edits=[dict(field=numeric['id'],value=str(int(numeric['max'])+1))])
     assert contents(kind)==rows
   for action in before['actions']:
    mutate(kind,mode=action['value']);req('undo');assert contents(kind)==rows,(kind,action,'bulk undo')
   if kind=='clock3':
    mutate(kind,mode='berryfix');assert contents(kind)[1]['fields'][0]['value']=='734';req('undo')
   if kind=='honey':
    mutate(kind,id='0',mode='ready');assert contents(kind)[0]['fields'][0]['value']=='1080';req('undo')
   if kind=='gsball':
    mutate(kind,id='event',mode='enable');assert contents(kind)[0]['detail']=='Enabled';req('undo')
   if kind=='zygarde':
    mutate(kind,mode='give');complete=contents(kind);count=int(complete[0]['fields'][0]['value']);assert count==len(rows)-1 and all(x['fields'][0]['value']=='2' for x in complete[1:]);mutate(kind,mode='give');assert int(contents(kind)[0]['fields'][0]['value'])==count;req('undo');req('undo')
   if kind=='medals':
    mutate(kind,mode='give');awarded=contents(kind);assert all(next(f for f in x['fields'] if f['id']=='Date')['value'] for x in awarded[1:]);mutate(kind,id='0',edits=[dict(field='Date',value='2024-02-29')]);assert next(f for f in contents(kind)[1]['fields'] if f['id']=='Date')['value']=='2024-02-29';req('undo');req('undo')
    req('extraSet',ok=False,kind=kind,id='0',revision=rev(),edits=[dict(field='Date',value='2023-02-29')])
   if kind in ['medals','donuts']:
    original=out/(game+'-'+kind+'.bin');req('extraExport',kind=kind,id='0',path=str(original));assert original.stat().st_size==(1020 if kind=='medals' else 72)
    replacement=out/'replacement.bin';replacement.write_bytes(original.read_bytes()[:-1]);req('extraSet',ok=False,kind=kind,id='0',mode='import',revision=rev(),path=str(replacement));assert contents(kind)==rows
    replacement.write_bytes(original.read_bytes());mutate(kind,id='0',mode='import',path=str(replacement));assert contents(kind)==rows;req('undo')
   state=req('state')
  print('PASS',game,', '.join(t['id'] for t in tools) or 'capability gating',flush=True)
 print('PASS',checks,'game-extra protocol operations',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
