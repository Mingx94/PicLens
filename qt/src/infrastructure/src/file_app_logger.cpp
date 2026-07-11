#include <piclens/infrastructure/file_app_logger.h>

#include <piclens/infrastructure/app_data_paths.h>

#include <QDir>
#include <QFile>
#include <QFileInfo>

#include <typeinfo>

namespace piclens::infrastructure {

FileAppLogger::FileAppLogger(QString logPath, NowProvider now)
    : m_logPath(std::move(logPath))
    , m_now(now ? std::move(now) : [] { return QDateTime::currentDateTime(); })
    , m_worker([this](std::stop_token stopToken) { processQueue(stopToken); })
{
}

FileAppLogger::~FileAppLogger()
{
    {
        const std::scoped_lock lock(m_mutex);
        m_stopping = true;
    }
    m_condition.notify_all();
    if (m_worker.joinable()) {
        m_worker.join();
    }
}

QString FileAppLogger::defaultLogPath()
{
    return app_data_paths::logPath();
}

const QString &FileAppLogger::logPath() const
{
    return m_logPath;
}

void FileAppLogger::info(const QString &message) noexcept
{
    write(QStringLiteral("INFO"), message, {});
}

void FileAppLogger::error(const std::exception &exception, const QString &message) noexcept
{
    const QString details = QStringLiteral("%1: %2")
                                .arg(
                                    QString::fromLatin1(typeid(exception).name()),
                                    QString::fromUtf8(exception.what()));
    write(QStringLiteral("ERROR"), message, details);
}

void FileAppLogger::error(const QString &details, const QString &message) noexcept
{
    write(QStringLiteral("ERROR"), message, details);
}

void FileAppLogger::write(
    QString level,
    const QString &message,
    const QString &details) noexcept
{
    try {
        QString entry = QStringLiteral("%1 [%2] %3\n")
                            .arg(m_now().toString(Qt::ISODateWithMs), level, message);
        if (!details.isEmpty()) {
            entry.append(details).append(QLatin1Char('\n'));
        }
        enqueue(std::move(entry));
    } catch (...) {
    }
}

void FileAppLogger::enqueue(QString entry) noexcept
{
    try {
        {
            const std::scoped_lock lock(m_mutex);
            if (m_stopping) {
                return;
            }
            if (m_queue.size() >= MaxQueuedLogMessages) {
                m_queue.pop_front();
            }
            m_queue.push_back(std::move(entry));
        }
        m_condition.notify_one();
    } catch (...) {
    }
}

void FileAppLogger::processQueue(std::stop_token stopToken) noexcept
{
    try {
        while (true) {
            std::deque<QString> entries;
            {
                std::unique_lock lock(m_mutex);
                m_condition.wait(lock, stopToken, [this] {
                    return m_stopping || !m_queue.empty();
                });
                entries.swap(m_queue);
                if (entries.empty() && (m_stopping || stopToken.stop_requested())) {
                    return;
                }
            }
            appendBatch(entries);
        }
    } catch (...) {
    }
}

void FileAppLogger::appendBatch(const std::deque<QString> &entries) noexcept
{
    if (entries.empty()) {
        return;
    }
    try {
        const QFileInfo info(m_logPath);
        if (!QDir().mkpath(info.absolutePath())) {
            return;
        }
        QFile file(m_logPath);
        if (!file.open(QIODevice::WriteOnly | QIODevice::Append)) {
            return;
        }
        for (const QString &entry : entries) {
            file.write(entry.toUtf8());
        }
        file.flush();
    } catch (...) {
    }
}

} // namespace piclens::infrastructure
