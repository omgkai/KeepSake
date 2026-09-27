#!/usr/bin/env python3
"""Create a signed Sparkle feed for a notarized KeepSake release archive.
The update-signing private key stays in macOS Keychain.
"""
import argparse, struct, datetime, email.utils, pathlib, plistlib, subprocess, tempfile, urllib.parse, xml.etree.ElementTree as ET, zipfile
SPARKLE='http://www.andymatuschak.org/xml-namespaces/sparkle'
ET.register_namespace('sparkle',SPARKLE)

def metadata(archive, architecture):
    with zipfile.ZipFile(archive) as z:
        info=plistlib.loads(z.read('KeepSake.app/Contents/Info.plist'))
        with z.open('KeepSake.app/Contents/MacOS/PKHeXSwift') as executable:header=executable.read(8)
        if len(header)!=8 or struct.unpack('<II',header)!=(0xFEEDFACF,{'arm64':0x0100000C,'x86_64':0x01000007}.get(architecture)):raise ValueError('Executable architecture does not match feed')
    if info.get('CFBundleIdentifier')!='io.keepsake.app':raise ValueError('Unexpected app identity')
    version=info['CFBundleShortVersionString'];build=info['CFBundleVersion']
    if not build.isdigit() or not all(p.isdigit() for p in version.split('.')):raise ValueError('Invalid release version')
    if architecture not in ('arm64','x86_64'):raise ValueError('Unsupported architecture')
    if archive.name!=f'KeepSake-{version}-macOS-{architecture}.zip':raise ValueError('Archive name does not match app version and architecture')
    for key in ('SUPublicEDKey','SUVerifyUpdateBeforeExtraction','SURequireSignedFeed'):
        if not info.get(key):raise ValueError('Missing updater verification configuration')
    return info

def feed(info,archive,signature):
    root=ET.Element('rss',{'version':'2.0'});channel=ET.SubElement(root,'channel')
    ET.SubElement(channel,'title').text='KeepSake updates'
    item=ET.SubElement(channel,'item');ET.SubElement(item,'title').text='KeepSake '+info['CFBundleShortVersionString']
    ET.SubElement(item,'pubDate').text=email.utils.format_datetime(datetime.datetime.now(datetime.timezone.utc))
    ET.SubElement(item,'{'+SPARKLE+'}version').text=info['CFBundleVersion']
    ET.SubElement(item,'{'+SPARKLE+'}shortVersionString').text=info['CFBundleShortVersionString']
    ET.SubElement(item,'{'+SPARKLE+'}minimumSystemVersion').text=info['LSMinimumSystemVersion']
    ET.SubElement(item,'description').text='A new KeepSake update is ready. Your journals and save files stay on this Mac. Export unsaved work before installing.'
    url='https://github.com/omgkai/KeepSake/releases/download/v'+info['CFBundleShortVersionString']+'/'+urllib.parse.quote(archive.name)
    ET.SubElement(item,'enclosure',{'url':url,'length':str(archive.stat().st_size),'type':'application/octet-stream','{'+SPARKLE+'}edSignature':signature})
    return ET.tostring(root,encoding='utf-8',xml_declaration=True)

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('archive',type=pathlib.Path);p.add_argument('--architecture',choices=['arm64','x86_64'],required=True);p.add_argument('--sign-update',type=pathlib.Path,required=True);p.add_argument('--account',default='KeepSake');p.add_argument('--output',type=pathlib.Path,required=True);a=p.parse_args()
    info=metadata(a.archive,a.architecture)
    def sign(*args):return subprocess.run([str(a.sign_update),'--account',a.account,*map(str,args)],check=True,capture_output=True,text=True).stdout.strip()
    public=subprocess.check_output([str(a.sign_update.with_name('generate_keys')),'--account',a.account,'-p'],text=True).strip()
    if public!=info['SUPublicEDKey']:raise ValueError('Keychain signing key does not match the app public key')
    signature=sign('-p',a.archive)
    sign('--verify',a.archive,signature)
    a.output.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(dir=a.output.parent) as tmp:
        path=pathlib.Path(tmp)/'appcast.xml';path.write_bytes(feed(info,a.archive,signature));sign(path);sign('--verify',path);path.replace(a.output)
    print('Signed and verified:',a.output)
if __name__=='__main__':main()
