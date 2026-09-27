from pathlib import Path
import json,collections
import argparse
parser=argparse.ArgumentParser(description='Map PKHeX WinForms translations to native common labels.')
parser.add_argument('pkhex_text',type=Path,help='PKHeX.WinForms/Resources/text directory at the bundled revision')
args=parser.parse_args()
src=args.pkhex_text;dest=Path(__file__).resolve().parent
langs=['en','fr','de','es','it','ja','ko','zh-Hans','zh-Hant','es-419']
def read(lang):return dict(l.split('=',1) for l in (src/f'lang_{lang}.txt').read_text(encoding='utf-8-sig').splitlines() if '=' in l)
def clean(s):return s.replace('&','').strip().removesuffix(':').replace('...','…')
en=read('en');tables={}
for lang in langs:
 raw=read(lang);table={}
 for key,value in en.items():
  english=clean(value);translated=clean(raw.get(key,value))
  if english and '\\' not in english and '\n' not in english and '{' not in english and translated:
   if english not in table or table[english]==english:table[english]=translated
 tables[lang]=table
for row in (dest/'keepSake.tsv').read_text().splitlines():
 cells=row.split('|');assert len(cells)==9,(cells[0],len(cells))
 english=cells[0]
 for lang,text in zip(['en','fr','de','es','it','ja','ko','zh-Hans','zh-Hant'],cells):tables[lang][english]=text
 tables['es-419'][english]=cells[3]
for lang,table in tables.items():
 d=dest/f'{lang}.lproj';d.mkdir(exist_ok=True)
 (d/'Localizable.strings').write_text('\n'.join(json.dumps(k,ensure_ascii=False)+' = '+json.dumps(v,ensure_ascii=False)+';' for k,v in sorted(table.items()))+'\n')
 print(lang,len(table))
(dest/'README.md').write_text('Common editor translations are mapped from PKHeX.WinForms Resources/text/lang_*.txt at the vendored PKHeX revision. GPL-3.0-or-later. KeepSake-specific navigation translations are in keepSake.tsv. Advanced prose without a translation falls back to English; this is not a claim of complete interface translation.\n')
