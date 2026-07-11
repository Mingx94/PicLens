#pragma once

#include <QDateTime>
#include <QString>

#include <condition_variable>
#include <deque>
#include <exception>
#include <functional>
#include <mutex>
#include <thread>

namespace piclens::infrastructure {

class FileAppLogger final
{
public:
    using NowProvider = std::function<QDateTime()>;

    explicit FileAppLogger(QString logPath, NowProvider now = {});
    ~FileAppLogger();

    FileAppLogger(const FileAppLogger &) = delete;
    FileAppLogger &operator=(const FileAppLogger &) = delete;
    FileAppLogger(FileAppLogger &&) = delete;
    FileAppLogger &operator=(FileAppLogger &&) = delete;

    [[nodiscard]] static QString defaultLogPath();
    [[nodiscard]] const QString &logPath() const;

    void info(const QString &message) noexcept;
    void error(const std::exception &exception, const QString &message) noexcept;
    void error(const QString &details, const QString &message) noexcept;

private:
    static constexpr std::size_t MaxQueuedLogMessages = 5000;

    void write(QString level, const QString &message, const QString &details) noexcept;
    void enqueue(QString entry) noexcept;
    void processQueue(std::stop_token stopToken) noexcept;
    void appendBatch(const std::deque<QString> &entries) noexcept;

    QString m_logPath;
    NowProvider m_now;
    std::mutex m_mutex;
    std::condition_variable_any m_condition;
    std::deque<QString> m_queue;
    bool m_stopping = false;
    std::jthread m_worker;
};

} // namespace piclens::infrastructure
