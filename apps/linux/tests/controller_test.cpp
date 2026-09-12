#include "controller.h"
#include <QtTest>
#include <QTemporaryDir>
#include <QDir>
#include <QImage>
using namespace piclens;
class ControllerTest:public QObject{
 Q_OBJECT
private slots:
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
};
QTEST_MAIN(ControllerTest)
#include "controller_test.moc"
