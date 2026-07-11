#pragma once

#include <piclens/core/models.h>

#include <QObject>
#include <QUrl>

namespace piclens::presentation {

class ViewerController : public QObject
{
    Q_OBJECT
    Q_PROPERTY(bool open READ isOpen NOTIFY stateChanged)
    Q_PROPERTY(int currentIndex READ currentIndex NOTIFY stateChanged)
    Q_PROPERTY(int imageCount READ imageCount NOTIFY stateChanged)
    Q_PROPERTY(QString currentName READ currentName NOTIFY stateChanged)
    Q_PROPERTY(QString currentPath READ currentPath NOTIFY stateChanged)
    Q_PROPERTY(QUrl currentSourceUrl READ currentSourceUrl NOTIFY stateChanged)
    Q_PROPERTY(bool imageVisible READ imageVisible NOTIFY stateChanged)
    Q_PROPERTY(bool unsupportedAnimated READ unsupportedAnimated NOTIFY stateChanged)
    Q_PROPERTY(QString unsupportedMessage READ unsupportedMessage NOTIFY stateChanged)
    Q_PROPERTY(bool canGoPrevious READ canGoPrevious NOTIFY stateChanged)
    Q_PROPERTY(bool canGoNext READ canGoNext NOTIFY stateChanged)
    Q_PROPERTY(double zoom READ zoom NOTIFY stateChanged)
    Q_PROPERTY(double offsetX READ offsetX NOTIFY stateChanged)
    Q_PROPERTY(double offsetY READ offsetY NOTIFY stateChanged)
    Q_PROPERTY(bool canZoomIn READ canZoomIn NOTIFY stateChanged)
    Q_PROPERTY(bool canZoomOut READ canZoomOut NOTIFY stateChanged)
    Q_PROPERTY(QString errorMessage READ errorMessage NOTIFY stateChanged)

public:
    explicit ViewerController(QObject *parent = nullptr);

    [[nodiscard]] bool isOpen() const;
    [[nodiscard]] int currentIndex() const;
    [[nodiscard]] int imageCount() const;
    [[nodiscard]] QString currentName() const;
    [[nodiscard]] QString currentPath() const;
    [[nodiscard]] QUrl currentSourceUrl() const;
    [[nodiscard]] bool imageVisible() const;
    [[nodiscard]] bool unsupportedAnimated() const;
    [[nodiscard]] QString unsupportedMessage() const;
    [[nodiscard]] bool canGoPrevious() const;
    [[nodiscard]] bool canGoNext() const;
    [[nodiscard]] double zoom() const;
    [[nodiscard]] double offsetX() const;
    [[nodiscard]] double offsetY() const;
    [[nodiscard]] bool canZoomIn() const;
    [[nodiscard]] bool canZoomOut() const;
    [[nodiscard]] QString errorMessage() const;

    void openSnapshot(core::ImageSequenceSnapshot snapshot);
    Q_INVOKABLE void close();
    Q_INVOKABLE void previous();
    Q_INVOKABLE void next();
    Q_INVOKABLE void zoomAt(
        double pointerX,
        double pointerY,
        int delta,
        double viewportWidth,
        double viewportHeight);
    Q_INVOKABLE void zoomIn(double viewportWidth, double viewportHeight);
    Q_INVOKABLE void zoomOut(double viewportWidth, double viewportHeight);
    Q_INVOKABLE void resetZoom();
    Q_INVOKABLE void panBy(double deltaX, double deltaY);
    Q_INVOKABLE void reportLoadFailure(const QString &details);

signals:
    void stateChanged();
    void opened(const QString &path, int imageCount);
    void closed(const QString &path);
    void loadFailed(const QString &path, const QString &details);

private:
    [[nodiscard]] const core::ImageListItem *currentImage() const;
    void changeIndex(int index);

    core::ImageSequenceSnapshot m_snapshot;
    bool m_open = false;
    double m_zoom = 1;
    double m_offsetX = 0;
    double m_offsetY = 0;
    QString m_errorMessage;
};

} // namespace piclens::presentation
