"""Download only the three accepted original files; verify fixed bytes before use."""
from pathlib import Path
from urllib.request import Request, urlopen
import hashlib, json
root = Path(__file__).resolve().parent
out = root / 'corpus-original'
out.mkdir(exist_ok=True)
for item in json.loads((root / 'evidence/corpus-sources.json').read_text()):
    path = out / item['file']
    if not path.exists():
        request = Request(item['downloadUrl'], headers={'User-Agent': 'PicLens validation/1.0'})
        with urlopen(request, timeout=60) as response:
            data = response.read(30 * 1024 * 1024 + 1)
        if hashlib.sha256(data).hexdigest() != item['sha256']:
            raise RuntimeError('Source bytes changed: ' + item['file'])
        path.write_bytes(data)
    if hashlib.sha256(path.read_bytes()).hexdigest() != item['sha256']:
        raise RuntimeError('Invalid local source: ' + item['file'])
    print(item['file'], item['sha256'])
