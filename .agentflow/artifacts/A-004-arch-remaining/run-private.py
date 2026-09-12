from pathlib import Path
import os,sys,subprocess
repo=Path(__file__).resolve().parents[3]
root=Path(os.environ.get('PICLENS_ARCH_ROOTFS',str(repo/'.agentflow/artifacts/A-003-arch-todo/clean-bootstrap/rootless/root.x86_64')))
work=Path(__file__).resolve().parent
app=Path(os.environ.get('PICLENS_APP_BUILD','/tmp/piclens-arch-validation.IHj5Y4Ng/build'))
(work/'runtime/home').mkdir(parents=True,exist_ok=True)
(work/'runtime/run').mkdir(parents=True,exist_ok=True)
(work/'runtime/run').chmod(0o700)
args=['bwrap','--unshare-all','--die-with-parent','--uid','1000','--gid','1000','--bind',str(root),'/','--bind',str(work),'/work','--ro-bind',str(app),'/app','--proc','/proc','--dev','/dev','--tmpfs','/tmp','--tmpfs','/run','--chdir','/work','--clearenv','--setenv','PATH','/usr/bin','--setenv','LANG','C.UTF-8','--setenv','HOME','/work/runtime/home','--setenv','XDG_RUNTIME_DIR','/work/runtime/run','/usr/bin/dbus-run-session','--','/usr/bin/python','/work/private-session.py',*sys.argv[1:]]
p=subprocess.run(args);sys.exit(p.returncode)
