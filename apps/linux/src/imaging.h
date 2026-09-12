#pragma once

#include <QObject>
#include <QImage>
#include <QRect>
#include <QHash>
#include <QThread>
#include <memory>

namespace piclens {

// All pixels are owned by immutable, shared payloads. Tiles have one replicated
// border pixel on each side, RGBA8888 premultiplied, tightly packed width * 4.
// rect is the interior in full-image coordinates; pixels includes the border.
struct ImageTile { QRect rect; QImage pixels; };
struct Frame {
    QString token;
    QImage image; // thumbnail/preview interior; read-only view sharing tile allocation
    QString path;
    qint64 mtime = 0;
    qint64 fileSize = 0;
    int edge = 0;
    QSize size;
    QList<ImageTile> tiles;
    qint64 byteSize() const {
        qint64 result = 0;
        for (const auto &tile : tiles) result += tile.pixels.sizeInBytes();
        return result;
    }
};
using FramePtr = std::shared_ptr<const Frame>;
using ImagePayload = Frame;
using ImagePayloadPtr = FramePtr;

class ImagingBackend;
// Construct and call on the GUI thread. One shared service for gallery + viewer.
// Caller supplies a unique selection/generation request token and cancels tiles
// on unload. edge: 0 original, 1024 viewer preview, 1..1023 thumbnail.
class Imaging : public QObject {
    Q_OBJECT
public:
    explicit Imaging(QString workerPath, QString cacheDirectory, QObject *parent = nullptr);
    ~Imaging() override;
    // false means rejected synchronously (queue full, duplicate token, invalid
    // edge or shutdown); no completed signal is owed for rejected requests.
    Q_INVOKABLE bool enqueue(const QString &path, int edge, const QString &token, int priority = 0);
    Q_INVOKABLE void cancel(const QString &token);
    Q_INVOKABLE void clearRecent();
    Q_INVOKABLE void clearGallery(); // cancels edge 1..1023 and clears recent cache
    Q_INVOKABLE void cancelAll();
    // Reject enqueue until quiesced. Cancels work and asynchronously waits for
    // every physical process to exit and every transport to be cleaned up.
    Q_INVOKABLE void quiesce();
    void shutdown();
    int outstanding() const { return pending_.size(); }
    static constexpr int MaxProcesses = 8;
    static constexpr int MaxQueued = 256;
    static constexpr int TimeoutMs = 15000;
Q_SIGNALS:
    // Exactly one terminal result for each accepted token, including cancellation.
    void completed(const QString &token, piclens::FramePtr payload, const QString &error);
    void quiesced();
private:
    QThread thread_;
    ImagingBackend *backend_ = nullptr;
    struct Pending { quint64 serial; int edge; bool cancelled = false; };
    QHash<QString, Pending> pending_;
    quint64 serial_ = 0;
    bool stopped_ = false;
    bool quiescing_ = false;
};
} // namespace piclens
Q_DECLARE_METATYPE(piclens::FramePtr)
