import QtQuick

// Mock-controller fixture; requires the application's PicLens.Native module.
// Use the production binary with --components for the supported preview.
// Mock data only. This entry point never opens or changes real files.
Item {
    id: showcase
    QtObject {
        id: app
        signal scrollTo(int row)
        signal focusGallery
        function attachItem(item) {
        }
        property var library: libraryModel
        property var tree: treeModel
        property string folder: "/展示圖庫"
        property string rootPath: "/展示圖庫"
        property string search: ""
        property int sortIndex: 0
        property bool recursive: false
        property int thumbnailSize: 160
        property bool sidebarCollapsed: false
        property string status: "元件展示 · 不會變更檔案"
        property string settingsError: ""
        property bool busy: false
        property int count: libraryModel.count
        property int selectionCount: 0
        property bool viewerOpen: false
        property string viewerName: "展示圖片.png"
        property string viewerError: ""
        property int viewerIndex: 0
        property int viewerCount: 2
        property real zoom: 1
        property string confirmTitle: "確認作業"
        property string confirmText: "這是元件展示，不會變更任何檔案。"
        property bool confirmOpen: false
        property bool renameOpen: false
        property string renameStem: "展示圖片"
        property var results: [
            {
                source: "/展示圖庫/原始圖片.png",
                target: "/展示圖庫/新檔名.png",
                status: "成功",
                reason: "展示資料"
            }
        ]
        property bool resultsOpen: false
        property string toastText: "展示作業已完成"
        property bool toastOpen: false
        property bool toastError: false
        property bool dark: false
        function pick(url) {
            status = "展示模式不讀取資料夾";
        }
        function navigate(path) {
            folder = path;
        }
        function history(delta) {
        }
        function refresh() {
        }
        function toggleTree(index) {
            treeModel.setProperty(index, "expanded", !treeModel.get(index).expanded);
        }
        function activateTree(index) {
            folder = treeModel.get(index).path;
        }
        function select(index, modifiers, rightClick) {
            viewerIndex = index;
            for (let i = 0; i < libraryModel.count; ++i)
                libraryModel.setProperty(i, "selected", i === index);
            selectionCount = 1;
        }
        function openViewer(index) {
            if (index !== undefined && index >= 0)
                viewerIndex = index;
            viewerOpen = true;
        }
        function closeViewer() {
            viewerOpen = false;
        }
        function viewerStep(delta) {
            viewerIndex = Math.max(0, Math.min(viewerCount - 1, viewerIndex + delta));
        }
        function zoomBy(factor) {
            zoom *= factor;
        }
        function resetZoom() {
            zoom = 1;
        }
        function reveal(path) {
        }
        function clearSelection() {
            for (let i = 0; i < libraryModel.count; ++i)
                libraryModel.setProperty(i, "selected", false);
            selectionCount = 0;
        }
        function requestOperation(kind, target) {
            if (kind === "rename")
                renameOpen = true;
            else
                confirmOpen = true;
        }
        function confirm(accepted) {
            confirmOpen = false;
            renameOpen = false;
            if (accepted)
                toastOpen = true;
        }
        function cancelBatch() {
            busy = false;
        }
        function retrySettings() {
            settingsError = "";
        }
        function dropRename(target) {
            toastOpen = true;
        }
        function setVisible(paths) {
        }
        function moveSelection(delta, extend) {
            select(Math.max(0, Math.min(libraryModel.count - 1, viewerIndex + delta)), 0, false);
        }
        function setDragActive(active) {
        }
        function clearToast() {
            toastOpen = false;
        }
    }
    ListModel {
        id: libraryModel
        ListElement {
            path: "/展示圖庫/資料夾"
            name: "資料夾"
            folder: true
            animated: false
            selected: false
            imageKey: ""
            error: ""
            detail: "子資料夾"
        }
        ListElement {
            path: "/展示圖庫/圖片.png"
            name: "很長的繁體中文圖片名稱與 Unicode 測試.png"
            folder: false
            animated: false
            selected: false
            imageKey: ""
            error: ""
            detail: "1920 × 1080"
        }
        ListElement {
            path: "/展示圖庫/損毀.gif"
            name: "損毀.gif"
            folder: false
            animated: true
            selected: false
            imageKey: ""
            error: "無法解碼圖片"
            detail: ""
        }
    }
    ListModel {
        id: treeModel
        ListElement {
            path: "/展示圖庫/資料夾"
            name: "資料夾"
            depth: 0
            expanded: true
            loading: false
        }
        ListElement {
            path: "/展示圖庫/資料夾/子資料夾"
            name: "子資料夾"
            depth: 1
            expanded: false
            loading: false
        }
    }
    Main {
        id: mainWindow
    }
    Timer {
        interval: 200
        repeat: true
        running: Qt.application.arguments.indexOf("--smoke") >= 0
        property int step: 0
        onTriggered: {
            ++step;
            if (step === 1) {
                app.dark = true;
                mainWindow.width = 800;
                mainWindow.height = 600;
            }
            if (step === 2)
                app.requestOperation("jpg");
            if (step === 3) {
                app.confirm(false);
                app.requestOperation("rename");
            }
            if (step === 4) {
                app.confirm(true);
                app.resultsOpen = true;
            }
            if (step === 5) {
                app.resultsOpen = false;
                app.openViewer(1);
            }
            if (step === 6) {
                app.closeViewer();
                app.dark = false;
            }
            if (step === 7)
                Qt.quit();
        }
    }
}
