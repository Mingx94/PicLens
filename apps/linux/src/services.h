#pragma once
#include "domain.h"
#include <QJsonObject>
#include <QStringList>
#include <atomic>
#include <memory>

namespace piclens {
using Cancel=std::shared_ptr<std::atomic_bool>;
struct ScanResult { QVector<Entry> entries; QStringList errors; };
bool isAnimated(const QString& path,const Cancel& cancel);
ScanResult scanFolder(const QString& folder,bool recursive,const Cancel& cancel);
QStringList childFolders(const QString& folder,const Cancel& cancel,QString* error);
struct ProfileRead { QJsonObject settings; QString error; bool writable=true; };
QString dataRoot(const QString& overridePath={});
ProfileRead readProfile(const QString& root);
QString writeProfile(const QString& root,const QJsonObject& settings);
void appendLog(const QString& root,const QString& message);
}
