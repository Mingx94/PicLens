pragma ComponentBehavior: Bound
import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import PicLens

Rectangle {
    id: pane
    required property AppController appController
    signal openFolderRequested()
    color: Theme.surface
    border.width: 1
    border.color: Theme.line

    readonly property int tileWidth: appController.gridViewMode
                                     ? appController.thumbnails.requestedSize + 24
                                     : gallery.width
    readonly property int tileHeight: appController.gridViewMode
                                      ? appController.thumbnails.requestedSize + 64
                                      : 100

    function restoreGalleryFocus() {
        gallery.forceActiveFocus()
    }

    ListModel {
        id: sortOptions
        ListElement { label: "名稱 ↑"; sortKey: 0; sortDirection: 0 }
        ListElement { label: "名稱 ↓"; sortKey: 0; sortDirection: 1 }
        ListElement { label: "修改時間 ↑"; sortKey: 1; sortDirection: 0 }
        ListElement { label: "修改時間 ↓"; sortKey: 1; sortDirection: 1 }
    }

    function synchronizeSort() {
        for (let index = 0; index < sortOptions.count; ++index) {
            const option = sortOptions.get(index)
            if (option.sortKey === appController.library.sortKey
                    && option.sortDirection === appController.library.sortDirection) {
                sortCombo.currentIndex = index
                return
            }
        }
    }

    Connections {
        target: pane.appController.library
        function onSortChanged() { pane.synchronizeSort() }
    }
    Connections {
        target: pane.appController.fileOperations
        function onDropRenamePreviewReady() { dropRenameDialog.open() }
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        Item {
            Layout.fillWidth: true
            Layout.preferredHeight: 146

            Column {
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.leftMargin: Theme.space5
                anchors.rightMargin: Theme.space5
                anchors.top: parent.top
                anchors.topMargin: Theme.space4
                spacing: Theme.space1

                Text {
                    width: parent.width
                    text: pane.appController.library.currentFolderName
                    color: Theme.primaryText
                    font.pixelSize: 24
                    font.weight: Font.DemiBold
                    elide: Text.ElideRight
                }
                Text {
                    width: parent.width
                    text: pane.appController.library.currentFolderPath || "選擇資料夾後開始瀏覽"
                    color: Theme.secondaryText
                    font.pixelSize: 12
                    elide: Text.ElideMiddle
                }
            }

            Row {
                id: controls
                anchors.right: parent.right
                anchors.rightMargin: Theme.space5
                anchors.bottom: parent.bottom
                anchors.bottomMargin: Theme.space3
                spacing: Theme.space2

                Switch {
                    id: recursiveSwitch
                    text: "含子資料夾"
                    checked: pane.appController.library.includeSubfolders
                    onToggled: pane.appController.setIncludeSubfolders(checked)
                }
                ComboBox {
                    id: sortCombo
                    width: 136
                    model: sortOptions
                    textRole: "label"
                    onActivated: {
                        const option = sortOptions.get(currentIndex)
                        pane.appController.changeSort(option.sortKey, option.sortDirection)
                    }
                    Component.onCompleted: pane.synchronizeSort()
                }
                Text {
                    anchors.verticalCenter: parent.verticalCenter
                    text: "縮圖"
                    color: Theme.secondaryText
                    font.pixelSize: 13
                }
                Slider {
                    id: sizeSlider
                    width: 104
                    from: 120
                    to: 240
                    stepSize: 20
                    value: pane.appController.thumbnails.requestedSize
                    onMoved: sizeCommit.restart()
                    ToolTip.visible: hovered || pressed
                    ToolTip.text: Math.round(value).toString()
                }
                Timer {
                    id: sizeCommit
                    interval: 250
                    onTriggered: pane.appController.setThumbnailSize(sizeSlider.value)
                }
                ToolbarButton {
                    symbol: "▦"
                    accessibleName: "格狀檢視"
                    checked: pane.appController.gridViewMode
                    checkable: true
                    ToolTip.visible: hovered
                    ToolTip.text: "格狀檢視"
                    onClicked: pane.appController.setGridViewMode(true)
                }
                ToolbarButton {
                    symbol: "☷"
                    accessibleName: "列表檢視"
                    checked: !pane.appController.gridViewMode
                    checkable: true
                    ToolTip.visible: hovered
                    ToolTip.text: "列表檢視"
                    onClicked: pane.appController.setGridViewMode(false)
                }
                ToolbarButton {
                    symbol: "⋯"
                    accessibleName: "更多圖庫動作"
                    enabled: pane.appController.fileOperations.canProcessVisible
                             || pane.appController.fileOperations.busy
                    ToolTip.visible: hovered
                    ToolTip.text: "更多圖庫動作"
                    onClicked: operationsMenu.open()

                    Menu {
                        id: operationsMenu
                        y: parent.height
                        MenuItem {
                            text: "將目前顯示項目轉為 JPG"
                            enabled: pane.appController.fileOperations.canProcessVisible
                            onTriggered: {
                                if (pane.appController.fileOperations.visibleImageCount >= 50)
                                    convertDialog.open()
                                else
                                    pane.appController.fileOperations.convertVisible()
                            }
                        }
                        MenuItem {
                            text: "清除同名非 JPG 檔案"
                            enabled: pane.appController.fileOperations.canProcessVisible
                            onTriggered: cleanupDialog.open()
                        }
                        MenuSeparator { }
                        MenuItem {
                            text: "取消目前檔案操作"
                            enabled: pane.appController.fileOperations.busy
                            onTriggered: pane.appController.fileOperations.cancel()
                        }
                    }
                }
            }
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 1
            color: Theme.line
        }

        Item {
            Layout.fillWidth: true
            Layout.fillHeight: true

            GridView {
                id: gallery
                anchors.fill: parent
                anchors.margins: Theme.space4
                clip: true
                boundsBehavior: Flickable.StopAtBounds
                model: pane.appController.library.items
                cellWidth: pane.tileWidth
                cellHeight: pane.tileHeight
                reuseItems: true
                cacheBuffer: Math.max(height, cellHeight * 2)
                property bool internalDragActive: false
                property real dragPointerX: 0
                property real dragPointerY: 0
                property string dropTargetPath: ""

                function targetPathAt(x, y) {
                    if (x < 0 || x > width || y < 0 || y > height)
                        return ""
                    const target = itemAt(x + contentX, y + contentY)
                    if (!target || target.isFolder)
                        return ""
                    return target.path
                }

                function beginInternalDrag(sourcePath, x, y) {
                    pane.appController.fileOperations.beginImageDrag(sourcePath)
                    internalDragActive = pane.appController.fileOperations.dragActive
                    updateInternalDrag(x, y)
                }

                function updateInternalDrag(x, y) {
                    if (!internalDragActive)
                        return
                    dragPointerX = x
                    dragPointerY = y
                    const candidate = targetPathAt(x, y)
                    dropTargetPath = candidate
                    if (pane.appController.dragAutoScrollDelta(y, height) !== 0)
                        dragAutoScroll.start()
                    else
                        dragAutoScroll.stop()
                }

                function endInternalDrag(x, y, canceled) {
                    if (!internalDragActive)
                        return
                    updateInternalDrag(x, y)
                    const targetPath = canceled ? "" : dropTargetPath
                    internalDragActive = false
                    dropTargetPath = ""
                    dragAutoScroll.stop()
                    if (targetPath.length > 0)
                        pane.appController.fileOperations.requestDropRenamePreview(targetPath)
                    else
                        pane.appController.fileOperations.cancelImageDrag()
                }

                function cancelInternalDrag() {
                    endInternalDrag(dragPointerX, dragPointerY, true)
                }

                Timer {
                    id: dragAutoScroll
                    interval: 33
                    repeat: true
                    onTriggered: {
                        const delta = pane.appController.dragAutoScrollDelta(
                            gallery.dragPointerY, gallery.height)
                        if (delta === 0) {
                            stop()
                            return
                        }
                        const maximum = Math.max(0, gallery.contentHeight - gallery.height)
                        gallery.contentY = Math.max(0, Math.min(maximum, gallery.contentY + delta))
                        gallery.dropTargetPath = gallery.targetPathAt(
                            gallery.dragPointerX, gallery.dragPointerY)
                    }
                }

                delegate: GalleryTile {
                    width: gallery.cellWidth - Theme.space2
                    height: gallery.cellHeight - Theme.space2
                    appController: pane.appController
                    thumbnailSize: pane.appController.thumbnails.requestedSize
                    listMode: !pane.appController.gridViewMode
                    dropRenameTarget: gallery.internalDragActive
                                      && gallery.dropTargetPath === path
                }

                ScrollBar.vertical: ScrollBar { }
            }

            Column {
                anchors.centerIn: parent
                width: Math.min(parent.width - 48, 440)
                spacing: Theme.space3
                visible: !pane.appController.library.busy && gallery.count === 0

                Text {
                    width: parent.width
                    text: pane.appController.library.currentFolderPath.length === 0
                          ? "選擇一個圖片資料夾"
                          : pane.appController.library.hasSearchQuery
                            ? "沒有符合條件的項目"
                            : "這個資料夾沒有可顯示的圖片"
                    color: Theme.primaryText
                    font.pixelSize: 22
                    font.weight: Font.DemiBold
                    horizontalAlignment: Text.AlignHCenter
                    wrapMode: Text.Wrap
                }
                Button {
                    anchors.horizontalCenter: parent.horizontalCenter
                    visible: pane.appController.library.currentFolderPath.length === 0
                    text: "選擇資料夾"
                    onClicked: pane.openFolderRequested()
                }
                Text {
                    width: parent.width
                    text: pane.appController.library.currentFolderPath.length === 0
                          ? "PicLens 會在本機整理並快取縮圖。"
                          : pane.appController.library.hasSearchQuery
                            ? "清除搜尋，或換一個關鍵字。"
                            : "可開啟其他資料夾，或啟用「含子資料夾」。"
                    color: Theme.secondaryText
                    horizontalAlignment: Text.AlignHCenter
                    wrapMode: Text.Wrap
                }
            }

            BusyIndicator {
                anchors.centerIn: parent
                width: 46
                height: 46
                running: pane.appController.library.busy && gallery.count === 0
                visible: running
            }

            Rectangle {
                x: Math.min(parent.width - width - 8, Math.max(8, gallery.x + gallery.dragPointerX + 12))
                y: Math.min(parent.height - height - 8, Math.max(8, gallery.y + gallery.dragPointerY + 12))
                width: dragPreviewText.implicitWidth + 24
                height: 38
                radius: 19
                visible: gallery.internalDragActive
                color: Theme.commandBar
                border.width: 1
                border.color: Theme.strongLine
                z: 20

                Text {
                    id: dragPreviewText
                    anchors.centerIn: parent
                    text: pane.appController.fileOperations.dragSourceCount <= 1
                          ? "拖曳 1 張圖片"
                          : "拖曳 " + pane.appController.fileOperations.dragSourceCount + " 張圖片"
                    color: Theme.primaryText
                    font.weight: Font.DemiBold
                }
            }

            Rectangle {
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.bottom: parent.bottom
                anchors.margins: Theme.space4
                height: Math.max(errorText.implicitHeight, 38) + 20
                radius: Theme.cornerRadius
                color: "#FFF1F0"
                border.width: 1
                border.color: "#FDA29B"
                visible: pane.appController.library.errorMessage.length > 0

                RowLayout {
                    anchors.fill: parent
                    anchors.margins: 10
                    spacing: Theme.space3

                    Text {
                        id: errorText
                        Layout.fillWidth: true
                        text: pane.appController.library.errorMessage
                        color: Theme.danger
                        wrapMode: Text.Wrap
                        verticalAlignment: Text.AlignVCenter
                    }
                    Button {
                        text: "重試"
                        onClicked: pane.appController.reload()
                    }
                }
            }
        }
    }

    Dialog {
        id: convertDialog
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(440, pane.width - 48)
        title: "轉換為 JPG"
        modal: true
        standardButtons: Dialog.Yes | Dialog.Cancel
        onAccepted: pane.appController.fileOperations.convertVisible()

        contentItem: Text {
            width: convertDialog.availableWidth
            text: "要將目前顯示的 " + pane.appController.fileOperations.visibleImageCount
                  + " 張圖片轉為 JPG 嗎？原始檔案會保留。"
            color: Theme.primaryText
            wrapMode: Text.Wrap
        }
    }

    Dialog {
        id: cleanupDialog
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(460, pane.width - 48)
        title: "清除同名檔案"
        modal: true
        standardButtons: Dialog.Yes | Dialog.Cancel
        onAccepted: pane.appController.fileOperations.clearSameBasename()

        contentItem: Text {
            width: cleanupDialog.availableWidth
            text: "要將目前顯示圖片中，已有同名 JPG 的非 JPG 檔案移至回收筒嗎？"
            color: Theme.primaryText
            wrapMode: Text.Wrap
        }
    }

    Dialog {
        id: dropRenameDialog
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(560, pane.width - 48)
        title: "確認拖放重新命名"
        modal: true
        closePolicy: Popup.CloseOnEscape
        onAccepted: pane.appController.fileOperations.confirmDropRename()
        onRejected: pane.appController.fileOperations.cancelDropRenamePreview()

        footer: DialogButtonBox {
            Button {
                text: "套用重新命名"
                DialogButtonBox.buttonRole: DialogButtonBox.AcceptRole
            }
            Button {
                text: "取消"
                DialogButtonBox.buttonRole: DialogButtonBox.RejectRole
            }
            onAccepted: dropRenameDialog.accept()
            onRejected: dropRenameDialog.reject()
        }

        contentItem: Column {
            width: dropRenameDialog.availableWidth
            spacing: Theme.space3

            Text {
                width: parent.width
                text: "將重新命名 " + pane.appController.fileOperations.dropRenameCount
                      + " 個，略過 " + pane.appController.fileOperations.dropRenameSkippedCount + " 個。"
                color: Theme.primaryText
                font.weight: Font.DemiBold
                wrapMode: Text.Wrap
            }
            Rectangle {
                width: parent.width
                height: Math.min(300, Math.max(80, previewText.implicitHeight + 24))
                radius: Theme.cornerRadius
                color: Theme.tileFrame
                border.width: 1
                border.color: Theme.line

                Text {
                    id: previewText
                    anchors.fill: parent
                    anchors.margins: 12
                    text: pane.appController.fileOperations.dropRenamePreviewText
                    color: Theme.secondaryText
                    font.family: "monospace"
                    wrapMode: Text.WrapAnywhere
                }
            }
        }
    }
}
