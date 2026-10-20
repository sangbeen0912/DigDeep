"""Publish only the completed WebGL build to DigDeep's GitHub Pages branch."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import urllib.error
import urllib.request

REPOSITORY = 'sangbeen0912/DigDeep'
BRANCH = 'gh-pages'
os.environ['GIT_TERMINAL_PROMPT'] = '0'
os.environ['GCM_INTERACTIVE'] = 'never'

def git(folder, *args):
    location = folder.resolve()
    root = next((p for p in [location, *location.parents] if (p / '.git').exists()), location)
    command = ['git', '-c', 'safe.directory=' + str(root), '-C', str(folder), *args]
    return subprocess.run(command, check=True, capture_output=True, text=True, encoding='utf-8').stdout.strip()

class GitHub:
    def __init__(self, project):
        result = subprocess.run(['git', '-c', 'safe.directory=' + str(project.parent.resolve()), '-C', str(project), 'credential', 'fill'],
            input='protocol=https\nhost=github.com\n\n', text=True, capture_output=True)
        credential = dict(line.split('=', 1) for line in result.stdout.splitlines() if '=' in line)
        token = credential.get('password')
        if not token:
            raise RuntimeError('No stored GitHub authentication. Sign in with Git Credential Manager first.')
        self.headers = {'Accept': 'application/vnd.github+json', 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token,
            'User-Agent': 'DigDeep-Pages-Deployment', 'X-GitHub-Api-Version': '2022-11-28'}
    def call(self, suffix='', method='GET', body=None, allow_missing=False):
        endpoint = 'https://api.github.com/repos/' + REPOSITORY + suffix
        data = None if body is None else json.dumps(body).encode('utf-8')
        request = urllib.request.Request(endpoint, data=data, headers=self.headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                content = response.read()
                return json.loads(content) if content else {}
        except urllib.error.HTTPError as error:
            if error.code == 404 and allow_missing:
                return None
            raw = error.read().decode('utf-8', errors='replace')
            try:
                message = json.loads(raw).get('message', 'API request failed')
            except ValueError:
                message = raw[:700] or 'Empty API error response'
            message = message.replace(self.headers['Authorization'].split(' ', 1)[1], '[redacted]')
            raise RuntimeError(f'GitHub {method} {suffix}: HTTP {error.code}: {message}') from None

def prepare(project):
    source = project / 'Builds' / 'Web'
    if not (source / 'index.html').is_file():
        raise RuntimeError('WebGL build is missing.')
    publish = project / 'Builds' / 'PagesPublish'
    publish.mkdir(parents=True, exist_ok=True)
    if not (publish / '.git').is_dir():
        git(publish, 'init')
        git(publish, 'remote', 'add', 'origin', f'https://github.com/{REPOSITORY}.git')
        remote = git(publish, 'ls-remote', '--heads', 'origin', f'refs/heads/{BRANCH}')
        if remote:
            git(publish, 'fetch', '--depth=1', 'origin', BRANCH)
            git(publish, 'checkout', '-b', BRANCH, 'FETCH_HEAD')
        else:
            git(publish, 'checkout', '--orphan', BRANCH)
    else:
        if git(publish, 'remote', 'get-url', 'origin') != f'https://github.com/{REPOSITORY}.git':
            raise RuntimeError('Publishing checkout points to a different repository.')
        if git(publish, 'status', '--porcelain'):
            raise RuntimeError('Publishing checkout has unfinished changes. Inspect before replacing files.')
        git(publish, 'fetch', 'origin', BRANCH)
        git(publish, 'merge', '--ff-only', f'origin/{BRANCH}')
    for setting in ['user.name', 'user.email']:
        value = git(project, 'config', setting)
        git(publish, 'config', setting, value)
    target = (publish / 'Build').resolve()
    if target.parent != publish.resolve():
        raise RuntimeError('Publishing asset path is outside the checkout.')
    if target.is_dir():
        shutil.rmtree(target)
    target.mkdir()
    html = (source / 'index.html').read_text(encoding='utf-8')
    assets = []
    for original in sorted((source / 'Build').iterdir()):
        if not original.is_file():
            raise RuntimeError('Unexpected subdirectory in WebGL output.')
        content = original.read_bytes()
        if len(content) >= 100 * 1024 * 1024:
            raise RuntimeError('Web asset exceeds the GitHub file size limit: ' + original.name)
        digest = hashlib.sha256(content).hexdigest()
        stem, extension = original.name.split('.', 1)
        name = stem + '.' + digest[:12] + '.' + extension
        shutil.copy2(original, target / name)
        html = html.replace('Build/' + original.name, 'Build/' + name)
        assets.append({'path': 'Build/' + name, 'sha256': digest, 'bytes': len(content)})
    for asset in assets:
        if asset['path'] not in html:
            raise RuntimeError('Asset missing from generated loader configuration: ' + asset['path'])
    (publish / 'index.html').write_text(html, encoding='utf-8')
    (publish / '.nojekyll').write_text('', encoding='utf-8')
    manifest = {'game': 'DigDeep', 'built_at': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'source_commit': git(project, 'rev-parse', 'HEAD'), 'assets': assets}
    (publish / 'release.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(json.dumps({'prepared': str(publish), 'files': len(assets) + 3, 'asset_bytes': sum(a['bytes'] for a in assets)}))
    return publish, manifest

def publish(project):
    api = GitHub(project)
    repo = api.call()
    if repo.get('full_name') != REPOSITORY or not repo.get('permissions', {}).get('admin'):
        raise RuntimeError('Repository administrator permission is required for Pages setup.')
    pages = api.call('/pages', allow_missing=True)
    expected = {'branch': BRANCH, 'path': '/'}
    if pages and (pages.get('source') != expected or pages.get('build_type', 'legacy') != 'legacy'):
        raise RuntimeError('Existing Pages publishing source differs. Preserve it until the user chooses the intended source.')
    checkout, release = prepare(project)
    git(checkout, 'add', '--all', '--', 'Build', 'index.html', '.nojekyll', 'release.json')
    git(checkout, 'commit', '-m', 'Publish DigDeep WebGL ' + release['built_at'])
    commit = git(checkout, 'rev-parse', 'HEAD')
    git(checkout, 'push', 'origin', 'HEAD:refs/heads/' + BRANCH)
    if pages is None:
        pages = api.call('/pages', 'POST', {'build_type': 'legacy', 'source': expected})
    status = {'repository': REPOSITORY, 'branch': BRANCH, 'commit': commit, 'url': pages['html_url'], 'release': release}
    (project / 'Builds' / 'PagesDeployment.json').write_text(json.dumps(status, indent=2), encoding='utf-8')
    print(json.dumps({key: status[key] for key in ['repository', 'branch', 'commit', 'url']}))

def configure(project):
    api = GitHub(project)
    checkout = project / 'Builds' / 'PagesPublish'
    commit = git(checkout, 'rev-parse', 'HEAD')
    release = json.loads((checkout / 'release.json').read_text(encoding='utf-8'))
    pages = api.call('/pages', allow_missing=True)
    if pages is None:
        pages = api.call('/pages', 'POST', {'build_type': 'legacy', 'source': {'branch': BRANCH, 'path': '/'}})
    if pages.get('source') != {'branch': BRANCH, 'path': '/'}:
        raise RuntimeError('Pages source differs from the uploaded branch.')
    result = {'repository': REPOSITORY, 'branch': BRANCH, 'commit': commit, 'url': pages['html_url'], 'release': release}
    (project / 'Builds' / 'PagesDeployment.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({key: result[key] for key in ['repository', 'branch', 'commit', 'url']}))

def status(project):
    api = GitHub(project)
    pages = api.call('/pages', allow_missing=True)
    build = api.call('/pages/builds/latest', allow_missing=True) if pages else None
    print(json.dumps({'pages': None if not pages else {key: pages.get(key) for key in ['status', 'html_url', 'source', 'https_enforced']},
        'build': None if not build else {key: build.get(key) for key in ['status', 'commit', 'error', 'created_at', 'updated_at']}}))

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['publish', 'configure', 'status'])
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    try:
        {'publish': publish, 'configure': configure, 'status': status}[args.command](args.project.resolve())
    except (RuntimeError, subprocess.CalledProcessError) as error:
        if isinstance(error, subprocess.CalledProcessError):
            print('Git operation failed: ' + (error.stderr or str(error)))
        else:
            print(str(error))
        raise SystemExit(1)
