pragma ComponentBehavior: Bound
import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Window
import PicLens

Rectangle {
    id: overlay
    required property AppController appController
    visible: appController.viewer.open
    z: 100
    color: Theme.viewerCanvas
    focus: visible

    function closeViewer() {
        appController.viewer.close()
    }

    function decodeTier(zoom) {
        if (zoom <= 1)
            return 1
        if (zoom <= 2)
            return 2
        return 4
    }

    Keys.onEscapePressed: function(event) {
        closeViewer()
        event.accepted = true
    }
    Keys.onLeftPressed: function(event) {
        if (appController.viewer.zoom > 1)
            appController.viewer.panBy(48, 0)
        else
            appController.viewer.previous()
        event.accepted = true
    }
    Keys.onRightPressed: function(event) {
        if (appController.viewer.zoom > 1)
            appController.viewer.panBy(-48, 0)
        else
            appController.viewer.next()
        event.accepted = true
    }
    Keys.onUpPressed: function(event) {
        appController.viewer.panBy(0, 48)
        event.accepted = true
    }
    Keys.onDownPressed: function(event) {
        appController.viewer.panBy(0, -48)
        event.accepted = true
    }

    onVisibleChanged: {
        if (visible)
            forceActiveFocus()
    }

    Rectangle {
        id: topBar
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: parent.top
        height: 60
        color: Theme.viewerChrome
        border.width: 1
        border.color: Theme.viewerLine

        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: Theme.space5
            anchors.rightMargin: Theme.space4
            spacing: Theme.space3

            Column {
                Layout.fillWidth: true
                spacing: 1
                Text {
                    width: parent.width
                    text: overlay.appController.viewer.currentName
                    color: "white"
                    font.pixelSize: 15
                    font.weight: Font.Medium
                    elide: Text.ElideMiddle
                }
                Text {
                    text: "圖片檢視器"
                    color: Theme.viewerSecondaryText
                    font.pixelSize: 11
                }
            }
            Button {
                text: "關閉  Esc"
                onClicked: overlay.closeViewer()
            }
        }
    }

    Item {
        id: canvas
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: topBar.bottom
        anchors.bottom: commandBar.top
        clip: true

        Image {
            id: fullImage
            anchors.fill: parent
            anchors.margins: Theme.space6
            source: overlay.appController.viewer.currentSourceUrl
            visible: overlay.appController.viewer.imageVisible
            asynchronous: true
            cache: false
            // Decode close to the pixels that can be displayed. The tier changes only at
            // meaningful zoom boundaries, avoiding a full-size texture for large photos.
            sourceSize: {
                const pixelRatio = Math.max(1, Screen.devicePixelRatio)
                const tier = overlay.decodeTier(overlay.appController.viewer.zoom)
                return Qt.size(
                    Math.min(8192, Math.max(1, Math.ceil(canvas.width * pixelRatio * tier))),
                    Math.min(8192, Math.max(1, Math.ceil(canvas.height * pixelRatio * tier))))
            }
            fillMode: Image.PreserveAspectFit
            scale: overlay.appController.viewer.zoom
            transformOrigin: Item.Center
            transform: Translate {
                x: overlay.appController.viewer.offsetX
                y: overlay.appController.viewer.offsetY
            }
            onStatusChanged: {
                if (status === Image.Error)
                    overlay.appController.viewer.reportLoadFailure("Qt Quick Image could not load the source.")
            }
        }

        BusyIndicator {
            anchors.centerIn: parent
            running: fullImage.visible && fullImage.status === Image.Loading
            visible: running
        }

        Column {
            anchors.centerIn: parent
            width: Math.min(parent.width - 64, 520)
            spacing: Theme.space3
            visible: overlay.appController.viewer.unsupportedAnimated

            Text {
                width: parent.width
                text: "無法預覽動畫圖片"
                color: "white"
                font.pixelSize: 22
                font.weight: Font.DemiBold
                horizontalAlignment: Text.AlignHCenter
            }
            Text {
                width: parent.width
                text: overlay.appController.viewer.unsupportedMessage
                color: Theme.viewerSecondaryText
                wrapMode: Text.Wrap
                horizontalAlignment: Text.AlignHCenter
            }
        }

        Column {
            anchors.centerIn: parent
            width: Math.min(parent.width - 64, 520)
            spacing: Theme.space3
            visible: overlay.appController.viewer.errorMessage.length > 0

            Text {
                width: parent.width
                text: "圖片載入失敗"
                color: "white"
                font.pixelSize: 22
                font.weight: Font.DemiBold
                horizontalAlignment: Text.AlignHCenter
            }
            Text {
                width: parent.width
                text: overlay.appController.viewer.errorMessage
                color: Theme.viewerSecondaryText
                wrapMode: Text.Wrap
                horizontalAlignment: Text.AlignHCenter
            }
        }

        WheelHandler {
            onWheel: function(event) {
                overlay.appController.viewer.zoomAt(
                    event.x,
                    event.y,
                    event.angleDelta.y,
                    canvas.width,
                    canvas.height)
                event.accepted = true
            }
        }

        DragHandler {
            id: panHandler
            target: null
            enabled: overlay.appController.viewer.zoom > 1
            property real previousX: 0
            property real previousY: 0
            onActiveChanged: {
                previousX = 0
                previousY = 0
            }
            onActiveTranslationChanged: {
                overlay.appController.viewer.panBy(
                    activeTranslation.x - previousX,
                    activeTranslation.y - previousY)
                previousX = activeTranslation.x
                previousY = activeTranslation.y
            }
        }
    }

    Rectangle {
        id: commandBar
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: Theme.space4
        height: 48
        width: commandRow.implicitWidth + 16
        radius: 12
        color: Theme.viewerChrome
        border.width: 1
        border.color: Theme.viewerLine

        Row {
            id: commandRow
            anchors.centerIn: parent
            spacing: Theme.space1

            Button {
                text: "‹  上一張"
                enabled: overlay.appController.viewer.canGoPrevious
                onClicked: overlay.appController.viewer.previous()
            }
            Button {
                text: "−"
                enabled: overlay.appController.viewer.canZoomOut
                onClicked: overlay.appController.viewer.zoomOut(canvas.width, canvas.height)
                ToolTip.visible: hovered
                ToolTip.text: "縮小"
            }
            Button {
                text: "重設"
                onClicked: overlay.appController.viewer.resetZoom()
            }
            Button {
                text: "+"
                enabled: overlay.appController.viewer.canZoomIn
                onClicked: overlay.appController.viewer.zoomIn(canvas.width, canvas.height)
                ToolTip.visible: hovered
                ToolTip.text: "放大"
            }
            Button {
                text: "下一張  ›"
                enabled: overlay.appController.viewer.canGoNext
                onClicked: overlay.appController.viewer.next()
            }
        }
    }
}
