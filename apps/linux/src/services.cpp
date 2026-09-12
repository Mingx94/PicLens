#include "services.h"
#include <QDir>
#include <QDirIterator>
#include <QFile>
#include <QDateTime>
#include <QJsonDocument>
#include <QSaveFile>
#include <QStandardPaths>
#include <QMutex>
#include <QMutexLocker>
#include <QtEndian>

namespace piclens {
namespace {
bool cancelled(const Cancel& c){return c&&c->load();}
bool skip(QFile& f,qint64 n){return n>=0&&n<=f.size()-f.pos()&&f.seek(f.pos()+n);}
bool blocks(QFile& f,const Cancel& c){char n;while(!cancelled(c)&&f.getChar(&n)){if(!quint8(n))return true;if(!skip(f,quint8(n)))return false;}return false;}
}
bool isAnimated(const QString& path,const Cancel& cancel){
    QString ext=QFileInfo(path).suffix().toLower();if(ext!="gif"&&ext!="webp")return false;
    QFile f(path);if(!f.open(QIODevice::ReadOnly))return false;
    if(ext=="webp"){
        auto h=f.read(12);if(h.size()!=12||h.left(4)!="RIFF"||h.mid(8)!="WEBP")return false;
        while(!cancelled(cancel)&&f.bytesAvailable()>=8){auto b=f.read(8);if(b.left(4)=="ANIM"||b.left(4)=="ANMF")return true;auto n=qFromLittleEndian<quint32>(reinterpret_cast<const uchar*>(b.constData()+4));if(!skip(f,qint64(n)+(n&1)))break;}return false;
    }
    auto h=f.read(13);if(h.size()!=13||(h.left(6)!="GIF87a"&&h.left(6)!="GIF89a"))return false;
    auto flags=quint8(h[10]);if((flags&128)&&!skip(f,3*(1<<((flags&7)+1))))return false;
    int count=0;char b;
    while(!cancelled(cancel)&&f.getChar(&b)){
        if(quint8(b)==0x3b)break;
        if(quint8(b)==0x21){if(!skip(f,1)||!blocks(f,cancel))break;}
        else if(quint8(b)==0x2c){if(++count>1)return true;auto desc=f.read(9);if(desc.size()!=9)break;flags=quint8(desc[8]);if((flags&128)&&!skip(f,3*(1<<((flags&7)+1))))break;if(!skip(f,1)||!blocks(f,cancel))break;}
        else break;
    }return false;
}
ScanResult scanFolder(const QString& folder,bool recursive,const Cancel& cancel){
    ScanResult result;QStringList pending{folder};QSet<QString> visited;
    const QStringList extensions{"jpg","jpeg","png","bmp","webp","gif"};
    while(!pending.isEmpty()&&!cancelled(cancel)){
        QString current=pending.takeLast();QFileInfo dir(current);QString identity=dir.canonicalFilePath();
        if(identity.isEmpty()||!dir.isReadable()){result.errors<<QStringLiteral("無法讀取資料夾：")+current;continue;}
        if(visited.contains(identity))continue;visited.insert(identity);
        QDirIterator it(current,QDir::AllEntries|QDir::NoDotAndDotDot|QDir::Hidden|QDir::System,QDirIterator::NoIteratorFlags);
        while(it.hasNext()&&!cancelled(cancel)){
            it.next();auto info=it.fileInfo();
            if(info.isDir()&&recursive){if(!info.isSymLink())pending<<info.absoluteFilePath();continue;}
            if(!info.isDir()&&!extensions.contains(info.suffix().toLower()))continue;
            Entry e;e.path=info.absoluteFilePath();e.name=info.fileName();e.extension="."+info.suffix().toLower();e.folder=info.isDir();e.size=e.folder?0:info.size();e.modified=info.lastModified().toMSecsSinceEpoch();e.animated=!e.folder&&isAnimated(e.path,cancel);result.entries<<e;
        }
    }return result;
}
QStringList childFolders(const QString& folder,const Cancel& cancel,QString* error){
    QStringList result;QFileInfo info(folder);if(!info.isDir()||!info.isReadable()){*error=QStringLiteral("無法讀取資料夾：")+folder;return result;}
    QDirIterator it(folder,QDir::Dirs|QDir::NoDotAndDotDot|QDir::Hidden,QDirIterator::NoIteratorFlags);
    while(it.hasNext()&&!cancelled(cancel)){it.next();if(!it.fileInfo().isSymLink())result<<it.filePath();}
    std::sort(result.begin(),result.end(),[](const auto&a,const auto&b){return a.compare(b,Qt::CaseInsensitive)<0;});return result;
}
QString dataRoot(const QString& overridePath){
    QString root=overridePath.trimmed();if(root.isEmpty())root=qEnvironmentVariable("PICLENS_DATA_ROOT").trimmed();
    if(root.isEmpty()){
#ifdef Q_OS_WIN
        root=QDir(qEnvironmentVariable("LOCALAPPDATA")).filePath("PicLens");
#else
        QString base=qEnvironmentVariable("XDG_DATA_HOME");if(base.isEmpty()||!QDir::isAbsolutePath(base))base=QDir::homePath()+"/.local/share";root=base+"/PicLens";
#endif
    }return QFileInfo(root).absoluteFilePath();
}
ProfileRead readProfile(const QString& root){
    ProfileRead result;QDir().mkpath(root);QFile f(root+"/piclens-settings.json");if(!f.exists())return result;
    if(!f.open(QIODevice::ReadOnly)){result.error=QStringLiteral("無法讀取設定：")+f.errorString();result.writable=false;return result;}
    QJsonParseError error;auto document=QJsonDocument::fromJson(f.readAll(),&error);f.close();
    if(error.error!=QJsonParseError::NoError||!document.isObject()){
        QString quarantine=f.fileName()+".corrupt."+QString::number(QDateTime::currentMSecsSinceEpoch());
        if(!f.rename(quarantine)){result.writable=false;result.error=QStringLiteral("損壞設定無法隔離，已停止寫入設定。");}
        else result.error=QStringLiteral("損壞設定已隔離：")+quarantine;
        return result;
    }result.settings=document.object();return result;
}
QString writeProfile(const QString& root,const QJsonObject& settings){
    QSaveFile file(root+"/piclens-settings.json");file.setDirectWriteFallback(false);
    if(!file.open(QIODevice::WriteOnly))return file.errorString();auto bytes=QJsonDocument(settings).toJson();
    if(file.write(bytes)!=bytes.size()||!file.commit())return file.errorString();return {};
}
void appendLog(const QString& root,const QString& message){
    static QMutex lock;QMutexLocker guard(&lock);QDir().mkpath(root+"/Logs");QFile file(root+"/Logs/PicLens.log");
    if(file.open(QIODevice::WriteOnly|QIODevice::Append))file.write((QDateTime::currentDateTimeUtc().toString(Qt::ISODateWithMs)+" [Qt] "+message+"\n").toUtf8());
}
}
