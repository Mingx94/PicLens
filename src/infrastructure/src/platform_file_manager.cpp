#include <piclens/infrastructure/platform_file_manager.h>

#include <QDir>
#include <QFileInfo>
#include <QProcess>

#include <stdexcept>
#include <utility>

#ifdef Q_OS_WIN
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <shellapi.h>
#endif

namespace piclens::infrastructure {
namespace {

bool launchDetached(const ProcessLaunchRequest &request)
{
    return QProcess::startDetached(request.program, request.arguments);
}

} // namespace

PlatformFileManager::PlatformFileManager(ProcessLauncher launcher)
    : m_launcher(launcher ? std::move(launcher) : launchDetached)
{
}

void PlatformFileManager::moveToTrash(const QString &path) const
{
    const QFileInfo info(path);
    if (!info.exists()) {
        throw std::runtime_error("Source path does not exist.");
    }

#ifdef Q_OS_WIN
    std::wstring source = QDir::toNativeSeparators(info.absoluteFilePath()).toStdWString();
    source.push_back(L'\0');
    source.push_back(L'\0');

    SHFILEOPSTRUCTW operation{};
    operation.wFunc = FO_DELETE;
    operation.pFrom = source.c_str();
    operation.fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT;
    const int result = SHFileOperationW(&operation);
    if (result != 0 || operation.fAnyOperationsAborted) {
        throw std::runtime_error(
            QStringLiteral("Recycle Bin operation failed with code %1.").arg(result).toStdString());
    }
#elif defined(Q_OS_LINUX)
    QProcess process;
    process.setProgram(QStringLiteral("gio"));
    process.setArguments({QStringLiteral("trash"), info.absoluteFilePath()});
    process.setProcessChannelMode(QProcess::MergedChannels);
    process.start(QIODevice::ReadOnly);
    if (!process.waitForStarted() || !process.waitForFinished(-1)
        || process.exitStatus() != QProcess::NormalExit || process.exitCode() != 0) {
        throw std::runtime_error(
            QStringLiteral("gio trash failed: %1")
                .arg(QString::fromUtf8(process.readAll()))
                .toStdString());
    }
#else
    Q_UNUSED(path)
    throw std::runtime_error("Trash is only supported on Windows and Linux.");
#endif
}

ProcessLaunchRequest PlatformFileManager::revealRequest(const QString &path)
{
    const QFileInfo info(path);
    if (!info.exists() || !info.isFile()) {
        throw std::runtime_error("Reveal path must be an existing file.");
    }

#ifdef Q_OS_WIN
    return {
        .program = QStringLiteral("explorer.exe"),
        .arguments = {
            QStringLiteral("/select,"),
            QDir::toNativeSeparators(info.absoluteFilePath()),
        },
    };
#elif defined(Q_OS_LINUX)
    return {
        .program = QStringLiteral("xdg-open"),
        .arguments = {info.absolutePath()},
    };
#else
    throw std::runtime_error("Reveal is only supported on Windows and Linux.");
#endif
}

void PlatformFileManager::reveal(const QString &path) const
{
    const ProcessLaunchRequest request = revealRequest(path);
    if (!m_launcher(request)) {
        throw std::runtime_error("File manager could not be started.");
    }
}

} // namespace piclens::infrastructure
