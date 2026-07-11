#include <piclens/presentation/library_controller.h>

#include <piclens/core/list_item_sorter.h>
#include <piclens/core/path_rules.h>

#include <QFileInfo>
#include <QFutureWatcher>
#include <QtConcurrentRun>

#include <exception>
#include <stdexcept>
#include <utility>

namespace piclens::presentation {
namespace {

struct ScanTaskResult {
    QVector<core::ListItem> items;
    QString errorMessage;
    bool canceled = false;
};

QString sortOptionLabel(core::SortState sort)
{
    if (sort.key == core::SortKey::Name && sort.direction == core::SortDirection::Asc) {
        return QStringLiteral("名稱由小到大");
    }
    if (sort.key == core::SortKey::Name && sort.direction == core::SortDirection::Desc) {
        return QStringLiteral("名稱由大到小");
    }
    if (sort.key == core::SortKey::ModifiedAt && sort.direction == core::SortDirection::Asc) {
        return QStringLiteral("修改時間最舊到最新");
    }
    return QStringLiteral("修改時間最新到最舊");
}

} // namespace

LibraryController::LibraryController(
    ScanFunction scan,
    ResolveFolderFunction resolveFolder,
    QObject *parent)
    : QObject(parent)
    , m_scan(std::move(scan))
    , m_resolveFolder(std::move(resolveFolder))
    , m_items(this)
{
    if (!m_scan || !m_resolveFolder) {
        throw std::invalid_argument("Library controller dependencies are required.");
    }
    m_scanPool.setMaxThreadCount(2);
    m_scanPool.setExpiryTimeout(30'000);
}

LibraryController::~LibraryController()
{
    if (m_activeScanStop) {
        m_activeScanStop->request_stop();
    }
    m_scanPool.waitForDone();
}

QString LibraryController::currentFolderPath() const
{
    return m_currentFolderPath;
}

QString LibraryController::currentFolderName() const
{
    if (m_currentFolderPath.isEmpty()) {
        return QStringLiteral("未選擇資料夾");
    }
    const QString name = QFileInfo(m_currentFolderPath).fileName();
    return name.isEmpty() ? m_currentFolderPath : name;
}

QString LibraryController::navigationRootPath() const
{
    return m_navigationRootPath;
}

bool LibraryController::includeSubfolders() const
{
    return m_includeSubfolders;
}

int LibraryController::sortKey() const
{
    return static_cast<int>(m_sort.key);
}

int LibraryController::sortDirection() const
{
    return static_cast<int>(m_sort.direction);
}

QString LibraryController::sortLabel() const
{
    return sortOptionLabel(m_sort);
}

QString LibraryController::recursiveModeLabel() const
{
    return m_includeSubfolders
        ? QStringLiteral("含子資料夾")
        : QStringLiteral("僅目前資料夾");
}

QString LibraryController::searchQuery() const
{
    return m_searchQuery;
}

bool LibraryController::hasSearchQuery() const
{
    return !m_searchQuery.trimmed().isEmpty();
}

bool LibraryController::busy() const
{
    return m_busy;
}

QString LibraryController::statusText() const
{
    return m_statusText;
}

QString LibraryController::errorMessage() const
{
    return m_errorMessage;
}

bool LibraryController::canGoBack() const
{
    return m_historyIndex > 0;
}

bool LibraryController::canGoForward() const
{
    return m_historyIndex >= 0 && m_historyIndex < m_history.size() - 1;
}

int LibraryController::selectedCount() const
{
    return m_selectedPaths.size();
}

bool LibraryController::hasSelectedImages() const
{
    return !m_selectedPaths.isEmpty();
}

bool LibraryController::hasSingleSelectedImage() const
{
    return m_selectedPaths.size() == 1;
}

QString LibraryController::selectionSummary() const
{
    if (m_selectedPaths.isEmpty()) {
        return {};
    }
    return m_selectedPaths.size() == 1
        ? QStringLiteral("已選取 1 張圖片")
        : QStringLiteral("已選取 %1 張圖片").arg(m_selectedPaths.size());
}

QStringList LibraryController::selectedPaths() const
{
    return m_selectedPaths;
}

bool LibraryController::containsImagePath(const QString &path) const
{
    return std::any_of(m_visibleItems.cbegin(), m_visibleItems.cend(), [&](const core::ListItem &item) {
        const auto *image = std::get_if<core::ImageListItem>(&item);
        return image && core::path_rules::pathEquals(image->path, path);
    });
}

QVector<core::ImageListItem> LibraryController::visibleImages() const
{
    QVector<core::ImageListItem> images;
    images.reserve(m_visibleItems.size());
    for (const auto &item : m_visibleItems) {
        if (const auto *image = std::get_if<core::ImageListItem>(&item)) {
            images.append(*image);
        }
    }
    return images;
}

std::optional<core::ImageSequenceSnapshot> LibraryController::createImageSequenceSnapshot(
    const QString &requestedPath,
    bool preferSelectionOrder) const
{
    const QVector<core::ImageListItem> images = visibleImages();
    if (images.isEmpty()) {
        return std::nullopt;
    }
    const QString targetPath = preferSelectionOrder && !m_selectedPaths.isEmpty()
        ? m_selectedPaths.constFirst()
        : requestedPath;
    const auto match = std::find_if(images.cbegin(), images.cend(), [&](const core::ImageListItem &image) {
        return core::path_rules::pathEquals(image.path, targetPath);
    });
    if (match == images.cend()) {
        return std::nullopt;
    }
    return core::ImageSequenceSnapshot{
        .sourceFolderPath = m_currentFolderPath,
        .includeSubfolders = m_includeSubfolders,
        .sort = m_sort,
        .images = images,
        .currentIndex = static_cast<int>(std::distance(images.cbegin(), match)),
    };
}

LibraryItemModel *LibraryController::items()
{
    return &m_items;
}

void LibraryController::applyInitialSettings(const core::AppSettings &settings)
{
    const bool sortChangedValue = m_sort != settings.sort;
    const bool recursiveChangedValue = m_includeSubfolders != settings.includeSubfolders;
    m_sort = settings.sort;
    m_includeSubfolders = settings.includeSubfolders;
    if (sortChangedValue) {
        emit sortChanged();
    }
    if (recursiveChangedValue) {
        emit includeSubfoldersChanged();
    }
}

void LibraryController::navigateToFolder(
    const QString &folderPath,
    bool replaceHistory,
    bool persist,
    const QString &navigationRootPath)
{
    const auto resolved = m_resolveFolder(folderPath);
    if (!resolved.has_value()) {
        setStatusText(QStringLiteral("資料夾無法使用：%1").arg(folderPath));
        return;
    }

    const QString rootPath = navigationRootPath.isEmpty()
        ? (m_navigationRootPath.isEmpty() ? *resolved : m_navigationRootPath)
        : navigationRootPath;
    setNavigationRootPath(rootPath);
    setCurrentFolderPath(*resolved);
    emit navigationStateChanged();
    recordHistory({.folderPath = *resolved, .rootPath = rootPath}, replaceHistory);
    if (persist) {
        emit lastFolderPersistenceRequested(*resolved);
    }
    requestScan();
}

void LibraryController::reload()
{
    requestScan();
}

void LibraryController::goBack()
{
    if (!canGoBack()) {
        return;
    }
    --m_historyIndex;
    notifyNavigationAvailability();
    const HistoryEntry entry = m_history.at(m_historyIndex);
    setNavigationRootPath(entry.rootPath);
    setCurrentFolderPath(entry.folderPath);
    emit navigationStateChanged();
    requestScan();
}

void LibraryController::goForward()
{
    if (!canGoForward()) {
        return;
    }
    ++m_historyIndex;
    notifyNavigationAvailability();
    const HistoryEntry entry = m_history.at(m_historyIndex);
    setNavigationRootPath(entry.rootPath);
    setCurrentFolderPath(entry.folderPath);
    emit navigationStateChanged();
    requestScan();
}

void LibraryController::changeSort(core::SortState sort)
{
    if (m_sort == sort) {
        return;
    }
    const bool scanWasActive = m_busy;
    m_sort = sort;
    clearSelection();
    emit sortChanged();
    emit sortPersistenceRequested(sortKey(), sortDirection());
    setStatusText(QStringLiteral("排序已變更為 %1。").arg(sortLabel()));
    if (scanWasActive) {
        requestScan();
        return;
    }
    m_currentItems = core::list_item_sorter::sort(
        m_currentItems,
        m_sort,
        !m_includeSubfolders);
    applySearchFilter();
}

void LibraryController::setIncludeSubfolders(bool includeSubfolders)
{
    if (m_includeSubfolders == includeSubfolders) {
        return;
    }
    m_includeSubfolders = includeSubfolders;
    emit includeSubfoldersChanged();
    emit includeSubfoldersPersistenceRequested(includeSubfolders);
    requestScan();
}

void LibraryController::setSearchQuery(const QString &searchQuery)
{
    if (m_searchQuery == searchQuery) {
        return;
    }
    m_searchQuery = searchQuery;
    clearSelection();
    applySearchFilter();
    emit searchQueryChanged();
    setStatusText(hasSearchQuery()
        ? QStringLiteral("搜尋「%1」：%2 個項目。")
              .arg(m_searchQuery.trimmed())
              .arg(m_items.rowCount())
        : QStringLiteral("已顯示 %1 個項目。").arg(m_items.rowCount()));
}

void LibraryController::setSelectedPaths(const QStringList &paths)
{
    const QStringList availableImages = m_items.imagePaths();
    QSet<QString> availableKeys;
    for (const QString &path : availableImages) {
        availableKeys.insert(core::path_rules::pathKey(path));
    }

    QStringList selected;
    QSet<QString> selectedKeys;
    for (const QString &path : paths) {
        const QString key = core::path_rules::pathKey(path);
        if (availableKeys.contains(key) && !selectedKeys.contains(key)) {
            selected.append(path);
            selectedKeys.insert(key);
        }
    }
    if (selected == m_selectedPaths) {
        return;
    }
    m_selectedPaths = std::move(selected);
    m_items.setSelectedPathKeys(std::move(selectedKeys));
    emit selectionChanged();
}

void LibraryController::selectPath(
    const QString &path,
    bool controlModifier,
    bool shiftModifier)
{
    int targetRow = -1;
    for (int row = 0; row < m_visibleItems.size(); ++row) {
        const auto *image = std::get_if<core::ImageListItem>(&m_visibleItems.at(row));
        if (image && core::path_rules::pathEquals(image->path, path)) {
            targetRow = row;
            break;
        }
    }
    if (targetRow < 0) {
        return;
    }

    if (shiftModifier && !m_selectedPaths.isEmpty()) {
        int anchorRow = -1;
        const QString anchorPath = m_selectedPaths.constLast();
        for (int row = 0; row < m_visibleItems.size(); ++row) {
            const auto *image = std::get_if<core::ImageListItem>(&m_visibleItems.at(row));
            if (image && core::path_rules::pathEquals(image->path, anchorPath)) {
                anchorRow = row;
                break;
            }
        }
        if (anchorRow >= 0) {
            const int first = std::min(anchorRow, targetRow);
            const int last = std::max(anchorRow, targetRow);
            QStringList range;
            for (int row = first; row <= last; ++row) {
                if (const auto *image = std::get_if<core::ImageListItem>(&m_visibleItems.at(row))) {
                    range.append(image->path);
                }
            }
            setSelectedPaths(range);
            return;
        }
    }

    if (controlModifier) {
        QStringList selected = m_selectedPaths;
        const auto match = std::find_if(selected.cbegin(), selected.cend(), [&](const QString &selectedPath) {
            return core::path_rules::pathEquals(selectedPath, path);
        });
        if (match == selected.cend()) {
            selected.append(path);
        } else {
            selected.removeAt(static_cast<int>(std::distance(selected.cbegin(), match)));
        }
        setSelectedPaths(selected);
        return;
    }

    setSelectedPaths({path});
}

void LibraryController::prepareContextSelection(const QString &path)
{
    const bool alreadySelected = std::any_of(
        m_selectedPaths.cbegin(),
        m_selectedPaths.cend(),
        [&](const QString &selectedPath) {
            return core::path_rules::pathEquals(selectedPath, path);
        });
    if (!alreadySelected) {
        selectPath(path, false, false);
    }
}

void LibraryController::clearSelection()
{
    if (m_selectedPaths.isEmpty()) {
        return;
    }
    m_selectedPaths.clear();
    m_items.setSelectedPathKeys({});
    emit selectionChanged();
}

void LibraryController::refreshAfterFileOperation()
{
    requestScan();
}

void LibraryController::setExternalStatus(QString statusText)
{
    setStatusText(std::move(statusText));
}

void LibraryController::requestScan()
{
    clearSelection();
    if (m_activeScanStop) {
        m_activeScanStop->request_stop();
    }
    const quint64 generation = ++m_scanGeneration;

    if (m_currentFolderPath.isEmpty()) {
        m_currentItems.clear();
        m_visibleItems.clear();
        m_items.replaceItems({});
        setBusy(false);
        return;
    }

    auto stop = std::make_shared<std::stop_source>();
    m_activeScanStop = stop;
    const core::ListQuery query{
        .folderPath = m_currentFolderPath,
        .includeSubfolders = m_includeSubfolders,
        .sort = m_sort,
    };
    const ScanFunction scan = m_scan;
    setErrorMessage({});
    setBusy(true);

    auto *watcher = new QFutureWatcher<ScanTaskResult>(this);
    connect(watcher, &QFutureWatcher<ScanTaskResult>::finished, this, [this, watcher, generation] {
        ScanTaskResult result = watcher->result();
        watcher->deleteLater();
        if (generation != m_scanGeneration) {
            return;
        }
        m_activeScanStop.reset();
        if (result.canceled) {
            setBusy(false);
            return;
        }
        if (!result.errorMessage.isEmpty()) {
            m_currentItems.clear();
            m_visibleItems.clear();
            m_items.replaceItems({});
            setErrorMessage(QStringLiteral("無法載入資料夾：%1").arg(result.errorMessage));
            setStatusText(m_errorMessage);
            emit scanFailed(m_currentFolderPath, result.errorMessage);
            setBusy(false);
            return;
        }

        m_currentItems = std::move(result.items);
        applySearchFilter();
        setStatusText(hasSearchQuery()
            ? QStringLiteral("搜尋「%1」：%2 個項目。")
                  .arg(m_searchQuery.trimmed())
                  .arg(m_items.rowCount())
            : QStringLiteral("已載入 %1 個項目。").arg(m_items.rowCount()));
        setBusy(false);
    });

    watcher->setFuture(QtConcurrent::run(&m_scanPool, [scan, query, stop] {
        try {
            return ScanTaskResult{
                .items = scan(query, stop->get_token()),
                .errorMessage = {},
                .canceled = false,
            };
        } catch (const std::exception &exception) {
            return ScanTaskResult{
                .items = {},
                .errorMessage = QString::fromUtf8(exception.what()),
                .canceled = stop->stop_requested(),
            };
        } catch (...) {
            return ScanTaskResult{
                .items = {},
                .errorMessage = QStringLiteral("未知錯誤"),
                .canceled = stop->stop_requested(),
            };
        }
    }));
}

void LibraryController::applySearchFilter()
{
    const QString query = m_searchQuery.trimmed();
    if (query.isEmpty()) {
        m_visibleItems = m_currentItems;
    } else {
        m_visibleItems.clear();
        m_visibleItems.reserve(m_currentItems.size());
        for (const core::ListItem &item : m_currentItems) {
            const QString &name = core::list_item_sorter::itemName(item);
            const QString &path = core::list_item_sorter::itemPath(item);
            if (name.contains(query, Qt::CaseInsensitive)
                || path.contains(query, Qt::CaseInsensitive)) {
                m_visibleItems.append(item);
            }
        }
    }
    m_items.replaceItems(m_visibleItems);
}

void LibraryController::recordHistory(HistoryEntry entry, bool replaceHistory)
{
    const bool oldCanBack = canGoBack();
    const bool oldCanForward = canGoForward();
    if (replaceHistory) {
        m_history = {std::move(entry)};
        m_historyIndex = 0;
    } else if (m_historyIndex < 0
               || !core::path_rules::pathEquals(m_history.at(m_historyIndex).folderPath, entry.folderPath)
               || !core::path_rules::pathEquals(m_history.at(m_historyIndex).rootPath, entry.rootPath)) {
        if (m_historyIndex < m_history.size() - 1) {
            m_history.resize(m_historyIndex + 1);
        }
        m_history.append(std::move(entry));
        m_historyIndex = m_history.size() - 1;
    }
    if (oldCanBack != canGoBack() || oldCanForward != canGoForward()) {
        notifyNavigationAvailability();
    }
}

void LibraryController::setNavigationRootPath(QString rootPath)
{
    if (core::path_rules::pathEquals(m_navigationRootPath, rootPath)) {
        return;
    }
    m_navigationRootPath = std::move(rootPath);
    emit navigationRootPathChanged();
}

void LibraryController::setCurrentFolderPath(QString folderPath)
{
    if (core::path_rules::pathEquals(m_currentFolderPath, folderPath)) {
        return;
    }
    m_currentFolderPath = std::move(folderPath);
    emit currentFolderPathChanged();
}

void LibraryController::setBusy(bool busy)
{
    if (m_busy == busy) {
        return;
    }
    m_busy = busy;
    emit busyChanged();
}

void LibraryController::setStatusText(QString statusText)
{
    if (m_statusText == statusText) {
        return;
    }
    m_statusText = std::move(statusText);
    emit statusTextChanged();
}

void LibraryController::setErrorMessage(QString errorMessage)
{
    if (m_errorMessage == errorMessage) {
        return;
    }
    m_errorMessage = std::move(errorMessage);
    emit errorMessageChanged();
}

void LibraryController::notifyNavigationAvailability()
{
    emit navigationAvailabilityChanged();
}

} // namespace piclens::presentation
