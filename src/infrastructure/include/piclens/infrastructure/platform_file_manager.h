#pragma once

#include <QString>
#include <QStringList>

#include <functional>

namespace piclens::infrastructure {

struct ProcessLaunchRequest {
    QString program;
    QStringList arguments;
};

class PlatformFileManager final
{
public:
    using ProcessLauncher = std::function<bool(const ProcessLaunchRequest &)>;

    explicit PlatformFileManager(ProcessLauncher launcher = {});

    void moveToTrash(const QString &path) const;
    void reveal(const QString &path) const;

    [[nodiscard]] static ProcessLaunchRequest revealRequest(const QString &path);

private:
    ProcessLauncher m_launcher;
};

} // namespace piclens::infrastructure
