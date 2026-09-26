#!/usr/bin/env python3
"""Cross-generation wardrobe, journals, event work, treat cases and block operations."""
import json,pathlib,subprocess,sys,zipfile
helper=pathlib.Path(sys.argv[1]);out=pathlib.Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen([str(helper)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True);checks=0
def req(op,ok=True,**kw):
 global checks
 p.stdin.write(json.dumps(dict(op=op,**kw))+'\n');p.stdin.flush();r=json.loads(p.stdout.readline());assert r['ok']==ok,(op,kw,r.get('error'));checks+=1;return r.get('data')
def rev():return req('state')['revision']
try:
 for game in ['RD','C','E','Pt','B2','X','AS','SN','US','GP','SW','BD','PLA','SL','ZA']:
  state=req('demo',version=game);key=state['entityJournalKey'];assert len(key)==64 and key==state['slots'][0]['journalKey']
  guide=req('speciesGuide');assert guide['entries'] and all(len(e['stats'])==6 and 0<=e['type1']<=17 for e in guide['entries'])
  assert req('state')==state
  req('exportEntity',path=str(out/(game+'-before.pk')))
  req('entitySet',field='Nickname',value='Keepsake');assert req('state')['entityJournalKey']==key;req('undo')
  req('exportEntity',path=str(out/(game+'-after.pk')));assert (out/(game+'-before.pk')).read_bytes()==(out/(game+'-after.pk')).read_bytes()
  if state['generation']>=6:
   req('entityAction',action='shiny');assert req('state')['entityJournalKey']==key;req('undo')
  work=req('eventWork')
  if work['supported']:
   req('eventWorkSet',id=0,value='1',revision=work['revision']);assert req('eventWork')['entries'][0]['value']=='1';req('undo');assert req('eventWork')['entries']==work['entries']
   req('eventWorkSet',ok=False,id=-1,value='0',revision=rev());req('eventWorkSet',ok=False,id=0,value='1',revision=-1);req('eventWorkSet',ok=False,id=0,value='9999999999999999',revision=rev());assert req('eventWork')['entries']==work['entries']
  if state['canFashion']:
   f=req('fashion');rows=f['items'];editable=[r for r in rows if r['editable']]
   if editable:
    row=editable[0];req('wardrobeSet',revision=f['revision'],id=row['id'],owned=not row['owned']);assert next(x for x in req('fashion')['items'] if x['id']==row['id'])['owned']!=row['owned'];req('undo');assert req('fashion')['items']==rows
    req('wardrobeSet',revision=rev(),mode='give',category=row['category']);assert all(x['owned'] for x in req('fashion')['items'] if x['editable'] and x['category']==row['category']);req('undo');assert req('fashion')['items']==rows
    req('wardrobeSet',ok=False,revision=-1,id=row['id'],owned=True);req('wardrobeSet',ok=False,revision=rev(),id='invalid',owned=True);assert req('fashion')['items']==rows
  cases=req('treats');assert cases['supported']==state['canTreats']
  if cases['supported']:assert req('treats',kind='previous-game-case')['kind']==cases['kind']
  for collection in cases['collections']:
   kind=collection['value'];original=req('treats',kind=kind);assert original['entries']
   req('treatsSet',kind=kind,mode='give',revision=rev());full=req('treats',kind=kind);assert full['entries']!=original['entries'];req('undo');assert req('treats',kind=kind)['entries']==original['entries']
   entry=original['entries'][0];field=entry['fields'][0];value=field['choices'][-1]['value'] if field['choices'] else '1'
   req('treatsSet',kind=kind,id=entry['id'],revision=rev(),edits=[dict(field=field['id'],value=value)]);assert req('treats',kind=kind)['entries'][0]['fields'][0]['value']==value;req('undo');assert req('treats',kind=kind)['entries']==original['entries']
   req('treatsSet',ok=False,kind=kind,id=-1,revision=rev(),edits=[]);req('treatsSet',ok=False,kind=kind,mode='give',revision=-1)
   req('treatsSet',kind=kind,mode='give',revision=rev());full=req('treats',kind=kind);req('treatsSet',kind=kind,mode='clear',revision=rev());req('undo');assert req('treats',kind=kind)['entries']==full['entries'];req('undo')
   if kind=='puffs':
    req('treatsSet',kind=kind,mode='best',revision=rev());req('treatsSet',kind=kind,mode='sort',revision=rev());req('undo');req('undo')
  print('PASS',game,'guide, identity, event variables, wardrobe and supported treats',flush=True)
 for game in ['SW','PLA','SL','ZA']:
  req('demo',version=game);listing=req('saveBlocks');assert listing['supported']
  block=next(x for x in listing['entries'] if x['size']>0);key=block['id'];before=req('saveBlock',key=key)
  dest=out/(game+'-'+key+'.bin');req('saveBlockExport',key=key,path=str(dest));original=dest.read_bytes();assert len(original)==before['size']
  changed=out/'replacement.bin';changed.write_bytes(bytes([original[0]^1])+original[1:]);req('saveBlockSet',key=key,mode='import',path=str(changed),revision=rev());assert req('saveBlock',key=key)['hex']!=before['hex'];req('undo');assert req('saveBlock',key=key)['hex']==before['hex']
  changed.write_bytes(b'');req('saveBlockSet',ok=False,key=key,mode='import',path=str(changed),revision=rev());assert req('saveBlock',key=key)['hex']==before['hex']
  req('saveBlockSet',ok=False,key=key,value='1',revision=-1)
  archive=out/(game+'-blocks.zip');req('saveBlocksExport',path=str(archive))
  with zipfile.ZipFile(archive) as z:
   metadata=json.loads(z.read('blocks.json'));assert len(metadata)==listing['total'];assert z.read(key+'.bin')==original
  print('PASS',game,'block import/export, archive, size rejection and Undo',flush=True)
 print('PASS',checks,'RC tool operations',flush=True)
finally:p.stdin.close();p.wait(timeout=10)
