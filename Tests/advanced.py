#!/usr/bin/env python3
"""Test advanced editing and staged batch behavior using generated fixtures only."""
import json,pathlib,subprocess,sys
helper,saves,scratch=map(pathlib.Path,sys.argv[1:4]); scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
checks=0
def req(op,expected=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==expected,(op,r);checks+=1;return r.get('data')
def value(s,id):return next(f['value'] for f in s['fields'] if f['id']==id)
try:
 s=req('open',path=str(saves/'B2.sav'))
 roots=req('saveObject',path=''); print('B2 structures:',', '.join(n['id'] for n in roots['nodes']),flush=True)
 # Root advanced edit must survive writing and reopening.
 req('objectSet',path='',field='Money',value='54321')
 req('exportSave',path=str(scratch/'advanced.sav'));s=req('open',path=str(scratch/'advanced.sav'))
 assert next(f['value'] for f in s['saveFields'] if f['id']=='Money')=='54321'
 flags=req('events');assert flags['supported'];previous=flags['entries'][0]['value']
 assert req('eventSet',index=0,value=not previous)['entries'][0]['value']!=previous
 req('undo'); assert req('events')['entries'][0]['value']==previous
 print('PASS advanced root and event undo',flush=True)
 s=req('showdownImport',text='Pikachu @ Light Ball\nAbility: Static\nLevel: 55\nTimid Nature\nEVs: 252 SpA / 4 SpD / 252 Spe\n- Thunderbolt\n- Protect')
 assert value(s,'CurrentLevel')=='55';assert value(s,'HeldItem')=='236';req('apply')
 previous=s['entityLevel']
 preview=req('batchPreview',text='=Species=25\n.CurrentLevel=60',scope='box');assert preview['count']==1 and not preview['errors']
 assert req('state')['entityLevel']==55
 s=req('batchApply',token=preview['token']);assert s['entityLevel']==60
 assert req('undo')['entityLevel']==55
 preview=req('batchPreview',text='=Species=25\n.CurrentLevel=70',scope='box');req('saveSet',field='Money',value='900')
 req('batchApply',token=preview['token'],expected=False)
 req('batchPreview',text='bad instructions',scope='box',expected=False)
 req('exportSave',path=str(scratch/'batch.sav'));s=req('open',path=str(scratch/'batch.sav'));assert s['entityLevel']==55 and s['checksumValid']
 print('PASS Showdown import, batch preview/apply/undo, stale preview rejection, export/reopen',flush=True)
 # Scarlet's blank sample is deliberately not exportable, but nested edits must
 # change the actual engine buffer (objectSet verifies this before accepting).
 req('demo');root=req('saveObject',path='');assert any(n['id']=='RaidPaldea' for n in root['nodes'])
 raidlist=req('saveObject',path='RaidPaldea');assert len(raidlist['nodes'])>0
 raid=req('saveObject',path='RaidPaldea.@raid:0')
 req('objectSet',path='RaidPaldea.@raid:0',field='RaidPaldea.@raid:0.Seed',value='123456')
 assert value(req('saveObject',path='RaidPaldea.@raid:0'),'RaidPaldea.@raid:0.Seed')=='123456'
 req('undo');assert value(req('saveObject',path='RaidPaldea.@raid:0'),'RaidPaldea.@raid:0.Seed')!='123456'
 config=req('saveObject',path='Config');print('Scarlet config fields:',len(config['fields']),flush=True)
 pouches=req('inventory')['pouches'];pouch=pouches[0]
 req('inventorySet',pouch=pouch['id'],slot=0,item=pouch['items'][0]['item'],count=1)
 fields=req('inventoryFields',pouch=pouch['id'],slot=0)
 if fields:
  toggle=next((f for f in fields if f['kind']=='bool' and f['editable']),None)
  if toggle:
   new='false' if toggle['value']=='true' else 'true'
   result=req('inventoryFieldSet',pouch=pouch['id'],slot=0,field=toggle['id'],value=new)
   assert next(f['value'] for f in result if f['id']==toggle['id'])==new
 print('PASS nested raid edits, undo, inventory extra fields',flush=True)
 print('PASS',checks,'advanced protocol operations and assertions',flush=True)
finally:
 p.stdin.close();p.wait(timeout=10)
