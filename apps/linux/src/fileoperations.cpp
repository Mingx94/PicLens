#include "fileoperations.h"

#include <QDir>
#include <QElapsedTimer>
#include <QFile>
#include <QFileInfo>
#include <QProcess>
#include <QTemporaryFile>
#include <QtLogging>
#include <algorithm>
#include <cerrno>
#include <cstring>
#include <exception>
#ifdef Q_OS_LINUX
#include <fcntl.h>
#include <linux/fs.h>
#include <sys/syscall.h>
#include <unistd.h>
#endif

namespace piclens {
namespace {
bool exists(const QString &path) { const QFileInfo f(path); return f.exists() || f.isSymbolicLink(); }
bool hasSymlink(QString path) {
    path = QFileInfo(path).absoluteFilePath();
    while (true) {
        if (QFileInfo(path).isSymbolicLink()) return true;
        const auto parent = QFileInfo(path).dir().absolutePath();
        if (parent == path) return false;
        path = parent;
    }
}
FileResult result(const FilePlan &p, ResultStatus status, const QString &reason) {
    return {p.source, p.target, status, reason};
}
FileResult moveNoReplace(const FilePlan &p, const QString &source) {
#ifdef Q_OS_LINUX
    // 不使用 QFile::rename 的跨裝置複製 fallback，也不使用會覆寫的 POSIX rename。
    const auto from = QFile::encodeName(source), to = QFile::encodeName(p.target);
    if (::syscall(SYS_renameat2, AT_FDCWD, from.constData(), AT_FDCWD, to.constData(), RENAME_NOREPLACE) == 0)
        return result(p, ResultStatus::Succeeded, QStringLiteral("完成"));
    const int error = errno;
    if (error == EEXIST || error == ENOTEMPTY)
        return result(p, ResultStatus::Skipped, QStringLiteral("目標已存在，未覆寫"));
    return result(p, ResultStatus::Failed, QStringLiteral("不可覆寫改名失敗：%1").arg(QString::fromLocal8Bit(std::strerror(error))));
#else
    Q_UNUSED(source);
    return result(p, ResultStatus::Failed, QStringLiteral("此檔案後端僅支援 Linux renameat2"));
#endif
}
FileResult trash(const FilePlan &p, std::stop_token stop, const FileOperationOptions &options) {
    QProcess process;
    process.setProgram(options.gioProgram);
    process.setArguments({QStringLiteral("trash"), QStringLiteral("--"), p.source});
    process.setProcessChannelMode(QProcess::SeparateChannels);
    QElapsedTimer timer;
    timer.start();
    process.start();
    // QProcess 本身在工作執行緒建立，不依賴 UI event loop。
    while (process.state() == QProcess::Starting && !stop.stop_requested() && timer.elapsed() < options.trashTimeoutMs)
        process.waitForStarted(25);
    if (process.state() == QProcess::NotRunning && process.error() == QProcess::FailedToStart)
        return result(p, ResultStatus::Failed, QStringLiteral("無法啟動 gio trash：%1").arg(process.errorString()));
    QByteArray error;
    while (process.state() != QProcess::NotRunning) {
        // 持續排空，診斷只保留尾端 8 KiB，避免 helper 輸出無界成長。
        process.readAllStandardOutput();
        error = (error + process.readAllStandardError()).right(8192);
        if (stop.stop_requested() || timer.elapsed() >= options.trashTimeoutMs) {
            process.kill();
            // kill 後同步 reap；不能在 child 尚存活時交付完成。
            process.waitForFinished(-1);
            return result(p, ResultStatus::Unknown, stop.stop_requested()
                ? QStringLiteral("回收程序已取消，請確認來源是否仍存在")
                : QStringLiteral("回收程序逾時，請確認來源是否仍存在"));
        }
        process.waitForFinished(25);
    }
    error = (error + process.readAllStandardError()).right(8192);
    if (process.exitStatus() == QProcess::NormalExit && process.exitCode() == 0)
        return result(p, ResultStatus::Succeeded, QStringLiteral("已移至回收筒"));
    // 啟動後失敗仍可能已移動檔案，保守回報 Unknown，不重試、不永久刪除。
    return result(p, ResultStatus::Unknown, QStringLiteral("回收程序未正常完成，請確認來源：%1").arg(QString::fromUtf8(error)));
}
FileResult executeOne(const FilePlan &p, std::stop_token stop, const OperationHooks &hooks,
                      const FileOperationOptions &options) {
    if (stop.stop_requested()) return result(p, ResultStatus::Canceled, QStringLiteral("尚未執行"));
    if (!p.skip.isEmpty()) return result(p, ResultStatus::Skipped, p.skip);
    if (!QDir::isAbsolutePath(p.source) || p.source.contains(QChar(0)) ||
        (p.kind != OperationKind::Trash && (!QDir::isAbsolutePath(p.target) || p.target.contains(QChar(0)))))
        return result(p, ResultStatus::Failed, QStringLiteral("計畫必須使用有效絕對路徑"));
    if (hasSymlink(p.source) || (!p.target.isEmpty() && hasSymlink(p.target)))
        return result(p, ResultStatus::Failed, QStringLiteral("不修改符號連結或其目錄內的檔案"));
    if (p.stamp.size < 0 || FileStamp::capture(p.source) != p.stamp)
        return result(p, ResultStatus::Failed, QStringLiteral("來源不存在或已變更，請重新確認"));
    if (p.kind != OperationKind::Trash) {
        if (p.source == p.target) return result(p, ResultStatus::Skipped, QStringLiteral("檔名未變更"));
        if (exists(p.target)) return result(p, ResultStatus::Skipped, QStringLiteral("目標已存在，未覆寫"));
        if (p.checkStem) {
            const QFileInfo target(p.target);
            const auto entries = target.dir().entryInfoList(QDir::AllEntries | QDir::Hidden | QDir::System | QDir::NoDotAndDotDot);
            for (const auto &e : entries)
                if (e.absoluteFilePath() != p.source && e.completeBaseName() == target.completeBaseName())
                    return result(p, ResultStatus::Skipped, QStringLiteral("目標序號已被其他副檔名占用"));
        }
    }
    if (stop.stop_requested()) return result(p, ResultStatus::Canceled, QStringLiteral("尚未執行"));
    if (p.kind == OperationKind::Rename) return moveNoReplace(p, p.source);
    if (p.kind == OperationKind::Trash) return trash(p, stop, options);
    if (p.kind != OperationKind::Jpeg && p.kind != OperationKind::Webp)
        return result(p, ResultStatus::Failed, QStringLiteral("未知的檔案操作"));
    if (!hooks.encode) return result(p, ResultStatus::Failed, QStringLiteral("尚未提供外部編碼 worker"));
    QTemporaryFile temporary(QFileInfo(p.target).dir().absoluteFilePath(QStringLiteral(".piclens-XXXXXX.tmp")));
    if (!temporary.open()) return result(p, ResultStatus::Failed, temporary.errorString());
    const auto tempPath = temporary.fileName();
    temporary.close();
    const auto encoded = hooks.encode(p.source, tempPath, p.kind, stop);
    if (stop.stop_requested()) return result(p, ResultStatus::Canceled, QStringLiteral("已取消，未提交輸出"));
    if (!encoded.succeeded) return result(p, ResultStatus::Failed, encoded.error);
    if (hasSymlink(p.source) || hasSymlink(tempPath) || hasSymlink(QFileInfo(p.target).absolutePath()) ||
        FileStamp::capture(p.source) != p.stamp)
        return result(p, ResultStatus::Failed, QStringLiteral("轉換期間來源或路徑已變更，未提交輸出"));
    if (!QFileInfo(tempPath).isFile() || QFileInfo(tempPath).size() <= 0)
        return result(p, ResultStatus::Failed, QStringLiteral("編碼 worker 未產生有效輸出"));
    if (stop.stop_requested()) return result(p, ResultStatus::Canceled, QStringLiteral("已取消，未提交輸出"));
    return moveNoReplace(p, tempPath);
    // QTemporaryFile 在失敗、例外與取消時只清除本次輸出；原檔不刪除。
}
}
FileOperations::FileOperations(FileOperationOptions options) : options_(std::move(options)) {
    options_.trashTimeoutMs = std::clamp(options_.trashTimeoutMs, 1, 300000);
}
BatchResult FileOperations::execute(const QList<FilePlan> &plans, std::stop_token stop, const OperationHooks &hooks) const {
    BatchResult batch;
    batch.items.reserve(plans.size());
    for (const auto &plan : plans) {
        FileResult r;
        try {
            if (hooks.progress && !stop.stop_requested()) hooks.progress(batch.total(), plans.size(), plan);
            r = executeOne(plan, stop, hooks, options_);
        } catch (const std::exception &e) {
            r = result(plan, ResultStatus::Failed, QString::fromUtf8(e.what()));
        } catch (...) { r = result(plan, ResultStatus::Failed, QStringLiteral("檔案操作發生未知例外")); }
        batch.items.append(r);
        // 日誌 callback 失敗不得改變已完成結果，也不能中斷批次。
        qInfo().noquote() << "file-operation" << static_cast<int>(r.status) << "source=" << r.source
                         << "target=" << r.target << "reason=" << r.message;
        try { if (hooks.log) hooks.log(r); }
        catch (...) { qWarning() << "檔案操作日誌 callback 失敗"; }
    }
    return batch;
}
} // namespace piclens
