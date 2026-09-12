#include "services.h"
#include "models.h"
#include <QtTest>
#include <QTemporaryDir>
#include <QFile>
#include <QDir>
#include <QFileInfo>
#include <memory>
using namespace piclens;
namespace {
class EnvironmentGuard {
public:
 explicit EnvironmentGuard(const QByteArray &name):name_(name),wasSet_(qEnvironmentVariableIsSet(name.constData())),value_(qgetenv(name.constData())){}
 ~EnvironmentGuard(){if(wasSet_)qputenv(name_,value_);else qunsetenv(name_);}
private:
 QByteArray name_;bool wasSet_;QByteArray value_;
};
void makeFile(const QString &path,const QByteArray &data="fixture"){
 QFile file(path);QVERIFY(file.open(QIODevice::WriteOnly));QVERIFY(file.write(data)==data.size());
}
}
class AppTest:public QObject{
 Q_OBJECT
private slots:
 void atomicSettings(){QTemporaryDir d;QVERIFY(d.isValid());QJsonObject old{{"thumbnailSize",160}};QCOMPARE(writeProfile(d.path(),old),QString{});QCOMPARE(readProfile(d.path()).settings,old);QFile file(d.filePath("piclens-settings.json"));QVERIFY(file.open(QIODevice::WriteOnly));file.write("broken");file.close();auto result=readProfile(d.path());QVERIFY(result.writable);QVERIFY(!result.error.isEmpty());QVERIFY(!QFile::exists(file.fileName()));QCOMPARE(QDir(d.path()).entryList({"*.corrupt.*"},QDir::Files).size(),1);}
 void scanProjection(){QTemporaryDir d;QDir().mkpath(d.filePath("nested"));for(auto name:{"a2.png","a10.jpg","ignored.svg"}){QFile f(d.filePath(name));QVERIFY(f.open(QIODevice::WriteOnly));f.write("fixture");}auto c=std::make_shared<std::atomic_bool>(false);auto result=scanFolder(d.path(),false,c);QCOMPARE(result.entries.size(),3);Settings s;auto projection=project(result.entries,"",s);QVERIFY(projection.first().folder);QCOMPARE(projection[1].name,QString("a2.png"));QCOMPARE(project(result.entries,"a10",s).size(),1);c->store(true);QVERIFY(scanFolder(d.path(),true,c).entries.isEmpty());}
 void singleReset(){Rows model({"path","selected"});QSignalSpy resets(&model,&QAbstractItemModel::modelReset);QVariantList rows;for(int i=0;i<10000;++i)rows<<QVariantMap{{"path",QString::number(i)},{"selected",false}};model.replace(rows);QCOMPARE(resets.size(),1);QCOMPARE(model.rowCount(),10000);model.change(9999,{{"selected",true}});QVERIFY(model.get(9999)["selected"].toBool());QCOMPARE(resets.size(),1);}
 void dataRootPrecedenceAndAtomicFailure(){
  QTemporaryDir d;QVERIFY(d.isValid());EnvironmentGuard data("PICLENS_DATA_ROOT"),xdg("XDG_DATA_HOME");
  const auto explicitRoot=d.filePath("明確 root");const auto environmentRoot=d.filePath("環境 root");const auto xdgRoot=d.filePath("xdg root");
  qputenv("PICLENS_DATA_ROOT",("  "+environmentRoot+"  ").toUtf8());qputenv("XDG_DATA_HOME",xdgRoot.toUtf8());
  QCOMPARE(dataRoot("  "+explicitRoot+"  "),QFileInfo(explicitRoot).absoluteFilePath());
  QCOMPARE(dataRoot(),QFileInfo(environmentRoot).absoluteFilePath());
  qunsetenv("PICLENS_DATA_ROOT");QCOMPARE(dataRoot(),QDir(xdgRoot).filePath("PicLens"));
  const auto profile=d.filePath("profile");QVERIFY(QDir().mkpath(profile));const QJsonObject old{{"thumbnailSize",160}};QCOMPARE(writeProfile(profile,old),QString{});
  const auto settingsPath=QDir(profile).filePath("piclens-settings.json");QFile before(settingsPath);QVERIFY(before.open(QIODevice::ReadOnly));const auto oldBytes=before.readAll();before.close();
  const auto originalPermissions=QFileInfo(profile).permissions();
  if(!QFile::setPermissions(profile,QFileDevice::ReadOwner|QFileDevice::ExeOwner))QSKIP("無法建立設定檔寫入失敗案例；跳過權限測試");
  const auto error=writeProfile(profile,QJsonObject{{"thumbnailSize",240}});
  const bool restored=QFile::setPermissions(profile,originalPermissions);
  QVERIFY(restored);
  if(error.isEmpty())QSKIP("目前執行身分可繞過目錄權限；跳過權限失敗案例");
  QFile after(settingsPath);const bool opened=after.open(QIODevice::ReadOnly);const auto newBytes=opened?after.readAll():QByteArray{};after.close();
  QVERIFY(opened);QCOMPARE(newBytes,oldBytes);
}
 void recursiveScanSymlinkCancelAndErrors(){
  QTemporaryDir d;QVERIFY(d.isValid());const auto root=d.path();const auto level=d.filePath("level1"),deep=d.filePath("level1/level2");QVERIFY(QDir().mkpath(deep));
  QString deepChain=d.filePath("chain");for(int i=0;i<80;++i){deepChain+="/d"+QString::number(i);QVERIFY(QDir().mkpath(deepChain));}
  makeFile(d.filePath("top.PNG"));makeFile(deep+"/nested.jpg");makeFile(deep+"/animated.gif");makeFile(deep+"/ignored.txt");makeFile(deepChain+"/deep.png");QVERIFY(QDir().mkpath(d.filePath("empty")));
#ifdef Q_OS_LINUX
  QVERIFY(QFile::link(level,d.filePath("level1/level2/loop")));
#endif
  auto cancel=std::make_shared<std::atomic_bool>(false);const auto flat=scanFolder(root,false,cancel);QCOMPARE(flat.entries.size(),4);const auto recursive=scanFolder(root,true,cancel);QCOMPARE(recursive.entries.size(),4);
  QVERIFY(std::none_of(recursive.entries.begin(),recursive.entries.end(),[](const auto &entry){return entry.path.contains("loop");}));
  QString error;QVERIFY(childFolders(root,cancel,&error).contains(level));QVERIFY(childFolders(d.filePath("missing"),cancel,&error).isEmpty());QVERIFY(!error.isEmpty());
#ifdef Q_OS_LINUX
  const auto restricted=d.filePath("restricted");QVERIFY(QDir().mkpath(restricted));makeFile(restricted+"/hidden.png");
  const bool permissionChanged=QFile::setPermissions(restricted,QFileDevice::WriteOwner);const auto permissionResult=scanFolder(root,true,cancel);
  const bool permissionReported=!permissionResult.errors.isEmpty();QFile::setPermissions(restricted,QFileDevice::ReadOwner|QFileDevice::WriteOwner|QFileDevice::ExeOwner);QVERIFY(permissionChanged);QVERIFY(permissionReported);
#endif
  cancel->store(true);QVERIFY(scanFolder(root,true,cancel).entries.isEmpty());
 }
};
QTEST_GUILESS_MAIN(AppTest)
#include "app_test.moc"
