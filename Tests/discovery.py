#!/usr/bin/env python3
"""Encounter, library, move-plus and box availability adapters; synthetic/public data only."""
import json,pathlib,subprocess,sys,shutil
helper,fixtures,saves,scratch=map(pathlib.Path,sys.argv[1:5]);scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);checks=0

def req(op,expected=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();line=p.stdout.readline();assert line,(op,p.poll());r=json.loads(line)
 assert r['ok']==expected,(op,kw,r.get('error'));checks+=1;return r.get('data')
def search(**kw):return req('encounterSearch',species=25,**kw)
def prep(data,id=0,expected=True):return req('encounterPrepare',id=id,token=data['token'],expected=expected)
try:
 # Read-only search, explicit filters, stale-result and invalid-input rejection.
 for game in ['B2','BD','PLA','SL','ZA']:
  before=req('demo',version=game);data=req('encounterSearch',species=133 if game=='B2' else 25,version=game,category='Slot');assert data['entries'],game
  assert req('state')==before
  s=prep(data);assert s['pending'] and s['entityName']!='Empty slot' and s['slots']==before['slots']
  assert req('undo')['entityNickname']==before['entityNickname'];prep(data,expected=False)
  print('PASS',game,'encounter search/prepare/undo',flush=True)
 req('demo',version='SL');data=search(version='SL',category='Egg');assert data['entries'] and all(x['egg'] for x in data['entries'])
 req('encounterPrepare',id=0,token='stale',expected=False);prep(data,id=99999,expected=False)
 before=req('state')
 for kw in [dict(species=0),dict(species=9999),dict(species=25,form=99),dict(species=25,version='nonsense'),dict(species=25,category='Slot, Egg'),dict(species=25,moves=[1,2,3,4,5]),dict(species=25,moves=[9999])]:req('encounterSearch',expected=False,**kw)
 assert req('state')==before
 data=search(version='SL',category='Slot',shiny='Never',moves=[85]);assert all(x['shiny']=='Never' for x in data['entries'])
 # Preparing a generated encounter survives a Pokémon-file export/reopen.
 data=search(version='SL',category='Slot');prep(data);out=scratch/'encounter.pk9';req('exportEntity',path=str(out));s=req('open',path=str(out));assert s['entityExtension']=='pk9'
 # Z-A move-plus edits are separate from TM records and survive storage/reopen.
 req('demo',version='ZA');records=req('plusRecords');assert records['entries'];before=req('moveRecords')
 entry=records['entries'][0];req('plusRecordsSet',index=entry['id'],learned=not entry['learned']);assert req('plusRecords')['entries'][0]['learned']!=entry['learned'];assert req('moveRecords')==before
 req('undo');assert req('plusRecords')['entries'][0]['learned']==entry['learned'];req('redo')
 req('plusRecordsSet',index=-1,learned=True,expected=False);req('plusRecordsSet',index=9999,learned=True,expected=False)
 for mode in ['current','tm','seed','clear']:
  s=req('plusRecordsSet',mode=mode);assert s['pending'];assert req('moveRecords')==before
 assert not any(x['learned'] for x in req('plusRecords')['entries'])
 req('plusRecordsSet',index=entry['id'],learned=True);req('apply');assert req('select',box=0,slot=0,party=False)['canPlusRecords']
 out=scratch/'plus.pa9';req('exportEntity',path=str(out));s=req('open',path=str(out));assert s['entityExtension']=='pa9' and req('plusRecords')['entries'][0]['learned']
 req('demo',version='PLA');req('plusRecords',expected=False);req('plusRecordsSet',mode='clear',expected=False)
 # Box counters and raw bytes use exact readback; all writes undoable.
 for game in ['B2','BD','X','US','SW','PLA','SL','ZA']:
  state=req('demo',version=game);data=state['boxLayout'];assert data['canUnlock'],game
  old=data['unlocked'];s=req('boxLayoutSet',mode='unlocked',count=7);assert s['boxLayout']['unlocked']==7 and s['slots']==state['slots']
  assert req('undo')['boxLayout']['unlocked']==old;assert req('redo')['boxLayout']['unlocked']==7
  req('boxLayoutSet',mode='unlocked',count=state['boxCount']+1,expected=False)
  if data['flags']:
   old=data['flags'][0];s=req('boxLayoutSet',mode='flag',index=0,value=0 if old else 1);assert s['boxLayout']['flags'][0]!=(old)
   assert req('undo')['boxLayout']['flags'][0]==old
   req('boxLayoutSet',mode='flag',index=999,value=1,expected=False);req('boxLayoutSet',mode='flag',index=0,value=256,expected=False)
 req('demo',version='RD');assert not req('state')['boxLayout']['canUnlock'];req('boxLayoutSet',mode='unlocked',count=7,expected=False)
 for game in ['B2','BD']:
  original=(saves/(game+'.sav')).read_bytes();req('open',path=str(saves/(game+'.sav')));req('boxLayoutSet',mode='unlocked',count=7)
  out=scratch/(game+'-unlocked.sav');req('exportSave',path=str(out));s=req('open',path=str(out));assert s['checksumValid'] and s['boxLayout']['unlocked']==7;assert (saves/(game+'.sav')).read_bytes()==original
 # Mixed-format folder library, no source writes, bounded parse errors, subfolder behavior.
 folder=scratch/'library';folder.mkdir(exist_ok=True);sub=folder/'nested';sub.mkdir(exist_ok=True)
 originals={}
 for ext in ['pk3','pk6','pb8','pa8','pk9']:
  source=next(x for x in fixtures.rglob('*.'+ext) if 'Legal' in x.parts);dest=(sub if ext=='pa8' else folder)/('sample.'+ext);shutil.copy2(source,dest);originals[dest]=dest.read_bytes()
 (folder/'invalid.pk9').write_bytes(b'bad');(folder/'ignored.txt').write_text('ordinary text');(folder/'empty.pk9').write_bytes(b'')
 link=folder/'linked.pk9'
 if not link.exists():link.symlink_to((folder/'sample.pk9').resolve())
 req('demo',version='SL');before=req('state');data=req('libraryScan',path=str(folder),recursive=False);assert len(data['entries'])==4 and data['skipped']==2;assert req('state')==before
 stale=data['token'];data=req('libraryScan',path=str(folder),recursive=True);assert len(data['entries'])==5 and data['skipped']==2
 req('libraryPrepare',id=0,token=stale,expected=False);req('libraryPrepare',id=9999,token=data['token'],expected=False)
 entry=next(x for x in data['entries'] if x['format']=='pk9');s=req('libraryPrepare',id=entry['id'],token=data['token']);req('exportEntity',path=str(folder/'sample.pk9'),expected=False);assert s['pending'] and s['entityName']==entry['name'] and s['slots']==before['slots'];req('undo')
 # No save open: read the cached snapshot even if its source was later changed.
 req('open',path=str(folder/'sample.pk6'));(folder/'sample.pk9').write_bytes(b'changed after scan');s=req('libraryPrepare',id=entry['id'],token=data['token']);assert s['entityExtension']=='pk9' and s['entityName']==entry['name'];(folder/'sample.pk9').write_bytes(originals[folder/'sample.pk9'])
 for source,original in originals.items():assert source.read_bytes()==original
 req('libraryScan',path=str(folder/'absent'),recursive=True,expected=False)
 print('PASS',checks,'discovery operations: encounters, move-plus, box availability and folder-library snapshots',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
