#!/usr/bin/env python3
"""Exercise native workflow adapters with generated saves and public fixtures."""
import csv,json,pathlib,subprocess,sys
helper,fixtures,saves,scratch=map(pathlib.Path,sys.argv[1:5]);scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
checks=0
def req(op,expected=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==expected,(op,r);checks+=1;return r.get('data')
def fixture(ext):return next(f for f in fixtures.rglob('*.'+ext) if 'Legal' in f.parts)
try:
 for ext in ['pa8','pk8','pk9']:
  source=fixture(ext);before=source.read_bytes();req('open',path=str(source));data=req('moveRecords');assert data['entries']
  entry=data['entries'][0];new=not entry['learned']
  req('moveRecordsSet',index=entry['id'],learned=new,mastered=not entry['mastered'])
  assert req('moveRecords')['entries'][0]['learned']==new
  req('undo');assert req('moveRecords')['entries'][0]['learned']==entry['learned']
  req('redo');dest=scratch/('records.'+ext);req('exportEntity',path=str(dest));req('open',path=str(dest))
  assert req('moveRecords')['entries'][0]['learned']==new
  if data['shop']:assert req('moveRecords')['entries'][0]['mastered']!=entry['mastered']
  req('moveRecordsSet',index=99999,learned=True,mastered=True,expected=False)
  req('moveRecordsSet',mode='clear');assert not any(e['learned'] or e['mastered'] for e in req('moveRecords')['entries'])
  req('moveRecordsSet',mode='current');req('moveRecordsSet',mode='all')
  assert source.read_bytes()==before
 print('PASS move shop/mastery and TM/TR edits, undo/redo, export/reopen, bounds, suggestions',flush=True)
 req('demo',version='PLA');data=req('research',species=25);assert len(data['tasks'])==30 and len(data['choices'])==242
 active=[t for t in data['tasks'] if t['active']];assert active and all(t['thresholds'] for t in active)
 first=active[0];old=first['count']
 req('researchSet',species=25,edits=[{'id':first['id'],'count':33}])
 assert req('research',species=25)['tasks'][first['id']]['count']==33
 req('undo');assert req('research',species=25)['tasks'][first['id']]['count']==old
 req('researchSet',species=25,edits=[{'id':first['id'],'count':44},{'id':29,'count':60001}],expected=False)
 assert req('research',species=25)['tasks'][first['id']]['count']==old
 req('researchSet',species=25,edits=[{'id':first['id'],'count':33}]);req('researchSet',species=25,mode='report')
 assert req('research',species=25)['points']>0
 req('researchSet',species=25,mode='solitude',value=True);assert req('research',species=25)['solitude']
 req('research',species=1,expected=False)
 print('PASS Hisui research counts, rollback, report progress, Path of Solitude, species validation',flush=True)
 req('open',path=str(saves/'B2.sav'));req('research',species=25,expected=False)
 rows=req('storage');assert any(r['species']==25 for r in rows)
 req('entityEdit',edits=[{'field':'Nickname','value':'=1+1'}],apply=True)
 report=scratch/'report.csv';req('storageReport',path=str(report))
 parsed=list(csv.DictReader(report.open()));assert parsed and parsed[0]['Nickname']=="'=1+1"
 req('storageReport',path=str(saves/'B2.sav'),expected=False)
 a=req('boxExport',path=str(scratch),all=False);b=req('boxExport',path=str(scratch),all=True)
 assert a['path']!=b['path'] and a['count']>=1
 exported=next(pathlib.Path(a['path']).rglob('*.pk5'));s=req('open',path=str(exported));assert s['entityNickname']=='=1+1'
 print('PASS storage search data, CSV formula escaping, unique folder exports, Pokémon reopen',flush=True)
 req('open',path=str(saves/'B2.sav'));actions=req('boxActions');assert any(a['value']=='SortSpecies' for a in actions)
 preview=req('boxPreview',action='ModifyMaxLevel',all=False);assert preview['count']>0 and req('state')['entityLevel']==25
 req('batchApply',token=preview['token']);assert req('state')['entityLevel']==100
 req('undo');assert req('state')['entityLevel']==25
 preview=req('boxPreview',action='DeleteAll',all=False);assert preview['count']>0
 req('batchApply',token=preview['token']);assert not req('storage')
 req('undo');assert req('storage')
 preview=req('boxPreview',action='DeleteAll',all=True);req('saveSet',field='Money',value='456')
 req('batchApply',token=preview['token'],expected=False)
 req('boxPreview',action='MissingAction',expected=False)
 dest=scratch/'boxes.sav';req('exportSave',path=str(dest));assert req('open',path=str(dest))['checksumValid']
 print('PASS box action discovery, staged modify/delete, apply/undo, stale preview rejection, save roundtrip',flush=True)
 gifts=req('gifts');assert len(gifts)>100
 req('demo',version='PLA');gift=next(g for g in gifts if g['game']=='Gen8a' and g['entity'])
 s=req('giftPrepare',id=gift['id']);assert s['pending'] and s['entityName']==gift['name']
 assert req('undo')['entityName']=='Pikachu'
 dest=scratch/('gift.'+gift['extension']);req('giftExport',id=gift['id'],path=str(dest));assert dest.stat().st_size>0
 req('giftPrepare',id=-1,expected=False)
 req('open',path=str(fixture('pa8')));req('giftExport',id=gift['id'],path=str(fixture('pa8')),expected=False)
 print('PASS bundled Mystery Gift search, prepare/undo, card export and original-file protection',flush=True)
 print('PASS',checks,'workflow operations and assertions',flush=True)
finally:
 p.stdin.close();p.wait(timeout=10)
