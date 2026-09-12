#include "controller.h"
#include "imageitem.h"
#include <QApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QQuickStyle>
#include <QQuickWindow>
#include <QTest>
#include <QJsonDocument>
#include <QJsonArray>
#include <QFile>
#include <QDir>
#include <QElapsedTimer>
#include <QCryptographicHash>
#include <QImageReader>
#include <QTextStream>
#include <QPainter>
using namespace piclens;
static void writeJson(QString path,QJsonObject o){QFile f(path);if(!f.open(QIODevice::WriteOnly))qFatal("output failed");f.write(QJsonDocument(o).toJson());}
int main(int argc,char**argv){
 QApplication gui(argc,argv);QQuickStyle::setStyle("Basic");
 const auto args=gui.arguments();if(args.size()<4)return 2;
 const QString folder=args.at(1),root=args.at(2),worker=args.at(3);QDir().mkpath(root);
 if(args.contains("--prepare")){
  QDir().mkpath(folder+"/../corpus");const QString out=folder+"/../corpus/";
  const QStringList names={"Shaki_waterfall.jpg","Gull_portrait_ca_usa.jpg","Hopetoun_falls.jpg"};
  for(const auto&n:names){QImage im(folder+"/"+n);if(im.isNull())return 3;QFile::copy(folder+"/"+n,out+n);}
  QImage shaki(folder+"/Shaki_waterfall.jpg"),gull(folder+"/Gull_portrait_ca_usa.jpg"),hopetoun(folder+"/Hopetoun_falls.jpg");
  if(!shaki.save(out+"瀑布_無損.png")||!gull.save(out+"海鷗_無壓縮.bmp")||!hopetoun.save(out+"森林_WebP.webp","WEBP",100))return 4;
  QFile::copy(folder+"/Gull_portrait_ca_usa.jpg",out+"海鷗_原檔.jpeg");
  QImage composite(4000,3000,QImage::Format_RGB32);composite.fill(Qt::white);
  {QPainter p(&composite);p.drawImage(QRect(0,0,2000,1500),gull);p.drawImage(QRect(2000,0,2000,1500),hopetoun);p.drawImage(QRect(0,1500,2000,1500),shaki);p.drawImage(QRect(2000,1500,2000,1500),gull);}
  if(!composite.save(out+"照片拼圖_12MP.jpg","JPG",90))return 4;
  QImage chart(2400,1600,QImage::Format_RGBA8888);chart.fill(Qt::transparent);
  {QPainter p(&chart);p.setRenderHint(QPainter::Antialiasing);p.setFont(QFont("sans-serif",40));p.setPen(Qt::black);p.drawText(100,90,"PicLens validation: generated chart with alpha");for(int y=200;y<1500;y+=100){p.setPen(QColor(160,160,160,100));p.drawLine(100,y,2300,y);}for(int i=0;i<12;++i){p.setBrush(QColor(20+i*15,80,180,180));p.setPen(Qt::NoPen);p.drawRect(150+i*175,1400-(i+1)*90,100,(i+1)*90);}}
  if(!chart.save(out+"圖表_透明.png"))return 4;
  return 0;
 }
 qmlRegisterType<ImageItem>("PicLens.Native",1,0,"ImageItem");
 auto*provider=new ThumbProvider;Controller c(root+"/profile",worker,provider);
 QQmlApplicationEngine engine;engine.addImageProvider("thumb",provider);
 engine.rootContext()->setContextProperty("app",&c);engine.rootContext()->setContextProperty("launchWidth",1600);engine.rootContext()->setContextProperty("launchHeight",1000);engine.rootContext()->setContextProperty("showComponents",false);
 engine.load(QUrl("qrc:/qml/Main.qml"));auto*w=qobject_cast<QQuickWindow*>(engine.rootObjects().value(0));if(!w||!QTest::qWaitForWindowExposed(w))return 5;
 w->requestActivate();QTest::qWaitForWindowActive(w);
 c.start(folder);QElapsedTimer ready;ready.start();while(c.count()==0&&ready.elapsed()<10000)QTest::qWait(20);if(c.count()==0)return 6;
 QTest::qWait(1000);QJsonArray manifest,observations;
 auto*rows=qobject_cast<Rows*>(c.library());for(int i=0;i<rows->rowCount();++i){auto row=rows->get(i);QString path=row.value("path").toString();QFile f(path);f.open(QIODevice::ReadOnly);QImageReader reader(path);manifest.append(QJsonObject{{"path",path},{"bytes",double(f.size())},{"sha256",QString(QCryptographicHash::hash(f.readAll(),QCryptographicHash::Sha256).toHex())},{"width",reader.size().width()},{"height",reader.size().height()}});}
 const int count=c.count();
 for(int i=0;i<count;++i){
  int before=c.metrics().value("viewerSharpPaintCount").toInt();if(i==0)c.openViewer(0);else c.viewerStep(1);
  QElapsedTimer wait;wait.start();while(c.metrics().value("viewerSharpPaintCount").toInt()==before&&wait.elapsed()<10000&&c.viewerError().isEmpty()){QTest::qWait(5);}
  QJsonObject obs{{"name",c.viewerName()},{"index",c.viewerIndex()},{"painted",c.metrics().value("viewerSharpPaintCount").toInt()>before},{"error",c.viewerError()},{"waitMilliseconds",double(wait.elapsed())}};observations.append(obs);QTextStream(stdout)<<QJsonDocument(obs).toJson(QJsonDocument::Compact)<<Qt::endl;
 }
 // Same Viewer A-B-A and close/reopen are separate identified samples.
 if(count>1){c.viewerStep(-1);QTest::qWait(1000);c.viewerStep(1);QTest::qWait(1000);c.closeViewer();QTest::qWait(100);c.openViewer(count-1);QTest::qWait(1500);}
 w->grabWindow().save(root+"/viewer.png");c.closeViewer();QElapsedTimer stop;stop.start();c.shutdown();auto result=c.metrics();result.insert("manifest",manifest);result.insert("serialObservations",observations);result.insert("shutdownMilliseconds",double(stop.elapsed()));result.insert("windowWidth",w->width());result.insert("windowHeight",w->height());result.insert("devicePixelRatio",w->devicePixelRatio());writeJson(root+"/results.json",result);
 return 0;
}
