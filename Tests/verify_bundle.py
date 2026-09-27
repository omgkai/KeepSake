#!/usr/bin/env python3
"""Verify an extracted local bundle; this is not a notarization or parity claim."""
import argparse, hashlib, json, pathlib, plistlib, subprocess, tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('app', type=pathlib.Path)
args = parser.parse_args()
app = args.app.resolve()
root = pathlib.Path(__file__).resolve().parents[1]
subprocess.run(['codesign', '--verify', '--deep', '--strict', str(app)], check=True, timeout=60)
info = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
expected = plistlib.loads((root / 'Source/Packaging/Info.plist').read_bytes())
for key in ['CFBundleIdentifier', 'CFBundleShortVersionString', 'CFBundleVersion']:
    assert info[key] == expected[key], key
assert 'LSEnvironment' not in info, 'QA environment leaked into release bundle'
assert info['CFBundleLocalizations'] == expected['CFBundleLocalizations']
for language in expected['CFBundleLocalizations']:
    resource = pathlib.Path(language + '.lproj') / 'Localizable.strings'
    assert (app / 'Contents/Resources' / resource).read_bytes() == (root / 'Source/Assets/Localization' / resource).read_bytes()
for key in ['SUPublicEDKey','SURequireSignedFeed','SUVerifyUpdateBeforeExtraction']:
    assert info[key] == expected[key], key
framework=app/'Contents/Frameworks/Sparkle.framework'
assert (framework/'Sparkle').is_file()
assert (framework/'Updater.app').is_dir()
assert (app/'Contents/Resources/Sparkle-LICENSE.txt').read_bytes() == (root/'Source/Packaging/Sparkle-LICENSE.txt').read_bytes()
links=subprocess.check_output(['otool','-L',str(app/'Contents/MacOS/PKHeXSwift')],text=True)
assert '@rpath/Sparkle.framework' in links
assert b'io.keepsake.updater-test' not in (app/'Contents/MacOS/PKHeXSwift').read_bytes(), 'QA updater override leaked'
count = 0
for name in ['GamePortraits', 'Portraits', 'Badges', 'Donuts', 'Sprites', 'PaldeaItems', 'HisuiItems', 'Items', 'Balls', 'MoveTypes', 'GameLogos', 'Ribbons', 'Wallpapers']:
    for asset in (root / 'Source/Assets' / name).rglob('*'):
        if not asset.is_file() or asset.name == '.DS_Store':
            continue
        target = app / 'Contents/Resources' / name / asset.relative_to(root / 'Source/Assets' / name)
        assert target.read_bytes() == asset.read_bytes(), str(target)
        count += 1
for name in ['LICENSE', 'THIRD-PARTY-NOTICES.md']:
    assert (app / 'Contents/Resources' / name).read_bytes() == (root / name).read_bytes()
assert (app / 'Contents/Resources/AppIcon.icns').read_bytes() == (root / 'Source/Assets/AppIcon.icns').read_bytes()
for name in ['LICENSE.txt', 'THIRD-PARTY-NOTICES.TXT', 'PKHeX.Core.AutoMod.dll']:
    assert (app / 'Contents/Helpers' / name).is_file(), name
helper = app / 'Contents/Helpers/PKHeXBridge'
requests = [{'op': 'demo', 'version': 'B2'}, {'op': 'extraTools'}, {'op': 'demo', 'version': 'X'}, {'op': 'extraTools'}, {'op': 'demo', 'version': 'VL'}, {'op': 'teamPreview', 'members': [{'species': 25, 'shiny': True, 'name': 'Pikachu'}]}, {'op': 'demo', 'version': 'Pt'}, {'op': 'extraTools'}, {'op':'extraPage','kind':'misc4'}, {'op':'dexBulkOptions'}, {'op':'demo','version':'BD'}, {'op':'eventResearch'}, {'op':'extraTools'}, {'op':'demo','version':'ZA'}, {'op':'zaEvents','group':'Flags','showEmpty':True}, {'op':'nameBytesInfo','field':'Nickname'}]
with tempfile.TemporaryDirectory(prefix='keepsake-bundle-') as scratch:
    result = subprocess.run([str(helper)], input=''.join(json.dumps(r)+'\n' for r in requests), text=True, capture_output=True, cwd=scratch, timeout=90, check=True)
replies = [json.loads(line) for line in result.stdout.splitlines()]
assert len(replies) == len(requests) and all(r['ok'] for r in replies), replies
assert {'dlc5', 'globallink5', 'misc5', 'mail'} <= {r['id'] for r in replies[1]['data']}
assert {'link6', 'training6'} <= {r['id'] for r in replies[3]['data']}
assert {'underground4', 'chatter', 'misc4', 'mail'} <= {r['id'] for r in replies[7]['data']}
assert replies[5]['data']['ready'] and replies[5]['data']['entries'][0]['sprite'].endswith('s')
assert any(row['id'] == 'dotart' for row in replies[8]['data']['entries'])
assert any(row['value'] == 'complete' for row in replies[9]['data']['actions'])
assert replies[10]['data']['canEvents']
assert any(row['section'] == 'System flags' and row['named'] for row in replies[11]['data']['entries'])
assert 'unlocks8b' in {r['id'] for r in replies[12]['data']}
assert len(replies[14]['data']['groups']) == 15 and replies[14]['data']['entries']
assert replies[15]['data']['text'] == 'Pikachu' and replies[15]['data']['capacity'] == 26
print(f'PASS {info["CFBundleShortVersionString"]}: local signature, {count} resources, notices, clean environment, content/Link/Underground/Chatter/Training tools and isolated shiny Auto-Legality generation')
print('Native executable SHA256:', hashlib.sha256((app / 'Contents/MacOS/PKHeXSwift').read_bytes()).hexdigest())
print('Bundle integrity verification does not establish notarization or full Windows parity.')
