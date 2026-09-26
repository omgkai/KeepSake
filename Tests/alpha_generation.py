#!/usr/bin/env python3
"""Regression: Auto-Legality preserves requested Alpha; search exposes real Alpha encounters."""
import json,pathlib,subprocess,sys,tempfile
with tempfile.TemporaryDirectory(prefix='keepsake-alpha-') as tmp:
 p=subprocess.Popen([sys.argv[1],str(pathlib.Path(tmp)/'settings.json')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
 count=0
 def call(op,ok=True,**kw):
  global count
  p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());count+=1;assert r['ok']==ok,(op,r);return r.get('data')
 def alpha(state):return next(f['value'] for f in state['fields'] if f['id']=='IsAlpha')
 try:
  call('demo',version='PLA')
  for species in [25,399,403]:
   for mode in ['Alpha','NotAlpha']:
    results=call('encounterSearch',species=species,version='PLA',alpha=mode)
    assert results['entries'] and all(row['alpha']==(mode=='Alpha') for row in results['entries'])
    state=call('encounterPrepare',id=results['entries'][0]['id'],token=results['token'])
    assert alpha(state)==str(mode=='Alpha').lower()
  before=call('state');call('encounterSearch',ok=False,species=25,alpha='invalid');assert call('state')==before
  results=call('encounterSearch',species=25,version='PLA',alpha='NotAlpha',category='Slot')
  state=call('encounterPrepare',id=results['entries'][0]['id'],token=results['token']);assert alpha(state)=='false'
  edits=[dict(field='IsAlpha',value='true'),dict(field='CurrentLevel',value='100'),dict(field='Move4',value='85')]+[dict(field='IV_'+n,value='31') for n in ['HP','ATK','DEF','SPA','SPD','SPE']]
  before=call('entityEdit',edits=edits);assert before['legality']=='invalid' and alpha(before)=='true'
  preview=call('legalityPreview');assert preview['ready'];assert call('state')==before
  state=call('legalityApply',token=preview['token']);assert state['legality']=='valid' and alpha(state)=='true'
  assert call('undo')['fields']==before['fields']
  # A level-one Alpha Pikachu cannot be generated; failing must leave the draft intact.
  before=call('entityEdit',edits=[dict(field='CurrentLevel',value='1')]);call('legalityPreview',ok=False);assert call('state')==before
  print(f'PASS: {count} protocol requests; Alpha/non-Alpha search and preparation for three species, Alpha regeneration, preview purity, Undo and impossible-request preservation')
 finally:p.stdin.close();p.wait(timeout=10)
