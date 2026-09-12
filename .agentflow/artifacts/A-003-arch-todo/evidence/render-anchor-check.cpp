#include "imageitem.h"
#include <QGuiApplication>
#include <QQuickWindow>
#include <QTest>
#include <QJsonDocument>
#include <QJsonObject>
#include <QTextStream>
#include <QDir>
using namespace piclens;
int main(int argc,char**argv){QGuiApplication app(argc,argv);QQuickWindow window;window.resize(800,600);window.setColor(Qt::black);auto *item=new ImageItem(window.contentItem());window.show();if(!QTest::qWaitForWindowExposed(&window))return 2;QTest::qWait(100);item->setSize(window.size());
 auto frame=std::make_shared<Frame>();frame->size={200,200};frame->token="anchor";QImage pixels(202,202,QImage::Format_RGBA8888_Premultiplied);for(int y=0;y<202;++y)for(int x=0;x<202;++x)pixels.setPixelColor(x,y,QColor(std::clamp(x-1,0,199),std::clamp(y-1,0,199),100));frame->tiles.append({{0,0,200,200},pixels});item->setFrame(frame);QTest::qWait(100);
 QPoint anchor(window.width()*.65,window.height()*.45);auto before=window.grabWindow();auto c=before.pixelColor(anchor);QTest::wheelEvent(&window,anchor,{0,120});QTest::qWait(80);auto after=window.grabWindow();auto d=after.pixelColor(anchor);bool anchored=std::abs(c.red()-d.red())<=2&&std::abs(c.green()-d.green())<=2;
 QPoint center(window.width()/2,window.height()/2),delta(30,20);auto beforePan=after.pixelColor(center);QTest::mousePress(&window,Qt::LeftButton,Qt::NoModifier,center);QTest::mouseMove(&window,center+delta);QTest::mouseRelease(&window,Qt::LeftButton,Qt::NoModifier,center+delta);QTest::qWait(80);auto moved=window.grabWindow();auto afterPan=moved.pixelColor(center+delta);bool pan=std::abs(beforePan.red()-afterPan.red())<=2&&std::abs(beforePan.green()-afterPan.green())<=2;
 bool zoom=std::abs(item->zoom()-1.2)<.00001;item->reset();bool reset=item->zoom()==1.;std::weak_ptr<const Frame> weak=frame;item->setFrame({});frame.reset();QTest::qWait(80);window.grabWindow();QTest::qWait(40);bool released=weak.expired();
 QJsonObject result{{"platform",QGuiApplication::platformName()},{"windowWidth",window.width()},{"windowHeight",window.height()},{"beforeAnchorColor",c.name()},{"afterAnchorColor",d.name()},{"pointerAnchorPreserved",anchored},{"panMovesImageWithPointer",pan},{"zoomStep1p2",zoom},{"resetFit100Percent",reset},{"frameReleasedAfterSceneGraphUpdate",released}};QTextStream(stdout)<<QJsonDocument(result).toJson();return anchored&&pan&&zoom&&reset&&released?0:1;}
