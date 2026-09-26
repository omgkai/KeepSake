#!/usr/bin/env python3
"""Ribbon, marking, and Super Training roundtrips using public upstream entities."""
import json,pathlib,subprocess,sys
helper,fixtures,scratch=map(pathlib.Path,sys.argv[1:4]);scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
checks=0
def req(op,expected=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline())
 assert r['ok']==expected,(op,kw,r.get('error'),r.get('data',{}).get('entityExtension'));checks+=1;return r.get('data')
def fixture(ext):return next(f for f in fixtures.rglob('*.'+ext) if 'Legal' in f.parts)
def dec():return req('decorations')
def edit(mode,**kw):return req('decorationsSet',mode=mode,**kw)
def values(data):return {r['id']:r['value'] for r in data['entries']}
def fields(state):return {f['id']:f['value'] for f in state['fields']}
def training(mode,**kw):return req('superTrainingSet',mode=mode,**kw)['superTraining']
try:
 for ext in ['pk3','pk4','pk5','pk6','pk7','pb7','pk8','pb8','pa8','pk9']:
  source=fixture(ext);original=source.read_bytes();s=req('open',path=str(source));data=s['decorations'];assert data['entries'];assert s['entityExtension']==ext
  baseline=scratch/('before-read.'+ext);req('exportEntity',path=str(baseline));dec();after_read=scratch/('after-read.'+ext);req('exportEntity',path=str(after_read));assert baseline.read_bytes()==after_read.read_bytes()
  initial=values(data);ribbon=next(r for r in data['entries'] if not r['count']);v=1-ribbon['value']
  edit('ribbon',id=ribbon['id'],value=v);assert values(dec())[ribbon['id']]==v
  req('undo');assert values(dec())==initial
  req('redo');assert values(dec())[ribbon['id']]==v
  count=next((r for r in data['entries'] if r['count']),None)
  if count:
   edit('ribbon',id=count['id'],value=count['max']);assert values(dec())[count['id']]==count['max']
   before=values(dec());edit('ribbon',id=count['id'],value=count['max']+1,expected=False);assert values(dec())==before
  edit('ribbon',id='Nickname',value=1,expected=False)
  edit('markingsClear');mark=data['markings'][1]
  chosen=2 if data['colored'] else 1
  result=edit('marking',id=mark['id'],value=chosen)
  expected_value='Pink' if data['colored'] else 'true';assert fields(result)[mark['id']]==expected_value
  # Gen 3 has triangle and square bits swapped; the named control must set only triangle.
  assert fields(result)['MarkingSquare']==('None' if data['colored'] else 'false')
  req('undo');assert all(m['value']==0 for m in dec()['markings']);req('redo')
  edit('marking',id=mark['id'],value=3,expected=False)
  if data['canAffix']:
   edit('ribbon',id='RibbonChampionKalos',value=1)
   title=next(c for c in dec()['affixedChoices'] if c['label']=='Kalos Champion')
   edit('affix',value=int(title['value']));assert dec()['affixed']==int(title['value'])
   edit('affix',value=255,expected=False)
  else:edit('affix',value=0,expected=False)
  before=dec();out=scratch/('ribbons.'+ext);req('exportEntity',path=str(out));req('open',path=str(out));assert dec()==before
  edit('all');assert all(r['value']==r['max'] for r in dec()['entries'])
  edit('clear');assert all(r['value']==0 for r in dec()['entries']) and dec()['affixed']==-1
  edit('suggest');edit('required')
  if data['canSuggestMarkings']:
   edits=[{'field':'IV_'+k,'value':str(v)} for k,v in [('HP',31),('ATK',0),('DEF',30),('SPA',1),('SPD',15),('SPE',31)]]
   req('entityEdit',edits=edits);edit('markingsSuggest')
   assert [m['value'] for m in dec()['markings']]==([1,2,2,1,0,1] if data['colored'] else [1,0,0,0,0,1])
  else:edit('markingsSuggest',expected=False)
  assert source.read_bytes()==original
  print('PASS',ext,'ribbons/counts/affix/shape markings, undo, bounds, suggestions and entity roundtrip',flush=True)
 req('open',path=str(fixture('pk1')));assert not dec()['entries'] and not dec()['markings']
 edit('all',expected=False);edit('marking',id='MarkingCircle',value=1,expected=False)
 req('superTrainingSet',mode='all',expected=False)
 for ext in ['pk6','pk7']:
  source=fixture(ext);original=source.read_bytes();req('open',path=str(source));data=training('clear')
  assert len(data['entries'])==30 and len(data['distribution'])==6
  before=req('state');evs={k:v for k,v in fields(before).items() if k.startswith('EV_')}
  if ext=='pk6':
   req('superTrainingSet',mode='regimen',id=18,value=True,expected=False)
   training('unlocked',value=True);training('complete',value=True)
   data=training('bag',bag=1,hits=255);assert data['bag']==1 and data['hits']==255
   req('superTrainingSet',mode='bag',bag=2,hits=256,expected=False);assert req('state')['superTraining']==data
  else:
   req('superTrainingSet',mode='unlocked',value=True,expected=False);req('superTrainingSet',mode='bag',bag=1,hits=1,expected=False)
  for index in range(30):
   data=training('regimen',id=index,value=True);assert data['entries'][index]['completed']
  for index in range(6):
   data=training('regimen',id=index,distribution=True,value=True);assert data['distribution'][index]['completed']
  req('superTrainingSet',mode='regimen',id=30,value=True,expected=False)
  req('undo');assert not req('state')['superTraining']['distribution'][-1]['completed'];req('redo')
  before=req('state');out=scratch/('training.'+ext);req('exportEntity',path=str(out));after=req('open',path=str(out));assert after['superTraining']==before['superTraining']
  assert {k:v for k,v in fields(after).items() if k.startswith('EV_')}==evs
  if ext=='pk6':
   data=training('unlocked',value=False);assert not data['complete'] and not any(e['completed'] for e in data['entries'][18:]);assert all(e['completed'] for e in data['entries'][:18])
   req('undo');assert all(e['completed'] for e in req('state')['superTraining']['entries'])
  training('clear');data=training('all',distribution=False);assert all(e['completed'] for e in data['entries']) and not any(e['completed'] for e in data['distribution'])
  data=training('all',distribution=True);assert all(e['completed'] for e in data['distribution'])
  assert source.read_bytes()==original
  print('PASS',ext,'all 36 Super Training regimens, bag/flags, atomic rejection, undo, entity roundtrip and EV preservation',flush=True)
 # Installing the edited Pokémon must retain ribbons and markings in a save slot.
 req('demo',version='PLA');edit('ribbon',id='RibbonHisui',value=1);edit('marking',id='MarkingHeart',value=2)
 req('entityEdit',edits=[],apply=True);req('select',box=0,slot=1,party=False);req('select',box=0,slot=0,party=False)
 assert values(dec())['RibbonHisui']==1 and next(m for m in dec()['markings'] if m['id']=='MarkingHeart')['value']==2
 print('PASS Set to Slot preserves ribbon and shape edits',flush=True)
# Ambiguous generation-6 data uses its extension on both open and import.
 upper=scratch/'ambiguous.PK6';upper.write_bytes(fixture('pk6').read_bytes());assert req('open',path=str(upper))['entityExtension']=='pk6'
 req('demo',version='X');s=req('importEntity',path=str(upper));assert s['entityExtension']=='pk6' and s['superTraining']['native']
 req('undo');assert req('state')['entityName']=='Pikachu'
 print('PASS Gen 6 filename hint on open/import, uppercase extension and import undo',flush=True)
 print('PASS',checks,'decorations/training operations and assertions',flush=True)
finally:
 p.stdin.close();p.wait(timeout=10)
