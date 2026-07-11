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

            Rectangle {
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.bottom: parent.bottom
                height: 1
                color: Theme.line
            }

            RowLayout {
                id: leftCommands
                anchors.left: parent.left
                anchors.leftMargin: Theme.space5
                anchors.verticalCenter: parent.verticalCenter
                spacing: Theme.space3

                RowLayout {
                    spacing: Theme.space2
                    Layout.rightMargin: Theme.space2

                    LensMark {
                        Layout.preferredWidth: 34
                        Layout.preferredHeight: 34
                    }
                    Text {
                        text: "PicLens"
                        color: Theme.primaryText
                        font.pixelSize: 21
                        font.weight: Font.Bold
                    }
                }
                ToolbarButton {
                    iconName: "menu"
                    outlined: true
                    accessibleName: window.appController.sidebarOpen ? "收合側欄" : "展開側欄"
                    ToolTip.text: window.appController.sidebarOpen ? "收合側欄" : "展開側欄"
                    ToolTip.visible: hovered
                    onClicked: window.appController.toggleSidebar()
                }
                Item {
                    Layout.preferredWidth: 77
                    Layout.preferredHeight: Theme.controlHeight

                    Rectangle {
                        anchors.fill: parent
                        radius: Theme.cornerRadius
                        color: Theme.surface
                        border.width: 1
                        border.color: Theme.line
                    }
                    Rectangle {
                        anchors.centerIn: parent
                        width: 1
                        height: parent.height
                        color: Theme.line
                    }
                    Row {
                        anchors.fill: parent

                        ToolbarButton {
                            width: 38
                            height: Theme.controlHeight
                            iconName: "chevron-left"
                            accessibleName: "上一個資料夾"
                            enabled: window.appController.library.canGoBack
                            ToolTip.text: "上一個資料夾"
                            ToolTip.visible: hovered
                            onClicked: window.appController.goBack()
                        }
                        ToolbarButton {
                            width: 38
                            height: Theme.controlHeight
                            iconName: "chevron-right"
                            accessibleName: "下一個資料夾"
                            enabled: window.appController.library.canGoForward
                            ToolTip.text: "下一個資料夾"
                            ToolTip.visible: hovered
                            onClicked: window.appController.goForward()
                        }
                    }
                }
                ToolbarButton {
                    iconName: "refresh"
                    accessibleName: "重新整理圖庫"
                    enabled: window.appController.library.currentFolderPath.length > 0
                    ToolTip.text: "重新整理"
                    ToolTip.visible: hovered
                    onClicked: window.appController.reload()
                }
            }

            TextField {
                id: librarySearch
                x: window.width >= 1040
                   ? Math.round((parent.width - width) / 2)
                   : leftCommands.x + leftCommands.width + Theme.space4
                anchors.verticalCenter: parent.verticalCenter
                width: window.width >= 1040
                       ? 420
                       : Math.max(180, rightCommands.x
                                  - (leftCommands.x + leftCommands.width) - Theme.space6)
                height: Theme.controlHeight
                leftPadding: 40
                rightPadding: 38
                placeholderText: "搜尋目前資料夾"
                placeholderTextColor: Theme.mutedText
                color: Theme.primaryText
                hoverEnabled: true
                selectByMouse: true
                text: window.appController.library.searchQuery
                Accessible.name: "搜尋目前資料夾"
                Accessible.searchEdit: true
                onTextEdited: window.appController.library.setSearchQuery(text)
                Keys.onEscapePressed: function(event) {
                    clear()
                    window.appController.library.setSearchQuery("")
                    event.accepted = true
                }

                background: Rectangle {
                    radius: Theme.cornerRadius
                    color: Theme.surface
                    border.width: 1
                    border.color: librarySearch.activeFocus ? Theme.accent
                                : librarySearch.hovered ? Theme.strongLine : Theme.line
                }
                AppIcon {
                    anchors.left: parent.left
                    anchors.leftMargin: 14
                    anchors.verticalCenter: parent.verticalCenter
                    width: 18
                    height: 18
                    name: "search"
                    color: Theme.secondaryText
                }
                ToolbarButton {
                    anchors.right: parent.right
                    anchors.rightMargin: 3
                    anchors.verticalCenter: parent.verticalCenter
                    width: 32
                    height: 32
                    visible: window.appController.library.hasSearchQuery
                    iconName: "close"
                    accessibleName: "清除搜尋"
                    ToolTip.text: "清除搜尋"
                    ToolTip.visible: hovered
                    onClicked: {
                        librarySearch.clear()
                        window.appController.library.setSearchQuery("")
                        librarySearch.forceActiveFocus()
                    }
                }
            }

            RowLayout {
                id: rightCommands
                anchors.right: parent.right
                anchors.rightMargin: Theme.space5
                anchors.verticalCenter: parent.verticalCenter
                spacing: Theme.space3

                Text {
                    visible: window.appController.settingsBusy
                    text: "正在儲存設定…"
                    color: Theme.secondaryText
                    font.pixelSize: 12
                }
                ToolbarButton {
                    iconName: "plus"
                    text: "開啟資料夾"
                    primary: true
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
                SplitView.preferredWidth: window.appController.sidebarOpen ? 228 : 0
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

            Rectangle {
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.top: parent.top
                height: 1
                color: Theme.line
            }

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: Theme.space6
                anchors.rightMargin: Theme.space6

                Rectangle {
                    Layout.preferredWidth: 8
                    Layout.preferredHeight: 8
                    radius: 4
                    color: window.appController.library.busy || window.appController.fileOperations.busy
                           ? Theme.accent : Theme.success
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
                AppIcon {
                    Layout.preferredWidth: 18
                    Layout.preferredHeight: 18
                    Layout.leftMargin: Theme.space4
                    name: "image"
                    color: Theme.secondaryText
                }
                Slider {
                    id: statusSizeSlider
                    Layout.preferredWidth: 148
                    Layout.preferredHeight: 28
                    from: 120
                    to: 240
                    stepSize: 20
                    value: window.appController.thumbnails.requestedSize
                    onMoved: statusSizeCommit.restart()
                    ToolTip.visible: hovered || pressed
                    ToolTip.text: "縮圖 " + Math.round(value)

                    background: Rectangle {
                        x: statusSizeSlider.leftPadding
                        y: statusSizeSlider.topPadding
                           + statusSizeSlider.availableHeight / 2 - height / 2
                        width: statusSizeSlider.availableWidth
                        height: 4
                        radius: 2
                        color: Theme.line

                        Rectangle {
                            width: statusSizeSlider.visualPosition * parent.width
                            height: parent.height
                            radius: parent.radius
                            color: Theme.accent
                        }
                    }
                    handle: Rectangle {
                        x: statusSizeSlider.leftPadding
                           + statusSizeSlider.visualPosition
                             * (statusSizeSlider.availableWidth - width)
                        y: statusSizeSlider.topPadding
                           + statusSizeSlider.availableHeight / 2 - height / 2
                        width: 18
                        height: 18
                        radius: 9
                        color: statusSizeSlider.pressed ? Theme.accentPressed : Theme.accent
                        border.width: 2
                        border.color: Theme.surface
                    }
                }
                Timer {
                    id: statusSizeCommit
                    interval: 250
                    onTriggered: window.appController.setThumbnailSize(statusSizeSlider.value)
                }
                AppIcon {
                    Layout.preferredWidth: 18
                    Layout.preferredHeight: 18
                    Layout.leftMargin: Theme.space3
                    name: "sidebar"
                    color: Theme.secondaryText
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
