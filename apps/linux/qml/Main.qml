import QtQuick
import QtQuick.Controls.Basic
import QtQuick.Layouts
import QtQuick.Dialogs
import PicLens.Native 1.0

ApplicationWindow {
    id: window
    readonly property bool componentsMode: typeof showComponents !== "undefined" && showComponents
    readonly property bool viewerShowing: app.viewerOpen
    readonly property bool toastShowing: app.toastOpen
    readonly property string currentToastText: app.toastText
    onViewerShowingChanged: returnFocus()
    onToastShowingChanged: {
        if (toastShowing)
            toastTimer.restart();
        else
            toastTimer.stop();
    }
    onCurrentToastTextChanged: if (toastShowing)
        toastTimer.restart()
    width: typeof launchWidth !== "undefined" ? launchWidth : 1600
    height: typeof launchHeight !== "undefined" ? launchHeight : 1000
    minimumWidth: 800
    minimumHeight: 600
    visible: true
    title: app.viewerOpen ? app.viewerName + " — PicLens" : "PicLens"
    color: appTheme.background
    font.family: "Noto Sans CJK TC"
    font.pixelSize: 14
    palette.window: appTheme.background
    palette.windowText: appTheme.foreground
    palette.base: appTheme.background
    palette.text: appTheme.foreground
    palette.button: appTheme.muted
    palette.buttonText: appTheme.foreground
    palette.highlight: appTheme.primary
    palette.highlightedText: appTheme.primaryForeground
    palette.mid: appTheme.border
    palette.dark: appTheme.secondary
    Theme {
        id: appTheme
        dark: app.dark
    }
    FolderDialog {
        id: picker
        title: "選擇圖庫資料夾"
        onAccepted: app.pick(selectedFolder)
    }
    TapHandler {
        parent: window.contentItem
        acceptedButtons: Qt.BackButton | Qt.ForwardButton
        enabled: !app.viewerOpen && !app.busy && !app.confirmOpen && !app.renameOpen && !app.resultsOpen
        onTapped: (point, button) => app.history(button === Qt.BackButton ? -1 : 1)
    }

    function returnFocus() {
        if (app.viewerOpen)
            viewerCanvas.forceActiveFocus();
        else
            gallery.focusGrid();
    }
    Shortcut {
        sequence: "Escape"
        enabled: app.confirmOpen || app.renameOpen || app.resultsOpen
        onActivated: {
            if (app.confirmOpen || app.renameOpen)
                app.confirm(false);
            else
                app.resultsOpen = false;
        }
    }
    Shortcut {
        sequence: "Ctrl+O"
        onActivated: picker.open()
    }
    Shortcut {
        sequence: "Ctrl+F"
        enabled: !app.viewerOpen
        onActivated: {
            searchField.forceActiveFocus();
            searchField.selectAll();
        }
    }
    Shortcut {
        sequence: "F5"
        onActivated: app.refresh()
    }
    Shortcut {
        sequence: "Alt+Left"
        enabled: !app.viewerOpen
        onActivated: app.history(-1)
    }
    Shortcut {
        sequence: "Alt+Right"
        enabled: !app.viewerOpen
        onActivated: app.history(1)
    }

    header: ColumnLayout {
        spacing: 0
        RowLayout {
            Layout.fillWidth: true
            Layout.margins: 12
            spacing: 8
            ActionButton {
                theme: appTheme
                glyph: app.viewerOpen ? "arrow-left" : "panel-left"
                hint: app.viewerOpen ? "返回圖庫" : "切換側欄"
                onClicked: app.viewerOpen ? app.closeViewer() : app.sidebarCollapsed = !app.sidebarCollapsed
            }
            Label {
                text: "PicLens"
                font.pixelSize: 20
                font.bold: true
                color: appTheme.foreground
            }
            Label {
                Layout.fillWidth: true
                text: app.viewerOpen ? app.viewerName : app.folder
                elide: Text.ElideMiddle
                color: appTheme.secondary
            }
            ActionButton {
                theme: appTheme
                text: app.dark ? "淺色" : "深色"
                hint: "切換明暗主題"
                onClicked: app.dark = !app.dark
            }
            ActionButton {
                theme: appTheme
                text: "開啟資料夾"
                objectName: "openFolderButton"
                glyph: "folder-open"
                prominent: true
                enabled: !app.busy
                onClicked: picker.open()
            }
        }
        Rectangle {
            Layout.fillWidth: true
            implicitHeight: 1
            color: appTheme.border
        }
        RowLayout {
            visible: !!app.settingsError
            Layout.fillWidth: true
            Layout.margins: visible ? 8 : 0
            Label {
                Layout.fillWidth: true
                text: "設定無法儲存：" + app.settingsError
                wrapMode: Text.Wrap
                color: appTheme.danger
            }
            ActionButton {
                theme: appTheme
                text: "重試"
                onClicked: app.retrySettings()
            }
        }
    }

    SplitView {
        anchors.fill: parent
        visible: !app.viewerOpen
        orientation: Qt.Horizontal
        handle: Rectangle {
            implicitWidth: 1
            color: appTheme.border
        }
        Rectangle {
            visible: !app.sidebarCollapsed
            SplitView.preferredWidth: 208
            SplitView.minimumWidth: 160
            SplitView.maximumWidth: 300
            color: appTheme.sidebar
            ColumnLayout {
                anchors.fill: parent
                anchors.margins: 12
                spacing: 8
                Label {
                    text: "資料夾"
                    color: appTheme.secondary
                    font.pixelSize: 12
                }
                ActionButton {
                    id: rootButton
                    theme: appTheme
                    Layout.fillWidth: true
                    text: app.rootPath ? app.rootPath : "選擇圖庫"
                    glyph: "folder-open"
                    hint: app.rootPath || "選擇圖庫"
                    onClicked: app.rootPath ? app.navigate(app.rootPath) : picker.open()
                    contentItem: Label {
                        text: rootButton.text
                        color: appTheme.foreground
                        elide: Text.ElideMiddle
                        verticalAlignment: Text.AlignVCenter
                    }
                }
                ListView {
                    id: treeView
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    clip: true
                    model: app.tree
                    boundsBehavior: Flickable.StopAtBounds
                    ScrollBar.vertical: ScrollBar {}
                    delegate: RowLayout {
                        id: branch
                        required property int index
                        required property string path
                        required property string name
                        required property int depth
                        required property bool expanded
                        required property bool loading
                        width: treeView.width - 12
                        height: 36
                        spacing: 4
                        Item {
                            Layout.preferredWidth: Math.min(branch.depth * 16, treeView.width / 3)
                        }
                        ActionButton {
                            theme: appTheme
                            implicitWidth: 28
                            implicitHeight: 28
                            glyph: "chevron-right"
                            rotation: branch.expanded ? 90 : 0
                            hint: (branch.expanded ? "收合 " : "展開 ") + branch.name
                            enabled: !branch.loading
                            onClicked: app.toggleTree(branch.index)
                        }
                        ActionButton {
                            id: branchButton
                            theme: appTheme
                            Layout.fillWidth: true
                            text: branch.loading ? "載入中…" : branch.name
                            hint: branch.path
                            onClicked: app.activateTree(branch.index)
                            contentItem: Label {
                                text: branchButton.text
                                elide: Text.ElideRight
                                verticalAlignment: Text.AlignVCenter
                                color: appTheme.foreground
                            }
                            background: Rectangle {
                                radius: 6
                                color: app.folder === branch.path || branchButton.hovered ? appTheme.muted : "transparent"
                                border.width: branchButton.activeFocus ? 2 : 0
                                border.color: appTheme.secondary
                            }
                            Keys.onRightPressed: if (!branch.expanded)
                                app.toggleTree(branch.index)
                            Keys.onLeftPressed: if (branch.expanded)
                                app.toggleTree(branch.index)
                        }
                    }
                }
            }
        }
        Item {
            SplitView.fillWidth: true
            ColumnLayout {
                anchors.fill: parent
                anchors.margins: window.width < 1000 ? 16 : 20
                spacing: 12
                GridLayout {
                    Layout.fillWidth: true
                    columns: width < 820 ? 1 : 2
                    columnSpacing: 12
                    rowSpacing: 8
                    RowLayout {
                        Layout.fillWidth: true
                        spacing: 8
                        ActionButton {
                            theme: appTheme
                            glyph: "arrow-left"
                            hint: "上一個資料夾（Alt+Left）"
                            onClicked: app.history(-1)
                        }
                        ActionButton {
                            theme: appTheme
                            glyph: "arrow-right"
                            hint: "下一個資料夾（Alt+Right）"
                            onClicked: app.history(1)
                        }
                        ActionButton {
                            theme: appTheme
                            glyph: "refresh-cw"
                            hint: "重新整理（F5）"
                            enabled: !app.busy
                            onClicked: app.refresh()
                        }
                        TextField {
                            id: searchField
                            Layout.fillWidth: true
                            Layout.minimumWidth: 100
                            placeholderText: "搜尋檔名…"
                            placeholderTextColor: appTheme.secondary
                            text: app.search
                            selectByMouse: true
                            onTextEdited: app.search = text
                            Accessible.name: "搜尋圖庫"
                            rightPadding: searchClearButton.width + 8
                            ActionButton {
                                id: searchClearButton
                                anchors.right: parent.right
                                anchors.rightMargin: 4
                                anchors.verticalCenter: parent.verticalCenter
                                theme: appTheme
                                implicitWidth: 28
                                implicitHeight: 28
                                glyph: "x"
                                hint: "清除搜尋"
                                visible: app.search.length > 0
                                enabled: !app.busy
                                onClicked: {
                                    app.search = "";
                                    searchField.forceActiveFocus();
                                }
                            }
                        }
                    }
                    RowLayout {
                        Layout.fillWidth: true
                        spacing: 8
                        NeutralComboBox {
                            theme: appTheme
                            Layout.preferredWidth: 150
                            model: ["名稱（升冪）", "名稱（降冪）", "修改時間（最舊優先）", "修改時間（最新優先）"]
                            currentIndex: app.sortIndex
                            onActivated: app.sortIndex = currentIndex
                            Accessible.name: "排序方式"
                        }
                        NeutralCheckBox {
                            theme: appTheme
                            text: "包含子資料夾"
                            checked: app.recursive
                            onToggled: app.recursive = checked
                        }
                        NeutralSlider {
                            theme: appTheme
                            Layout.fillWidth: true
                            Layout.minimumWidth: 64
                            Layout.maximumWidth: 160
                            from: 120
                            to: 240
                            stepSize: 20
                            value: app.thumbnailSize
                            onMoved: app.thumbnailSize = value
                            Accessible.name: "縮圖大小"
                        }
                    }
                }
                Flow {
                    Layout.fillWidth: true
                    Layout.preferredHeight: childrenRect.height
                    spacing: 8
                    ActionButton {
                        theme: appTheme
                        text: "轉為 JPG"
                        hint: "將目前搜尋與篩選結果轉為 JPG"
                        enabled: app.count > 0 && !app.busy
                        onClicked: app.requestOperation("jpg")
                    }
                    ActionButton {
                        theme: appTheme
                        text: "轉為 WebP"
                        hint: "將目前搜尋與篩選結果轉為 WebP"
                        enabled: app.count > 0 && !app.busy
                        onClicked: app.requestOperation("webp")
                    }
                    ActionButton {
                        theme: appTheme
                        text: "清理同名格式"
                        hint: "清理目前搜尋與篩選結果中的同名格式，保留 JPG／JPEG 與 WebP"
                        enabled: app.count > 0 && !app.busy
                        onClicked: app.requestOperation("cleanup")
                    }
                    ActionButton {
                        theme: appTheme
                        glyph: "pencil"
                        text: "重新命名"
                        enabled: app.selectionCount === 1 && !app.busy
                        onClicked: app.requestOperation("rename")
                    }
                    ActionButton {
                        theme: appTheme
                        glyph: "trash-2"
                        text: "移到回收筒"
                        destructive: true
                        enabled: app.selectionCount > 0 && !app.busy
                        onClicked: app.requestOperation("trash")
                    }
                    ActionButton {
                        theme: appTheme
                        text: "取消選取"
                        enabled: app.selectionCount > 0
                        onClicked: app.clearSelection()
                    }
                }
                Item {
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    Gallery {
                        id: gallery
                        anchors.fill: parent
                        theme: appTheme
                        onContextRequested: (path, folder, x, y) => {
                            contextMenu.targetPath = path;
                            contextMenu.isFolder = folder;
                            let p = gallery.mapToItem(window.contentItem, x, y);
                            contextMenu.popup(p.x, p.y);
                        }
                    }
                    ColumnLayout {
                        anchors.centerIn: parent
                        width: Math.min(parent.width - 40, 360)
                        visible: app.count === 0
                        Label {
                            Layout.fillWidth: true
                            horizontalAlignment: Text.AlignHCenter
                            text: app.busy ? "正在載入圖庫…" : app.rootPath ? "沒有符合條件的圖片" : "從資料夾開始瀏覽"
                            color: appTheme.foreground
                            font.pixelSize: 24
                            wrapMode: Text.Wrap
                        }
                        Label {
                            Layout.fillWidth: true
                            horizontalAlignment: Text.AlignHCenter
                            text: app.rootPath ? "試著清除搜尋，或切換資料夾。" : "選擇資料夾即可瀏覽圖片與子資料夾。"
                            color: appTheme.secondary
                            wrapMode: Text.Wrap
                        }
                        ActionButton {
                            Layout.alignment: Qt.AlignHCenter
                            theme: appTheme
                            text: app.rootPath && app.search ? "清除搜尋" : "開啟資料夾"
                            onClicked: app.rootPath && app.search ? app.search = "" : picker.open()
                        }
                    }
                }
            }
        }
    }

    Rectangle {
        anchors.fill: parent
        visible: app.viewerOpen
        color: "#09090b"
        Item {
            id: viewerCanvas
            anchors.fill: parent
            anchors.topMargin: viewerToolbar.height + 32
            focus: app.viewerOpen
            Keys.onPressed: event => {
                if (event.key === Qt.Key_Escape)
                    app.closeViewer();
                else if (event.key === Qt.Key_Left) {
                    if (app.zoom <= 1.01)
                        app.viewerStep(-1);
                } else if (event.key === Qt.Key_Right) {
                    if (app.zoom <= 1.01)
                        app.viewerStep(1);
                } else if (event.key === Qt.Key_Plus || event.key === Qt.Key_Equal)
                    app.zoomBy(1.2);
                else if (event.key === Qt.Key_Minus)
                    app.zoomBy(1 / 1.2);
                else if (event.key === Qt.Key_0)
                    app.resetZoom();
                else
                    return;
                event.accepted = true;
            }
            ImageItem {
                anchors.fill: parent
                // Native item owns wheel/pan and frame lifetime. Controller
                // forwards zoomChanged to app.zoom; no feedback binding here.
                Component.onCompleted: app.attachItem(this)
            }
            Label {
                anchors.centerIn: parent
                width: Math.min(parent.width - 48, 640)
                horizontalAlignment: Text.AlignHCenter
                wrapMode: Text.Wrap
                visible: app.viewerError.length > 0
                text: app.viewerError
                color: "#fca5a5"
            }
        }
        Theme {
            id: viewerTheme
            dark: true
        }
        RowLayout {
            id: viewerToolbar
            anchors.top: parent.top
            anchors.horizontalCenter: parent.horizontalCenter
            anchors.topMargin: 16
            spacing: 8
            ActionButton {
                theme: viewerTheme
                glyph: "chevron-left"
                hint: "上一張（←）"
                enabled: app.viewerIndex > 0
                onClicked: app.viewerStep(-1)
            }
            Label {
                text: (app.viewerIndex + 1) + " / " + app.viewerCount
                color: "#fafafa"
            }
            ActionButton {
                theme: viewerTheme
                glyph: "chevron-right"
                hint: "下一張（→）"
                enabled: app.viewerIndex + 1 < app.viewerCount
                onClicked: app.viewerStep(1)
            }
            ActionButton {
                theme: viewerTheme
                glyph: "minus"
                hint: "縮小（−）"
                onClicked: app.zoomBy(1 / 1.2)
            }
            ActionButton {
                theme: viewerTheme
                text: Math.round(app.zoom * 100) + "%"
                hint: "重設縮放（0）"
                onClicked: app.resetZoom()
            }
            ActionButton {
                theme: viewerTheme
                glyph: "plus"
                hint: "放大（+）"
                onClicked: app.zoomBy(1.2)
            }
            ActionButton {
                theme: viewerTheme
                glyph: "x"
                hint: "關閉檢視器（Esc）"
                onClicked: app.closeViewer()
            }
        }
    }

    footer: Rectangle {
        implicitHeight: 44
        color: appTheme.sidebar
        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 16
            anchors.rightMargin: 16
            spacing: 12
            BusyIndicator {
                running: app.busy
                visible: running
                implicitWidth: 28
                implicitHeight: 28
            }
            Label {
                Layout.fillWidth: true
                text: app.status
                elide: Text.ElideRight
                color: appTheme.secondary
            }
            Label {
                text: app.count + " 個項目 · 已選取 " + app.selectionCount
                color: appTheme.secondary
            }
            ActionButton {
                theme: appTheme
                text: "取消作業"
                visible: app.busy
                onClicked: app.cancelBatch()
            }
            ActionButton {
                theme: appTheme
                text: "作業結果"
                onClicked: app.resultsOpen = true
            }
        }
    }

    Menu {
        id: contextMenu
        property string targetPath: ""
        property bool isFolder: false
        MenuItem {
            text: "在檔案管理員中顯示"
            onTriggered: app.reveal(contextMenu.targetPath)
        }
        MenuSeparator {}
        MenuItem {
            text: "重新命名"
            enabled: !contextMenu.isFolder && app.selectionCount === 1 && !app.busy
            onTriggered: app.requestOperation("rename")
        }
        MenuItem {
            text: "移到回收筒"
            enabled: !contextMenu.isFolder && !app.busy
            palette.text: appTheme.danger
            onTriggered: app.requestOperation("trash")
        }
    }

    Dialog {
        id: confirmation
        anchors.centerIn: Overlay.overlay
        width: Math.min(window.width - 48, 480)
        modal: true
        title: app.confirmTitle
        visible: app.confirmOpen
        closePolicy: Popup.NoAutoClose
        contentItem: Label {
            text: app.confirmText
            wrapMode: Text.Wrap
            color: appTheme.foreground
        }
        footer: DialogButtonBox {
            Button {
                text: "取消"
                DialogButtonBox.buttonRole: DialogButtonBox.RejectRole
            }
            Button {
                text: "確認執行"
                DialogButtonBox.buttonRole: DialogButtonBox.AcceptRole
            }
            onAccepted: app.confirm(true)
            onRejected: app.confirm(false)
        }
        onClosed: window.returnFocus()
    }
    Dialog {
        id: renameDialog
        anchors.centerIn: Overlay.overlay
        width: Math.min(window.width - 48, 480)
        modal: true
        title: "重新命名"
        visible: app.renameOpen
        closePolicy: Popup.NoAutoClose
        contentItem: ColumnLayout {
            Label {
                text: "輸入新檔名，副檔名會保留。"
                color: appTheme.secondary
            }
            TextField {
                id: renameField
                Layout.fillWidth: true
                text: app.renameStem
                selectByMouse: true
                onTextEdited: app.renameStem = text
                Accessible.name: "新檔名"
                onAccepted: if (text.trim())
                    app.confirm(true)
            }
        }
        footer: DialogButtonBox {
            Button {
                text: "取消"
                DialogButtonBox.buttonRole: DialogButtonBox.RejectRole
            }
            Button {
                text: "重新命名"
                enabled: renameField.text.trim().length > 0
                DialogButtonBox.buttonRole: DialogButtonBox.AcceptRole
            }
            onAccepted: app.confirm(true)
            onRejected: app.confirm(false)
        }
        onOpened: {
            renameField.forceActiveFocus();
            renameField.selectAll();
        }
        onClosed: window.returnFocus()
    }
    Dialog {
        id: resultsDialog
        anchors.centerIn: Overlay.overlay
        width: Math.min(window.width - 48, 800)
        height: Math.min(window.height - 80, 640)
        modal: true
        title: "作業結果"
        visible: app.resultsOpen
        closePolicy: Popup.NoAutoClose
        contentItem: ScrollView {
            clip: true
            contentWidth: availableWidth
            ColumnLayout {
                width: resultsDialog.availableWidth
                spacing: 12
                Label {
                    visible: !app.results || app.results.length === 0
                    text: "目前沒有作業結果。"
                    color: appTheme.secondary
                }
                Repeater {
                    model: app.results
                    delegate: TextArea {
                        required property var modelData
                        Layout.fillWidth: true
                        readOnly: true
                        selectByMouse: true
                        wrapMode: TextEdit.Wrap
                        text: "來源：" + modelData.source + "\n目標：" + modelData.target + "\n狀態：" + modelData.status + (modelData.reason ? "\n說明：" + modelData.reason : "")
                        background: Rectangle {
                            color: appTheme.muted
                            radius: 6
                        }
                    }
                }
            }
        }
        footer: DialogButtonBox {
            Button {
                text: "關閉"
                DialogButtonBox.buttonRole: DialogButtonBox.RejectRole
            }
            onRejected: app.resultsOpen = false
        }
        onClosed: window.returnFocus()
    }
    Popup {
        id: toast
        parent: Overlay.overlay
        x: Math.max(16, parent.width - width - 24)
        y: Math.max(16, parent.height - height - 60)
        width: Math.min(window.width - 32, 480)
        visible: app.toastOpen
        closePolicy: Popup.NoAutoClose
        padding: 16
        background: Rectangle {
            radius: 10
            color: appTheme.card
            border.color: app.toastError ? appTheme.danger : appTheme.border
        }
        contentItem: RowLayout {
            Label {
                Layout.fillWidth: true
                text: (app.toastError ? "作業失敗：" : "") + app.toastText
                wrapMode: Text.Wrap
                color: appTheme.foreground
            }
            ActionButton {
                theme: appTheme
                text: "詳情"
                onClicked: {
                    app.resultsOpen = true;
                    app.clearToast();
                }
            }
            ActionButton {
                theme: appTheme
                glyph: "x"
                hint: "關閉通知"
                onClicked: app.clearToast()
            }
        }
        Timer {
            id: toastTimer
            interval: app.toastError ? 12000 : 6000
            onTriggered: app.clearToast()
        }
    }
    ComponentPanel {
        theme: appTheme
        visible: window.componentsMode
        width: Math.min(window.width - 48, 680)
        height: Math.min(window.height - 80, 700)
    }
}
