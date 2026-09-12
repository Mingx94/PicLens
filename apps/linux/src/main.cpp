#include "controller.h"
#include "imageitem.h"
#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QQuickWindow>
#include <QQuickStyle>
#include <QCommandLineParser>
#include <QIcon>
#include <QJsonDocument>
#include <QFile>
#include <QDir>
#include <QTimer>

int main(int argc,char**argv){
    QGuiApplication gui(argc,argv);QGuiApplication::setApplicationName("PicLens");QGuiApplication::setApplicationVersion(PICLENS_VERSION);
    QGuiApplication::setDesktopFileName("piclens");QGuiApplication::setWindowIcon(QIcon(":/Square150x150Logo.scale-200.png"));
    QQuickStyle::setStyle("Basic");QCommandLineParser parser;parser.setApplicationDescription("PicLens Arch Linux / Qt development preview");parser.addHelpOption();parser.addVersionOption();
    for(const auto& name:{"folder","data-root","smoke-ms","screenshot","metrics","viewer","width","height","diagnostic-items"})parser.addOption(QCommandLineOption(name,name,"value"));
    parser.addOption(QCommandLineOption("dark","Use dark palette"));parser.addOption(QCommandLineOption("exercise","Exercise non-destructive navigation"));parser.addOption(QCommandLineOption("components","Show component gallery"));
    if(!parser.parse(gui.arguments())){qCritical().noquote()<<parser.errorText();return 2;}
    if(parser.isSet("help"))parser.showHelp();if(parser.isSet("version"))parser.showVersion();
    auto number=[&](const char*key,int fallback,int minimum){if(!parser.isSet(key))return fallback;bool ok;int value=parser.value(key).toInt(&ok);if(!ok||value<minimum)throw std::runtime_error(QString("Invalid --%1").arg(key).toStdString());return value;};
    try{
        int width=number("width",1600,800),height=number("height",1000,600),smoke=number("smoke-ms",0,0),items=number("diagnostic-items",0,0);
        QString profile=piclens::dataRoot(parser.value("data-root"));QString worker=QCoreApplication::applicationDirPath()+"/piclens-worker";
#ifdef Q_OS_WIN
        worker+=".exe";
#else
        if(!QFileInfo::exists(worker))worker=QDir(QCoreApplication::applicationDirPath()).absoluteFilePath("../libexec/piclens/piclens-worker");
#endif
        if(!QFileInfo::exists(worker))throw std::runtime_error("piclens-worker missing");
        piclens::appendLog(profile,QString("啟動 version=%1 Qt=%2 platform=%3").arg(PICLENS_VERSION,qVersion(),QGuiApplication::platformName()));
        auto* provider=new piclens::ThumbProvider;
        piclens::Controller controller(profile,worker,provider); // engine owns provider; shutdown before engine destruction
        QQmlApplicationEngine engine;engine.addImageProvider("thumb",provider);
        qmlRegisterType<piclens::ImageItem>("PicLens.Native",1,0,"ImageItem");
        engine.rootContext()->setContextProperty("app",&controller);engine.rootContext()->setContextProperty("launchWidth",width);engine.rootContext()->setContextProperty("launchHeight",height);engine.rootContext()->setContextProperty("showComponents",parser.isSet("components"));
        if(parser.isSet("dark"))controller.setProperty("dark",true);
        engine.load(QUrl("qrc:/qml/Main.qml"));if(engine.rootObjects().isEmpty()){controller.shutdown();return 2;}
        auto* window=qobject_cast<QQuickWindow*>(engine.rootObjects().first());
        QTimer::singleShot(0,&controller,[&]{if(items)controller.diagnose(items);else controller.start(parser.value("folder"));});
        if(items)for(int step=1;step<=5;++step)QTimer::singleShot(step*600,&controller,[&,step]{emit controller.scrollTo(qMin(items-1,step*(items/5)));});
        if(parser.isSet("viewer")){QString target=QFileInfo(parser.value("viewer")).absoluteFilePath();auto* timer=new QTimer(&controller);timer->setInterval(50);QObject::connect(timer,&QTimer::timeout,&controller,[&,timer,target]{auto* model=qobject_cast<piclens::Rows*>(controller.library());for(int i=0;i<model->rowCount();++i)if(model->get(i).value("path")==target){controller.openViewer(i);timer->stop();return;}});timer->start();QTimer::singleShot(10000,timer,&QTimer::stop);}
        if(parser.isSet("exercise"))QTimer::singleShot(1200,&controller,&piclens::Controller::exercise);
        // Hidden/occluded desktop previews may not submit frames until capture.
        // Warm captures let QML image bindings and the scene graph settle.
        if(parser.isSet("screenshot"))for(int delay:{400,1000,1800})QTimer::singleShot(delay,&controller,[window]{if(window)window->grabWindow();});
        if(parser.isSet("screenshot"))QTimer::singleShot(qMax(parser.isSet("exercise")?6000:2200,smoke-500),&controller,[&,window]{QString path=QFileInfo(parser.value("screenshot")).absoluteFilePath();QDir().mkpath(QFileInfo(path).absolutePath());bool ok=window&&window->grabWindow().save(path);piclens::appendLog(profile,ok?"截圖 "+path:"截圖失敗 "+path);if(!ok)gui.exit(3);});
        if(smoke)QTimer::singleShot(qMax(smoke,parser.isSet("screenshot")?(parser.isSet("exercise")?6500:2600):smoke),&gui,&QGuiApplication::quit);
        QObject::connect(&gui,&QGuiApplication::aboutToQuit,&controller,[&]{controller.shutdown();});
        int result=gui.exec();controller.shutdown();
        if(parser.isSet("metrics")){QString path=QFileInfo(parser.value("metrics")).absoluteFilePath();QDir().mkpath(QFileInfo(path).absolutePath());QFile file(path);const auto data=QJsonDocument(controller.metrics()).toJson();if(!file.open(QIODevice::WriteOnly)||file.write(data)!=data.size()||!file.flush())return 3;}
        // Controller's destructor is idempotent and does not access the provider after shutdown.
        return result;
    }catch(const std::exception&e){qCritical().noquote()<<e.what();return 2;}
}
