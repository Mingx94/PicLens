#pragma once
#include "imaging.h"
#include <QQuickItem>

namespace piclens {
class ImageItem : public QQuickItem {
    Q_OBJECT
    Q_PROPERTY(qreal zoom READ zoom WRITE setZoom NOTIFY zoomChanged)
public:
    explicit ImageItem(QQuickItem *parent = nullptr);
    void setFrame(FramePtr frame);
    FramePtr frame() const { return frame_; }
    qreal zoom() const { return zoom_; }
    qreal scale() const { return zoom_; }
    void setScale(qreal value) { setZoom(value); }
    void setZoom(qreal value);
    Q_INVOKABLE void zoomBy(qreal factor);
    Q_INVOKABLE void zoomAt(qreal factor, QPointF pointer);
    Q_INVOKABLE void reset();
Q_SIGNALS:
    void zoomChanged();
    // Complete original texture submission followed by afterRendering, queued
    // to GUI. This does not claim OS compositor presentation.
    void framePresented(const QString &token);
protected:
    QSGNode *updatePaintNode(QSGNode *, UpdatePaintNodeData *) override;
    void geometryChange(const QRectF &, const QRectF &) override;
    void wheelEvent(QWheelEvent *) override;
    void mousePressEvent(QMouseEvent *) override;
    void mouseMoveEvent(QMouseEvent *) override;
    void mouseReleaseEvent(QMouseEvent *) override;
    void mouseUngrabEvent() override;
private:
    FramePtr frame_;
    qreal zoom_ = 1;
    QPointF pan_, pointer_;
    bool dragging_ = false;
    quint64 revision_ = 0, presented_ = 0;
};
} // namespace piclens
