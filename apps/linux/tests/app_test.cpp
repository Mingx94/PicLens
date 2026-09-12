#include "services.h"
#include "models.h"
#include <QtTest>
#include <QTemporaryDir>
#include <QFile>
#include <QDir>
using namespace piclens;
class AppTest:public QObject{
 Q_OBJECT
private slots:
 void atomicSettings(){QTemporaryDir d;QVERIFY(d.isValid());QJsonObject old{{"thumbnailSize",160}};QCOMPARE(writeProfile(d.path(),old),QString{});QCOMPARE(readProfile(d.path()).settings,old);QFile file(d.filePath("piclens-settings.json"));QVERIFY(file.open(QIODevice::WriteOnly));file.write("broken");file.close();auto result=readProfile(d.path());QVERIFY(result.writable);QVERIFY(!result.error.isEmpty());QVERIFY(!QFile::exists(file.fileName()));QCOMPARE(QDir(d.path()).entryList({"*.corrupt.*"},QDir::Files).size(),1);}
 void scanProjection(){QTemporaryDir d;QDir().mkpath(d.filePath("nested"));for(auto name:{"a2.png","a10.jpg","ignored.svg"}){QFile f(d.filePath(name));QVERIFY(f.open(QIODevice::WriteOnly));f.write("fixture");}auto c=std::make_shared<std::atomic_bool>(false);auto result=scanFolder(d.path(),false,c);QCOMPARE(result.entries.size(),3);Settings s;auto projection=project(result.entries,"",s);QVERIFY(projection.first().folder);QCOMPARE(projection[1].name,QString("a2.png"));QCOMPARE(project(result.entries,"a10",s).size(),1);c->store(true);QVERIFY(scanFolder(d.path(),true,c).entries.isEmpty());}
 void singleReset(){Rows model({"path","selected"});QSignalSpy resets(&model,&QAbstractItemModel::modelReset);QVariantList rows;for(int i=0;i<10000;++i)rows<<QVariantMap{{"path",QString::number(i)},{"selected",false}};model.replace(rows);QCOMPARE(resets.size(),1);QCOMPARE(model.rowCount(),10000);model.change(9999,{{"selected",true}});QVERIFY(model.get(9999)["selected"].toBool());QCOMPARE(resets.size(),1);}
};
QTEST_GUILESS_MAIN(AppTest)
#include "app_test.moc"
