pragma ComponentBehavior: Bound
import QtQuick
import QtQuick.Controls
import PicLens

Item {
    id: tile
    required property AppController appController
    required property int index
    required property string itemType
    required property string path
    required property string name
    required property string extension
    required property var modifiedAtMs
    required property var sizeBytes
    required property bool animated
    required property bool selected
    required property var thumbnailUrl
    property int thumbnailSize: 200
    property bool listMode: false
    property bool dropRenameTarget: false
    readonly property bool isFolder: itemType === "folder"
    readonly property int visualThumbnailSize: listMode ? 76 : thumbnailSize
    activeFocusOnTab: true
    Accessible.role: Accessible.ListItem
    Accessible.name: name
    Accessible.description: isFolder
                            ? "資料夾"
                            : (animated ? "不支援動畫圖片" : "圖片") + "，" + sizeLabel()
    Accessible.focusable: true
    Accessible.selectable: !isFolder
    Accessible.selected: selected
    Accessible.onPressAction: {
        tile.forceActiveFocus()
        if (tile.isFolder)
            tile.appController.navigateFromTree(tile.path)
        else
            tile.appController.selectLibraryItem(tile.path, Qt.NoModifier)
    }

    function requestVisibleThumbnail() {
        if (!isFolder && visible)
            appController.requestThumbnail(path, animated)
    }
    function cancelThumbnail() {
        if (!isFolder)
            appController.cancelThumbnail(path)
    }
    function sizeLabel() {
        if (isFolder || sizeBytes === undefined || sizeBytes === null)
            return "資料夾"
        if (sizeBytes >= 1048576)
            return (sizeBytes / 1048576).toFixed(1) + " MB"
        if (sizeBytes >= 1024)
            return Math.round(sizeBytes / 1024) + " KB"
        return sizeBytes + " B"
    }
    function modifiedLabel() {
        if (modifiedAtMs === undefined || modifiedAtMs === null)
            return ""
        return Qt.formatDateTime(new Date(Number(modifiedAtMs)), "yyyy/MM/dd HH:mm")
    }

    implicitWidth: listMode ? 640 : thumbnailSize + 16
    implicitHeight: listMode ? 92 : thumbnailSize + 56

    Rectangle {
        anchors.fill: parent
        radius: Theme.cornerRadius
        color: tile.selected ? Theme.selected : tileMouse.containsMouse ? Theme.hover : "transparent"
        border.width: tile.dropRenameTarget ? 3 : tile.activeFocus ? 2 : tile.selected ? 1 : 0
        border.color: tile.dropRenameTarget ? Theme.accent : Theme.accent
    }

    Rectangle {
        id: frame
        x: Theme.space1
        y: tile.listMode ? Math.round((tile.height - height) / 2) : Theme.space1
        width: tile.listMode ? tile.visualThumbnailSize : tile.width - Theme.space2
        height: tile.visualThumbnailSize
        radius: Theme.cornerRadius
        color: Theme.tileFrame
        border.width: 1
        border.color: tileMouse.containsMouse ? Theme.strongLine : Theme.line
        clip: true

        Image {
            id: thumbnail
            anchors.fill: parent
            anchors.margins: Theme.space1
            visible: !tile.isFolder && status === Image.Ready
            source: tile.thumbnailUrl ?? ""
            asynchronous: true
            cache: false
            fillMode: Image.PreserveAspectFit
        }

        Item {
            anchors.centerIn: parent
            visible: tile.isFolder
            width: Math.max(42, tile.visualThumbnailSize * 0.3)
            height: width * 0.72

            Rectangle {
                x: parent.width * 0.08
                y: 0
                width: parent.width * 0.42
                height: parent.height * 0.3
                radius: Theme.cornerRadius
                color: Theme.accent
            }
            Rectangle {
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.bottom: parent.bottom
                height: parent.height * 0.78
                radius: Theme.cornerRadius
                color: Theme.accent
            }
        }

        BusyIndicator {
            anchors.centerIn: parent
            width: 34
            height: 34
            running: !tile.isFolder && thumbnail.status === Image.Loading
            visible: running
        }

        Text {
            anchors.centerIn: parent
            visible: !tile.isFolder && thumbnail.status !== Image.Ready && thumbnail.status !== Image.Loading
            text: tile.extension.length > 0 ? tile.extension.toUpperCase() : "IMAGE"
            color: Theme.mutedText
            font.pixelSize: 12
            font.weight: Font.Medium
        }

        Rectangle {
            visible: tile.animated
            anchors.right: parent.right
            anchors.bottom: parent.bottom
            anchors.margins: Theme.space2
            width: animatedLabel.implicitWidth + 12
            height: 24
            radius: 12
            color: Theme.commandBar
            border.width: 1
            border.color: Theme.strongLine

            Text {
                id: animatedLabel
                anchors.centerIn: parent
                text: "動圖"
                color: Theme.primaryText
                font.pixelSize: 11
                font.weight: Font.Medium
            }
        }
    }

    Text {
        id: nameLabel
        x: tile.listMode ? frame.x + frame.width + Theme.space3 : Theme.space2
        y: tile.listMode
           ? Math.round((tile.height - height - listDetail.implicitHeight - Theme.space1) / 2)
           : frame.y + frame.height + Theme.space2
        width: tile.listMode
               ? tile.width - x - Theme.space3
               : tile.width - Theme.space4
        text: tile.name
        color: Theme.primaryText
        font.pixelSize: 14
        font.weight: Font.Medium
        horizontalAlignment: tile.listMode ? Text.AlignLeft : Text.AlignHCenter
        verticalAlignment: Text.AlignVCenter
        wrapMode: Text.Wrap
        maximumLineCount: tile.listMode ? 1 : 2
        elide: Text.ElideRight
    }

    Text {
        id: listDetail
        visible: tile.listMode
        x: nameLabel.x
        y: nameLabel.y + nameLabel.height + Theme.space1
        width: nameLabel.width
        text: tile.isFolder
              ? "資料夾"
              : tile.extension.toUpperCase() + " · " + tile.sizeLabel()
                + (tile.modifiedLabel().length > 0 ? " · " + tile.modifiedLabel() : "")
        color: Theme.secondaryText
        font.pixelSize: 12
        elide: Text.ElideRight
    }

    MouseArea {
        id: tileMouse
        anchors.fill: parent
        hoverEnabled: true
        acceptedButtons: Qt.LeftButton | Qt.RightButton
        cursorShape: Qt.PointingHandCursor
        onClicked: function(mouse) {
            tile.forceActiveFocus()
            tile.GridView.view.currentIndex = tile.index
            if (mouse.button === Qt.RightButton) {
                if (!tile.isFolder) {
                    tile.appController.prepareContextSelection(tile.path)
                    contextMenu.popup()
                }
            } else if (tile.isFolder) {
                tile.appController.navigateFromTree(tile.path)
            } else {
                tile.appController.selectLibraryItem(tile.path, mouse.modifiers)
            }
        }
        onDoubleClicked: function(mouse) {
            if (mouse.button === Qt.LeftButton && !tile.isFolder)
                tile.appController.openViewer(tile.path, false)
        }
    }

    DragHandler {
        id: internalDrag
        target: null
        acceptedButtons: Qt.LeftButton
        dragThreshold: 8
        enabled: !tile.isFolder && !tile.appController.fileOperations.busy

        function galleryPosition() {
            return tile.mapToItem(tile.GridView.view, centroid.position.x, centroid.position.y)
        }

        onActiveChanged: {
            const view = tile.GridView.view
            const point = galleryPosition()
            if (active) {
                view.beginInternalDrag(tile.path, point.x, point.y)
            } else {
                view.endInternalDrag(point.x, point.y, false)
            }
        }
        onTranslationChanged: {
            if (active) {
                const point = galleryPosition()
                tile.GridView.view.updateInternalDrag(point.x, point.y)
            }
        }
        onCanceled: tile.GridView.view.cancelInternalDrag()
    }

    Keys.onSpacePressed: function(event) {
        if (!tile.isFolder) {
            tile.appController.selectLibraryItem(tile.path, event.modifiers)
            event.accepted = true
        }
    }
    Keys.onReturnPressed: function(event) {
        if (tile.isFolder)
            tile.appController.navigateFromTree(tile.path)
        else
            tile.appController.openViewer(tile.path, true)
        event.accepted = true
    }
    Keys.onEnterPressed: function(event) {
        if (tile.isFolder)
            tile.appController.navigateFromTree(tile.path)
        else
            tile.appController.openViewer(tile.path, true)
        event.accepted = true
    }
    Keys.onPressed: function(event) {
        if (event.key === Qt.Key_F10
                && (event.modifiers & Qt.ShiftModifier)
                && !tile.isFolder) {
            tile.appController.prepareContextSelection(tile.path)
            contextMenu.popup()
            event.accepted = true
        }
    }

    Menu {
        id: contextMenu

        MenuItem {
            text: "在檔案管理器中顯示"
            enabled: !tile.appController.fileOperations.busy
            onTriggered: tile.appController.fileOperations.reveal(tile.path)
        }
        MenuSeparator { }
        MenuItem {
            text: "重新命名"
            enabled: tile.appController.fileOperations.canRename
            onTriggered: renameDialog.open()
        }
        MenuItem {
            text: "移至回收筒"
            enabled: tile.appController.fileOperations.canTrash
            onTriggered: trashDialog.open()
        }
    }

    Dialog {
        id: renameDialog
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: 420
        title: "重新命名圖片"
        modal: true
        standardButtons: Dialog.Ok | Dialog.Cancel
        closePolicy: Popup.CloseOnEscape
        onOpened: {
            renameField.text = tile.appController.fileOperations.selectedBaseName
            renameField.forceActiveFocus()
            renameField.selectAll()
        }
        onAccepted: tile.appController.fileOperations.renameSelected(renameField.text)

        contentItem: Column {
            spacing: Theme.space3
            Text {
                text: "輸入新的檔名（副檔名會保留）"
                color: Theme.primaryText
            }
            TextField {
                id: renameField
                width: 340
                selectByMouse: true
                onAccepted: renameDialog.accept()
            }
        }
    }

    Dialog {
        id: trashDialog
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: 420
        title: "將選取的圖片移至回收筒"
        modal: true
        standardButtons: Dialog.Yes | Dialog.Cancel
        closePolicy: Popup.CloseOnEscape
        onAccepted: tile.appController.fileOperations.trashSelected()

        contentItem: Text {
            width: trashDialog.availableWidth
            text: tile.appController.library.selectedCount === 1
                  ? "要將這張圖片移至回收筒嗎？"
                  : "要將選取的 " + tile.appController.library.selectedCount + " 張圖片移至回收筒嗎？"
            color: Theme.primaryText
            wrapMode: Text.Wrap
        }
    }

    Component.onCompleted: requestVisibleThumbnail()
    Component.onDestruction: cancelThumbnail()
    GridView.onPooled: cancelThumbnail()
    GridView.onReused: requestVisibleThumbnail()
}
