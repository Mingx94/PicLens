#include "controller.h"
#include "imageitem.h"
#include <QGuiApplication>
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
#include <QTextStream>
using namespace piclens;
int main(int argc,char**argv){
 QGuiApplication gui(argc,argv);QQuickStyle::setStyle("Basic");
 qmlRegisterType<ImageItem>("PicLens.Native",1,0,"ImageItem");
 const QString root=gui.arguments().at(1),worker=gui.arguments().at(2);QDir().mkpath(root);
 auto*provider=new ThumbProvider;Controller c(root+"/profile",worker,provider);
 QQmlApplicationEngine engine;engine.addImageProvider("thumb",provider);
 engine.rootContext()->setContextProperty("app",&c);
 engine.rootContext()->setContextProperty("launchWidth",1600);
 engine.rootContext()->setContextProperty("launchHeight",1000);
 engine.rootContext()->setContextProperty("showComponents",false);
 engine.load(QUrl("qrc:/qml/Main.qml"));
 auto*w=qobject_cast<QQuickWindow*>(engine.rootObjects().value(0));if(!w||!QTest::qWaitForWindowExposed(w))return 2;
 w->requestActivate();if(!QTest::qWaitForWindowActive(w))return 2;
 QElapsedTimer load;load.start();c.diagnose(10000);const auto loadMs=load.elapsed();QTest::qWait(300);
 if(c.count()!=10000)return 3;
 c.setSearch("image999");const auto searchMs=c.metrics().value("searchMilliseconds");const int filteredCount=c.count();if(filteredCount!=11)return 4;
 c.setSearch("");QJsonArray positions;
 for(int row:{2000,4000,6000,8000,9999}){emit c.scrollTo(row);QTest::qWait(650);positions.append(row);}
 const bool galleryStayedOpen=!c.viewerOpen();const bool screenshot=w->grabWindow().save(root+"/grid.png");
 QElapsedTimer shutdown;shutdown.start();c.shutdown();const auto shutdownMs=shutdown.elapsed();auto result=c.metrics();
 result.insert("syntheticModelLoadMilliseconds",static_cast<double>(loadMs));
 result.insert("filteredSearchMilliseconds",searchMs);result.insert("filteredSearchCount",filteredCount);
 result.insert("scrollRows",positions);result.insert("galleryStayedOpen",galleryStayedOpen);
 result.insert("actualWidth",w->width());result.insert("actualHeight",w->height());result.insert("shutdownMilliseconds",static_cast<double>(shutdownMs));
 result.insert("sourceCommit",gui.arguments().at(3));result.insert("dataset","10000 generated model entries; no image decode");
 QFile output(root+"/results.json");if(!output.open(QIODevice::WriteOnly))return 5;output.write(QJsonDocument(result).toJson());
 QTextStream(stdout)<<QJsonDocument(result).toJson();return galleryStayedOpen&&screenshot&&c.count()==10000&&shutdownMs<5000?0:6;
}
