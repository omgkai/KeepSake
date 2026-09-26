#!/usr/bin/env python3
"""Detailed Hisui dex and gift album adapters; generated saves only."""
import json,pathlib,subprocess,sys
helper,saves,scratch=map(pathlib.Path,sys.argv[1:4]);scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
checks=0
def req(op,expected=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush()
 response=json.loads(p.stdout.readline());assert response['ok']==expected,(op,kw,response)
 checks+=1;return response.get('data')
def detail(species=25,form=0):return req('dexDetails',species=species,form=form)
def album():return req('giftAlbum')
try:
 req('demo',version='PLA');original=detail()
 assert req('state')['canResearch'] and not req('state')['canGiftAlbum']
 for key in ['seen','obtained','caught']:
  for bit in range(8):
   values=dict(seen=0,obtained=0,caught=0);values[key]=1<<bit
   req('dexDetailsSet',species=25,form=0,mode='flags',**values)
   assert all(detail()[k]==v for k,v in values.items())
 req('dexDetailsSet',species=25,form=0,mode='flags',seen=255,obtained=255,caught=255)
 before=detail();req('dexDetailsSet',species=25,form=0,mode='flags',seen=1,obtained=256,caught=1,expected=False);assert detail()==before
 req('dexDetailsSet',species=25,form=0,mode='display',displayForm=0,female=True,shiny=True,alpha=True)
 data=detail();assert data['female'] and data['alpha'] and data['shiny']
 req('undo');assert detail()==before
 req('redo');assert detail()['alpha']
 sizes=dict(minHeight=0.4,maxHeight=0.9,minWeight=2.5,maxWeight=8.0)
 req('dexDetailsSet',species=25,form=0,mode='size',hasMax=True,**sizes)
 data=detail();assert data['hasMax'] and all(abs(data[k]-v)<1e-6 for k,v in sizes.items())
 req('dexDetailsSet',species=25,form=0,mode='size',hasMax=True,**dict(sizes,maxHeight=0.1),expected=False);assert detail()==data
 req('dexDetailsSet',species=25,form=0,mode='size',hasMax=True,**dict(sizes,minWeight=-1),expected=False);assert detail()==data
 req('dexDetailsSet',species=25,form=0,mode='size',hasMax=True,**dict(sizes,maxWeight=1e100),expected=False);assert detail()==data
 req('dexDetailsSet',species=25,form=0,mode='size',hasMax=False,**sizes);data=detail();assert data['maxHeight']==data['minHeight'] and data['maxWeight']==data['minWeight']
 req('dexDetails',species=1,expected=False);req('dexDetails',species=25,form=255,expected=False)
 # Actual form IDs survive the choice list, including multi-form species.
 unown=detail(201);assert len(unown['forms'])==28
 req('dexDetailsSet',species=201,form=27,mode='flags',seen=17,obtained=1,caught=0);assert detail(201,27)['seen']==17
 req('dexDetailsSet',species=201,form=27,mode='display',displayForm=27,female=False,shiny=False,alpha=False);assert detail(201,27)['displayForm']==27
 req('dexDetails',species=900,form=1,expected=False) # noble form
 req('giftAlbum',expected=False)
 print('PASS Hisui variant bits, display flags, form IDs, sizes, bounds, rollback and undo/redo (synthetic in-memory save)',flush=True)
 source=saves/'B2.sav';original=source.read_bytes();req('open',path=str(source));assert req('state')['canGiftAlbum']
 assert all(g['empty'] for g in album()['entries'])
 baseline=scratch/'baseline.sav';req('exportSave',path=str(baseline))
 for _ in range(3):album()
 afterread=scratch/'afterread.sav';req('exportSave',path=str(afterread));assert baseline.read_bytes()==afterread.read_bytes()
 gifts=req('gifts');pgfs=[g for g in gifts if g['generation']==5 and g['exportable'] and g['entity']]
 first,second=pgfs[:2]
 req('giftAlbumSet',mode='library',index=8,id=first['id']);data=album();assert data['entries'][0]['card']==first['card'] and data['entries'][8]['empty']
 assert first['card'] in data['received']
 req('giftAlbumSet',mode='library',index=8,id=second['id']);assert album()['entries'][1]['card']==second['card']
 req('giftAlbumSet',mode='used',index=0,value=True);assert album()['entries'][0]['used']
 req('undo');assert album()['entries'][0]['used']!=True
 req('redo');assert album()['entries'][0]['used']
 req('giftAlbumSet',mode='usedAll',value=False);assert not any(g['used'] for g in album()['entries'] if not g['empty'])
 before=album()
 incompatible=next(g for g in gifts if g['generation']==8 and g['exportable'])
 req('giftAlbumSet',mode='library',index=0,id=incompatible['id'],expected=False);assert album()==before
 req('giftAlbumSet',mode='delete',index=-1,expected=False);req('giftAlbumSet',mode='flag',card=2048,value=True,expected=False);assert album()==before
 req('giftAlbumSet',mode='delete',index=0);assert album()['entries'][0]['card']==second['card'] and album()['entries'][1]['empty']
 assert first['card'] in album()['received']
 req('undo');assert album()==before
 req('giftAlbumSet',mode='flag',card=1234,value=True);assert 1234 in album()['received']
 req('giftAlbumSet',mode='flag',card=1234,value=False);assert 1234 not in album()['received']
 card=scratch/'gift.pgf';req('giftAlbumExport',index=0,path=str(card));card_before=card.read_bytes()
 req('giftAlbumSet',mode='import',index=7,path=str(card));assert album()['entries'][2]['card']==first['card']
 req('giftAlbumExport',index=0,path=str(card),expected=False);req('giftAlbumExport',index=0,path=str(source),expected=False)
 bad=scratch/'bad.pgf';bad.write_text('not a card');before=album();req('giftAlbumSet',mode='import',index=0,path=str(bad),expected=False);assert album()==before
 output=scratch/'album.sav';req('exportSave',path=str(output));assert req('open',path=str(output))['checksumValid'];assert album()==before
 assert source.read_bytes()==original and card.read_bytes()==card_before
 req('dexDetails',species=25,expected=False)
 print('PASS Gen 5 encryption-neutral reads, card install/packing/status/history/import/export, rollback, undo/redo, checksum-valid save roundtrip and originals preserved',flush=True)
 # Gen 4 slot groups and Lock Capsule routing are tested in sample memory.
 req('demo',version='HG');data=album();assert len(data['entries'])==12 and data['entries'][-1]['special']
 pcds=[g for g in gifts if g['generation']==4 and g['extension']=='pcd']
 normal=next(g for g in pcds if g['entity'])
 # Synthetic item card: PGT type=Item, item=533 inside a 0x358-byte PCD.
 capsule=scratch/'lock-capsule.pcd';raw=bytearray(0x358);raw[0:2]=(3).to_bytes(2,'little');raw[4:8]=(533).to_bytes(4,'little');capsule.write_bytes(raw)
 req('giftAlbumSet',mode='library',index=8,id=normal['id']);assert not album()['entries'][8]['empty']
 req('giftAlbumSet',mode='import',index=0,path=str(capsule));assert not album()['entries'][11]['empty']
 req('giftAlbumSet',mode='delete',index=8);assert album()['entries'][8]['empty'] and not album()['entries'][11]['empty']
 req('undo');assert not album()['entries'][8]['empty']
 assert not album()['entries'][8]['canUse'];req('giftAlbumSet',mode='used',index=8,value=True,expected=False)
 req('giftAlbumSet',mode='library',index=0,id=normal['id']);assert not album()['entries'][0]['empty']
 req('demo',version='Pt');req('giftAlbumSet',mode='import',index=0,path=str(capsule),expected=False)
 print('PASS Gen 4 PCD/PGT conversion, HGSS Lock Capsule routing/isolation, undo and incompatible-game rejection (sample memory)',flush=True)
 for game,gen in [('X',6),('US',7)]:
  req('demo',version=game)
  gift=next(g for g in gifts if g['generation']==gen and g['entity'] and g['extension']==('wc6' if gen==6 else 'wc7'))
  req('giftAlbumSet',mode='library',index=0,id=gift['id']);assert album()['entries'][0]['card']==gift['card']
  req('giftAlbumSet',mode='used',index=0,value=True);assert album()['entries'][0]['used']
  req('undo');req('redo');assert album()['entries'][0]['used']
  out=scratch/('album.'+gift['extension']);req('giftAlbumExport',index=0,path=str(out))
  req('giftAlbumSet',mode='delete',index=0);assert album()['entries'][0]['empty']
  req('giftAlbumSet',mode='import',index=0,path=str(out));assert album()['entries'][0]['used']
 print('PASS Gen 6 and 7 card/status edits, undo/redo and card-file roundtrips (sample memory)',flush=True)
 print('PASS',checks,'dex/album operations and assertions',flush=True)
finally:
 p.stdin.close();p.wait(timeout=10)
