#include "controller.h"
#include <QtTest>
#include <QTemporaryDir>
#include <QDir>
#include <QFile>
#include <QImage>
using namespace piclens;
class ControllerTest:public QObject{
 Q_OBJECT
private slots:
 void missingStartupPathKeepsPickerState(){
    QTemporaryDir dir;QVERIFY(dir.isValid());
    const QString profile=dir.filePath("profile");
    const QString missing=dir.filePath("missing-library");
    ThumbProvider provider;Controller c(profile,QString::fromUtf8(PICLENS_WORKER),&provider);
    c.start(missing);
    QCOMPARE(c.rootPath(),QString{});QCOMPARE(c.folder(),QString{});QCOMPARE(c.count(),0);
    QVERIFY(c.status().contains(QStringLiteral("資料夾")));
    c.shutdown();
    QTRY_VERIFY_WITH_TIMEOUT(QFile::exists(profile+"/Logs/PicLens.log"),5000);
    QFile log(profile+"/Logs/PicLens.log");QVERIFY(log.open(QIODevice::ReadOnly));
    QVERIFY(QString::fromUtf8(log.readAll()).contains(missing));
 }
 void fileStartupPathKeepsPickerState(){
    QTemporaryDir dir;QVERIFY(dir.isValid());
    const QString profile=dir.filePath("profile");
    const QString file=dir.filePath("not-a-folder.png");
    QFile marker(file);QVERIFY(marker.open(QIODevice::WriteOnly));marker.write("not an image");marker.close();
    ThumbProvider provider;Controller c(profile,QString::fromUtf8(PICLENS_WORKER),&provider);
    c.start(file);
    QCOMPARE(c.rootPath(),QString{});QCOMPARE(c.folder(),QString{});QCOMPARE(c.count(),0);
    QVERIFY(c.status().contains(QStringLiteral("資料夾")));
    c.shutdown();
    QTRY_VERIFY_WITH_TIMEOUT(QFile::exists(profile+"/Logs/PicLens.log"),5000);
    QFile log(profile+"/Logs/PicLens.log");QVERIFY(log.open(QIODevice::ReadOnly));
    QVERIFY(QString::fromUtf8(log.readAll()).contains(file));
 }
 void unreadableStartupPathKeepsPickerState(){
    QTemporaryDir dir;QVERIFY(dir.isValid());
    const QString profile=dir.filePath("profile");
    const QString unreadable=dir.filePath("unreadable");
    QVERIFY(QDir().mkpath(unreadable));
    QVERIFY(QFile::setPermissions(unreadable,QFileDevice::WriteOwner));
    ThumbProvider provider;Controller c(profile,QString::fromUtf8(PICLENS_WORKER),&provider);
    c.start(unreadable);
    QCOMPARE(c.rootPath(),QString{});QCOMPARE(c.folder(),QString{});QCOMPARE(c.count(),0);
    QVERIFY(c.status().contains(QStringLiteral("資料夾")));
    QVERIFY(QFile::setPermissions(unreadable,QFileDevice::ReadOwner|QFileDevice::WriteOwner|QFileDevice::ExeOwner));
    c.shutdown();
 }
 void invalidRestoredStartupPathKeepsPickerState(){
    QTemporaryDir dir;QVERIFY(dir.isValid());
    const QString profile=dir.filePath("profile");
    const QString missing=dir.filePath("restored-library");
    QJsonObject settings{{"lastFolderPath",missing}};
    QVERIFY(QDir().mkpath(profile));
    QCOMPARE(writeProfile(profile,settings),QString{});
    ThumbProvider provider;Controller c(profile,QString::fromUtf8(PICLENS_WORKER),&provider);
    c.start({});
    QCOMPARE(c.rootPath(),QString{});QCOMPARE(c.folder(),QString{});QCOMPARE(c.count(),0);
    QVERIFY(c.status().contains(QStringLiteral("資料夾")));
    QTRY_VERIFY_WITH_TIMEOUT(QFile::exists(profile+"/Logs/PicLens.log"),5000);
    QFile log(profile+"/Logs/PicLens.log");QVERIFY(log.open(QIODevice::ReadOnly));
    QVERIFY(QString::fromUtf8(log.readAll()).contains(missing));
    c.shutdown();
    QCOMPARE(readProfile(profile).settings.value("lastFolderPath").toString(),missing);
 }
 void shiftUpKeepsIndependentCursor(){
    QTemporaryDir dir;QString library=dir.filePath("images");QDir().mkpath(library+"/child");
    QImage image(2,2,QImage::Format_ARGB32);image.fill(Qt::green);
    for(const auto& name:{"a.png","b.png","c.png"})QVERIFY(image.save(library+"/"+name));
    ThumbProvider provider;Controller c(dir.filePath("profile"),QString::fromUtf8(PICLENS_WORKER),&provider);
    c.start(library);QTRY_COMPARE_WITH_TIMEOUT(c.count(),4,5000);auto* model=qobject_cast<Rows*>(c.library());QVERIFY(model);
    QSignalSpy scroll(&c,&Controller::scrollTo);
    c.select(3,0); // c；row 0 是資料夾，不參與圖片範圍。
    c.moveSelection(-1,true);QCOMPARE(c.selectionCount(),2);
    c.moveSelection(-1,true);QCOMPARE(c.selectionCount(),3);QCOMPARE(scroll.last().at(0).toInt(),1);
    QVERIFY(!model->get(0)["selected"].toBool());
    for(int row=1;row<=3;++row)QVERIFY(model->get(row)["selected"].toBool());
    c.moveSelection(1,true);QCOMPARE(c.selectionCount(),2); // anchor 仍是 c，回縮到 b。
    QVERIFY(!model->get(1)["selected"].toBool());QVERIFY(model->get(2)["selected"].toBool());QVERIFY(model->get(3)["selected"].toBool());
    c.clearSelection();c.moveSelection(1,false);QCOMPARE(c.selectionCount(),1);QVERIFY(model->get(1)["selected"].toBool());
    c.select(3,0);c.setSearch("png");c.moveSelection(1,false);QCOMPARE(c.selectionCount(),1);QVERIFY(model->get(0)["selected"].toBool());
    c.shutdown();
 }
 void navigationAndThumbnails(){
    QTemporaryDir dir;QString library=dir.filePath("images");QDir().mkpath(library+"/child");QImage image(40,30,QImage::Format_ARGB32);image.fill(Qt::green);QVERIFY(image.save(library+"/a.png"));QVERIFY(image.save(library+"/b.png"));
    ThumbProvider provider;Controller c(dir.filePath("profile"),QString::fromUtf8(PICLENS_WORKER),&provider);c.pick(QUrl::fromLocalFile(library));
    QTRY_COMPARE_WITH_TIMEOUT(c.count(),3,5000);QCOMPARE(c.rootPath(),library);auto* model=qobject_cast<Rows*>(c.library());
    c.setVisible({library+"/a.png",library+"/b.png"});
    QTRY_VERIFY_WITH_TIMEOUT(!model->get(1)["imageKey"].toString().isEmpty(),10000);QVERIFY(model->get(1)["error"].toString().isEmpty());
    c.select(1,0);c.select(2,Qt::ControlModifier);QCOMPARE(c.selectionCount(),2);c.openViewer();QCOMPARE(c.viewerCount(),2);QCOMPARE(c.viewerName(),QString("a.png"));c.viewerStep(1);QCOMPARE(c.viewerName(),QString("b.png"));c.closeViewer();
    c.setSearch("b.png");QCOMPARE(c.count(),1);QCOMPARE(c.selectionCount(),0);QCOMPARE(c.rootPath(),library);c.setSearch("");
    c.navigate(library+"/child");QTRY_COMPARE_WITH_TIMEOUT(c.count(),0,5000);QCOMPARE(c.rootPath(),library);c.history(-1);QTRY_COMPARE_WITH_TIMEOUT(c.count(),3,5000);c.shutdown();
    QCOMPARE(readProfile(dir.filePath("profile")).settings["lastFolderPath"].toString(),library);
 }
 void canceledConfirmationDoesNotWrite(){
    QTemporaryDir dir;QString library=dir.filePath("images");QDir().mkpath(library);QImage image(2,2,QImage::Format_ARGB32);image.fill(Qt::red);
    for(int i=0;i<50;++i)QVERIFY(image.save(library+QString("/a%1.png").arg(i)));
    ThumbProvider provider;Controller c(dir.filePath("profile"),QString::fromUtf8(PICLENS_WORKER),&provider);c.start(library);QTRY_COMPARE_WITH_TIMEOUT(c.count(),50,5000);
    c.requestOperation("jpg");QTRY_VERIFY_WITH_TIMEOUT(c.confirmOpen(),5000);QVERIFY(c.confirmText().contains("50"));c.confirm(false);QVERIFY(!c.confirmOpen());QVERIFY(QDir(library).entryList({"*.jpg"},QDir::Files).isEmpty());QCOMPARE(QDir(library).entryList({"*.png"},QDir::Files).size(),50);c.shutdown();
 }
 void controllerUsesSingleResetForLargeSearch(){
    QTemporaryDir dir;QVERIFY(dir.isValid());
    ThumbProvider provider;Controller c(dir.filePath("profile"),QString::fromUtf8(PICLENS_WORKER),&provider);
    auto* model=qobject_cast<Rows*>(c.library());QVERIFY(model);QSignalSpy resets(model,&QAbstractItemModel::modelReset);
    c.diagnose(10000);QCOMPARE(c.count(),10000);QCOMPARE(model->rowCount(),10000);QCOMPARE(resets.size(),1);
    c.setSearch("image9999");QCOMPARE(c.count(),1);QCOMPARE(resets.size(),2);QCOMPARE(c.selectionCount(),0);
    c.setSearch("");QCOMPARE(c.count(),10000);QCOMPARE(resets.size(),3);
    QCOMPARE(c.metrics().value("itemCount").toInt(),10000);c.shutdown();
 }
 void staleNavigationAndImmutableViewerSnapshot(){
    QTemporaryDir dir;QVERIFY(dir.isValid());
    const QString first=dir.filePath("first"),second=dir.filePath("second");QVERIFY(QDir().mkpath(first));QVERIFY(QDir().mkpath(second));
    QImage image(8,6,QImage::Format_RGBA8888);image.fill(Qt::blue);
    QVERIFY(image.save(first+"/a.png"));QVERIFY(image.save(first+"/b.png"));QVERIFY(image.save(second+"/c.png"));QVERIFY(image.save(second+"/d.png"));
    ThumbProvider provider;Controller c(dir.filePath("profile"),QString::fromUtf8(PICLENS_WORKER),&provider);
    c.start(first);c.navigate(second);QTRY_COMPARE_WITH_TIMEOUT(c.folder(),QDir::cleanPath(second),5000);QTRY_COMPARE_WITH_TIMEOUT(c.count(),2,5000);
    c.openViewer(0);QVERIFY(c.viewerOpen());QCOMPARE(c.viewerCount(),2);const auto snapshotName=c.viewerName();
    c.viewerStep(1);c.viewerStep(-1);QCOMPARE(c.viewerName(),snapshotName);
    c.refresh();QTRY_COMPARE_WITH_TIMEOUT(c.count(),2,5000);QCOMPARE(c.viewerCount(),2);QCOMPARE(c.viewerName(),snapshotName);
    QSignalSpy focus(&c,&Controller::focusGallery);c.closeViewer();QVERIFY(!c.viewerOpen());QCOMPARE(c.viewerCount(),0);QCOMPARE(focus.size(),1);
    c.openViewer(0);QVERIFY(c.viewerOpen());QCOMPARE(c.viewerCount(),2);c.closeViewer();c.shutdown();
 }
 void renameReportsResultAndKeepsSourceScope(){
    QTemporaryDir dir;QVERIFY(dir.isValid());
    const QString library=dir.filePath("圖片");QVERIFY(QDir().mkpath(library));QImage image(5,5,QImage::Format_RGB32);image.fill(Qt::red);const auto source=library+"/source.png";QVERIFY(image.save(source));
    ThumbProvider provider;Controller c(dir.filePath("profile"),QString::fromUtf8(PICLENS_WORKER),&provider);c.start(library);QTRY_COMPARE_WITH_TIMEOUT(c.count(),1,5000);c.select(0,0);c.requestOperation("rename","新名稱");
    QTRY_VERIFY_WITH_TIMEOUT(c.results().size()==1,5000);const auto result=c.results().first().toMap();QCOMPARE(result.value("status").toString(),QStringLiteral("成功"));QCOMPARE(result.value("source").toString(),source);QVERIFY(QFile::exists(library+"/新名稱.png"));QVERIFY(!QFile::exists(source));c.shutdown();
 }
};
QTEST_MAIN(ControllerTest)
#include "controller_test.moc"
