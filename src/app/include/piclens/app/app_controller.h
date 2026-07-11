#pragma once

#include <piclens/infrastructure/file_app_logger.h>
#include <piclens/infrastructure/file_operation_service.h>
#include <piclens/infrastructure/folder_scanner.h>
#include <piclens/infrastructure/json_settings_store.h>
#include <piclens/infrastructure/platform_file_manager.h>
#include <piclens/infrastructure/thumbnail_service.h>
#include <piclens/presentation/folder_tree_model.h>
#include <piclens/presentation/file_operation_controller.h>
#include <piclens/presentation/library_controller.h>
#include <piclens/presentation/thumbnail_coordinator.h>
#include <piclens/presentation/viewer_controller.h>

#include <QObject>
#include <QThreadPool>
#include <QUrl>

#include <optional>

namespace piclens::app {

class AppController : public QObject
{
    Q_OBJECT
    Q_PROPERTY(bool initialized READ initialized NOTIFY initializedChanged)
    Q_PROPERTY(bool settingsBusy READ settingsBusy NOTIFY settingsBusyChanged)
    Q_PROPERTY(bool sidebarOpen READ sidebarOpen NOTIFY sidebarOpenChanged)
    Q_PROPERTY(bool gridViewMode READ gridViewMode NOTIFY gridViewModeChanged)
    Q_PROPERTY(bool folderSelectionSuppressed READ folderSelectionSuppressed CONSTANT)
    Q_PROPERTY(piclens::presentation::LibraryController *library READ library CONSTANT)
    Q_PROPERTY(piclens::presentation::FolderTreeModel *folderTree READ folderTree CONSTANT)
    Q_PROPERTY(piclens::presentation::ThumbnailCoordinator *thumbnails READ thumbnails CONSTANT)
    Q_PROPERTY(piclens::presentation::FileOperationController *fileOperations READ fileOperations CONSTANT)
    Q_PROPERTY(piclens::presentation::ViewerController *viewer READ viewer CONSTANT)

public:
    explicit AppController(QObject *parent = nullptr);
    AppController(QString settingsPath, QString logPath, QObject *parent = nullptr);
    AppController(
        QString settingsPath,
        QString logPath,
        QString thumbnailCacheRoot,
        QObject *parent = nullptr);
    ~AppController() override;

    [[nodiscard]] bool initialized() const;
    [[nodiscard]] bool settingsBusy() const;
    [[nodiscard]] bool sidebarOpen() const;
    [[nodiscard]] bool gridViewMode() const;
    [[nodiscard]] bool folderSelectionSuppressed() const;
    [[nodiscard]] presentation::LibraryController *library();
    [[nodiscard]] presentation::FolderTreeModel *folderTree();
    [[nodiscard]] presentation::ThumbnailCoordinator *thumbnails();
    [[nodiscard]] presentation::FileOperationController *fileOperations();
    [[nodiscard]] presentation::ViewerController *viewer();
    [[nodiscard]] infrastructure::ThumbnailService *thumbnailService();

    Q_INVOKABLE void initialize();
    void openFolderFromPicker(const QString &folderPath);
    Q_INVOKABLE void openFolderUrl(const QUrl &folderUrl);
    Q_INVOKABLE void navigateFromTree(const QString &folderPath);
    Q_INVOKABLE void goBack();
    Q_INVOKABLE void goForward();
    Q_INVOKABLE void reload();
    Q_INVOKABLE void changeSort(int sortKey, int sortDirection);
    Q_INVOKABLE void setIncludeSubfolders(bool includeSubfolders);
    Q_INVOKABLE void requestThumbnail(const QString &sourcePath, bool animated);
    Q_INVOKABLE void cancelThumbnail(const QString &sourcePath);
    Q_INVOKABLE void setThumbnailSize(double thumbnailSize);
    Q_INVOKABLE void selectLibraryItem(const QString &path, int modifiers);
    Q_INVOKABLE void prepareContextSelection(const QString &path);
    Q_INVOKABLE void clearSelection();
    Q_INVOKABLE void toggleSidebar();
    Q_INVOKABLE void setGridViewMode(bool gridViewMode);
    void suppressFolderSelection();
    Q_INVOKABLE void openViewer(const QString &path, bool preferSelectionOrder = false);
    Q_INVOKABLE double dragAutoScrollDelta(double pointerY, double viewportHeight) const;

signals:
    void initializedChanged();
    void settingsBusyChanged();
    void sidebarOpenChanged();
    void gridViewModeChanged();
    void folderSelectionRequired();
    void settingsPersisted();

private:
    void synchronizeFolderTree();
    void persistSettings(core::AppSettingsPatch patch, QString operationName);
    void beginSettingsOperation();
    void endSettingsOperation();
    [[nodiscard]] static std::optional<QString> resolveFolder(const QString &folderPath);

    infrastructure::FolderScanner m_scanner;
    infrastructure::JsonSettingsStore m_settingsStore;
    infrastructure::FileAppLogger m_logger;
    infrastructure::ThumbnailService m_thumbnailService;
    infrastructure::FileOperationService m_fileOperationService;
    infrastructure::PlatformFileManager m_platformFileManager;
    presentation::LibraryController m_library;
    presentation::FileOperationController m_fileOperations;
    presentation::FolderTreeModel m_folderTree;
    presentation::ThumbnailCoordinator m_thumbnails;
    presentation::ViewerController m_viewer;
    QThreadPool m_settingsPool;
    bool m_initialized = false;
    bool m_initializationStarted = false;
    int m_pendingSettingsOperations = 0;
    bool m_sidebarOpen = true;
    bool m_gridViewMode = true;
    bool m_folderSelectionSuppressed = false;
};

} // namespace piclens::app
