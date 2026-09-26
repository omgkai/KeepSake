#!/usr/bin/env python3
"""Verify bundled game identities, provenance and helper sample routing."""
import json,pathlib,re,struct,subprocess,sys
source=pathlib.Path(__file__).resolve().parents[1]/'Source'
assets=source/'Assets/GameLogos'
files={p.stem for p in assets.glob('*.png')}
manifest=json.loads((assets/'sources.json').read_text())
assert {x['code'] for x in manifest}==files
for entry in manifest:
 assert entry['source'].startswith('https://') and entry['image'].startswith('https://')
 data=(assets/(entry['code']+'.png')).read_bytes();assert data.startswith(b'\x89PNG\r\n\x1a\n')
 w,h=struct.unpack('>II',data[16:24]);assert w>0 and h>0
p=subprocess.Popen([sys.argv[1]],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
def req(**v):
 p.stdin.write(json.dumps(v)+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok'],r;return r['data']
try:
 games=req(op='sampleGames')
 for game in games:
  code=game['value'];assert code in files
  state=req(op='demo',version=code);assert state['gameLogoVersion']==code,(code,state['gameLogoVersion'])
 assert 'save is SAV4Ranch ? "RANCH"' in (source/'Engine/Program.cs').read_text()
 print('PASS',len(files),'PNG assets and provenance;',len(games),'sample logo identities')
finally:
 p.stdin.close();p.wait(timeout=10)
