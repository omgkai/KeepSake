from pathlib import Path
import json,csv,re,unicodedata,collections
r=Path(__file__).resolve().parent;dest=r.parents[1]/'Source/Assets/Portraits';rows=json.loads((r/'forms.json').read_text());pk={int(x['id']):x for x in csv.DictReader((r/'pokemon.csv').open())};forms=list(csv.DictReader((r/'pokemon_forms.csv').open()))
def norm(x):return re.sub('[^a-z0-9]','',unicodedata.normalize('NFD',x.lower()))
byform={}
for f in forms:
 p=pk[int(f['pokemon_id'])];sp=int(p['species_id']);tag=f['form_identifier'];named=f'{sp}-{tag}.png';numeric=p['id']+'.png'
 file=named if (dest/named).exists() else numeric if f['is_default']=='1' and (dest/numeric).exists() else None
 if file:byform[sp,norm(tag)]=file
names={x['species']:norm(x['name']) for x in rows}
for entry in pk.values():
 sp=int(entry['species_id']);name=norm(entry['identifier']);base=names.get(sp,'');file=entry['id']+'.png'
 if base and name.startswith(base) and (dest/file).exists():byform[sp,name[len(base):]]=file
# Filename-only cosmetic forms supplement the API's stat-form records.
for p in dest.glob('*.png'):
 if '-' in p.stem:
  sp,tag=p.stem.split('-',1)
  if sp.isdigit():byform[int(sp),norm(tag)]=p.name
aliases={'f':'female','m':'male','mmega':'malemega','fmega':'femalemega','normalmega':'mega','paldeacombat':'paldeacombatbreed','paldeablaze':'paldeablazebreed','paldeaaqua':'paldeaaquabreed','noiceface':'noice','jumbo':'super','alolan':'alola','galarian':'galar','hisuian':'hisui','paldean':'paldea','original':'originalcap','hoenn':'hoenncap','sinnoh':'sinnohcap','unova':'unovacap','kalos':'kaloscap','alola':'alolacap','partner':'partnercap','world':'worldcap','phd':'phd','pokeball':'pokeball','westsea':'west','eastsea':'east','plantcloak':'plant','sandycloak':'sandy','trashcloak':'trash','50':'50','10':'10','familyofthree':'familyofthree','familyoffour':'familyoffour','twosegment':'twosegment','threesegment':'threesegment','single strike':'singlestrike'}
identities={};sprites={};unmatched={};collisions=[]
for row in rows:
 sp=row['species'];fo=row['form'];g=row['gender'];arg=row['argument'];names=[norm(row['formName']),norm(row['showdown'])];file=None
 if sp in [414,664,665] or (sp in [854,855,1012,1013] and fo==1) or (sp in [25,133] and row['formName']=='Starter'):file=f'{sp}.png'
 elif sp==550 and fo==2:file='10247.png'
 elif sp==555 and fo==2:file='10177.png'
 elif sp==718 and fo==3:file='718.png'
 elif sp==774 and fo<7:file='774.png'
 elif sp==649 and fo>0:file=byform.get((sp,['','douse','shock','burn','chill'][fo]))
 elif sp==931 and fo>0:file=byform.get((sp,['','blueplumage','yellowplumage','whiteplumage'][fo]))
 elif sp in [1007,1008] and fo>0:file=byform.get((sp,norm(row['formName'])+('build' if sp==1007 else 'mode')))
 elif sp==1017 and fo in [1,2,3]:file=byform.get((sp,norm(row['formName'])+'mask'))
 elif sp==869:
  creams=['vanilla-cream','ruby-cream','matcha-cream','mint-cream','lemon-cream','salted-cream','ruby-swirl','caramel-swirl','rainbow-swirl'];sweets=['strawberry','berry','love','star','clover','flower','ribbon']
  if fo<len(creams):file=f'869-{creams[fo]}-{sweets[arg]}-sweet.png'
 elif sp==201:file=f'201-{chr(97+fo) if fo<26 else "exclamation" if fo==26 else "question"}.png'
 else:
  for tag in names:
   options=[tag,aliases.get(tag,tag)]
   if sp==25 and row['context']!='Gen6':options=options[::-1]
   for key in options:
    if (sp,key) in byform:file=byform[sp,key];break
   if file:break
  if file is None and fo==0:file=f'{sp}.png'
 if file and (dest/file).exists():
  identities[f"{row['context']}:{sp}:{fo}:{arg}"]=file
  portrait=('female/'+file) if g==1 and (dest/'female'/file).exists() else file
  # Prefer base-form identities for sprite keys intentionally shared by multiple forms.
  key=row['sprite'];old=sprites.get(key)
  if old is None or (fo==0 and g==0):sprites[key]=portrait
 else:unmatched[f'{sp}:{fo}:{arg}']=(row['name'],row['formName'],row['showdown'])
for sp in range(1,1026):
 assert (dest/f'{sp}.png').exists() and (dest/f'shiny/{sp}.png').exists(),sp
 sprites.setdefault(f'b_{sp}',f'{sp}.png')
(dest/'manifest.json').write_text(json.dumps({'identities':identities,'sprites':sprites},sort_keys=True,indent=2))
(r/'unmatched.json').write_text(json.dumps(unmatched,indent=2));print('identities',len(identities),'sprite aliases',len(sprites),'unmatched',len(unmatched));print(json.dumps(unmatched,indent=1)[:18000])
