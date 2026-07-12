pragma ComponentBehavior: Bound
import QtQuick
import QtQuick.Controls
import PicLens

Item {
    id: tile
    signal internalDragStarted(string sourcePath, real x, real y)
    signal internalDragUpdated(real x, real y)
    signal internalDragFinished(real x, real y, bool canceled)
    signal contextMenuRequested(string sourcePath, real x, real y)
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
    objectName: isFolder ? "" : path
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
        if (appController && !isFolder && visible)
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
        color: tile.selected ? Theme.accentSoft : tileMouse.containsMouse ? Theme.hover : "transparent"
        border.width: tile.dropRenameTarget ? 3 : tile.activeFocus || tile.selected ? 2 : 0
        border.color: tile.dropRenameTarget ? Theme.accent : Theme.accent
    }

    Rectangle {
        id: frame
        x: Theme.space1
        y: tile.listMode ? Math.round((tile.height - height) / 2) : Theme.space1
        width: tile.listMode ? tile.visualThumbnailSize : tile.width - Theme.space2
        height: tile.visualThumbnailSize
        radius: Theme.cornerRadius - 1
        color: Theme.tileFrame
        border.width: 1
        border.color: tileMouse.containsMouse ? Theme.strongLine : Theme.line
        clip: true

        Image {
            id: thumbnail
            anchors.fill: parent
            anchors.margins: 1
            visible: !tile.isFolder && status === Image.Ready
            source: tile.thumbnailUrl ?? ""
            asynchronous: true
            cache: false
            fillMode: Image.PreserveAspectFit
        }

        Item {
            anchors.centerIn: parent
            visible: tile.isFolder
            width: Math.max(42, tile.visualThumbnailSize * 0.42)
            height: width * 0.72

            Rectangle {
                x: parent.width * 0.08
                y: 0
                width: parent.width * 0.42
                height: parent.height * 0.3
                radius: Theme.cornerRadius
                color: Theme.folder
            }
            Rectangle {
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.bottom: parent.bottom
                height: parent.height * 0.78
                radius: Theme.cornerRadius
                color: Theme.folderLight
                border.width: 1
                border.color: Theme.folder
            }
        }

        Rectangle {
            visible: tile.selected && !tile.isFolder
            anchors.left: parent.left
            anchors.top: parent.top
            anchors.margins: Theme.space2
            width: 26
            height: 26
            radius: 13
            color: Theme.accent
            border.width: 2
            border.color: "white"
            z: 4

            Text {
                anchors.centerIn: parent
                text: "✓"
                color: "white"
                font.pixelSize: 14
                font.weight: Font.Bold
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
               ? tile.width - x - 190
               : tile.width - Theme.space4
        text: tile.name
        color: tile.selected ? Theme.accent : Theme.primaryText
        font.pixelSize: 14
        font.weight: tile.selected ? Font.DemiBold : Font.Medium
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
        color: Theme.secondaryText
        font.pixelSize: 12
        elide: Text.ElideRight
    }

    Text {
        visible: tile.listMode && !tile.isFolder
        anchors.right: parent.right
        anchors.rightMargin: Theme.space4
        anchors.verticalCenter: parent.verticalCenter
        width: 150
        text: tile.modifiedLabel()
        color: Theme.secondaryText
        font.pixelSize: 12
        horizontalAlignment: Text.AlignRight
        elide: Text.ElideRight
    }

    Rectangle {
        visible: tile.listMode
        anchors.left: nameLabel.left
        anchors.right: parent.right
        anchors.bottom: parent.bottom
        height: 1
        color: Theme.line
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
                    const point = tile.mapToItem(
                        tile.GridView.view, mouse.x, mouse.y)
                    tile.contextMenuRequested(tile.path, point.x, point.y)
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
            const point = galleryPosition()
            if (active) {
                tile.internalDragStarted(tile.path, point.x, point.y)
            } else {
                tile.internalDragFinished(point.x, point.y, false)
            }
        }
        onTranslationChanged: {
            if (active) {
                const point = galleryPosition()
                tile.internalDragUpdated(point.x, point.y)
            }
        }
        onCanceled: tile.internalDragFinished(0, 0, true)
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
            const point = tile.mapToItem(
                tile.GridView.view, tile.width / 2, tile.height / 2)
            tile.contextMenuRequested(tile.path, point.x, point.y)
            event.accepted = true
        }
    }

    Component.onCompleted: requestVisibleThumbnail()
    onThumbnailSizeChanged: requestVisibleThumbnail()
    Component.onDestruction: cancelThumbnail()
    GridView.onPooled: cancelThumbnail()
    GridView.onReused: requestVisibleThumbnail()
}
