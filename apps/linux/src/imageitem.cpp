#include "imageitem.h"
#include <QMouseEvent>
#include <QPointer>
#include <QQuickWindow>
#include <QSGSimpleTextureNode>
#include <QSGTransformNode>
#include <QWheelEvent>
#include <algorithm>
#include <cmath>

namespace piclens {
namespace {
class FrameNode : public QSGTransformNode {
public:
    FramePtr frame;
    quint64 revision = 0;
    bool scheduled = false;
};
}
ImageItem::ImageItem(QQuickItem *parent) : QQuickItem(parent) {
    setFlag(ItemHasContents, true);
    setAcceptedMouseButtons(Qt::AllButtons);
    setKeepMouseGrab(true);
    setClip(true);
}
void ImageItem::setFrame(FramePtr frame) {
    Q_ASSERT(QThread::currentThread() == thread());
    frame_ = std::move(frame); ++revision_; update();
}
void ImageItem::setZoom(qreal value) {
    if (!std::isfinite(value)) return;
    zoomAt(std::clamp(value, qreal(0.1), qreal(8)) / zoom_, {width() / 2, height() / 2});
}
void ImageItem::zoomBy(qreal factor) { zoomAt(factor, {width() / 2, height() / 2}); }
void ImageItem::zoomAt(qreal factor, QPointF pointer) {
    if (!std::isfinite(factor) || factor <= 0 || !std::isfinite(pointer.x()) || !std::isfinite(pointer.y())) return;
    const qreal next = std::clamp(zoom_ * factor, qreal(0.1), qreal(8));
    if (qFuzzyCompare(next, zoom_)) return;
    const QPointF anchor = pointer - QPointF(width() / 2, height() / 2);
    pan_ = anchor - (anchor - pan_) * (next / zoom_);
    zoom_ = next; emit zoomChanged(); update();
}
void ImageItem::reset() {
    pan_ = {}; dragging_ = false;
    if (zoom_ != 1) { zoom_ = 1; emit zoomChanged(); }
    update();
}
void ImageItem::geometryChange(const QRectF &now, const QRectF &before) {
    QQuickItem::geometryChange(now, before); update();
}
void ImageItem::wheelEvent(QWheelEvent *event) {
    const qreal steps = event->angleDelta().y() ? event->angleDelta().y() / 120.0 : event->pixelDelta().y() / 120.0;
    zoomAt(std::pow(1.2, steps), event->position()); event->accept();
}
void ImageItem::mousePressEvent(QMouseEvent *event) {
    dragging_ = event->button() == Qt::LeftButton;
    pointer_ = event->position(); event->accept();
}
void ImageItem::mouseMoveEvent(QMouseEvent *event) {
    if (dragging_) { pan_ += event->position() - pointer_; pointer_ = event->position(); update(); }
    event->accept();
}
void ImageItem::mouseReleaseEvent(QMouseEvent *event) { dragging_ = false; event->accept(); }
void ImageItem::mouseUngrabEvent() { dragging_ = false; }

QSGNode *ImageItem::updatePaintNode(QSGNode *old, UpdatePaintNodeData *) {
    auto *root = static_cast<FrameNode *>(old);
    if (!frame_ || frame_->tiles.isEmpty() || !window() || frame_->size.isEmpty()) { delete root; return nullptr; }
    if (!root || root->revision != revision_) {
        delete root;
        root = new FrameNode; root->frame = frame_; root->revision = revision_;
        for (const auto &tile : frame_->tiles) {
            QSGTexture *texture = window()->createTextureFromImage(tile.pixels);
            if (!texture) { delete root; return nullptr; }
            auto *node = new QSGSimpleTextureNode;
            node->setTexture(texture); node->setOwnsTexture(true);
            node->setFiltering(QSGTexture::Linear);
            node->setRect(tile.rect);
            node->setSourceRect(QRectF(1, 1, tile.rect.width(), tile.rect.height()));
            root->appendChildNode(node);
        }
    }
    const qreal fit = std::min(width() / frame_->size.width(), height() / frame_->size.height());
    QMatrix4x4 matrix;
    matrix.translate(float(width() / 2 + pan_.x()), float(height() / 2 + pan_.y()));
    matrix.scale(float(fit * zoom_));
    matrix.translate(-frame_->size.width() / 2.0f, -frame_->size.height() / 2.0f);
    root->setMatrix(matrix);
    if (frame_->edge == 0 && !root->scheduled && width() > 0 && height() > 0) {
        root->scheduled = true;
        const quint64 revision = revision_;
        const QString token = frame_->token;
        // QObject context cancels both callbacks if the item is destroyed.
        connect(window(), &QQuickWindow::afterRendering, this, [this, revision, token] {
            QMetaObject::invokeMethod(this, [this, revision, token] {
                if (revision_ == revision && presented_ != revision && frame_ && frame_->edge == 0) {
                    presented_ = revision; emit framePresented(token);
                }
            }, Qt::QueuedConnection);
        }, Qt::ConnectionType(Qt::DirectConnection | Qt::SingleShotConnection));
    }
    return root;
}
} // namespace piclens
