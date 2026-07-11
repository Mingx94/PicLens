#include "imagemodel.h"

#include <QDateTime>
#include <QDirIterator>
#include <QFileInfo>
#include <QImageReader>
#include <QMimeDatabase>
#include <QtConcurrentRun>

#include <algorithm>

namespace
{
QStringList supportedNameFilters()
{
    QStringList filters;
    const auto formats = QImageReader::supportedImageFormats();
    filters.reserve(formats.size());

    for (const QByteArray &format : formats) {
        filters.append(QStringLiteral("*.%1").arg(QString::fromLatin1(format)));
    }

    filters.removeDuplicates();
    return filters;
}
}

ImageModel::ImageModel(QObject *parent)
    : QAbstractListModel(parent)
{
    connect(&m_scanWatcher, &QFutureWatcherBase::finished, this, &ImageModel::finishScan);
}

int ImageModel::rowCount(const QModelIndex &parent) const
{
    return parent.isValid() ? 0 : m_entries.size();
}

QVariant ImageModel::data(const QModelIndex &index, int role) const
{
    if (!index.isValid() || index.row() < 0 || index.row() >= m_entries.size()) {
        return {};
    }

    const ImageEntry &entry = m_entries.at(index.row());
    switch (role) {
    case NameRole:
        return entry.name;
    case PathRole:
        return entry.path;
    case FileUrlRole:
        return entry.fileUrl;
    case ThumbnailUrlRole:
        return entry.thumbnailUrl;
    case ByteSizeRole:
        return entry.byteSize;
    case ModifiedAtMsRole:
        return entry.modifiedAtMs;
    default:
        return {};
    }
}

QHash<int, QByteArray> ImageModel::roleNames() const
{
    return {
        {NameRole, "name"},
        {PathRole, "path"},
        {FileUrlRole, "fileUrl"},
        {ThumbnailUrlRole, "thumbnailUrl"},
        {ByteSizeRole, "byteSize"},
        {ModifiedAtMsRole, "modifiedAtMs"},
    };
}

QUrl ImageModel::folderUrl() const
{
    return m_folderUrl;
}

QString ImageModel::folderPath() const
{
    return m_folderUrl.toLocalFile();
}

int ImageModel::count() const
{
    return m_entries.size();
}

bool ImageModel::loading() const
{
    return m_loading;
}

QString ImageModel::statusText() const
{
    return m_statusText;
}

void ImageModel::openFolder(const QUrl &folderUrl)
{
    if (!folderUrl.isLocalFile()) {
        return;
    }

    const QFileInfo folderInfo(folderUrl.toLocalFile());
    if (!folderInfo.exists() || !folderInfo.isDir()) {
        return;
    }

    const QUrl normalizedUrl = QUrl::fromLocalFile(folderInfo.canonicalFilePath());
    if (m_loading) {
        m_pendingFolderUrl = normalizedUrl;
        m_statusText = QStringLiteral("正在等待目前的掃描完成…");
        emit statusTextChanged();
        return;
    }

    startScan(normalizedUrl);
}

void ImageModel::startScan(const QUrl &folderUrl)
{
    m_folderUrl = folderUrl;
    m_loading = true;
    m_scanStartedAtMs = QDateTime::currentMSecsSinceEpoch();
    m_statusText = QStringLiteral("正在掃描圖片…");

    emit folderUrlChanged();
    emit loadingChanged();
    emit statusTextChanged();

    const QString path = folderUrl.toLocalFile();
    m_scanWatcher.setFuture(QtConcurrent::run([path] { return scanFolder(path); }));
}

void ImageModel::finishScan()
{
    const QVector<ImageEntry> scannedEntries = m_scanWatcher.result();

    beginResetModel();
    m_entries = scannedEntries;
    endResetModel();

    const qint64 elapsedMs = QDateTime::currentMSecsSinceEpoch() - m_scanStartedAtMs;
    m_loading = false;
    m_statusText = QStringLiteral("%1 張圖片 · 掃描 %2 ms").arg(m_entries.size()).arg(elapsedMs);

    emit countChanged();
    emit loadingChanged();
    emit statusTextChanged();

    if (!m_pendingFolderUrl.isEmpty()) {
        const QUrl nextFolderUrl = m_pendingFolderUrl;
        m_pendingFolderUrl.clear();
        startScan(nextFolderUrl);
    }
}

QVector<ImageEntry> ImageModel::scanFolder(const QString &folderPath)
{
    QVector<ImageEntry> entries;
    QDirIterator iterator(folderPath, supportedNameFilters(), QDir::Files | QDir::Readable, QDirIterator::NoIteratorFlags);

    while (iterator.hasNext()) {
        iterator.next();
        const QFileInfo info = iterator.fileInfo();
        const qint64 modifiedAtMs = info.lastModified().toMSecsSinceEpoch();

        entries.append(ImageEntry{
            .name = info.fileName(),
            .path = info.absoluteFilePath(),
            .fileUrl = QUrl::fromLocalFile(info.absoluteFilePath()),
            .thumbnailUrl = makeThumbnailUrl(info.absoluteFilePath(), modifiedAtMs),
            .byteSize = info.size(),
            .modifiedAtMs = modifiedAtMs,
        });
    }

    std::sort(entries.begin(), entries.end(), [](const ImageEntry &left, const ImageEntry &right) {
        return QString::localeAwareCompare(left.name, right.name) < 0;
    });

    return entries;
}

QString ImageModel::makeThumbnailUrl(const QString &path, qint64 modifiedAtMs)
{
    const QByteArray encodedPath = path.toUtf8().toBase64(QByteArray::Base64UrlEncoding | QByteArray::OmitTrailingEquals);
    return QStringLiteral("image://thumbnail/%1/%2")
        .arg(QString::fromLatin1(encodedPath), QString::number(modifiedAtMs));
}
