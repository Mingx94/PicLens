#include "domain.h"
#include "fileoperations.h"
#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QJsonArray>
#include <QJsonDocument>
#include <QTemporaryDir>
#include <QTextStream>
#include <algorithm>
#include <stdexcept>
#include <thread>

using namespace piclens;
namespace {
void check(bool ok, const char *message) { if (!ok) throw std::runtime_error(message); }
template<class F> void rejects(F f) {
    bool rejected = false;
    try { f(); } catch (const std::invalid_argument &) { rejected = true; }
    check(rejected, "invalid request was accepted");
}
QString write(const QTemporaryDir &dir, const QString &name, const QByteArray &data = "fixture") {
    const auto path = dir.filePath(name);
    QFile f(path);
    check(f.open(QIODevice::WriteOnly), "create fixture");
    check(f.write(data) == data.size(), "write fixture");
    return path;
}
QByteArray read(const QString &path) { QFile f(path); check(f.open(QIODevice::ReadOnly), "read fixture"); return f.readAll(); }
Entry entry(const QString &path, bool animated = false) {
    const QFileInfo f(path);
    const auto stamp = FileStamp::capture(path);
    return {path, f.fileName(), f.suffix(), false, animated, stamp.modified, stamp.size};
}
void sharedCases(const QString &path) {
    const auto cases = QJsonDocument::fromJson(read(path)).object();
    check(!cases.isEmpty(), "shared fixtures missing");
    for (const auto &value : cases.value("settings").toArray()) {
        const auto row = value.toObject();
        check(Settings::fromJson(row.value("input").toObject()).thumbnailSize == row.value("expectedSize").toInt(), "DATA-01 shared settings");
    }
    for (const auto &value : cases.value("naturalSort").toArray()) {
        const auto row = value.toObject();
        QStringList input, expected;
        for (const auto &v : row.value("input").toArray()) input.append(v.toString());
        for (const auto &v : row.value("expected").toArray()) expected.append(v.toString());
        std::stable_sort(input.begin(), input.end(), [](const auto &a, const auto &b) { return naturalCompare(a, b) < 0; });
        check(input == expected, "SORT-01 shared natural sort");
    }
}
void domainCases() {
    const auto s = Settings::fromJson(QByteArray(R"({"sort":{"key":1,"direction":99},"thumbnailSize":170,"windowWidth":600,"windowHeight":400,"unknown":true})"));
    check(s.sortKey == 1 && s.sortDirection == 0 && s.thumbnailSize == 180 && s.windowWidth == 800 && s.windowHeight == 600, "DATA-01 normalize");
    check(Settings::fromJson(s.toJson()).toJson() == s.toJson(), "DATA-01 roundtrip");
    check(!Settings::fromJson(QJsonObject{{"windowWidth", 900}}).windowWidth, "DATA-01 incomplete window dimensions");
    rejects([] { Settings::fromJson(QByteArray("{bad")); });
    rejects([] { Settings::fromJson(QByteArray(R"({"thumbnailSize":"170"})")); });
    rejects([] { Settings::fromJson(QByteArray(R"({"includeSubfolders":1})")); });
    check(naturalCompare("x99999999999999999999", "x100000000000000000000") < 0, "natural sort overflow");
    const QList<Entry> entries{{"/pics/a.jpg", "a.jpg", "jpg", false, false, 1, 1}, {"/pics/z", "z", "", true},
                              {"/pics/b.png", "b.png", "png", false, false, 1, 1}};
    Settings settings; settings.sortDirection = 1;
    check(project(entries, "", settings).first().folder, "SORT-01 descending folders first");
    check(project(entries, " A.JPG ", settings).size() == 1, "SEARCH-01 projection");
    check(sortEntries(entries, {1, 0}, false)[0].folder, "SORT-01 date sorting");
    check(sortEntries({entries[0], entries[2]}, {1, 1}, false)[0].path == entries[0].path, "SORT-01 stable equal values");
    check(imagePaths(entries).size() == 2, "SELECT-01 exclude folders");
    Selection selection;
    const QStringList visible{"a", "b", "c", "d"};
    selection.select("c", visible, false, false);
    selection.select("a", visible, true, false);
    check(selection.ordered() == QStringList{"c", "a"}, "SELECT-01 selection order");
    selection.select("d", visible, false, true);
    check(selection.ordered() == visible && selection.anchor() == "a", "SELECT-01 range anchor");
    selection.select("b", visible, false, true);
    check(selection.ordered() == QStringList{"a", "b"}, "SELECT-01 repeated shift");
    selection.select("d", visible, true, true);
    selection.select("b", visible, true, false);
    check(selection.ordered() == QStringList{"a", "c", "d"}, "SELECT-01 ctrl toggle");
    selection.contextSelect("c", visible);
    check(selection.ordered().first() == "a", "MENU-01 preserves selected order");
    selection.contextSelect("b", visible);
    check(selection.ordered() == QStringList{"b"}, "MENU-01 unselected target");
    selection.retainVisible({"a"});
    check(selection.ordered().isEmpty() && selection.anchor().isEmpty(), "SELECT-01 stale projection");
    check(!FilePlans::requiresConversionConfirmation(49) && FilePlans::requiresConversionConfirmation(50), "FILE-01 confirmation boundary");
}
void fileCases() {
    QTemporaryDir dir;
    check(dir.isValid(), "temporary fixture directory");
    const auto png = write(dir, "a.png"), jpg = write(dir, "a.jpg"), webp = write(dir, "a.webp"), gif = write(dir, "b.gif");
    const QList<Entry> entries{entry(png), entry(jpg), entry(webp), entry(gif, true)};
    const auto conversion = FilePlans::convert(entries, OperationKind::Webp);
    check(std::count_if(conversion.begin(), conversion.end(), [](const auto &p) { return !p.skip.isEmpty(); }) == 4, "FILE-01 skips and existing target");
    const auto cleanup = FilePlans::cleanup(entries);
    check(cleanup.size() == 1 && cleanup[0].source == png, "FILE-02 protected formats");
    check(FilePlans::cleanup({entry(png)}).isEmpty(), "FILE-02 visible scope only");
    rejects([&] { FilePlans::validateRename(png, "bad/name"); });
    rejects([&] { FilePlans::validateRename(png, ".."); });
    rejects([&] { FilePlans::validateRename(png, QString(256, 'x')); });
    check(FilePlans::validateRename(png, "CON").endsWith("CON.png"), "Arch allows Windows reserved names");
    check(!FilePlans::rename(png, "a").skip.isEmpty(), "FILE-03 same name skip");
    const auto other = write(dir, "other.png", "keep");
    rejects([&] { FilePlans::rename(png, "other"); });
    const auto base = write(dir, "base.png");
    const auto occupied = write(dir, "base-01.webp");
    const auto drop = FilePlans::dropRename({jpg, jpg}, base, {png, jpg, base, occupied});
    check(drop.size() == 1 && drop[0].target.endsWith("base-02.jpg") && drop[0].checkStem, "DRAG-01 cross-extension reservation");
    const auto names = QDir(dir.path()).entryList(QDir::AllEntries | QDir::NoDotAndDotDot | QDir::Hidden | QDir::System);
    const auto namesDrop = FilePlans::dropRename({jpg}, base, names);
    check(namesDrop.size() == 1 && namesDrop[0].target == drop[0].target, "DRAG-01 entryList names resolve relative to target directory");
    write(dir, "base-02.bmp");
    check(FileOperations().execute(drop).skipped() == 1 && QFile::exists(jpg), "DRAG-02 recheck stem after preview");
    auto race = FilePlans::rename(png, "race");
    write(dir, "race.png", "do-not-overwrite");
    FileOperations ops;
    check(ops.execute({race}).skipped() == 1 && read(race.target) == "do-not-overwrite" && QFile::exists(png), "FILE-03 preview collision");
    std::stop_source canceled;
    canceled.request_stop();
    check(ops.execute(FilePlans::trash({png, jpg}), canceled.get_token()).canceled() == 2 && QFile::exists(png), "FILE-02 canceled zero mutation");
    check(FileOperations({dir.filePath("absent-gio"), 100}).execute(FilePlans::trash({png})).failed() == 1 && QFile::exists(png), "FILE-02 helper failure never deletes");
    auto changed = FilePlans::rename(other, "new");
    write(dir, "other.png", "changed-size");
    check(ops.execute({changed}).failed() == 1, "FILE-03 changed source");
    const auto src = write(dir, "convert.png");
    const auto convert = FilePlans::convert({entry(src)}, OperationKind::Jpeg);
    OperationHooks hooks;
    hooks.encode = [](const QString &, const QString &temp, OperationKind, std::stop_token) {
        QFile f(temp); if (!f.open(QIODevice::WriteOnly)) return EncodeResult{false, "write failed"};
        f.write("encoded"); return EncodeResult{true, {}};
    };
    OperationHooks collision = hooks;
    collision.encode = [&](const QString &s, const QString &temp, OperationKind kind, std::stop_token stop) {
        const auto r = hooks.encode(s, temp, kind, stop);
        write(dir, "convert.jpg", "new-target"); return r;
    };
#ifdef Q_OS_LINUX
    check(ops.execute(convert, {}, collision).skipped() == 1 && read(convert[0].target) == "new-target", "FILE-01 atomic commit collision");
    const auto okSource = write(dir, "success.png");
    check(ops.execute({FilePlans::rename(okSource, "renamed")}).succeeded() == 1 && !QFile::exists(okSource), "FILE-03 Linux rename");
    const auto first = write(dir, "first.png"), second = write(dir, "second.png");
    std::stop_source afterFirst;
    OperationHooks cancelAfterFirst;
    cancelAfterFirst.log = [&](const FileResult &r) { if (r.status == ResultStatus::Succeeded) afterFirst.request_stop(); };
    const auto partialCancel = ops.execute({FilePlans::rename(first, "first-done"), FilePlans::rename(second, "second-done")}, afterFirst.get_token(), cancelAfterFirst);
    check(partialCancel.succeeded() == 1 && partialCancel.canceled() == 1 && QFile::exists(second), "RESULT-01 partial completion remains completed");
    const auto fresh = write(dir, "fresh.png");
    check(ops.execute(FilePlans::convert({entry(fresh)}, OperationKind::Jpeg), {}, hooks).succeeded() == 1 && QFile::exists(fresh), "FILE-01 source retained");
    const auto helper = write(dir, "stall", "#!/bin/sh\nexec sleep 10\n");
    QFile::setPermissions(helper, QFileDevice::ReadOwner | QFileDevice::WriteOwner | QFileDevice::ExeOwner);
    check(FileOperations({helper, 50}).execute(FilePlans::trash({png})).unknown() == 1 && QFile::exists(png), "FILE-02 trash timeout unknown");
    std::stop_source midCancel;
    std::jthread canceler([&] { std::this_thread::sleep_for(std::chrono::milliseconds(75)); midCancel.request_stop(); });
    check(FileOperations({helper, 1000}).execute(FilePlans::trash({png}), midCancel.get_token()).unknown() == 1, "FILE-02 active trash cancel unknown");
#endif
    std::stop_source encodingCancel;
    hooks.encode = [&](const QString &, const QString &temp, OperationKind, std::stop_token) {
        QFile f(temp); check(f.open(QIODevice::WriteOnly), "open canceled output"); f.write("partial");
        encodingCancel.request_stop(); return EncodeResult{true, {}};
    };
    const auto cancelSrc = write(dir, "cancel.png");
    const auto canceledPlans = FilePlans::convert({entry(cancelSrc)}, OperationKind::Jpeg);
    check(ops.execute(canceledPlans, encodingCancel.get_token(), hooks).canceled() == 1 && !QFile::exists(canceledPlans[0].target), "FILE-01 canceled encoder has no output");
    check(QDir(dir.path()).entryList({".piclens-*"}, QDir::Files | QDir::Hidden).isEmpty(), "FILE-01 temporary cleanup");
    const auto missing = dir.filePath("missing.png");
    auto skipped = FilePlans::rename(png, "a");
    const auto partial = ops.execute({FilePlan{missing, {}, OperationKind::Trash, {}, {}}, skipped});
    check(partial.total() == 2 && partial.failed() == 1 && partial.skipped() == 1, "RESULT-01 continue after failure");
}
}
int main(int argc, char **argv) {
    QCoreApplication app(argc, argv);
    try {
        const auto fixture = argc > 1 ? QString::fromLocal8Bit(argv[1])
            : QDir(QFileInfo(QString::fromUtf8(__FILE__)).absolutePath()).absoluteFilePath("../../../test-data/windows-native-cases.json");
        sharedCases(fixture); domainCases(); fileCases();
        QTextStream(stdout) << "Domain and file operation tests passed\n";
        return 0;
    } catch (const std::exception &e) {
        QTextStream(stderr) << e.what() << '\n'; return 1;
    }
}
