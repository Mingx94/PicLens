#include "domain.h"

#include <QDateTime>
#include <QDir>
#include <QFileInfo>
#include <QJsonDocument>
#include <QJsonParseError>
#include <QSet>
#include <algorithm>
#include <cmath>
#include <limits>
#include <stdexcept>

namespace piclens {
namespace {
[[noreturn]] void invalid(const QString &message) { throw std::invalid_argument(message.toStdString()); }
QJsonValue field(const QJsonObject &o, const QString &name) {
    for (auto i = o.begin(); i != o.end(); ++i)
        if (i.key().compare(name, Qt::CaseInsensitive) == 0) return i.value();
    return QJsonValue(QJsonValue::Undefined);
}
qint64 integer(const QJsonValue &v, qint64 fallback) {
    if (v.isUndefined()) return fallback;
    if (!v.isDouble() || !std::isfinite(v.toDouble()) || std::trunc(v.toDouble()) != v.toDouble()
        || v.toDouble() < -9007199254740991.0 || v.toDouble() > 9007199254740991.0)
        invalid(QStringLiteral("設定欄位必須是整數"));
    return static_cast<qint64>(v.toDouble());
}
bool boolean(const QJsonValue &v, bool fallback) {
    if (v.isUndefined()) return fallback;
    if (!v.isBool()) invalid(QStringLiteral("設定欄位必須是布林值"));
    return v.toBool();
}
QString absolute(const QString &p) { return QDir::cleanPath(QFileInfo(p).absoluteFilePath()); }
QString stemPath(const QString &p) {
    const QFileInfo f(p);
    return f.dir().absoluteFilePath(f.completeBaseName());
}
QString extension(const Entry &e) { return QFileInfo(e.path).suffix().toLower(); }
bool kept(const Entry &e) {
    const auto ext = extension(e);
    return ext == "jpg" || ext == "jpeg" || ext == "webp";
}
bool exists(const QString &path) { const QFileInfo f(path); return f.exists() || f.isSymbolicLink(); }
qsizetype count(const BatchResult &b, ResultStatus s) {
    return std::count_if(b.items.begin(), b.items.end(), [s](const auto &r) { return r.status == s; });
}
}

Settings Settings::normalize() const {
    auto s = *this;
    s.sortKey = sortKey == 1 ? 1 : 0;
    s.sortDirection = sortDirection == 1 ? 1 : 0;
    s.thumbnailSize = thumbnailSize == 0 ? 160 : ((std::clamp(thumbnailSize, 120, 240) + 10) / 20) * 20;
    if (windowWidth && windowHeight) {
        s.windowWidth = std::max<qint64>(800, *windowWidth);
        s.windowHeight = std::max<qint64>(600, *windowHeight);
    } else { s.windowWidth.reset(); s.windowHeight.reset(); }
    return s;
}
Settings Settings::fromJson(const QByteArray &json) {
    QJsonParseError error;
    const auto doc = QJsonDocument::fromJson(json, &error);
    if (error.error != QJsonParseError::NoError || !doc.isObject()) invalid(QStringLiteral("設定 JSON 損壞或不是物件"));
    return fromJson(doc.object());
}
Settings Settings::fromJson(const QJsonObject &json) {
    Settings s;
    const auto path = field(json, "lastFolderPath");
    if (!path.isUndefined() && !path.isNull()) {
        if (!path.isString()) invalid(QStringLiteral("lastFolderPath 必須是字串或 null"));
        s.lastFolderPath = path.toString();
    }
    const auto sortValue = field(json, "sort");
    if (!sortValue.isUndefined() && !sortValue.isNull()) {
        if (!sortValue.isObject()) invalid(QStringLiteral("sort 必須是物件"));
        const auto o = sortValue.toObject();
        s.sortKey = integer(field(o, "key"), 0) == 1 ? 1 : 0;
        s.sortDirection = integer(field(o, "direction"), 0) == 1 ? 1 : 0;
    }
    const auto size = integer(field(json, "thumbnailSize"), 160);
    s.thumbnailSize = size == 0 ? 160 : static_cast<int>(std::clamp<qint64>(size, 120, 240));
    s.includeSubfolders = boolean(field(json, "includeSubfolders"), false);
    s.sidebarCollapsed = boolean(field(json, "sidebarCollapsed"), false);
    for (const auto &name : {QStringLiteral("windowWidth"), QStringLiteral("windowHeight")}) {
        const auto v = field(json, name);
        if (!v.isUndefined() && !v.isNull()) {
            const auto n = integer(v, 0);
            if (n < 0 || n > std::numeric_limits<quint32>::max()) invalid(QStringLiteral("視窗尺寸超出範圍"));
            (name == "windowWidth" ? s.windowWidth : s.windowHeight) = n;
        }
    }
    return s.normalize();
}
QJsonObject Settings::toJson() const {
    const auto s = normalize();
    return {{"lastFolderPath", s.lastFolderPath.isNull() ? QJsonValue(QJsonValue::Null) : QJsonValue(s.lastFolderPath)},
            {"sort", QJsonObject{{"key", s.sortKey}, {"direction", s.sortDirection}}},
            {"includeSubfolders", s.includeSubfolders}, {"thumbnailSize", s.thumbnailSize},
            {"sidebarCollapsed", s.sidebarCollapsed},
            {"windowWidth", s.windowWidth ? QJsonValue(*s.windowWidth) : QJsonValue(QJsonValue::Null)},
            {"windowHeight", s.windowHeight ? QJsonValue(*s.windowHeight) : QJsonValue(QJsonValue::Null)}};
}
bool supportedImage(const QString &path) {
    static const QSet<QString> extensions{"jpg", "jpeg", "png", "bmp", "webp", "gif"};
    return extensions.contains(QFileInfo(path).suffix().toLower());
}
int naturalCompare(const QString &a, const QString &b) {
    qsizetype i = 0, j = 0;
    const auto digit = [](QChar c) { return c >= QLatin1Char('0') && c <= QLatin1Char('9'); };
    while (i < a.size() && j < b.size()) {
        if (digit(a[i]) && digit(b[j])) {
            const auto ai = i, bj = j;
            while (i < a.size() && digit(a[i])) ++i;
            while (j < b.size() && digit(b[j])) ++j;
            auto sa = ai, sb = bj;
            while (sa + 1 < i && a[sa] == QLatin1Char('0')) ++sa;
            while (sb + 1 < j && b[sb] == QLatin1Char('0')) ++sb;
            if (i - sa != j - sb) return i - sa < j - sb ? -1 : 1;
            const int c = QStringView(a).mid(sa, i - sa).compare(QStringView(b).mid(sb, j - sb));
            if (c) return c < 0 ? -1 : 1;
            if (i - ai != j - bj) return i - ai > j - bj ? -1 : 1;
        } else {
            if (a[i].toUpper() != b[j].toUpper()) return a[i].toUpper() < b[j].toUpper() ? -1 : 1;
            if (a[i] != b[j]) return a[i] < b[j] ? -1 : 1;
            ++i; ++j;
        }
    }
    return i == a.size() ? (j == b.size() ? 0 : -1) : 1;
}
QList<Entry> sortEntries(QList<Entry> entries, SortSettings sort, bool foldersFirst) {
    std::stable_sort(entries.begin(), entries.end(), [=](const Entry &a, const Entry &b) {
        if (foldersFirst && a.folder != b.folder) return a.folder;
        const int c = sort.key == 1 ? (a.modified < b.modified ? -1 : a.modified > b.modified ? 1 : 0)
                                    : naturalCompare(a.name, b.name);
        return sort.direction == 1 ? c > 0 : c < 0;
    });
    return entries;
}
QList<Entry> projectEntries(const QList<Entry> &entries, const QString &query) {
    QList<Entry> result;
    const auto term = query.trimmed();
    for (const auto &e : entries)
        if (e.name.contains(term, Qt::CaseInsensitive) || e.path.contains(term, Qt::CaseInsensitive)) result.append(e);
    return result;
}
QList<Entry> project(const QList<Entry> &entries, const QString &query, const Settings &settings) {
    return projectEntries(sortEntries(entries, {settings.sortKey, settings.sortDirection}, !settings.includeSubfolders), query);
}
QStringList imagePaths(const QList<Entry> &entries) {
    QStringList result;
    for (const auto &e : entries) if (!e.folder) result.append(e.path);
    return result;
}
void Selection::clear() { ordered_.clear(); anchor_.clear(); }
void Selection::retainVisible(const QStringList &visible) {
    const QSet<QString> paths(visible.begin(), visible.end());
    ordered_.removeIf([&](const QString &p) { return !paths.contains(p); });
    if (!paths.contains(anchor_)) anchor_.clear();
}
void Selection::select(const QString &path, const QStringList &visible, bool control, bool shift) {
    retainVisible(visible);
    const auto b = visible.indexOf(path), a = visible.indexOf(anchor_);
    if (b < 0) return;
    if (shift && a >= 0) {
        if (!control) ordered_.clear();
        for (auto i = std::min(a, b); i <= std::max(a, b); ++i)
            if (!contains(visible[i])) ordered_.append(visible[i]);
    } else {
        if (!control) ordered_.clear();
        if (contains(path)) ordered_.removeAll(path); else ordered_.append(path);
        anchor_ = path;
    }
}
void Selection::contextSelect(const QString &path, const QStringList &visible) {
    retainVisible(visible);
    if (visible.contains(path) && !contains(path)) select(path, visible, false, false);
}
FileStamp FileStamp::capture(const QString &path) {
    const QFileInfo f(path);
    if (!f.isFile() || f.isSymbolicLink()) return {};
    return {f.size(), f.lastModified().toMSecsSinceEpoch()};
}
qsizetype BatchResult::succeeded() const { return count(*this, ResultStatus::Succeeded); }
qsizetype BatchResult::skipped() const { return count(*this, ResultStatus::Skipped); }
qsizetype BatchResult::canceled() const { return count(*this, ResultStatus::Canceled); }
qsizetype BatchResult::unknown() const { return count(*this, ResultStatus::Unknown); }
qsizetype BatchResult::failed() const { return count(*this, ResultStatus::Failed) + unknown(); }
QString BatchResult::summary() const {
    return QStringLiteral("共 %1 張 · 成功 %2 · 略過 %3 · 取消 %4 · 失敗 %5（不確定 %6）")
        .arg(total()).arg(succeeded()).arg(skipped()).arg(canceled()).arg(failed()).arg(unknown());
}
QString FilePlans::validateRename(const QString &source, const QString &basename) {
    if (basename.trimmed().isEmpty() || basename == "." || basename == ".." || basename.contains('/') || basename.contains(QChar(0)))
        invalid(QStringLiteral("檔名不可為空、.、..，或包含 / 與 NUL"));
    const QFileInfo f(absolute(source));
    const auto suffix = f.suffix().isEmpty() ? QString() : "." + f.suffix();
    if ((basename + suffix).toUtf8().size() > 255) invalid(QStringLiteral("檔名超過 255 bytes"));
    return f.dir().absoluteFilePath(basename + suffix);
}
FilePlan FilePlans::rename(const QString &source, const QString &basename) {
    const auto src = absolute(source), dst = validateRename(src, basename);
    if (src != dst && exists(dst)) invalid(QStringLiteral("目標已存在，不能覆寫"));
    return {src, dst, OperationKind::Rename, FileStamp::capture(src), src == dst ? QStringLiteral("檔名未變更") : QString()};
}
QList<FilePlan> FilePlans::convert(const QList<Entry> &visible, OperationKind kind) {
    if (kind != OperationKind::Jpeg && kind != OperationKind::Webp) invalid(QStringLiteral("不是轉換操作"));
    QList<FilePlan> plans;
    for (const auto &e : visible) {
        if (e.folder) continue;
        const auto src = absolute(e.path), ext = extension(e);
        QString skip;
        if (e.animated) skip = QStringLiteral("動畫圖片不支援轉換");
        else if (kind == OperationKind::Webp && kept(e)) skip = QStringLiteral("保留既有 JPG／WebP");
        else if (kind == OperationKind::Jpeg && (ext == "jpg" || ext == "jpeg")) skip = QStringLiteral("已是 JPG");
        const auto target = stemPath(src) + (kind == OperationKind::Jpeg ? ".jpg" : ".webp");
        if (skip.isEmpty() && exists(target)) skip = QStringLiteral("目標已存在，未覆寫");
        plans.append({src, target, kind, FileStamp::capture(src), skip});
    }
    return plans;
}
QList<FilePlan> FilePlans::cleanup(const QList<Entry> &visible) {
    QSet<QString> keep;
    for (const auto &e : visible) if (!e.folder && kept(e)) keep.insert(stemPath(absolute(e.path)));
    QStringList sources;
    for (const auto &e : visible)
        if (!e.folder && !kept(e) && keep.contains(stemPath(absolute(e.path)))) sources.append(e.path);
    return trash(sources);
}
QList<FilePlan> FilePlans::trash(const QStringList &sources) {
    QList<FilePlan> result;
    QSet<QString> seen;
    for (const auto &s : sources) {
        const auto p = absolute(s);
        if (seen.contains(p)) continue;
        seen.insert(p);
        result.append({p, {}, OperationKind::Trash, FileStamp::capture(p), {}});
    }
    return result;
}
QList<FilePlan> FilePlans::dropRename(const QStringList &sources, const QString &target, const QStringList &existing) {
    const auto dst = absolute(target);
    const QFileInfo f(dst);
    QStringList occupied;
    for (const auto &p : existing)
        occupied.append(absolute(QDir::isAbsolutePath(p) ? p : f.dir().absoluteFilePath(p)));
    occupied.append(dst);
    QSet<QString> seen;
    QList<FilePlan> plans;
    // 每張從 1 找起，填補最小空號。不同副檔名共用占用空間。
    for (const auto &s : sources) {
        const auto src = absolute(s);
        if (src == dst || seen.contains(src)) continue;
        seen.insert(src);
        QString candidate;
        for (quint64 n = 1; ; ++n) {
            const auto name = f.completeBaseName() + QStringLiteral("-%1").arg(n, 2, 10, QLatin1Char('0'));
            candidate = validateRename(f.dir().absoluteFilePath(QFileInfo(src).fileName()), name);
            const auto stem = stemPath(candidate);
            if (std::none_of(occupied.begin(), occupied.end(), [&](const QString &p) { return p != src && stemPath(p) == stem; })) break;
        }
        plans.append({src, candidate, OperationKind::Rename, FileStamp::capture(src),
                      src == candidate ? QStringLiteral("已是目標名稱") : QString(), true});
        occupied.append(candidate);
    }
    return plans;
}
} // namespace piclens
