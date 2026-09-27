#!/usr/bin/env python3
"""Stage a newer official PKHeX release for review; never merge or publish apps."""
import json, os, pathlib, re, shutil, subprocess, tempfile, urllib.request, xml.etree.ElementTree as ET
ROOT = pathlib.Path(__file__).resolve().parents[1]
PIN = ROOT / 'Source/Vendor/PKHeX-UPSTREAM.json'
UPSTREAM = 'kwsch/PKHeX'

def run(*args, cwd=ROOT):
    return subprocess.check_output(args, cwd=cwd, text=True).strip()

def api(path):
    request = urllib.request.Request('https://api.github.com/' + path,
        headers={'Authorization': 'Bearer ' + os.environ['GH_TOKEN'], 'Accept': 'application/vnd.github+json'})
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.load(response)

def version(tag):
    if not re.fullmatch(r'\d{2}\.\d{2}\.\d{2}', tag):
        raise ValueError('Unrecognized official release tag; manual review required: ' + tag)
    return tuple(map(int, tag.split('.')))

def output(**values):
    with open(os.environ['GITHUB_OUTPUT'], 'a') as stream:
        for key, value in values.items():
            stream.write(f'{key}={value}\n')

def main():
    pin = json.loads(PIN.read_text())
    release = api(f'repos/{UPSTREAM}/releases/latest')
    tag = release['tag_name']
    if release['draft'] or release['prerelease']:
        raise ValueError('Expected a stable published release')
    if version(tag) <= version(pin['tag']):
        print(f'Already current: bundled {pin["tag"]}; latest official release {tag}.')
        output(check='true' if os.environ.get('CHECK_CURRENT') == 'true' else 'false', ref=run('git', 'rev-parse', 'HEAD'))
        return
    repo = os.environ['GITHUB_REPOSITORY']
    branch = 'upstream/pkhex-' + tag
    pulls = json.loads(run('gh', 'pr', 'list', '--repo', repo, '--head', branch, '--state', 'all', '--json', 'number,state,headRefOid,url'))
    if pulls:
        opened = next((p for p in pulls if p['state'] == 'OPEN'), None)
        if opened:
            print('Existing update PR: ' + opened['url'])
            output(check='true', ref=opened['headRefOid'])
        else:
            print('This release already has a closed/merged PR; not reopening it automatically.')
            output(check='false')
        return
    remote = run('git', 'ls-remote', '--heads', 'origin', 'refs/heads/' + branch)
    if remote:
        # Recover a previous run that pushed successfully but could not create its PR.
        sha = remote.split()[0]
    else:
        with tempfile.TemporaryDirectory() as temp:
            source = pathlib.Path(temp) / 'PKHeX'
            run('git', 'clone', '--quiet', '--depth=1', '--single-branch', '--branch', tag,
                'https://github.com/' + UPSTREAM + '.git', str(source))
            sha_upstream = run('git', 'rev-parse', 'HEAD', cwd=source)
            props = ET.parse(source / 'Directory.Build.props').getroot()
            found = props.findtext('./PropertyGroup/Version')
            if found is None or tuple(map(int, found.split('.'))) != version(tag):
                raise ValueError('Release tag and Core version differ; review manually.')
            run('git', 'switch', '-c', branch)
            vendor = ROOT / 'Source/Vendor'
            shutil.rmtree(vendor / 'PKHeX.Core')
            shutil.copytree(source / 'PKHeX.Core', vendor / 'PKHeX.Core', symlinks=True)
            for name in ('Directory.Build.props', 'LICENSE', 'icon.png'):
                shutil.copy2(source / name, vendor / name)
            shutil.copy2(source / 'README.md', vendor / 'PKHeX-CORE-README.md')
            pin.update(tag=tag, commit=sha_upstream, release_url=f'https://github.com/{UPSTREAM}/releases/tag/{tag}')
            PIN.write_text(json.dumps(pin, indent=2) + '\n')
            build = ROOT / 'build.sh'
            text, count = re.subn(r'-p:SourceRevisionId=[0-9a-f]{40}', '-p:SourceRevisionId=' + sha_upstream, build.read_text())
            if count != 1:
                raise ValueError('Could not locate the build revision pin')
            build.write_text(text)
            run('git', 'config', 'user.name', 'github-actions[bot]')
            run('git', 'config', 'user.email', '41898282+github-actions[bot]@users.noreply.github.com')
            run('git', 'add', 'Source/Vendor/PKHeX.Core', 'Source/Vendor/Directory.Build.props', 'Source/Vendor/LICENSE',
                'Source/Vendor/icon.png', 'Source/Vendor/PKHeX-CORE-README.md', 'Source/Vendor/PKHeX-UPSTREAM.json', 'build.sh')
            run('git', 'commit', '-m', 'Update PKHeX.Core to ' + tag)
            sha = run('git', 'rev-parse', 'HEAD')
            run('git', 'push', 'origin', 'HEAD:refs/heads/' + branch)
    if not re.fullmatch('[0-9a-f]{40}', sha):
        raise ValueError('Invalid candidate commit')
    body = f'''Update the bundled PKHeX.Core to official release [{tag}](https://github.com/{UPSTREAM}/releases/tag/{tag}).

This is a review candidate. It does not merge itself, update Sparkle feeds, sign an app, or publish downloads.

Core source, its build properties, license and revision metadata are updated together. Auto-Legality stays pinned so compatibility failures remain visible. Existing sprites, translated catalogs, artwork, and extracted Windows data retain their separately documented revisions.

The scheduled workflow runs engine/Auto-Legality compilation and synthetic-save compatibility checks against this exact commit. See the **PKHeX compatibility** commit status and [workflow run](https://github.com/{repo}/actions/runs/{os.environ['GITHUB_RUN_ID']}). Missing or failing status means the update is not ready.

Before merging:
- [ ] Review upstream changes and any required bridge or Auto-Legality changes.
- [ ] Confirm compatibility checks pass; review native Mac builds and relevant real saves.
- [ ] Decide whether catalogs or extracted game data also need refreshing.
- [ ] Prepare a separate KeepSake release, sign/notarize both Mac builds, and publish signed update feeds after review.
'''
    with tempfile.NamedTemporaryFile(mode='w', suffix='.md') as stream:
        stream.write(body); stream.flush()
        url = run('gh', 'pr', 'create', '--repo', repo, '--base', 'main', '--head', branch, '--draft',
                  '--title', 'Update PKHeX.Core to ' + tag, '--body-file', stream.name)
    print(url)
    output(check='true', ref=sha)

if __name__ == '__main__':
    main()
