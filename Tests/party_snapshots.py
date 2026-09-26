#!/usr/bin/env python3
"""Party journal capture inputs across generations, clone identity and failed-open preservation."""
import base64, json, pathlib, subprocess, sys
helper=pathlib.Path(sys.argv[1]).resolve();out=pathlib.Path(sys.argv[2]).resolve();out.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
checks=0
def req(op,ok=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());checks+=1
 assert r['ok']==ok,(op,r.get('error'))
 return r.get('data')
try:
 for game in ['RD','C','E','Pt','B2','X','US','GP','SW','BD','PLA','SL','ZA']:
  before=req('demo',version=game)
  if not before['partySlots']:continue
  for n in range(6):
   before=req('entityEdit',edits=[dict(field='Species',value=str(25 if n<2 else 133)),dict(field='Nickname',value='Pika' if n<2 else 'Eevee')],apply=True,destinationBox=0,destinationSlot=n,destinationParty=True)
  party=[x for x in before['partySlots'] if not x['empty']]
  assert len(party)==6 and [x['index'] for x in party]==list(range(6))
  assert party[0]['journalKey']==party[1]['journalKey'],'clone identity fixture'
  assert all(len(x['journalKey'])==64 and base64.b64decode(x['pokemonData']) for x in party)
  verifier=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
  try:
   for slot in party:
    file=out/(game+'-'+str(slot['index'])+'.'+slot['pokemonExtension']);file.write_bytes(base64.b64decode(slot['pokemonData']))
    verifier.stdin.write(json.dumps({'op':'open','path':str(file)})+'\n');verifier.stdin.flush();opened=json.loads(verifier.stdout.readline());checks+=1
    assert opened['ok'],opened.get('error')
    readback=opened['data'];assert readback['entityExtension']==slot['pokemonExtension']
    assert int(next(f['value'] for f in readback['fields'] if f['id']=='Species'))==slot['species']
  finally:verifier.stdin.close();verifier.wait(timeout=10)
  assert req('state')==before,'reading snapshot inputs changed save'
  (out/(game+'-party.json')).write_text(json.dumps(party))
  bad=out/'invalid save';bad.write_bytes(b'not a Pokemon save')
  req('open',ok=False,path=str(bad));assert req('state')==before,'failed file open changed active save'
  req('undo');assert sum(not x['empty'] for x in req('state')['partySlots'])==5,'failed open cleared undo'
  print('PASS',game,'party snapshots, order/clones, read purity, failed-open state/Undo preservation',flush=True)
 print('PASS',checks,'protocol operations',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
