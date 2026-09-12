#!/usr/bin/env python3
"""Run the real PicLens terminal interface in disposable profiles and PTYs."""
import argparse
import errno
import json
import os
from pathlib import Path
import pty
import select
import shlex
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("binary")
parser.add_argument("output")
args = parser.parse_args()
root = Path(args.output).resolve()
root.mkdir(exist_ok=False)
env = os.environ.copy()
for key, name in [("HOME", "home"), ("XDG_DATA_HOME", "data"),
                  ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache")]:
    (root / name).mkdir()
    env[key] = str(root / name)
env.update(QT_QPA_PLATFORM="wayland", QT_QPA_PLATFORMTHEME="", PS1="piclens-check> ")
cases = [("help", ["--help"], 0), ("version", ["--version"], 0),
         ("unknown", ["--unknown"], 2), ("missing-argument", ["--metrics"], 2),
         ("invalid-number", ["--smoke-ms", "bad"], 2),
         ("metrics-full", ["--metrics", "/dev/full", "--smoke-ms", "1"], 3),
         ("metrics-success", ["--metrics", str(root / "metrics.json"), "--smoke-ms", "100"], 0),
         ("missing-folder", ["--folder", str(root / "missing"), "--smoke-ms", "100"], 0)]
results = []
for name, options, expected in cases:
    command = [str(Path(args.binary).resolve()), "--data-root", str(root / name), *options]
    master, slave = pty.openpty()
    identity = os.ttyname(slave)
    process = subprocess.Popen(["/bin/bash", "--noprofile", "--norc", "-i"],
                               stdin=slave, stdout=slave, stderr=slave, env=env,
                               start_new_session=True)
    os.close(slave)
    transcript = b""
    journey = "tty\n" + shlex.join(command) + "\nresult=$?\nprintf 'PICLENS_EXIT=%s\\n' \"$result\"\nexit \"$result\"\n"
    os.write(master, journey.encode())
    while True:
        readable, _, _ = select.select([master], [], [], 1)
        if readable:
            try:
                block = os.read(master, 8192)
            except OSError as error:
                if error.errno == errno.EIO:
                    break
                raise
            if not block:
                break
            transcript += block
        elif process.poll() is not None:
            break
    code = process.wait()
    os.close(master)
    text = transcript.decode(errors="replace")
    assert identity in text and f"PICLENS_EXIT={expected}\r\n" in text
    assert code == expected, (name, code, text)
    results.append(dict(name=name, terminal=identity, argv=command, exit=code,
                        expected=expected, output=text[-4096:]))
metrics = json.loads((root / "metrics.json").read_text())
assert metrics["schemaVersion"] == 1 and metrics["processScope"] == "self"
assert metrics["rssBytes"] > 0 and metrics["peakRssBytes"] > 0
assert metrics["libraryMilliseconds"] == 0 and metrics["lastCompletedBatch"] is None
log = (root / "missing-folder/Logs/PicLens.log").read_text()
assert "啟動資料夾無效" in log and "正常關閉；背景工作已回收" in log
(root / "pty-results.json").write_text(json.dumps(results, ensure_ascii=False, indent=2))
print(f"PASS: {len(results)} real PTY cases; additive metrics and startup/shutdown logs verified")
