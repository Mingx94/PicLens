#include "imaging.h"
#include <QCryptographicHash>
#include <QDataStream>
#include <QDateTime>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QPointer>
#include <QProcess>
#include <QRegularExpression>
#include <QTemporaryDir>
#include <QTimer>
#include <QUuid>
#include <algorithm>
#include <functional>
#include <utility>

namespace piclens {
namespace {
constexpr qint64 RecentLimit = 32LL * 1024 * 1024;
constexpr qint64 OriginalLimit = 256LL * 1024 * 1024;
constexpr int Interior = 2046;
struct Request { QString path, token; int edge, priority; quint64 serial; };
struct Identity {
    QString path;
    qint64 mtime = 0, size = -1;
    int edge = 0;
    bool operator==(const Identity &) const = default;
    QString key() const {
        QByteArray bytes;
        QDataStream stream(&bytes, QIODevice::WriteOnly);
        stream << path << mtime << size << edge;
        return QString::fromLatin1(QCryptographicHash::hash(bytes, QCryptographicHash::Sha256).toHex());
    }
};
Identity identify(const Request &r) {
    const QFileInfo info(r.path);
    return {info.absoluteFilePath(), info.lastModified().toMSecsSinceEpoch(), info.isFile() ? info.size() : -1, r.edge};
}
FramePtr readFrame(const QString &path, const Identity &identity, QString &error) {
    QFile file(path);
    if (!file.open(QIODevice::ReadOnly)) { error = file.errorString(); return {}; }
    QDataStream stream(&file); stream.setByteOrder(QDataStream::LittleEndian);
    quint32 magic; qint32 width, height, count;
    stream >> magic >> width >> height >> count;
    if (magic != 0x504C5431 || width <= 0 || height <= 0 ||
        qint64(width) * height > OriginalLimit / 4 ||
        (identity.edge > 0 && (width > identity.edge || height > identity.edge)) ||
        count != ((width + Interior - 1) / Interior) * ((height + Interior - 1) / Interior)) {
        error = QStringLiteral("無效的像素傳輸標頭"); return {};
    }
    auto frame = std::make_shared<Frame>();
    frame->path = identity.path; frame->mtime = identity.mtime; frame->fileSize = identity.size;
    frame->edge = identity.edge; frame->size = QSize(width, height);
    for (int y = 0; y < height; y += Interior) for (int x = 0; x < width; x += Interior) {
        qint32 tx, ty, w, h, stride; stream >> tx >> ty >> w >> h >> stride;
        if (stream.status() != QDataStream::Ok || tx != x || ty != y ||
            w != std::min(Interior, width - x) || h != std::min(Interior, height - y) || stride != (w + 2) * 4) {
            error = QStringLiteral("無效的分塊傳輸"); return {};
        }
        QImage pixels(w + 2, h + 2, QImage::Format_RGBA8888_Premultiplied);
        if (pixels.isNull() || stream.readRawData(reinterpret_cast<char *>(pixels.bits()), pixels.sizeInBytes()) != pixels.sizeInBytes()) {
            error = QStringLiteral("像素傳輸不完整"); return {};
        }
        frame->tiles.append({QRect(x, y, w, h), std::move(pixels)});
    }
    if (!file.atEnd() || stream.status() != QDataStream::Ok) { error = QStringLiteral("像素傳輸長度不符"); return {}; }
    if (identity.edge > 0) {
        // A read-only strided view of the interior. Keep the tile's shared
        // allocation alive even if a provider outlives the Frame. No second
        // preview-sized allocation; Qt detaches if a consumer tries to mutate.
        auto *owner = new QImage(frame->tiles.first().pixels);
        frame->image = QImage(owner->constBits() + owner->bytesPerLine() + 4,
                             width, height, owner->bytesPerLine(), owner->format(),
                             [](void *context) { delete static_cast<QImage *>(context); }, owner);
    }
    return frame;
}
}

class ImagingBackend : public QObject {
public:
    using Deliver = std::function<void(const Request &, FramePtr, const QString &)>;
    ImagingBackend(QString executable, QString directory, Deliver deliver, std::function<void()> quiet)
        : executable_(std::move(executable)), directory_(QDir(directory).filePath("thumbnails-v1")), deliver_(std::move(deliver)), quiet_(std::move(quiet)) {}
    void init() {
        QDir().mkpath(directory_);
        temporary_ = std::make_unique<QTemporaryDir>(QDir::tempPath() + "/piclens-imaging-XXXXXX");
        prune();
        timer_ = new QTimer(this); timer_->setInterval(5000);
        connect(timer_, &QTimer::timeout, this, [this] { if (dirty_ && !stopped_) { dirty_ = false; prune(); } });
        timer_->start();
    }
    void enqueue(Request request) {
        if (stopped_) { deliver_(request, {}, QStringLiteral("服務已關閉")); return; }
        if (queue_.size() >= Imaging::MaxQueued && jobs_.size() >= Imaging::MaxProcesses) {
            deliver_(request, {}, QStringLiteral("圖片工作佇列已滿")); return;
        }
        queue_.append(std::move(request));
        std::stable_sort(queue_.begin(), queue_.end(), [](const Request &a, const Request &b) { return a.priority > b.priority; });
        pump();
    }
    void cancel(quint64 serial) {
        // A completion may already be in transit to the GUI. Evict only the
        // affected result, preserving unrelated recently completed thumbnails.
        for (qsizetype i = recent_.size(); i-- > 0;) if (recent_[i].serials.contains(serial)) {
            recentBytes_ -= recent_[i].frame->byteSize(); recent_.removeAt(i);
        }
        for (qsizetype i = 0; i < queue_.size(); ++i) if (queue_[i].serial == serial) {
            const auto request = queue_.takeAt(i); deliver_(request, {}, QStringLiteral("已取消")); return;
        }
        for (const auto &job : jobs_) {
            for (qsizetype i = 0; i < job->requests.size(); ++i) if (job->requests[i].serial == serial) {
                const auto request = job->requests.takeAt(i);
                deliver_(request, {}, QStringLiteral("已取消"));
                if (job->requests.isEmpty()) { job->cancelled = true; job->process->kill(); }
                return;
            }
        }
    }
    void clearRecent() { recent_.clear(); recentBytes_ = 0; ++epoch_; }
    void quiesce() {
        quiescing_ = true;
        clearRecent();
        for (const auto &request : queue_) deliver_(request, {}, QStringLiteral("已取消"));
        queue_.clear();
        for (const auto &job : jobs_) {
            for (const auto &request : job->requests) deliver_(request, {}, QStringLiteral("已取消"));
            job->requests.clear(); job->cancelled = true; job->process->kill();
        }
        notifyQuiet();
    }
    void stop() {
        stopped_ = true;
        if (timer_) timer_->stop();
        queue_.clear();
        // Kill all children first, then reap each. No GUI event is required.
        for (const auto &job : jobs_) { job->process->disconnect(this); job->process->kill(); }
        for (const auto &job : jobs_) {
            job->process->waitForFinished(-1);
            QFile::remove(job->output);
            delete job->process;
        }
        jobs_.clear(); clearRecent(); temporary_.reset();
    }
private:
    struct Job {
        Identity identity; QList<Request> requests; QProcess *process = nullptr;
        QString output, error; QByteArray stdoutTail; bool cancelled = false, finished = false;
        quint64 epoch = 0;
    };
    struct Recent { QString key; FramePtr frame; QList<quint64> serials; };
    QString executable_, directory_;
    Deliver deliver_;
    std::function<void()> quiet_;
    QTimer *timer_ = nullptr;
    std::unique_ptr<QTemporaryDir> temporary_;
    QList<Request> queue_;
    QList<std::shared_ptr<Job>> jobs_;
    QList<Recent> recent_;
    qint64 recentBytes_ = 0;
    quint64 epoch_ = 0;
    bool stopped_ = false, dirty_ = false, quiescing_ = false;

    void notifyQuiet() {
        if (quiescing_ && jobs_.isEmpty() && queue_.isEmpty()) {
            quiescing_ = false; quiet_();
        }
    }

    void prune() {
        // Dedicated cache namespace; reject symlinks and unrelated file names.
        const auto files = QDir(directory_).entryInfoList({"*.png"}, QDir::Files | QDir::NoSymLinks, QDir::Time);
        const QRegularExpression owned("^[0-9a-f]{64}\\.png$");
        int retained = 0;
        for (const auto &file : files) if (owned.match(file.fileName()).hasMatch() && ++retained > 2000) QFile::remove(file.absoluteFilePath());
    }
    void deliverFrame(const Request &request, FramePtr frame, const QString &error = {}) {
        if (frame) {
            auto tagged = std::make_shared<Frame>(*frame); tagged->token = request.token;
            deliver_(request, std::move(tagged), error);
        } else deliver_(request, {}, error);
    }
    void pump() {
        while (!stopped_ && !queue_.isEmpty() && jobs_.size() < Imaging::MaxProcesses) {
            Request request = queue_.takeFirst();
            const Identity identity = identify(request);
            if (identity.size < 0) { deliver_(request, {}, QStringLiteral("來源不存在或不是檔案")); continue; }
            const QString key = identity.key();
            bool found = false;
            for (qsizetype i = 0; i < recent_.size(); ++i) if (recent_[i].key == key) {
                auto entry = recent_.takeAt(i);
                entry.serials.append(request.serial);
                if (entry.serials.size() > Imaging::MaxQueued + Imaging::MaxProcesses) entry.serials.removeFirst();
                recent_.append(entry); deliverFrame(request, entry.frame); found = true; break;
            }
            if (found) continue;
            for (auto &job : jobs_) if (!job->cancelled && job->identity == identity) {
                job->requests.append(request); found = true; break;
            }
            if (found) continue;
            if (!temporary_ || !temporary_->isValid()) { deliver_(request, {}, QStringLiteral("無法建立暫存目錄")); continue; }
            auto job = std::make_shared<Job>();
            job->identity = identity; job->requests.append(request); job->epoch = epoch_;
            job->output = temporary_->filePath(QUuid::createUuid().toString(QUuid::WithoutBraces) + ".rgba");
            auto *process = new QProcess(this); job->process = process;
            jobs_.append(job);
            connect(process, &QProcess::readyReadStandardError, this, [job] {
                job->error = (job->error + QString::fromUtf8(job->process->readAllStandardError())).right(8192);
            });
            connect(process, &QProcess::readyReadStandardOutput, this, [job] {
                job->stdoutTail = (job->stdoutTail + job->process->readAllStandardOutput()).right(128);
            });
            connect(process, &QProcess::finished, this, [this, job](int code, QProcess::ExitStatus status) { finish(job, code == 0 && status == QProcess::NormalExit); });
            connect(process, &QProcess::errorOccurred, this, [this, job](QProcess::ProcessError error) {
                if (error == QProcess::FailedToStart) { job->error = job->process->errorString(); finish(job, false); }
            });
            QTimer::singleShot(Imaging::TimeoutMs, process, [job] {
                if (!job->finished) { job->error = QStringLiteral("圖片解碼逾時"); job->cancelled = true; job->process->kill(); }
            });
            const QString cache = request.edge > 0 ? QDir(directory_).filePath(key + ".png") : QString();
            process->start(executable_, {"--decode", identity.path, QString::number(request.edge), job->output, cache});
        }
    }
    void finish(const std::shared_ptr<Job> &job, bool ok) {
        if (job->finished) return;
        job->finished = true;
        FramePtr frame;
        if (ok && !job->cancelled && !job->requests.isEmpty()) {
            frame = readFrame(job->output, job->identity, job->error);
            if (frame && identify(job->requests.first()) != job->identity) { frame.reset(); job->error = QStringLiteral("來源已變更，結果已淘汰"); }
        }
        QFile::remove(job->output);
        if (job->stdoutTail.contains("cache-written")) dirty_ = true;
        if (frame && job->identity.edge > 0 && job->identity.edge < 1024 && job->epoch == epoch_) {
            const qint64 bytes = frame->byteSize();
            if (bytes <= RecentLimit) {
                while (!recent_.isEmpty() && (recent_.size() >= 256 || recentBytes_ + bytes > RecentLimit)) {
                    const auto old = recent_.takeFirst(); recentBytes_ -= old.frame->byteSize();
                }
                QList<quint64> serials;
                for (const auto &request : job->requests) serials.append(request.serial);
                recent_.append({job->identity.key(), frame, serials}); recentBytes_ += bytes;
            }
        }
        const QString error = frame ? QString() : (job->error.isEmpty() ? QStringLiteral("解碼失敗或已取消") : job->error);
        for (const auto &request : job->requests) deliverFrame(request, frame, error);
        jobs_.removeOne(job);
        job->process->deleteLater();
        notifyQuiet();
        pump();
    }
};

Imaging::Imaging(QString workerPath, QString cacheDirectory, QObject *parent) : QObject(parent) {
    qRegisterMetaType<FramePtr>();
    backend_ = new ImagingBackend(std::move(workerPath), std::move(cacheDirectory), [this](const Request &request, FramePtr frame, const QString &error) {
        QMetaObject::invokeMethod(this, [this, request, frame = std::move(frame), error] {
            auto it = pending_.find(request.token);
            if (stopped_ || it == pending_.end() || it->serial != request.serial) return;
            const bool cancelled = it->cancelled;
            pending_.erase(it);
            emit completed(request.token, cancelled ? FramePtr() : frame, cancelled ? QStringLiteral("已取消") : error);
        }, Qt::QueuedConnection);
    }, [this] {
        QMetaObject::invokeMethod(this, [this] {
            if (!stopped_ && quiescing_) { quiescing_ = false; emit quiesced(); }
        }, Qt::QueuedConnection);
    });
    backend_->moveToThread(&thread_);
    connect(&thread_, &QThread::started, backend_, [this] { backend_->init(); });
    connect(&thread_, &QThread::finished, backend_, &QObject::deleteLater);
    thread_.start();
}
Imaging::~Imaging() { shutdown(); }
bool Imaging::enqueue(const QString &path, int edge, const QString &token, int priority) {
    Q_ASSERT(QThread::currentThread() == thread());
    if (stopped_ || quiescing_ || edge < 0 || edge > 1024 || token.isEmpty() || pending_.contains(token) || pending_.size() >= MaxQueued + MaxProcesses) return false;
    const quint64 serial = ++serial_;
    pending_.insert(token, {serial, edge});
    QMetaObject::invokeMethod(backend_, [backend = backend_, request = Request{path, token, edge, priority, serial}] { backend->enqueue(request); }, Qt::QueuedConnection);
    return true;
}
void Imaging::cancel(const QString &token) {
    Q_ASSERT(QThread::currentThread() == thread());
    auto it = pending_.find(token);
    if (stopped_ || it == pending_.end() || it->cancelled) return;
    it->cancelled = true;
    QMetaObject::invokeMethod(backend_, [backend = backend_, serial = it->serial] { backend->cancel(serial); }, Qt::QueuedConnection);
}
void Imaging::clearRecent() {
    if (!stopped_) QMetaObject::invokeMethod(backend_, [backend = backend_] { backend->clearRecent(); }, Qt::QueuedConnection);
}
void Imaging::clearGallery() {
    clearRecent();
    for (auto it = pending_.cbegin(); it != pending_.cend(); ++it) if (it->edge > 0 && it->edge < 1024) cancel(it.key());
}
void Imaging::cancelAll() { for (const auto &token : pending_.keys()) cancel(token); }
void Imaging::quiesce() {
    Q_ASSERT(QThread::currentThread() == thread());
    if (stopped_ || quiescing_) return;
    quiescing_ = true;
    for (auto &pending : pending_) pending.cancelled = true;
    QMetaObject::invokeMethod(backend_, [backend = backend_] { backend->quiesce(); }, Qt::QueuedConnection);
}
void Imaging::shutdown() {
    Q_ASSERT(QThread::currentThread() == thread());
    if (stopped_) return;
    stopped_ = true;
    QMetaObject::invokeMethod(backend_, [backend = backend_] { backend->stop(); }, Qt::BlockingQueuedConnection);
    thread_.quit(); thread_.wait(); backend_ = nullptr;
    const auto pending = std::exchange(pending_, {});
    for (auto it = pending.cbegin(); it != pending.cend(); ++it) emit completed(it.key(), {}, QStringLiteral("服務已關閉"));
}
} // namespace piclens
