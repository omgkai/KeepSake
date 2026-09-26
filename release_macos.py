#!/usr/bin/env python3
"""Sign a COPY of a built app, submit to Apple, staple, and produce a release ZIP.
Credentials stay in Keychain. This command uploads the copied app to Apple.
"""
import argparse,hashlib,json,pathlib,plistlib,subprocess,tempfile

def run(*args,**kw):
 return subprocess.run([str(a) for a in args],check=True,**kw)

def main():
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('app',type=pathlib.Path)
 parser.add_argument('--identity',required=True,help='Installed Developer ID Application identity name or certificate hash')
 parser.add_argument('--keychain-profile',required=True,help='Existing notarytool Keychain profile name, never a password')
 parser.add_argument('--output',type=pathlib.Path,required=True,help='New output directory; must not already exist')
 args=parser.parse_args();app=args.app.resolve();output=args.output.resolve()
 if not (app/'Contents/Info.plist').is_file():parser.error('Choose a built .app bundle.')
 if args.identity=='-':parser.error('A Developer ID identity is required; ad-hoc signing cannot be notarized.')
 if output.exists():parser.error('Choose a new output directory to preserve existing releases.')
 identities=run('security','find-identity','-v','-p','codesigning',capture_output=True,text=True).stdout
 if args.identity not in identities:parser.error('The requested signing identity is not installed in Keychain.')
 run('xcrun','--find','notarytool',capture_output=True)
 output.mkdir(parents=True);target=output/'KeepSake.app';run('ditto',app,target)
 with tempfile.TemporaryDirectory(prefix='keepsake-notarize-') as tmp:
  tmp=pathlib.Path(tmp);entitlements=tmp/'engine.plist'
  entitlements.write_bytes(plistlib.dumps({'com.apple.security.cs.allow-jit':True}))
  magic={b'\xfe\xed\xfa\xce',b'\xce\xfa\xed\xfe',b'\xfe\xed\xfa\xcf',b'\xcf\xfa\xed\xfe',b'\xca\xfe\xba\xbe',b'\xbe\xba\xfe\xca'}
  for file in sorted(target.rglob('*')):
   if file.is_symlink() or not file.is_file():continue
   with file.open('rb') as stream:is_native=stream.read(4) in magic
   if not is_native:continue
   flags=['codesign','--force','--timestamp','--options','runtime','--sign',args.identity]
   if file.name=='PKHeXBridge':flags+=['--entitlements',entitlements]
   run(*flags,file)
  run('codesign','--force','--timestamp','--options','runtime','--sign',args.identity,target)
  run('codesign','--verify','--deep','--strict',target)
  archive=tmp/'submission.zip';run('ditto','-c','-k','--sequesterRsrc','--keepParent',target,archive)
  result=run('xcrun','notarytool','submit',archive,'--keychain-profile',args.keychain_profile,'--wait','--output-format','json',capture_output=True,text=True)
  report=json.loads(result.stdout);(output/'notarization.json').write_text(json.dumps(report,indent=2)+'\n')
  if report.get('status')!='Accepted':raise SystemExit('Apple did not accept this submission. See notarization.json; no release ZIP was created.')
  run('xcrun','stapler','staple',target);run('xcrun','stapler','validate',target)
  run('codesign','--verify','--deep','--strict',target)
  run('spctl','--assess','--type','execute','--verbose=2',target)
  zip_path=output/'KeepSake.zip';run('ditto','-c','-k','--sequesterRsrc','--keepParent',target,zip_path)
  checksum=hashlib.sha256()
  with zip_path.open('rb') as stream:
   for chunk in iter(lambda:stream.read(1024*1024),b''):checksum.update(chunk)
  digest=checksum.hexdigest()
  (output/'SHA256SUMS').write_text(digest+'  KeepSake.zip\n')
  print(zip_path)
if __name__=='__main__':main()
