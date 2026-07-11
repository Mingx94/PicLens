pragma ComponentBehavior: Bound
import QtQuick
import QtQuick.Controls
import QtQuick.Dialogs
import QtQuick.Layouts
import PicLens

ApplicationWindow {
    id: window
    required property AppController appController
    width: 1220
    height: 820
    minimumWidth: 840
    minimumHeight: 580
    visible: true
    title: appController.viewer.open
           ? "PicLens - " + appController.viewer.currentName
           : appController.library.currentFolderName + " — PicLens"
    color: Theme.appBackground

    HistoryMouseHandler {
        anchors.fill: parent
        enabled: !window.appController.viewer.open
        z: 1000
        onBackRequested: window.appController.goBack()
        onForwardRequested: window.appController.goForward()
    }

    FolderDialog {
        id: folderDialog
        title: "選擇圖片資料夾"
        onAccepted: window.appController.openFolderUrl(selectedFolder)
    }

    Connections {
        target: window.appController
        function onFolderSelectionRequired() {
            if (!window.appController.folderSelectionSuppressed)
                folderDialog.open()
        }
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: Theme.commandHeight
            color: Theme.commandBar
            border.width: 1
            border.color: Theme.line

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: Theme.space4
                anchors.rightMargin: Theme.space4
                spacing: Theme.space2

                Text {
                    text: "PicLens"
                    color: Theme.primaryText
                    font.pixelSize: 18
                    font.weight: Font.DemiBold
                    Layout.rightMargin: Theme.space3
                }
                ToolbarButton {
                    symbol: "☰"
                    accessibleName: window.appController.sidebarOpen ? "收合側欄" : "展開側欄"
                    ToolTip.text: window.appController.sidebarOpen ? "收合側欄" : "展開側欄"
                    ToolTip.visible: hovered
                    onClicked: window.appController.toggleSidebar()
                }
                ToolbarButton {
                    symbol: "‹"
                    accessibleName: "上一個資料夾"
                    enabled: window.appController.library.canGoBack
                    ToolTip.text: "上一個資料夾"
                    ToolTip.visible: hovered
                    onClicked: window.appController.goBack()
                }
                ToolbarButton {
                    symbol: "›"
                    accessibleName: "下一個資料夾"
                    enabled: window.appController.library.canGoForward
                    ToolTip.text: "下一個資料夾"
                    ToolTip.visible: hovered
                    onClicked: window.appController.goForward()
                }
                ToolbarButton {
                    symbol: "↻"
                    accessibleName: "重新整理圖庫"
                    enabled: window.appController.library.currentFolderPath.length > 0
                    ToolTip.text: "重新整理"
                    ToolTip.visible: hovered
                    onClicked: window.appController.reload()
                }
                TextField {
                    id: librarySearch
                    Layout.preferredWidth: 250
                    placeholderText: "搜尋目前 Library"
                    selectByMouse: true
                    text: window.appController.library.searchQuery
                    Accessible.name: "搜尋目前 Library"
                    Accessible.searchEdit: true
                    onTextEdited: window.appController.library.setSearchQuery(text)
                    Keys.onEscapePressed: function(event) {
                        clear()
                        window.appController.library.setSearchQuery("")
                        event.accepted = true
                    }
                }
                ToolbarButton {
                    visible: window.appController.library.hasSearchQuery
                    symbol: "×"
                    accessibleName: "清除搜尋"
                    ToolTip.text: "清除搜尋"
                    ToolTip.visible: hovered
                    onClicked: {
                        librarySearch.clear()
                        window.appController.library.setSearchQuery("")
                        librarySearch.forceActiveFocus()
                    }
                }
                Item { Layout.fillWidth: true }
                Text {
                    visible: window.appController.settingsBusy
                    text: "正在儲存設定…"
                    color: Theme.secondaryText
                    font.pixelSize: 12
                }
                ToolbarButton {
                    symbol: "+"
                    text: "開啟資料夾"
                    onClicked: folderDialog.open()
                }
            }
        }

        SplitView {
            Layout.fillWidth: true
            Layout.fillHeight: true
            orientation: Qt.Horizontal

            FolderTreePane {
                appController: window.appController
                visible: window.appController.sidebarOpen
                SplitView.minimumWidth: 190
                SplitView.preferredWidth: window.appController.sidebarOpen ? 260 : 0
                SplitView.maximumWidth: 420
            }
            LibraryPane {
                id: libraryPane
                appController: window.appController
                SplitView.minimumWidth: 600
                SplitView.fillWidth: true
                onOpenFolderRequested: folderDialog.open()
            }
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: Theme.statusHeight
            color: Theme.commandBar
            border.width: 1
            border.color: Theme.line

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: Theme.space6
                anchors.rightMargin: Theme.space6

                Rectangle {
                    Layout.preferredWidth: 7
                    Layout.preferredHeight: 7
                    radius: 4
                    color: window.appController.library.busy || window.appController.fileOperations.busy
                           ? Theme.accent : "#12B76A"
                }
                Text {
                    visible: window.appController.library.hasSelectedImages
                    text: window.appController.library.selectionSummary
                    color: Theme.accent
                    font.pixelSize: 12
                    font.weight: Font.Medium
                }
                Text {
                    text: window.appController.library.statusText
                    color: Theme.secondaryText
                    font.pixelSize: 12
                    elide: Text.ElideRight
                    Layout.fillWidth: true
                }
                Text {
                    text: window.appController.thumbnails.activeRequestCount > 0
                          ? "縮圖 " + window.appController.thumbnails.activeRequestCount
                          : window.appController.library.recursiveModeLabel
                    color: Theme.mutedText
                    font.pixelSize: 12
                }
            }
        }
    }

    Connections {
        target: window.appController.viewer
        function onStateChanged() {
            if (!window.appController.viewer.open)
                libraryPane.restoreGalleryFocus()
        }
    }

    ViewerOverlay {
        anchors.fill: parent
        appController: window.appController
    }

    Component.onCompleted: appController.initialize()
}
