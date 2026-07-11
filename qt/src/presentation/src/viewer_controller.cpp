#include <piclens/presentation/viewer_controller.h>

#include <piclens/core/zoom_math.h>

#include <utility>

namespace piclens::presentation {

ViewerController::ViewerController(QObject *parent)
    : QObject(parent)
{
}

bool ViewerController::isOpen() const { return m_open; }
int ViewerController::currentIndex() const { return m_snapshot.currentIndex; }
int ViewerController::imageCount() const { return m_snapshot.images.size(); }
QString ViewerController::currentName() const
{
    const auto *image = currentImage();
    return image ? image->name : QStringLiteral("尚未選取圖片");
}
QString ViewerController::currentPath() const
{
    const auto *image = currentImage();
    return image ? image->path : QString{};
}
QUrl ViewerController::currentSourceUrl() const
{
    return imageVisible() ? QUrl::fromLocalFile(currentPath()) : QUrl{};
}
bool ViewerController::imageVisible() const
{
    return currentImage() && !unsupportedAnimated();
}
bool ViewerController::unsupportedAnimated() const
{
    const auto *image = currentImage();
    return image && image->isAnimated;
}
QString ViewerController::unsupportedMessage() const
{
    const auto *image = currentImage();
    if (!image) {
        return QStringLiteral("尚未選取圖片");
    }
    return QStringLiteral("原生檢視器目前尚不支援播放動畫 %1。")
        .arg(image->extension.trimmed().remove(QLatin1Char('.')).toUpper());
}
bool ViewerController::canGoPrevious() const { return m_snapshot.currentIndex > 0; }
bool ViewerController::canGoNext() const
{
    return m_snapshot.currentIndex >= 0 && m_snapshot.currentIndex < m_snapshot.images.size() - 1;
}
double ViewerController::zoom() const { return m_zoom; }
double ViewerController::offsetX() const { return m_offsetX; }
double ViewerController::offsetY() const { return m_offsetY; }
bool ViewerController::canZoomIn() const
{
    return imageVisible() && m_zoom < core::zoom_math::MaxZoom;
}
bool ViewerController::canZoomOut() const
{
    return imageVisible() && m_zoom > core::zoom_math::MinZoom;
}
QString ViewerController::errorMessage() const { return m_errorMessage; }

void ViewerController::openSnapshot(core::ImageSequenceSnapshot snapshot)
{
    if (snapshot.currentIndex < 0 || snapshot.currentIndex >= snapshot.images.size()) {
        return;
    }
    m_snapshot = std::move(snapshot);
    m_open = true;
    m_errorMessage.clear();
    resetZoom();
    emit opened(currentPath(), imageCount());
}

void ViewerController::close()
{
    if (!m_open) return;
    const QString path = currentPath();
    m_open = false;
    emit stateChanged();
    emit closed(path);
}

void ViewerController::previous()
{
    if (canGoPrevious()) changeIndex(m_snapshot.currentIndex - 1);
}

void ViewerController::next()
{
    if (canGoNext()) changeIndex(m_snapshot.currentIndex + 1);
}

void ViewerController::zoomAt(
    double pointerX,
    double pointerY,
    int delta,
    double viewportWidth,
    double viewportHeight)
{
    if (!imageVisible() || delta == 0) return;
    const core::ZoomState next = core::zoom_math::zoomAtPoint(
        m_zoom,
        {.x = m_offsetX, .y = m_offsetY},
        {.x = viewportWidth / 2, .y = viewportHeight / 2},
        {.x = pointerX, .y = pointerY},
        delta);
    m_zoom = next.zoom;
    m_offsetX = next.offset.x;
    m_offsetY = next.offset.y;
    emit stateChanged();
}

void ViewerController::zoomIn(double viewportWidth, double viewportHeight)
{
    zoomAt(viewportWidth / 2, viewportHeight / 2, 1, viewportWidth, viewportHeight);
}

void ViewerController::zoomOut(double viewportWidth, double viewportHeight)
{
    zoomAt(viewportWidth / 2, viewportHeight / 2, -1, viewportWidth, viewportHeight);
}

void ViewerController::resetZoom()
{
    const core::ZoomState reset = core::zoom_math::resetZoomState();
    m_zoom = reset.zoom;
    m_offsetX = reset.offset.x;
    m_offsetY = reset.offset.y;
    emit stateChanged();
}

void ViewerController::panBy(double deltaX, double deltaY)
{
    if (!imageVisible() || m_zoom <= 1 || (deltaX == 0 && deltaY == 0)) return;
    m_offsetX += deltaX;
    m_offsetY += deltaY;
    emit stateChanged();
}

void ViewerController::reportLoadFailure(const QString &details)
{
    if (!m_open || !imageVisible()) return;
    m_errorMessage = QStringLiteral("無法載入圖片，詳細資訊已寫入診斷記錄。");
    emit stateChanged();
    emit loadFailed(currentPath(), details);
}

const core::ImageListItem *ViewerController::currentImage() const
{
    return m_snapshot.currentIndex >= 0 && m_snapshot.currentIndex < m_snapshot.images.size()
        ? &m_snapshot.images.at(m_snapshot.currentIndex)
        : nullptr;
}

void ViewerController::changeIndex(int index)
{
    m_snapshot.currentIndex = index;
    m_errorMessage.clear();
    const core::ZoomState reset = core::zoom_math::resetZoomState();
    m_zoom = reset.zoom;
    m_offsetX = reset.offset.x;
    m_offsetY = reset.offset.y;
    emit stateChanged();
}

} // namespace piclens::presentation
