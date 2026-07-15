#include <piclens/app/app_controller.h>

#include <piclens/core/path_rules.h>
#include <piclens/core/drag_interaction_rules.h>
#include <piclens/core/settings_rules.h>
#include <piclens/infrastructure/app_data_paths.h>

#include <QDir>
#include <QFileInfo>
#include <QFutureWatcher>
#include <QtConcurrentRun>

#include <algorithm>
#include <exception>
#include <utility>

namespace piclens::app {
namespace {

struct SettingsLoadResult {
    core::AppSettings settings = core::AppSettings::createDefault();
    QString errorDetails;
};

struct SettingsUpdateResult {
    QString errorDetails;
};

} // namespace

AppController::AppController(QObject *parent)
    : AppController(
          infrastructure::app_data_paths::settingsPath(),
          infrastructure::app_data_paths::logPath(),
          infrastructure::app_data_paths::thumbnailCacheRoot(),
          parent)
{
}

AppController::AppController(QString settingsPath, QString logPath, QObject *parent)
    : AppController(
          std::move(settingsPath),
          std::move(logPath),
          infrastructure::app_data_paths::thumbnailCacheRoot(),
          parent)
{
}

AppController::AppController(
    QString settingsPath,
    QString logPath,
    QString thumbnailCacheRoot,
    QObject *parent)
    : QObject(parent)
    , m_settingsStore(std::move(settingsPath))
    , m_logger(std::move(logPath))
    , m_thumbnailService(std::move(thumbnailCacheRoot))
    , m_library(
          [this](const core::ListQuery &query, std::stop_token stopToken) {
              return m_scanner.scan(query, stopToken);
          },
          [](const QString &path) { return resolveFolder(path); })
    , m_fileOperations(
          &m_library,
          [this](const QString &sourcePath, const QString &newFileName, std::stop_token stopToken) {
              return m_fileOperationService.rename(sourcePath, newFileName, stopToken);
          },
          [this](const QString &path, std::stop_token stopToken) {
              return m_fileOperationService.trash(path, stopToken);
          },
          [this](const QVector<core::ImageListItem> &images, std::stop_token stopToken) {
              return m_fileOperationService.convertVisibleToJpg(images, stopToken);
          },
          [this](const QVector<core::ImageListItem> &images, std::stop_token stopToken) {
              return m_fileOperationService.convertVisibleToWebp(images, stopToken);
          },
          [this](const QVector<core::ImageListItem> &images, std::stop_token stopToken) {
              return m_fileOperationService.trashSameBasenameExtras(images, stopToken);
          },
          [this](const QString &path) { m_platformFileManager.reveal(path); },
          [this](const QVector<QString> &sources, const QString &target, std::stop_token stopToken) {
              return m_fileOperationService.renameByDropTarget(sources, target, stopToken);
          },
          [](const QString &targetPath) {
              QVector<QString> paths;
              const QDir targetFolder(QFileInfo(targetPath).absolutePath());
              const QFileInfoList files = targetFolder.entryInfoList(
                  QDir::Files | QDir::NoDotAndDotDot,
                  QDir::Name);
              paths.reserve(files.size());
              for (const QFileInfo &file : files) {
                  paths.append(file.absoluteFilePath());
              }
              return paths;
          })
    , m_folderTree([this](const QString &path, std::stop_token stopToken) {
        return m_scanner.scanChildFolders(path, stopToken);
    })
    , m_thumbnails([this](const QString &path, int size, std::stop_token stopToken) {
        const infrastructure::ThumbnailResult loaded = m_thumbnailService.getOrCreate(
            path,
            size,
            stopToken);
        presentation::ThumbnailLoadResult result;
        result.canceled = loaded.status == infrastructure::ThumbnailStatus::Canceled;
        result.cacheHit = loaded.cacheHit;
        if (loaded.status == infrastructure::ThumbnailStatus::Ready) {
            result.cachePath = loaded.cachePath;
        } else if (loaded.status == infrastructure::ThumbnailStatus::Failed
                   || loaded.status == infrastructure::ThumbnailStatus::SourceMissing) {
            result.errorDetails = loaded.message.value_or(QStringLiteral("thumbnail_failed"));
        }
        return result;
    })
{
    m_settingsPool.setMaxThreadCount(1);
    m_settingsPool.setExpiryTimeout(30'000);

    connect(&m_library, &presentation::LibraryController::navigationStateChanged, this, [this] {
        m_thumbnails.cancelAll();
        synchronizeFolderTree();
    });
    connect(&m_library, &presentation::LibraryController::searchQueryChanged, this, [this] {
        m_thumbnails.cancelAll();
    });
    connect(
        &m_thumbnails,
        &presentation::ThumbnailCoordinator::thumbnailReady,
        this,
        [this](const QString &sourcePath, const QString &cachePath, int requestedSize) {
            m_library.items()->setThumbnailPath(sourcePath, cachePath, requestedSize);
        });
    connect(
        &m_thumbnails,
        &presentation::ThumbnailCoordinator::thumbnailFailed,
        this,
        [this](const QString &sourcePath, const QString &details, int requestedSize) {
            m_logger.error(
                details,
                QStringLiteral("Load thumbnail failed. Path=%1; RequestedSize=%2")
                    .arg(sourcePath)
                    .arg(requestedSize));
        });
    connect(
        &m_viewer,
        &presentation::ViewerController::opened,
        this,
        [this](const QString &path, int imageCount) {
            m_logger.info(QStringLiteral("Inline image viewer opened. Path=%1; ImageCount=%2")
                .arg(path).arg(imageCount));
        });
    connect(
        &m_viewer,
        &presentation::ViewerController::closed,
        this,
        [this](const QString &path) {
            m_logger.info(QStringLiteral("Inline image viewer closed. Path=%1").arg(path));
        });
    connect(
        &m_viewer,
        &presentation::ViewerController::loadFailed,
        this,
        [this](const QString &path, const QString &details) {
            m_logger.error(details, QStringLiteral("Viewer image load failed. Path=%1").arg(path));
        });
    connect(
        &m_fileOperations,
        &presentation::FileOperationController::operationFailed,
        this,
        [this](
            const QString &operation,
            const QString &sourcePath,
            const QString &targetPath,
            const QString &reason,
            const QString &details) {
            m_logger.error(
                details,
                QStringLiteral("File operation failed. Operation=%1; SourcePath=%2; TargetPath=%3; Reason=%4")
                    .arg(operation, sourcePath, targetPath, reason));
        });
    connect(
        &m_library,
        &presentation::LibraryController::lastFolderPersistenceRequested,
        this,
        [this](const QString &folderPath) {
            core::AppSettingsPatch patch;
            patch.lastFolderPath = folderPath;
            patch.hasLastFolderPath = true;
            persistSettings(
                std::move(patch),
                QStringLiteral("Persist last selected folder"));
        });
    connect(
        &m_library,
        &presentation::LibraryController::sortPersistenceRequested,
        this,
        [this](int sortKey, int sortDirection) {
            core::AppSettingsPatch patch;
            patch.sort = core::SortState{
                .key = static_cast<core::SortKey>(sortKey),
                .direction = static_cast<core::SortDirection>(sortDirection),
            };
            persistSettings(
                std::move(patch),
                QStringLiteral("Persist library sort"));
        });
    connect(
        &m_library,
        &presentation::LibraryController::includeSubfoldersPersistenceRequested,
        this,
        [this](bool includeSubfolders) {
            core::AppSettingsPatch patch;
            patch.includeSubfolders = includeSubfolders;
            persistSettings(
                std::move(patch),
                QStringLiteral("Persist recursive mode"));
        });
    connect(
        &m_library,
        &presentation::LibraryController::scanFailed,
        this,
        [this](const QString &folderPath, const QString &details) {
            m_logger.error(
                details,
                QStringLiteral("Load library failed. CurrentFolderPath=%1; IncludeSubfolders=%2")
                    .arg(folderPath)
                    .arg(m_library.includeSubfolders()));
        });
    connect(
        &m_folderTree,
        &presentation::FolderTreeModel::loadFailed,
        this,
        [this](const QString &folderPath, const QString &details) {
            m_logger.error(
                details,
                QStringLiteral("Load folder tree children failed. FolderPath=%1; FolderTreeRootPath=%2; CurrentFolderPath=%3")
                    .arg(folderPath, m_folderTree.rootPath(), m_library.currentFolderPath()));
            m_library.setExternalStatus(QStringLiteral("載入部分資料夾時發生錯誤，已寫入診斷記錄。"));
        });
}

AppController::~AppController()
{
    m_settingsPool.waitForDone();
}

bool AppController::initialized() const
{
    return m_initialized;
}

bool AppController::settingsBusy() const
{
    return m_pendingSettingsOperations > 0;
}

bool AppController::sidebarOpen() const
{
    return m_sidebarOpen;
}

bool AppController::gridViewMode() const
{
    return m_gridViewMode;
}

bool AppController::folderSelectionSuppressed() const
{
    return m_folderSelectionSuppressed;
}

presentation::LibraryController *AppController::library()
{
    return &m_library;
}

presentation::FolderTreeModel *AppController::folderTree()
{
    return &m_folderTree;
}

presentation::ThumbnailCoordinator *AppController::thumbnails()
{
    return &m_thumbnails;
}

presentation::FileOperationController *AppController::fileOperations()
{
    return &m_fileOperations;
}

presentation::ViewerController *AppController::viewer()
{
    return &m_viewer;
}

infrastructure::ThumbnailService *AppController::thumbnailService()
{
    return &m_thumbnailService;
}

void AppController::initialize()
{
    if (m_initializationStarted) {
        return;
    }
    m_initializationStarted = true;
    beginSettingsOperation();

    auto *watcher = new QFutureWatcher<SettingsLoadResult>(this);
    connect(watcher, &QFutureWatcher<SettingsLoadResult>::finished, this, [this, watcher] {
        const SettingsLoadResult result = watcher->result();
        watcher->deleteLater();
        endSettingsOperation();
        if (!result.errorDetails.isEmpty()) {
            m_logger.error(result.errorDetails, QStringLiteral("Load startup settings failed."));
            m_library.setExternalStatus(QStringLiteral("載入設定時發生錯誤，已使用預設值並寫入診斷記錄。"));
        }

        m_library.applyInitialSettings(result.settings);
        m_thumbnails.setRequestedSize(result.settings.thumbnailSize);
        m_initialized = true;
        emit initializedChanged();

        const auto initialFolder = result.settings.lastFolderPath.has_value()
            ? resolveFolder(*result.settings.lastFolderPath)
            : std::nullopt;
        if (!initialFolder.has_value()) {
            m_library.setExternalStatus(QStringLiteral("請選擇資料夾以開始瀏覽。"));
            emit folderSelectionRequired();
            return;
        }
        m_library.navigateToFolder(*initialFolder, true, false, *initialFolder);
    });

    watcher->setFuture(QtConcurrent::run(&m_settingsPool, [this] {
        try {
            return SettingsLoadResult{
                .settings = m_settingsStore.load(),
                .errorDetails = {},
            };
        } catch (const std::exception &exception) {
            return SettingsLoadResult{
                .settings = core::AppSettings::createDefault(),
                .errorDetails = QString::fromUtf8(exception.what()),
            };
        }
    }));
}

void AppController::openFolderFromPicker(const QString &folderPath)
{
    const auto resolved = resolveFolder(folderPath);
    if (!resolved.has_value()) {
        m_library.setExternalStatus(QStringLiteral("資料夾無法使用：%1").arg(folderPath));
        return;
    }
    m_library.navigateToFolder(*resolved, false, true, *resolved);
}

void AppController::openFolderUrl(const QUrl &folderUrl)
{
    openFolderFromPicker(folderUrl.toLocalFile());
}

void AppController::navigateFromTree(const QString &folderPath)
{
    m_library.navigateToFolder(
        folderPath,
        false,
        false,
        m_folderTree.rootPath());
}

void AppController::goBack()
{
    m_library.goBack();
}

void AppController::goForward()
{
    m_library.goForward();
}

void AppController::reload()
{
    m_thumbnails.cancelAll();
    m_library.reload();
}

void AppController::changeSort(int sortKey, int sortDirection)
{
    if (sortKey < static_cast<int>(core::SortKey::Name)
        || sortKey > static_cast<int>(core::SortKey::ModifiedAt)
        || sortDirection < static_cast<int>(core::SortDirection::Asc)
        || sortDirection > static_cast<int>(core::SortDirection::Desc)) {
        m_library.setExternalStatus(QStringLiteral("排序選項無效。"));
        return;
    }
    m_library.changeSort({
        .key = static_cast<core::SortKey>(sortKey),
        .direction = static_cast<core::SortDirection>(sortDirection),
    });
}

void AppController::setIncludeSubfolders(bool includeSubfolders)
{
    m_library.setIncludeSubfolders(includeSubfolders);
}

void AppController::requestThumbnail(const QString &sourcePath, bool animated)
{
    m_thumbnails.requestThumbnail(sourcePath, animated);
}

void AppController::cancelThumbnail(const QString &sourcePath)
{
    m_thumbnails.cancel(sourcePath);
}

void AppController::setThumbnailSize(double thumbnailSize)
{
    const int normalized = core::settings_rules::normalizeThumbnailSize(thumbnailSize);
    if (m_thumbnails.requestedSize() != normalized) {
        m_thumbnails.setRequestedSize(normalized);
        m_library.items()->clearThumbnails();
    }
    core::AppSettingsPatch patch;
    patch.thumbnailSize = normalized;
    persistSettings(std::move(patch), QStringLiteral("Persist thumbnail size"));
    m_library.setExternalStatus(QStringLiteral("縮圖大小已調整為 %1。").arg(normalized));
}

void AppController::selectLibraryItem(const QString &path, int modifiers)
{
    const auto keyboardModifiers = static_cast<Qt::KeyboardModifiers>(modifiers);
    m_library.selectPath(
        path,
        keyboardModifiers.testFlag(Qt::ControlModifier),
        keyboardModifiers.testFlag(Qt::ShiftModifier));
}

void AppController::prepareContextSelection(const QString &path)
{
    m_library.prepareContextSelection(path);
}

void AppController::clearSelection()
{
    m_library.clearSelection();
}

void AppController::toggleSidebar()
{
    m_sidebarOpen = !m_sidebarOpen;
    emit sidebarOpenChanged();
}

void AppController::setGridViewMode(bool gridViewMode)
{
    if (m_gridViewMode == gridViewMode) {
        return;
    }
    m_gridViewMode = gridViewMode;
    emit gridViewModeChanged();
}

void AppController::suppressFolderSelection()
{
    m_folderSelectionSuppressed = true;
}

void AppController::openViewer(const QString &path, bool preferSelectionOrder)
{
    const auto snapshot = m_library.createImageSequenceSnapshot(path, preferSelectionOrder);
    if (!snapshot.has_value()) {
        m_library.setExternalStatus(QStringLiteral("無法開啟圖片檢視器。"));
        return;
    }
    m_viewer.openSnapshot(*snapshot);
}

double AppController::dragAutoScrollDelta(double pointerY, double viewportHeight) const
{
    return core::drag_interaction_rules::calculateAutoScrollDelta(pointerY, viewportHeight);
}

void AppController::synchronizeFolderTree()
{
    const QString rootPath = m_library.navigationRootPath();
    const QString selectedPath = m_library.currentFolderPath();
    if (rootPath.isEmpty() || selectedPath.isEmpty()) {
        m_folderTree.clear();
        return;
    }
    if (!core::path_rules::pathEquals(m_folderTree.rootPath(), rootPath)) {
        m_folderTree.setRoot(rootPath, selectedPath);
    } else {
        m_folderTree.selectPath(selectedPath);
    }
}

void AppController::persistSettings(core::AppSettingsPatch patch, QString operationName)
{
    beginSettingsOperation();
    auto *watcher = new QFutureWatcher<SettingsUpdateResult>(this);
    connect(watcher, &QFutureWatcher<SettingsUpdateResult>::finished, this, [this, watcher, operationName] {
        const SettingsUpdateResult result = watcher->result();
        watcher->deleteLater();
        endSettingsOperation();
        if (!result.errorDetails.isEmpty()) {
            m_logger.error(result.errorDetails, operationName + QStringLiteral(" failed."));
            m_library.setExternalStatus(QStringLiteral("儲存設定時發生錯誤，已寫入診斷記錄。"));
            return;
        }
        emit settingsPersisted();
    });

    watcher->setFuture(QtConcurrent::run(&m_settingsPool, [this, patch = std::move(patch)] {
        try {
            static_cast<void>(m_settingsStore.update(patch));
            return SettingsUpdateResult{.errorDetails = {}};
        } catch (const std::exception &exception) {
            return SettingsUpdateResult{.errorDetails = QString::fromUtf8(exception.what())};
        }
    }));
}

void AppController::beginSettingsOperation()
{
    const bool wasBusy = settingsBusy();
    ++m_pendingSettingsOperations;
    if (!wasBusy) {
        emit settingsBusyChanged();
    }
}

void AppController::endSettingsOperation()
{
    const bool wasBusy = settingsBusy();
    m_pendingSettingsOperations = std::max(0, m_pendingSettingsOperations - 1);
    if (wasBusy != settingsBusy()) {
        emit settingsBusyChanged();
    }
}

std::optional<QString> AppController::resolveFolder(const QString &folderPath)
{
    const QFileInfo info(folderPath);
    if (!info.exists() || !info.isDir()) {
        return std::nullopt;
    }
    return QDir::cleanPath(info.absoluteFilePath());
}

} // namespace piclens::app
