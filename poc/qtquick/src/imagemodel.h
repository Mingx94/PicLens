#pragma once

#include <QAbstractListModel>
#include <QFutureWatcher>
#include <QUrl>

struct ImageEntry
{
    QString name;
    QString path;
    QUrl fileUrl;
    QString thumbnailUrl;
    qint64 byteSize = 0;
    qint64 modifiedAtMs = 0;
};

class ImageModel final : public QAbstractListModel
{
    Q_OBJECT
    Q_PROPERTY(QUrl folderUrl READ folderUrl NOTIFY folderUrlChanged)
    Q_PROPERTY(QString folderPath READ folderPath NOTIFY folderUrlChanged)
    Q_PROPERTY(int count READ count NOTIFY countChanged)
    Q_PROPERTY(bool loading READ loading NOTIFY loadingChanged)
    Q_PROPERTY(QString statusText READ statusText NOTIFY statusTextChanged)

public:
    enum Role {
        NameRole = Qt::UserRole + 1,
        PathRole,
        FileUrlRole,
        ThumbnailUrlRole,
        ByteSizeRole,
        ModifiedAtMsRole
    };
    Q_ENUM(Role)

    explicit ImageModel(QObject *parent = nullptr);

    [[nodiscard]] int rowCount(const QModelIndex &parent = QModelIndex()) const override;
    [[nodiscard]] QVariant data(const QModelIndex &index, int role) const override;
    [[nodiscard]] QHash<int, QByteArray> roleNames() const override;

    [[nodiscard]] QUrl folderUrl() const;
    [[nodiscard]] QString folderPath() const;
    [[nodiscard]] int count() const;
    [[nodiscard]] bool loading() const;
    [[nodiscard]] QString statusText() const;

    Q_INVOKABLE void openFolder(const QUrl &folderUrl);

signals:
    void folderUrlChanged();
    void countChanged();
    void loadingChanged();
    void statusTextChanged();

private:
    void startScan(const QUrl &folderUrl);
    void finishScan();
    static QVector<ImageEntry> scanFolder(const QString &folderPath);
    static QString makeThumbnailUrl(const QString &path, qint64 modifiedAtMs);

    QVector<ImageEntry> m_entries;
    QUrl m_folderUrl;
    QUrl m_pendingFolderUrl;
    QFutureWatcher<QVector<ImageEntry>> m_scanWatcher;
    bool m_loading = false;
    QString m_statusText = QStringLiteral("請選擇圖片資料夾");
    qint64 m_scanStartedAtMs = 0;
};
