#!/usr/bin/env python3
"""Tera special values used by the simplified More controls: storage, bounds and undo."""
import json,pathlib,subprocess,sys
helper,scratch=map(pathlib.Path,sys.argv[1:3]);scratch.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);checks=0
def req(op,expected=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline())
 assert r['ok']==expected,(op,kw,r.get('error'));checks+=1;return r.get('data')
def value(s,k):return next(f['value'] for f in s['fields'] if f['id']==k)
def edit(k,v,expected=True):return req('entityEdit',expected=expected,edits=[dict(field=k,value=v)])
try:
 req('demo',version='SL')
 for k in ['TeraTypeOriginal','TeraTypeOverride']:
  for v in ['Normal','Water','99']:
   s=edit(k,v);assert value(s,k)==v
  for v in ['Any','-1','18','20','127']:
   before=req('state');edit(k,v,False);after=req('state');assert value(after,k)==value(before,k)
  if k=='TeraTypeOriginal':edit(k,'19',False)
  else:assert value(edit(k,'19'),k)=='19'
 s=edit('TeraTypeOverride','99');assert value(s,'TeraTypeOverride')=='99'
 assert value(req('undo'),'TeraTypeOverride')=='19';assert value(req('redo'),'TeraTypeOverride')=='99'
 # Other enum fields must continue rejecting undefined values.
 edit('Nature','99',False)
 out=scratch/'tera.pk9';req('exportEntity',path=str(out));s=req('open',path=str(out));assert value(s,'TeraTypeOriginal')=='99' and value(s,'TeraTypeOverride')=='99'
 assert value(edit('TeraTypeOverride','19'),'TeraTypeOverride')=='19'
 out=scratch/'unchanged.pk9';req('exportEntity',path=str(out));s=req('open',path=str(out));assert value(s,'TeraTypeOverride')=='19'
 before=value(s,'IV_ATK');assert value(edit('HT_ATK','true'),'IV_ATK')==before;assert value(req('undo'),'HT_ATK')=='false'
 print('PASS',checks,'More-details operations: valid Tera types, unchanged/Stellar values, invalid enum rejection, undo and Pokémon-file roundtrip',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
