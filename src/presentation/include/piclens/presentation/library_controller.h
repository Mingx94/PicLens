#pragma once

#include <piclens/core/models.h>
#include <piclens/presentation/library_item_model.h>

#include <QObject>
#include <QThreadPool>

#include <functional>
#include <memory>
#include <optional>
#include <stop_token>

namespace piclens::presentation {

class LibraryController final : public QObject
{
    Q_OBJECT
    Q_PROPERTY(QString currentFolderPath READ currentFolderPath NOTIFY currentFolderPathChanged)
    Q_PROPERTY(QString currentFolderName READ currentFolderName NOTIFY currentFolderPathChanged)
    Q_PROPERTY(QString navigationRootPath READ navigationRootPath NOTIFY navigationRootPathChanged)
    Q_PROPERTY(bool includeSubfolders READ includeSubfolders NOTIFY includeSubfoldersChanged)
    Q_PROPERTY(int sortKey READ sortKey NOTIFY sortChanged)
    Q_PROPERTY(int sortDirection READ sortDirection NOTIFY sortChanged)
    Q_PROPERTY(QString sortLabel READ sortLabel NOTIFY sortChanged)
    Q_PROPERTY(QString recursiveModeLabel READ recursiveModeLabel NOTIFY includeSubfoldersChanged)
    Q_PROPERTY(QString searchQuery READ searchQuery WRITE setSearchQuery NOTIFY searchQueryChanged)
    Q_PROPERTY(bool hasSearchQuery READ hasSearchQuery NOTIFY searchQueryChanged)
    Q_PROPERTY(bool busy READ busy NOTIFY busyChanged)
    Q_PROPERTY(QString statusText READ statusText NOTIFY statusTextChanged)
    Q_PROPERTY(QString errorMessage READ errorMessage NOTIFY errorMessageChanged)
    Q_PROPERTY(bool canGoBack READ canGoBack NOTIFY navigationAvailabilityChanged)
    Q_PROPERTY(bool canGoForward READ canGoForward NOTIFY navigationAvailabilityChanged)
    Q_PROPERTY(int selectedCount READ selectedCount NOTIFY selectionChanged)
    Q_PROPERTY(bool hasSelectedImages READ hasSelectedImages NOTIFY selectionChanged)
    Q_PROPERTY(bool hasSingleSelectedImage READ hasSingleSelectedImage NOTIFY selectionChanged)
    Q_PROPERTY(QString selectionSummary READ selectionSummary NOTIFY selectionChanged)
    Q_PROPERTY(piclens::presentation::LibraryItemModel *items READ items CONSTANT)

public:
    using ScanFunction = std::function<QVector<core::ListItem>(const core::ListQuery &, std::stop_token)>;
    using ResolveFolderFunction = std::function<std::optional<QString>(const QString &)>;

    LibraryController(
        ScanFunction scan,
        ResolveFolderFunction resolveFolder,
        QObject *parent = nullptr);
    ~LibraryController() override;

    LibraryController(const LibraryController &) = delete;
    LibraryController &operator=(const LibraryController &) = delete;

    [[nodiscard]] QString currentFolderPath() const;
    [[nodiscard]] QString currentFolderName() const;
    [[nodiscard]] QString navigationRootPath() const;
    [[nodiscard]] bool includeSubfolders() const;
    [[nodiscard]] int sortKey() const;
    [[nodiscard]] int sortDirection() const;
    [[nodiscard]] QString sortLabel() const;
    [[nodiscard]] QString recursiveModeLabel() const;
    [[nodiscard]] QString searchQuery() const;
    [[nodiscard]] bool hasSearchQuery() const;
    [[nodiscard]] bool busy() const;
    [[nodiscard]] QString statusText() const;
    [[nodiscard]] QString errorMessage() const;
    [[nodiscard]] bool canGoBack() const;
    [[nodiscard]] bool canGoForward() const;
    [[nodiscard]] int selectedCount() const;
    [[nodiscard]] bool hasSelectedImages() const;
    [[nodiscard]] bool hasSingleSelectedImage() const;
    [[nodiscard]] QString selectionSummary() const;
    [[nodiscard]] QStringList selectedPaths() const;
    [[nodiscard]] bool containsImagePath(const QString &path) const;
    [[nodiscard]] QVector<core::ImageListItem> visibleImages() const;
    [[nodiscard]] std::optional<core::ImageSequenceSnapshot> createImageSequenceSnapshot(
        const QString &requestedPath,
        bool preferSelectionOrder) const;
    [[nodiscard]] LibraryItemModel *items();

    void applyInitialSettings(const core::AppSettings &settings);
    void navigateToFolder(
        const QString &folderPath,
        bool replaceHistory = false,
        bool persist = false,
        const QString &navigationRootPath = {});
    void reload();
    void goBack();
    void goForward();
    void changeSort(core::SortState sort);
    void setIncludeSubfolders(bool includeSubfolders);
    Q_INVOKABLE void setSearchQuery(const QString &searchQuery);
    void setSelectedPaths(const QStringList &paths);
    void selectPath(const QString &path, bool controlModifier, bool shiftModifier);
    void prepareContextSelection(const QString &path);
    void clearSelection();
    void refreshAfterFileOperation();
    void setExternalStatus(QString statusText);

signals:
    void currentFolderPathChanged();
    void navigationRootPathChanged();
    void includeSubfoldersChanged();
    void searchQueryChanged();
    void sortChanged();
    void busyChanged();
    void statusTextChanged();
    void errorMessageChanged();
    void navigationAvailabilityChanged();
    void navigationStateChanged();
    void selectionChanged();

    void lastFolderPersistenceRequested(const QString &folderPath);
    void sortPersistenceRequested(int sortKey, int sortDirection);
    void includeSubfoldersPersistenceRequested(bool includeSubfolders);
    void scanFailed(const QString &folderPath, const QString &details);

private:
    struct HistoryEntry {
        QString folderPath;
        QString rootPath;
    };

    void requestScan();
    void applySearchFilter(bool preserveThumbnails = true);
    void recordHistory(HistoryEntry entry, bool replaceHistory);
    void setCurrentFolderPath(QString folderPath);
    void setNavigationRootPath(QString rootPath);
    void setBusy(bool busy);
    void setStatusText(QString statusText);
    void setErrorMessage(QString errorMessage);
    void notifyNavigationAvailability();

    ScanFunction m_scan;
    ResolveFolderFunction m_resolveFolder;
    LibraryItemModel m_items;
    QVector<core::ListItem> m_currentItems;
    QVector<core::ListItem> m_visibleItems;
    QString m_currentFolderPath;
    QString m_navigationRootPath;
    core::SortState m_sort;
    bool m_includeSubfolders = false;
    QString m_searchQuery;
    bool m_busy = false;
    QString m_statusText = QStringLiteral("就緒。PicLens 已初始化。");
    QString m_errorMessage;
    QStringList m_selectedPaths;
    QVector<HistoryEntry> m_history;
    int m_historyIndex = -1;
    quint64 m_scanGeneration = 0;
    std::shared_ptr<std::stop_source> m_activeScanStop;
    QThreadPool m_scanPool;
};

} // namespace piclens::presentation
