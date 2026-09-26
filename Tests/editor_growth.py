#!/usr/bin/env python3
"""Transactional training, growth, Pokérus and ability presentation checks."""
import json,subprocess,sys
p=subprocess.Popen([sys.argv[1]],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True); count=0

def call(op,ok=True,**kw):
 global count
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==ok,r;count+=1;return r.get('data')
def fields(s):return {f['id']:f['value'] for f in s['fields']}
try:
 for version in ['RD','GD','E','HG','B2','X','US','GP','SW','PLA','BD','SL','ZA']:
  s=call('demo',version=version);g=s['growth'];assert g['level']==25 and g['floor']<=g['exp']<g['next']
  if version not in ['RD','GD']:
   options=call('lookup',kind='entityAbilities')
   if options:s=call('entityEdit',edits=[{'field':'AbilityChoice','value':options[0]['value']}])
  assert g['pokerus']==(version not in ['RD','GP','SL','ZA']),version
  original=fields(s)
  for action in ['clearIV','maxIV','randomIV']:
   s=call('entityAction',action=action);f=fields(s);ivs=[int(f['IV_'+k]) for k in ['HP','ATK','DEF','SPA','SPD','SPE'] if 'IV_'+k in f]
   assert all(0<=v<=(15 if version in ['RD','GD'] else 31) for v in ivs)
   if action=='clearIV':assert not any(ivs)
   if action=='maxIV':assert all(v==(15 if version in ['RD','GD'] else 31) for v in ivs)
   assert fields(call('undo'))==original
  if version not in ['GP','PLA']:
   for action in ['maxEV','randomEV','clearEV']:
    f=fields(call('entityAction',action=action));evs=[int(f['EV_'+k]) for k in ['HP','ATK','DEF','SPA','SPD','SPE'] if 'EV_'+k in f]
    assert all(0<=v<=(65535 if version in ['RD','GD'] else 252) for v in evs)
    if version not in ['RD','GD']:assert sum(evs)<=510
    if action=='clearEV':assert not any(evs)
    assert fields(call('undo'))==original
  if version not in ['RD','GD']:
   f=fields(call('entityAction',action='rerollPID'));assert f['PID']!=original['PID'] and f['Gender']==original['Gender'] and f['Nature']==original['Nature'] and f['IsShiny']=='false'
   assert fields(call('undo'))==original
   choices=call('lookup',kind='entityAbilities');assert all('Ability 1' not in c['label'] and 'Ability 2' not in c['label'] for c in choices)
   assert s['abilityDescription'],version
  for mode in ['Infected','Cured','None']:
   r=call('entityAction',action='pokerus'+mode,ok=g['pokerus'])
   if g['pokerus']:
    assert r['growth']['pokerusState']==mode
    assert fields(call('undo'))==original
  print('PASS',version,'growth/training/PID/Pokérus/abilities',flush=True)
 print('PASS',count,'requests',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
