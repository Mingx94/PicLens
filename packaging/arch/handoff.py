"""Freeze allowed working-tree bytes, not HEAD; deterministic tar/gzip metadata."""
import argparse
import gzip
import hashlib
import io
import json
import re
from pathlib import Path
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parents[2]

def version_info():
    cmake = (ROOT / 'apps/linux/CMakeLists.txt').read_text(encoding='utf-8')
    package = (ROOT / 'packaging/arch/PKGBUILD').read_text(encoding='utf-8')
    version = re.search(r'project\(PicLens VERSION (\d+\.\d+\.\d+)\b', cmake)
    pkgver = re.search(r'^pkgver=(\d+\.\d+\.\d+)$', package, re.M)
    pkgrel = re.search(r'^pkgrel=([1-9]\d*)$', package, re.M)
    if not version or not pkgver or not pkgrel or version[1] != pkgver[1]:
        raise SystemExit('CMake VERSION and PKGBUILD pkgver must match; pkgrel must be positive.')
    return version[1], pkgrel[1]


def verify_release(tag, version):
    if tag != f'arch/v{version}':
        raise SystemExit(f'Tag must match arch/v{version}')
    ref = f'refs/tags/{tag}'
    if git('cat-file', '-t', ref).strip() != b'tag':
        raise SystemExit('Annotated tag required')
    if git('rev-parse', ref + '^{commit}') != git('rev-parse', 'HEAD'):
        raise SystemExit('Tag must point to the checked-out commit')
    if git('status', '--porcelain', '--untracked-files=all').strip():
        raise SystemExit('Release requires a clean checkout')


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])


def allowed(name):
    p = Path(name)
    if any(part.startswith('.') or part.lower() in {
        'build', 'out', 'dist', 'target', 'artifacts', 'cmakefiles', 'node_modules',
        'logs', 'profile', 'private', 'screenshots', 'bin', 'obj', '__pycache__'
    } for part in p.parts):
        return False
    if p.name in {'CMakeUserPresets.json', 'CMakeCache.txt', 'compile_commands.json'}:
        return False
    if name in {'LICENSE', 'TODO.arch.md'}:
        return True
    if name in {'docs/engineering/arch-validation.md', 'docs/engineering/native-cleanup.md'} or (
        name.startswith('docs/') and name in TRACKED and p.suffix == '.md'
    ):
        return True
    if name.startswith('apps/linux/'):
        return p.name in {'CMakeLists.txt', 'CMakePresets.json', 'README.md', 'THIRD-PARTY.md'} or (
            len(p.parts) > 3 and p.parts[2] in {'src', 'qml', 'tests', 'cmake', 'resources'}
            and p.suffix.lower() in {'.cpp', '.cc', '.c', '.h', '.hpp', '.qml', '.js',
                                     '.qrc', '.cmake', '.json', '.svg', '.txt', '.py', '.sh', '.in'})
    if name.startswith('packaging/arch/'):
        return len(p.parts) == 3 and (p.name == 'PKGBUILD' or p.suffix in {
            '.py', '.ps1', '.sh', '.md', '.desktop', '.xml', '.png'})
    # Only repository-owned shared fixtures/assets; no arbitrary local photos.
    if name.startswith('test-data/'):
        return p.suffix.lower() in {'.json', '.txt', '.md'} or (
            name in TRACKED and p.suffix.lower() in {'.png', '.jpg', '.jpeg', '.bmp', '.gif', '.webp'})
    return name in TRACKED and name.startswith('assets/') and p.suffix.lower() in {
        '.png', '.ico', '.svg', '.otf', '.ttf', '.txt'}


TRACKED = set(git('ls-files', '-z').decode('utf-8').split('\0'))


def output_path(value):
    output = Path(value).resolve()
    # Resolve before checking containment; an existing output is never reused.
    dist = ROOT / 'dist'
    if output == ROOT or (ROOT in output.parents and dist not in output.parents):
        raise SystemExit('Output must be outside the repository or a new directory below repo/dist.')
    if output.exists():
        raise SystemExit('Output directory already exists; choose a new directory.')
    return output


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, help='New directory; never overwrite')
    parser.add_argument('--release-tag', help='Require a clean annotated arch/v<version> checkout')
    args = parser.parse_args()
    version, pkgrel = version_info()
    if args.release_tag:
        verify_release(args.release_tag, version)
    output = output_path(args.output)
    names = set(git('ls-files', '--cached', '--others', '--exclude-standard', '-z')
                .decode('utf-8').split('\0'))
    selected = sorted(n for n in names if n and allowed(n) and (ROOT / n).exists())
    for required in ['LICENSE', 'apps/linux/CMakeLists.txt', 'packaging/arch/PKGBUILD',
                     'packaging/arch/piclens.png']:
        if required not in selected:
            raise SystemExit(f'Missing required source: {required}')
    blobs = {}
    for name in selected:
        path = ROOT / name
        if path.is_symlink() or path.resolve() != path.absolute():
            raise SystemExit(f'Refusing symlink/reparse source: {name}')
        if not path.is_file():
            raise SystemExit(f'Not a regular file: {name}')
        blobs[name] = path.read_bytes()
    # Read once, then verify concurrent edits did not change this snapshot.
    for name, data in blobs.items():
        if (ROOT / name).read_bytes() != data:
            raise SystemExit(f'Source changed while freezing: {name}; retry after edits stop')
    output.mkdir(parents=True, exist_ok=False)
    archive = output / f'piclens-{version}-{"source" if args.release_tag else "worktree"}.tar.gz'
    with archive.open('wb') as raw:
        with gzip.GzipFile(filename='', mode='wb', fileobj=raw, mtime=0) as compressed:
            with tarfile.open(fileobj=compressed, mode='w', format=tarfile.USTAR_FORMAT) as tar:
                for name, data in blobs.items():
                    entry = tarfile.TarInfo(f'piclens-{version}/{name}')
                    entry.size = len(data)
                    entry.mode = 0o755 if name.endswith('.sh') else 0o644
                    entry.mtime = entry.uid = entry.gid = 0
                    tar.addfile(entry, io.BytesIO(data))
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    template = blobs['packaging/arch/PKGBUILD'].decode('utf-8')
    if template.count('@SOURCE_SHA256@') != 1:
        raise SystemExit('Expected exactly one source checksum placeholder')
    if args.release_tag:
        template = template.replace('piclens-${pkgver}-worktree.tar.gz', archive.name)
    (output / 'PKGBUILD').write_text(template.replace('@SOURCE_SHA256@', digest), encoding='utf-8', newline='\n')
    (output / 'SHA256SUMS').write_text(f'{digest}  {archive.name}\n', encoding='ascii')
    manifest = {
        'version': version, 'pkgrel': pkgrel,
        'source_kind': 'annotated release tag' if args.release_tag else 'working-tree snapshot; NOT a release tag',
        'tag': args.release_tag,
        'base_commit': git('rev-parse', 'HEAD').decode().strip(),
        'source_sha256': digest,
        'files': {name: hashlib.sha256(data).hexdigest() for name, data in blobs.items()},
    }
    (output / 'SOURCE-MANIFEST.json').write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'{archive}\nSHA256 {digest}\nReview SOURCE-MANIFEST.json before transfer.')


if __name__ == '__main__':
    main()
