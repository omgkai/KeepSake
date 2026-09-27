#!/usr/bin/env python3
import importlib.util,pathlib,plistlib,struct,tempfile,zipfile,xml.etree.ElementTree as ET
root=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('appcast',root/'create_appcast.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
base=plistlib.loads((root/'Source/Packaging/Info.plist').read_bytes())
with tempfile.TemporaryDirectory() as tmp:
 archive=pathlib.Path(tmp)/f"KeepSake-{base['CFBundleShortVersionString']}-macOS-arm64.zip"
 def write(info,cpu=0x0100000C):
  with zipfile.ZipFile(archive,'w') as z:
   z.writestr('KeepSake.app/Contents/Info.plist',plistlib.dumps(info));z.writestr('KeepSake.app/Contents/MacOS/PKHeXSwift',struct.pack('<II',0xFEEDFACF,cpu))
 write(base);info=module.metadata(archive,'arm64');feed=ET.fromstring(module.feed(info,archive,'signature'))
 item=feed.find('channel/item');assert item.find('{'+module.SPARKLE+'}version').text==base['CFBundleVersion']
 enclosure=item.find('enclosure');assert enclosure.get('url').endswith('/'+archive.name);assert enclosure.get('length')==str(archive.stat().st_size)
 for change,cpu in [({'CFBundleIdentifier':'wrong.app'},0x0100000C),({'SURequireSignedFeed':False},0x0100000C),({'SUVerifyUpdateBeforeExtraction':False},0x0100000C),({'CFBundleVersion':'invalid'},0x0100000C),({'CFBundleShortVersionString':'0.1'},0x0100000C),({},0x01000007)]:
  write(dict(base,**change),cpu)
  try:module.metadata(archive,'arm64')
  except ValueError:pass
  else:raise AssertionError((change,cpu))
print('PASS: appcast version, URL, size, identity, signing policy and CPU architecture guards')
