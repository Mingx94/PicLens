#pragma once

#include <QJsonObject>
#include <QList>
#include <QStringList>
#include <optional>

namespace piclens {

// modified 與 FileStamp::modified 均為 UTC Unix 毫秒。路徑使用絕對路徑、區分大小寫。
struct Entry {
    QString path, name, extension;
    bool folder = false, animated = false;
    qint64 modified = 0, size = 0;
};
struct SortSettings { int key = 0, direction = 0; };
struct Settings {
    QString lastFolderPath;
    int sortKey = 0, sortDirection = 0;
    bool includeSubfolders = false;
    int thumbnailSize = 160;
    bool sidebarCollapsed = false;
    std::optional<qint64> windowWidth, windowHeight;
    Settings normalize() const;
    // 錯誤型別／損壞 JSON 拋 std::invalid_argument；未知欄位忽略。
    static Settings fromJson(const QJsonObject &json);
    static Settings fromJson(const QByteArray &json);
    QJsonObject toJson() const;
};

bool supportedImage(const QString &path);
int naturalCompare(const QString &a, const QString &b);
QList<Entry> sortEntries(QList<Entry> entries, SortSettings sort, bool foldersFirst);
QList<Entry> projectEntries(const QList<Entry> &entries, const QString &query);
QList<Entry> project(const QList<Entry> &entries, const QString &query, const Settings &settings);
QStringList imagePaths(const QList<Entry> &entries);

class Selection {
public:
    const QStringList &ordered() const { return ordered_; }
    const QString &anchor() const { return anchor_; }
    bool contains(const QString &path) const { return ordered_.contains(path); }
    void clear();
    // visible 必須是當下 imagePaths(projection)；找不到的 path 不會被加入。
    void select(const QString &path, const QStringList &visible, bool control, bool shift);
    void contextSelect(const QString &path, const QStringList &visible);
    void retainVisible(const QStringList &visible);
private:
    QStringList ordered_;
    QString anchor_;
};

enum class OperationKind { Jpeg, Webp, Rename, Trash };
enum class ResultStatus { Succeeded, Skipped, Canceled, Failed, Unknown };
struct FileStamp {
    qint64 size = -1, modified = -1;
    bool operator==(const FileStamp &) const = default;
    static FileStamp capture(const QString &path);
};
struct FilePlan {
    QString source, target;
    OperationKind kind = OperationKind::Trash;
    FileStamp stamp;
    QString skip;
    bool checkStem = false;
};
struct FileResult {
    QString source, target;
    ResultStatus status = ResultStatus::Failed;
    QString message;
};
struct BatchResult {
    QList<FileResult> items;
    qsizetype total() const { return items.size(); }
    qsizetype succeeded() const;
    qsizetype skipped() const;
    qsizetype canceled() const;
    qsizetype unknown() const;
    qsizetype failed() const; // 包含 Unknown，避免讓不確定結果看似成功。
    QString summary() const;
};

// 計畫只讀磁碟，不修改檔案；請在背景執行緒建立並保留確認畫面快照。
// convert／cleanup 只使用傳入的可見投影。確認、request identity 與 UI 更新由應用層負責。
class FilePlans {
public:
    static bool requiresConversionConfirmation(qsizetype visibleImageCount) { return visibleImageCount >= 50; }
    static QString validateRename(const QString &source, const QString &basename);
    static FilePlan rename(const QString &source, const QString &basename);
    static QList<FilePlan> convert(const QList<Entry> &visible, OperationKind kind);
    static QList<FilePlan> cleanup(const QList<Entry> &visible);
    static QList<FilePlan> trash(const QStringList &sources);
    // existing 是目標目錄所有項目快照，含資料夾、隱藏檔與非圖片。
    // 接受絕對路徑，或 QDir::entryList() 的檔名；相對值一律以 target 的目錄解析。
    static QList<FilePlan> dropRename(const QStringList &sources, const QString &target,
                                      const QStringList &existing);
};
} // namespace piclens
