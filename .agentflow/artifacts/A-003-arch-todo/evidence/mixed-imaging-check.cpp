#include "imaging.h"
#include <QGuiApplication>
#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QElapsedTimer>
#include <QThread>
#include <QTextStream>
#include <QJsonDocument>
#include <QJsonObject>
#include <functional>
using namespace piclens;
bool waitFor(std::function<bool()> f,int ms){QElapsedTimer t;t.start();while(!f()&&t.elapsed()<ms){QCoreApplication::processEvents();QThread::msleep(5);}return f();}
int main(int argc,char**argv){
 if(argc>1&&QByteArray(argv[1])=="--stall"){QCoreApplication app(argc,argv);return app.exec();}
 QGuiApplication app(argc,argv);QString root=app.arguments()[1],worker=app.arguments()[2];QDir().mkpath(root);QImage image(32,20,QImage::Format_RGBA8888);image.fill(Qt::green);
 QString stall=root+"/stall.png",good=root+"/good.png";if(!image.save(stall)||!image.save(good))return 2;
 QFile script(root+"/wrapper");if(!script.open(QIODevice::WriteOnly))return 2;
 script.write(("#!/bin/sh\ncase \"$2\" in *stall.png) printf '%s\\n' \"$$\" >> \"$2.pids\"; exec '"+app.applicationFilePath()+"' --stall ;; *) exec '"+worker+"' \"$@\" ;; esac\n").toUtf8());script.close();script.setPermissions(QFileDevice::ReadOwner|QFileDevice::WriteOwner|QFileDevice::ExeOwner);
 int done=0,failed=0;bool goodDone=false;Imaging imaging(script.fileName(),root+"/cache");
 QObject::connect(&imaging,&Imaging::completed,&app,[&](QString token,FramePtr frame,QString error){++done;if(token=="good")goodDone=bool(frame);else if(!frame&&error.contains(QStringLiteral("逾時")))++failed;});
 bool enqueued=imaging.enqueue(stall,96,"stall-a")&&imaging.enqueue(stall,96,"stall-b")&&imaging.enqueue(good,96,"good");
 bool progress=waitFor([&]{return goodDone;},5000);int pendingWhenGood=imaging.outstanding();
 QFile pidFile(stall+".pids");if(!pidFile.open(QIODevice::ReadOnly))return 3;auto pidLines=pidFile.readAll().trimmed().split('\n');bool completed=waitFor([&]{return done==3;},20000);imaging.shutdown();
 bool reaped=true;for(const auto&pid:pidLines)if(QFile::exists("/proc/"+QString::fromUtf8(pid)+"/stat"))reaped=false;
 const auto temporary=QDir(QDir::tempPath()).entryList({"piclens-imaging-*"},QDir::Dirs|QDir::NoDotAndDotDot);
 QJsonObject result{{"enqueued",enqueued},{"goodCompletedBeforeStall",progress},{"pendingWhenGoodCompleted",pendingWhenGood},{"deduplicatedHelperCount",pidLines.size()},{"allCompleted",completed},{"timeoutResults",failed},{"reaped",reaped},{"remainingTemporaryDirectoryCount",temporary.size()}};
 QTextStream(stdout)<<QJsonDocument(result).toJson();return enqueued&&progress&&pendingWhenGood==2&&pidLines.size()==1&&completed&&failed==2&&reaped&&temporary.isEmpty()?0:1;
}
