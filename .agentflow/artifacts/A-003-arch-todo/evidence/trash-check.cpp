#include "domain.h"
#include "fileoperations.h"
#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QTextStream>
using namespace piclens;
int main(int argc,char **argv) {
 QCoreApplication app(argc,argv);QString root=QString::fromLocal8Bit(argv[1]);QDir().mkpath(root+"/fixtures");
 QString path=root+"/fixtures/測試 空白;$().png";QFile f(path);if(!f.open(QIODevice::WriteOnly|QIODevice::NewOnly))return 2;f.write("disposable fixture");f.close();
 auto result=FileOperations().execute(FilePlans::trash({path}));
 QTextStream(stdout)<<"total="<<result.total()<<" success="<<result.succeeded()<<" failed="<<result.failed()<<" unknown="<<result.unknown()<<" sourceExists="<<QFile::exists(path)<<Qt::endl;
 QString trash=root+"/data/Trash/files/"+QFileInfo(path).fileName();QFile saved(trash);
 bool verified=result.succeeded()==1&&!QFile::exists(path)&&saved.open(QIODevice::ReadOnly)&&saved.readAll()=="disposable fixture";
 QTextStream(stdout)<<"trashBytesVerified="<<verified<<Qt::endl;return verified?0:1;
}
