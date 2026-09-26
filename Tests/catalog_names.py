"""Every sample game's dropdown labels must decode as nonempty Swift strings."""
import json,subprocess,sys
p=subprocess.Popen([sys.argv[1]],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
checks=0

def req(op,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok'],r;checks+=1;return r['data']
try:
 for game in req('sampleGames'):
  s=req('demo',version=game['value'])
  for kind in ['species','items','moves','abilities','natures','balls','games']:
   for c in req('lookup',kind=kind):
    assert isinstance(c['value'],str) and isinstance(c['label'],str) and c['label'].strip(),(game,kind,c)
  if s['canInventory']:
   for pouch in req('inventory')['pouches']:
    assert all(isinstance(c['label'],str) and c['label'].strip() for c in pouch['choices'])
    assert all(isinstance(c['name'],str) and c['name'].strip() for c in pouch['items'])
 print('PASS',checks,'catalog/inventory requests across all 39 sample games; no null or blank labels')
finally:
 p.stdin.close();p.wait(timeout=15)
