import os,subprocess,time,json,sys,shutil
from pathlib import Path
mode=sys.argv[1] if len(sys.argv)>1 else 'x11'
scale=sys.argv[2] if len(sys.argv)>2 else '1'
out=Path('/work/evidence');base=Path('/work/runtime')/f'{mode}-{scale}-{int(time.time())}';base.mkdir(parents=True)
home=base/'home';home.mkdir();runtime=base/'run';runtime.mkdir();runtime.chmod(0o700)
os.environ.update(LANG='zh_TW.UTF-8',HOME=str(home),XDG_CONFIG_HOME=str(home/'config'),XDG_CACHE_HOME=str(home/'cache'),XDG_DATA_HOME=str(home/'data'),XDG_RUNTIME_DIR=str(runtime),DISPLAY=':91',XDG_CURRENT_DESKTOP='KDE',KDE_SESSION_VERSION='6',KDE_FULL_SESSION='true',XDG_SESSION_TYPE='x11',QT_QPA_PLATFORM='xcb',QT_QPA_PLATFORMTHEME='kde',QT_LINUX_ACCESSIBILITY_ALWAYS_ON='1',QT_IM_MODULE='fcitx',XMODIFIERS='@im=fcitx',LIBGL_ALWAYS_SOFTWARE='1',QT_SCALE_FACTOR=scale,LANGUAGE='zh_TW',GDK_BACKEND='x11')
os.environ.pop('WAYLAND_DISPLAY',None)
Path('/tmp/.X11-unix').mkdir(exist_ok=True);Path('/tmp/.X11-unix').chmod(0o1777)
subprocess.run(['dbus-update-activation-environment','--all'],check=True)
children=[];result={'mode':mode,'scale':scale,'base':str(base),'checks':{},'errors':[]};offset=(0,0)
def start(name,args,env=None):
 f=(base/(name+'.log')).open('a');p=subprocess.Popen(args,stdout=f,stderr=f,env=env);children.append(p);return p
def command(args):return subprocess.check_output(args,text=True,timeout=15).strip()
def keys(*args):command(['xdotool','key','--clearmodifiers',*args]);time.sleep(.2)
def type_text(text):command(['xdotool','type','--clearmodifiers','--delay','70',text]);time.sleep(.2)
try:
 start('xvfb',['Xvfb',':91','-screen','0','3200x2000x24','-dpi','96','-nolisten','tcp','-ac']);time.sleep(1)
 start('openbox',['openbox']);time.sleep(1)
 if mode=='kde':
  env=os.environ.copy();env['QT_SCALE_FACTOR']='1';env['QT_LOGGING_RULES']='kwin_scripting.debug=true'
  kwin=start('kwin',['kwin_wayland','--x11-display',':91','--socket','wayland-kde','--width','2800','--height','1800','--no-lockscreen'],env);time.sleep(4)
  os.environ.update(WAYLAND_DISPLAY='wayland-kde',QT_QPA_PLATFORM='wayland',XDG_SESSION_TYPE='wayland')
  subprocess.run(['dbus-update-activation-environment','--all'],check=True)
  start('plasma',['plasmashell']);time.sleep(4)
 config=Path(os.environ['XDG_CONFIG_HOME'])/'fcitx5';config.mkdir(parents=True)
 (config/'profile').write_text('[Groups/0]\nName=Default\nDefault Layout=us\nDefaultIM=chewing\n\n[Groups/0/Items/0]\nName=keyboard-us\nLayout=\n\n[Groups/0/Items/1]\nName=chewing\nLayout=\n\n[GroupOrder]\n0=Default\n')
 start('fcitx',['fcitx5','--disable=wayland,waylandim','--replace']);time.sleep(1)
 command(['gdbus','call','--session','--dest','org.a11y.Bus','--object-path','/org/a11y/bus','--method','org.a11y.Bus.GetAddress'])
 orca=start('orca',['orca','--replace','--debug-file',str(base/'orca-debug.log')]);time.sleep(1)
 library=base/'library';library.mkdir();(library/'child').mkdir();empty=base/'empty';empty.mkdir()
 for src,dst in [('Gull_portrait_ca_usa.jpg','中圖.jpg'),('Shaki_waterfall.jpg','bird.jpg'),('Hopetoun_falls.jpg','falls.jpg')]:shutil.copyfile('/work/corpus-original/'+src,library/dst)
 shutil.copyfile(library/'bird.jpg',library/'child'/'child.jpg')
 profile=base/'profile'
 def launch():return start('piclens',['/app/piclens','--data-root',str(profile),'--smoke-ms','300000','--width','1200','--height','800'])
 app=launch();time.sleep(3)
 import gi
 gi.require_version('Atspi','2.0');gi.require_version('Gdk','3.0')
 from gi.repository import Atspi,GLib,Gdk
 desktop=Atspi.get_desktop(0)
 def pump():
  ctx=GLib.MainContext.default()
  while ctx.pending():ctx.iteration(False)
 def nodes(obj,depth=0):
  if depth>16:return
  yield obj
  try:
   for i in range(min(obj.get_child_count(),200)):
    child=obj.get_child_at_index(i)
    if child is not None:yield from nodes(child,depth+1)
  except Exception:pass
 def application():
  pump()
  for i in range(desktop.get_child_count()):
   child=desktop.get_child_at_index(i)
   if child.get_name()=='PicLens':return child
  return None
 def find(name=None,role=None,showing=True):
  root=application()
  if root is None:return None
  for o in nodes(root):
   try:
    if name is not None and o.get_name()!=name:continue
    if role is not None and o.get_role_name()!=role:continue
    if showing and not o.get_state_set().contains(Atspi.StateType.SHOWING):continue
    return o
   except Exception:continue
  return None
 def wait(test,seconds=10):
  deadline=time.monotonic()+seconds
  while time.monotonic()<deadline:
   pump();v=test()
   if v:return v
   time.sleep(.1)
  raise RuntimeError('Timed out waiting for '+str(test))
 def tree(obj,depth=0):
  if obj is None or depth>16:return None
  try:
   o={'name':obj.get_name(),'role':obj.get_role_name(),'states':[s.value_nick for s in obj.get_state_set().get_states()],'children':[]}
   for i in range(min(obj.get_child_count(),150)):
    child=tree(obj.get_child_at_index(i),depth+1)
    if child:o['children'].append(child)
   return o
  except Exception as e:return {'error':str(e)}
 def snapshot(name):
  pump();(out/(mode+'-'+scale+'-'+name+'-atspi.json')).write_text(json.dumps(tree(application()),ensure_ascii=False,indent=2))
  root=Gdk.get_default_root_window();pb=Gdk.pixbuf_get_from_window(root,0,0,root.get_width(),root.get_height());pb.savev(str(out/(mode+'-'+scale+'-'+name+'.png')),'png',[],[])
 def action(o):
  iface=o.get_action_iface()
  if iface is None or not iface.do_action(0):raise RuntimeError('No usable accessible action '+o.get_name())
  time.sleep(.3)
 def click(o,right=False,double=False):
  x,y=point(o)
  command(['xdotool','mousemove',str(x),str(y)]);command(['xdotool','click','--repeat','2' if double else '1','--delay','120','3' if right else '1']);time.sleep(.3)
 def point(o):
  rect=o.get_component_iface().get_extents(Atspi.CoordType.SCREEN);x=int((rect.x+rect.width/2)*float(scale))+offset[0];y=int((rect.y+rect.height/2)*float(scale))+offset[1]
  result.setdefault('clicks',[]).append({'name':o.get_name(),'logicalRect':[rect.x,rect.y,rect.width,rect.height],'pixel':[x,y],'offset':offset})
  return x,y
 def text(o):return Atspi.Text.get_text(o,0,-1)
 wait(lambda:find('搜尋圖庫','text'))
 search_cmd=['xdotool','search','--onlyvisible']+(['--name','^PicLens$'] if mode=='x11' else ['--pid',str(kwin.pid)])
 wid=command(search_cmd).splitlines()[0];command(['xdotool','windowactivate','--sync',wid]);time.sleep(.4)
 if mode=='kde':
  geometry=dict(line.split('=',1) for line in command(['xdotool','getwindowgeometry','--shell',wid]).splitlines() if '=' in line);offset=(int(geometry['X']),int(geometry['Y']))
 if mode=='kde':
  import dbus,dbus.service
  from dbus.mainloop.glib import DBusGMainLoop
  DBusGMainLoop(set_as_default=True)
  class GeometryProbe(dbus.service.Object):
   @dbus.service.method('io.piclens.Validation',in_signature='s',out_signature='')
   def Geometry(self,value):result['kwinClientGeometry']=json.loads(str(value))
  service=GeometryProbe(dbus.service.BusName('io.piclens.Validation',bus=dbus.SessionBus()),'/Validation')
  script=base/'window-geometry.js'
  script.write_text('for (const w of workspace.stackingOrder) { if (w.caption === "PicLens") { const g=w.clientGeometry; callDBus("io.piclens.Validation", "/Validation", "io.piclens.Validation", "Geometry", JSON.stringify([g.x,g.y,g.width,g.height])); }}')
  script_id=command(['qdbus6','org.kde.KWin','/Scripting','org.kde.kwin.Scripting.loadScript',str(script)])
  command(['qdbus6','org.kde.KWin','/Scripting/Script'+script_id,'org.kde.kwin.Script.run'])
  geometry=wait(lambda:result.get('kwinClientGeometry'))
  offset=(offset[0]+geometry[0],offset[1]+geometry[1])
 result['checks']['emptyStartup']=find('從資料夾開始瀏覽') is not None
 keys('ctrl+o');dialog=wait(lambda:find('選擇圖庫資料夾','dialog'))
 result['checks']['nativeKdePicker']=any(o.get_role_name()=='tree' for o in nodes(dialog))
 snapshot('native-picker')
 keys('Escape');wait(lambda:find('選擇圖庫資料夾','dialog') is None)
 result['checks']['emptyPickerCancelPreservesEmpty']=find('從資料夾開始瀏覽') is not None and not (profile/'piclens-settings.json').exists()
 def pick(path):
  keys('ctrl+o');wait(lambda:find('選擇圖庫資料夾','dialog'));keys('ctrl+l');type_text(str(path));keys('Return');time.sleep(.6)
  if find('選擇圖庫資料夾','dialog'):
   button=find('開啟(O)','button') or find('Open','button') or find('開啟','button')
   if button:action(button)
   else:keys('Return')
  wait(lambda:find('選擇圖庫資料夾','dialog') is None)
  wait(lambda:(profile/'piclens-settings.json').exists());time.sleep(.7)
  return json.loads((profile/'piclens-settings.json').read_text())
 saved=pick(library);result['checks']['pickerSetsPersistentRoot']=saved.get('lastFolderPath')==str(library)
 wait(lambda:find('中圖.jpg','list item'));snapshot('gallery')
 before=(profile/'piclens-settings.json').read_bytes();keys('ctrl+o');wait(lambda:find('選擇圖庫資料夾','dialog'));keys('Escape');time.sleep(.6)
 result['checks']['populatedPickerCancelPreservesProfile']=(profile/'piclens-settings.json').read_bytes()==before
 keys('ctrl+f');search=wait(lambda:find('搜尋圖庫','text'));result['checks']['keyboardSearchFocus']=search.get_state_set().contains(Atspi.StateType.FOCUSED)
 command(['fcitx5-remote','-s','chewing']);command(['fcitx5-remote','-o']);time.sleep(.5)
 result['inputMethod']=command(['fcitx5-remote','-n']);type_text('5j/ ');snapshot('ime-preedit');keys('Return');time.sleep(.8)
 result['imeCommittedText']=text(search);result['checks']['realBopomofoCommit']=text(search)=='中'
 command(['fcitx5-remote','-c']);keys('ctrl+f');keys('ctrl+a','BackSpace');time.sleep(.5)
 tile=wait(lambda:find('中圖.jpg','list item'));click(tile);time.sleep(.4);tile=wait(lambda:find('中圖.jpg','list item'))
 result['selectedTileStates']=[s.value_nick for s in tile.get_state_set().get_states()]
 result['checks']['accessibleTileSelection']=tile.get_state_set().contains(Atspi.StateType.SELECTED)
 snapshot('selected')
 click(wait(lambda:find('中圖.jpg','list item')),right=True);time.sleep(.3);result['checks']['contextMenu']=find(role='popup menu') is not None;snapshot('menu');keys('Escape')
 click(wait(lambda:find('中圖.jpg','list item')),double=True);time.sleep(.5);result['checks']['viewerOpen']=find('關閉檢視器（Esc）','button') is not None;snapshot('viewer');keys('Escape')
 # Real desktop file operations stay within the disposable library/profile.
 click(wait(lambda:find('中圖.jpg','list item')),right=True)
 action(wait(lambda:find('在檔案管理員中顯示','menu item')));time.sleep(4)
 dolphin=[]
 for i in range(desktop.get_child_count()):
  a=desktop.get_child_at_index(i)
  if 'dolphin' in a.get_name().lower():dolphin.append(tree(a));result.setdefault('dolphinPids',[]).append(a.get_process_id())
 result['revealDesktopApps']=[desktop.get_child_at_index(i).get_name() for i in range(desktop.get_child_count())]
 result['revealProcesses']='\n'.join(line for line in command(['ps','-eo','pid,args']).splitlines() if '/usr/bin/dolphin ' in line)
 result['checks']['revealOpensDolphin']=bool(dolphin)
 snapshot('reveal')
 (out/(mode+'-'+scale+'-dolphin-atspi.json')).write_text(json.dumps(dolphin,ensure_ascii=False,indent=2))
 if mode=='x11':command(['xdotool','windowactivate','--sync',wid])
 elif dolphin:
  # End only the file manager launched by this test; activation may not focus it.
  import signal
  for pid in result['dolphinPids']:os.kill(pid,signal.SIGTERM)
  focus_script=base/'focus-piclens.js'
  focus_script.write_text('for (const w of workspace.stackingOrder) { if (w.caption === \"PicLens\") workspace.activeWindow=w; }')
  focus_id=command(['qdbus6','org.kde.KWin','/Scripting','org.kde.kwin.Scripting.loadScript',str(focus_script)])
  command(['qdbus6','org.kde.KWin','/Scripting/Script'+focus_id,'org.kde.kwin.Script.run'])
 time.sleep(.5)
 click(wait(lambda:find('bird.jpg','list item')));keys('Delete')
 action(wait(lambda:find('取消','button')));time.sleep(.5)
 result['checks']['trashCancelPreservesFile']=(library/'bird.jpg').exists()
 keys('Delete');action(wait(lambda:find('確認執行','button')))
 wait(lambda:not (library/'bird.jpg').exists());time.sleep(.5)
 trash=list((home/'data/Trash/files').glob('*'))
 result['checks']['trashMovesToPrivateRecycleBin']=any(p.name=='bird.jpg' for p in trash)
 # Drag past the native threshold, cancel with Escape while the button is held.
 def drag(cancel):
  src=point(wait(lambda:find('中圖.jpg','list item')));dst=point(wait(lambda:find('falls.jpg','list item')))
  command(['xdotool','mousemove',*map(str,src)]);command(['xdotool','mousedown','1']);time.sleep(.2)
  for f in [.15,.35,.6,.8,1]:
   command(['xdotool','mousemove',str(round(src[0]+(dst[0]-src[0])*f)),str(round(src[1]+(dst[1]-src[1])*f))]);time.sleep(.15)
  if cancel:command(['xdotool','key','Escape']);time.sleep(.3)
  command(['xdotool','mouseup','1']);time.sleep(.5)
 drag(True);snapshot('drag-escape')
 result['checks']['pointerGrabEscapeCancels']=find('確認執行','button') is None and (library/'中圖.jpg').exists() and (library/'falls.jpg').exists()
 drag(False);wait(lambda:find('確認執行','button'));snapshot('drop-confirmation');action(find('取消','button'));time.sleep(.5)
 result['checks']['dragDropCancelPreservesFiles']=(library/'中圖.jpg').exists() and (library/'falls.jpg').exists()
 checkbox=wait(lambda:find('包含子資料夾','check box'));click(checkbox);time.sleep(.5)
 result['checks']['checkboxState']=find('包含子資料夾','check box').get_state_set().contains(Atspi.StateType.CHECKED)
 click(find('包含子資料夾','check box'));time.sleep(.5)
 snapshot('controls')
 click(wait(lambda:find('排序方式','combo box')));snapshot('sort-options')
 option_names=['名稱（升冪）','名稱（降冪）','修改時間（最舊優先）','修改時間（最新優先）']
 result['checks']['sortOptionsNamed']=all(find(n,'list item') is not None for n in option_names)
 result['checks']['sortSelectedState']=bool(find(option_names[0],'list item') and find(option_names[0],'list item').get_state_set().contains(Atspi.StateType.SELECTED))
 keys('Escape')
 slider=find('縮圖大小','slider');v=slider.get_value_iface() if slider else None
 result['checks']['sliderAccessibleRange']=bool(v and v.get_minimum_value()==120 and v.get_maximum_value()==240)
 child=wait(lambda:find('child','list item'));click(child);wait(lambda:find('child.jpg','list item'))
 result['checks']['childNavigationPreservesRoot']=json.loads((profile/'piclens-settings.json').read_text()).get('lastFolderPath')==str(library)
 keys('alt+Left');wait(lambda:find('中圖.jpg','list item'));keys('F5');time.sleep(.7)
 app.terminate();app.wait(timeout=15);app=launch();time.sleep(3);wait(lambda:find('中圖.jpg','list item'))
 result['checks']['startupRestoresPickerFolder']=find(str(library),'button') is not None
 saved=pick(empty);result['checks']['emptyFolderSelection']=saved.get('lastFolderPath')==str(empty) and find('中圖.jpg','list item') is None
 snapshot('empty-folder');result['checks']['disabledEmptyOperations']=not find('移到回收筒','button').get_state_set().contains(Atspi.StateType.ENABLED);result['orcaRunning']=orca.poll() is None
except Exception as e:
 result['errors'].append(repr(e));import traceback;traceback.print_exc()
 try:snapshot('failure')
 except Exception:pass
finally:
 for p in reversed(children):
  if p.poll() is None:
   p.terminate()
   try:p.wait(timeout=8)
   except subprocess.TimeoutExpired:p.kill();p.wait()
 (out/(mode+'-'+scale+'-journey.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(result,ensure_ascii=False,indent=2),flush=True)
if result['errors'] or not all(result['checks'].values()):sys.exit(1)
