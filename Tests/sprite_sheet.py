#!/usr/bin/env python3
"""Verify every user-supplied sprite pixel against the documented source cell. Requires Pillow."""
from pathlib import Path
import json,hashlib,sys
from PIL import Image
assets=Path(sys.argv[1]);root=assets/'Gen9SpriteSheet';manifest=sys.argv[2] if len(sys.argv)>2 else 'manifest.json';m=json.loads((root/manifest).read_text());source=root/m['source']
assert hashlib.sha256(source.read_bytes()).hexdigest()==m['sha256']
im=Image.open(source).convert('RGBA');seen=set()
for e in m['entries']:
 assert e['file'] not in seen;seen.add(e['file']);x,y=e['column']*32,e['row']*32
 actual=Image.open(assets/'Sprites'/e['file']).convert('RGBA');assert actual.size==(32,32) and actual.tobytes()==im.crop((x,y,x+32,y+32)).tobytes(),e
assert len(seen)==142
assert {e['species'] for e in m['entries']} >= set(range(906,1026))
assert next(e for e in m['entries'] if e['file'] in ('b_925.png','b_925s.png'))['row']==11
assert next(e for e in m['entries'] if e['file'] in ('b_925-1.png','b_925-1s.png'))['row']==1
print('PASS all 142 sprite cells: source hash, exact RGBA pixels, unique filenames, Gen 9 coverage, Maushold mapping')
