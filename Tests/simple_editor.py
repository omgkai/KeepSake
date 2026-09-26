#!/usr/bin/env python3
"""Alpha and transactional simple-editor checks on synthetic/public fixtures."""
import json, pathlib, subprocess, sys
helper, fixtures, scratch = map(pathlib.Path, sys.argv[1:4])
scratch.mkdir(parents=True, exist_ok=True)
p = subprocess.Popen([str(helper)], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
checks = 0
def req(op, expected=True, **kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op, **kw))+'\n'); p.stdin.flush()
 result=json.loads(p.stdout.readline()); assert result['ok']==expected, (op,result)
 checks+=1; return result.get('data')
def field(s,k): return next(f['value'] for f in s['fields'] if f['id']==k)
def edit(**kw): return [{'field':k,'value':str(v)} for k,v in kw.items()]
try:
 source=next(f for f in fixtures.rglob('*.pa8') if 'Legal' in f.parts)
 original=source.read_bytes()
 s=req('open',path=str(source)); old=field(s,'IsAlpha')
 size={k:field(s,k) for k in ['HeightScalar','WeightScalar']}
 s=req('entityEdit',edits=edit(IsAlpha='false' if old=='true' else 'true'))
 assert field(s,'IsAlpha')!=old and all(field(s,k)==v for k,v in size.items())
 assert field(req('undo'),'IsAlpha')==old
 assert field(req('redo'),'IsAlpha')!=old
 req('exportEntity',path=str(scratch/'alpha.pa8'))
 s=req('open',path=str(scratch/'alpha.pa8')); assert field(s,'IsAlpha')!=old
 s=req('entityEdit',edits=edit(IsAlpha=old)); assert field(s,'IsAlpha')==old
 assert source.read_bytes()==original
 print('PASS Alpha on/off, undo/redo, export/reopen, size and original preservation',flush=True)
 s=req('demo',version='PLA'); before=field(s,'Nickname')
 req('entityEdit',edits=edit(Nickname='FAILED',CurrentLevel='101'),apply=True,expected=False)
 s=req('state');assert field(s,'Nickname')==before and not s['pending'] and not s['dirty'] and not s['canUndo']
 s=req('entityEdit',edits=edit(Nickname='ALPHA',CurrentLevel='40',IsAlpha='true'),apply=True)
 assert not s['pending'] and s['dirty'] and s['entityLevel']==40
 s=req('select',box=0,slot=0,party=False); assert field(s,'Nickname')=='ALPHA' and field(s,'IsAlpha')=='true'
 s=req('undo');assert field(s,'Nickname')==before and s['entityLevel']==25 and not s['dirty']
 assert len(s['stats'])==6
 assert req('lookup',kind='forms')
 s=req('entityEdit',edits=edit(Nickname='')); assert field(s,'IsNicknamed')=='false'
 s=req('entityAction',action='maxGrit'); assert all(int(field(s,'GV_'+k))==10 for k in ['HP','ATK','DEF','SPA','SPD','SPE'])
 req('demo',version='SL'); req('entityEdit',edits=edit(IsAlpha='true'),expected=False)
 assert not req('state')['pending']
 s=req('entityEdit',edits=edit(IsShiny='true')); assert field(s,'IsShiny')=='true'
 s=req('entityEdit',edits=edit(IsShiny='false')); assert field(s,'IsShiny')=='false'
 print('PASS atomic Set to Slot, rollback, undo, nickname reset, stats, forms, grit, shiny and unsupported Alpha rejection',flush=True)
 print('PASS',checks,'simple-editor operations and assertions',flush=True)
finally:
 p.stdin.close();p.wait(timeout=10)
