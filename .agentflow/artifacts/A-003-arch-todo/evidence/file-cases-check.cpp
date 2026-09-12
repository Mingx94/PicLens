#include "fileoperations.h"
#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QJsonDocument>
#include <QJsonObject>
#include <QTextStream>
using namespace piclens;
QString put(QString root,QString name){QString path=root+"/"+name;QFile f(path);if(!f.open(QIODevice::WriteOnly))return {};f.write("disposable image bytes");return path;}
QByteArray read(QString path){QFile f(path);f.open(QIODevice::ReadOnly);return f.readAll();}
Entry ent(QString p){QFileInfo f(p);auto s=FileStamp::capture(p);return {p,f.fileName(),f.suffix(),false,false,s.modified,s.size};}
int main(int argc,char**argv){QCoreApplication app(argc,argv);QString root=app.arguments()[1];QDir().mkpath(root);FileOperations ops;
 QString a=put(root,"a.png");auto original=read(a);auto ca=FilePlans::rename(a,"A");auto cr=ops.execute({ca});bool caseOnly=cr.succeeded()==1&&!QFile::exists(a)&&read(ca.target)==original;
 auto up=FilePlans::rename(ca.target,QString::fromUtf8("臺灣 e\xCC\x81"));auto ur=ops.execute({up});bool unicode=ur.succeeded()==1&&!QFile::exists(ca.target)&&read(up.target)==original;
 QString gone=put(root,"gone.png");auto gp=FilePlans::rename(gone,"absent");QFile::remove(gone);auto gr=ops.execute({gp});bool missing=gr.failed()==1&&!QFile::exists(gp.target);
 QString locked=root+"/locked";QDir().mkpath(locked);QString ls=put(locked,"keep.png");auto lp=FilePlans::rename(ls,"target");QFile::setPermissions(locked,QFileDevice::ReadOwner|QFileDevice::ExeOwner);auto lr=ops.execute({lp});QFile::setPermissions(locked,QFileDevice::ReadOwner|QFileDevice::WriteOwner|QFileDevice::ExeOwner);bool permission=lr.failed()==1&&read(ls)==original&&!QFile::exists(lp.target);
 QList<Entry> entries;for(auto suffix:{"jpg","jpeg","webp","png","bmp","gif"})entries<<ent(put(root,QString("same.")+suffix));auto plans=FilePlans::cleanup(entries);auto result=ops.execute(plans);bool cleanup=result.succeeded()==3&&QFile::exists(root+"/same.jpg")&&QFile::exists(root+"/same.jpeg")&&QFile::exists(root+"/same.webp");for(auto suffix:{"png","bmp","gif"})cleanup=cleanup&&!QFile::exists(root+"/same."+suffix);
 QJsonObject out{{"caseOnlyRename",caseOnly},{"unicodeDecomposedName",unicode},{"sourceDisappeared",missing},{"permissionFailurePreservesSource",permission},{"cleanupProtectsJpgJpegWebp",cleanup},{"cleanupSucceeded",result.succeeded()}};QTextStream(stdout)<<QJsonDocument(out).toJson();return caseOnly&&unicode&&missing&&permission&&cleanup?0:1;}
