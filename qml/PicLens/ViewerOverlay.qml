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

    function navigateLeft() {
        if (appController.viewer.zoom > 1)
            appController.viewer.panBy(48, 0)
        else
            appController.viewer.previous()
    }

    function navigateRight() {
        if (appController.viewer.zoom > 1)
            appController.viewer.panBy(-48, 0)
        else
            appController.viewer.next()
    }

    function decodeTier(zoom) {
        if (zoom <= 1)
            return 1
        if (zoom <= 2)
            return 2
        return 4
    }

    component ViewerButton: Button {
        id: control
        property string iconName: ""
        property string accessibleName: ""
        property bool circular: false

        implicitWidth: 38
        implicitHeight: 38
        padding: 0
        focusPolicy: Qt.StrongFocus
        Accessible.role: Accessible.Button
        Accessible.name: accessibleName
        Accessible.description: ToolTip.text
        Accessible.focusable: true
        Accessible.onPressAction: control.click()

        contentItem: AppIcon {
            name: control.iconName
            width: 20
            height: 20
            color: control.enabled ? Theme.viewerText : Theme.viewerDisabledText
        }

        background: Rectangle {
            radius: control.circular ? control.width / 2 : Theme.cornerRadius
            color: control.down ? Theme.viewerPressed
                 : control.hovered || control.activeFocus ? Theme.viewerHover
                 : "transparent"
            border.width: control.activeFocus ? 1 : 0
            border.color: Theme.viewerText
        }
    }

    // Window shortcuts keep viewer navigation working even after a command button
    // or another control becomes the active focus item.
    Shortcut {
        sequence: "Escape"
        context: Qt.WindowShortcut
        onActivated: overlay.closeViewer()
    }
    Shortcut {
        sequence: "Left"
        context: Qt.WindowShortcut
        onActivated: overlay.navigateLeft()
    }
    Shortcut {
        sequence: "Right"
        context: Qt.WindowShortcut
        onActivated: overlay.navigateRight()
    }
    Shortcut {
        sequence: "Up"
        context: Qt.WindowShortcut
        onActivated: overlay.appController.viewer.panBy(0, 48)
    }
    Shortcut {
        sequence: "Down"
        context: Qt.WindowShortcut
        onActivated: overlay.appController.viewer.panBy(0, -48)
    }

    // Keep direct key handling as a fallback for platforms where a bare arrow
    // key is not exposed as a shortcut sequence.
    Keys.onEscapePressed: function(event) {
        closeViewer()
        event.accepted = true
    }
    Keys.onLeftPressed: function(event) {
        navigateLeft()
        event.accepted = true
    }
    Keys.onRightPressed: function(event) {
        navigateRight()
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
            forceActiveFocus(Qt.OtherFocusReason)
    }
    Component.onCompleted: forceActiveFocus(Qt.OtherFocusReason)

    Item {
        id: canvas
        anchors.fill: parent
        clip: true

        readonly property url desiredSource: overlay.appController.viewer.currentSourceUrl
        readonly property int desiredTier: overlay.decodeTier(overlay.appController.viewer.zoom)
        readonly property bool hasDisplayedImage: frontImage.visible || backImage.visible
        property bool frontActive: true
        property bool bufferReady: false

        function decodeWidth(tier) {
            const pixelRatio = Math.max(1, Screen.devicePixelRatio)
            return Math.min(8192, Math.max(1, Math.ceil(width * pixelRatio * tier)))
        }

        function decodeHeight(tier) {
            const pixelRatio = Math.max(1, Screen.devicePixelRatio)
            return Math.min(8192, Math.max(1, Math.ceil(height * pixelRatio * tier)))
        }

        function activeLayer() {
            return frontActive ? frontImage : backImage
        }

        function standbyLayer() {
            return frontActive ? backImage : frontImage
        }

        function layerMatches(layer, source, tier, pixelWidth, pixelHeight) {
            return layer.requestedSource.toString() === source.toString()
                && layer.decodeTier === tier
                && layer.decodeWidth === pixelWidth
                && layer.decodeHeight === pixelHeight
        }

        function clearLayers() {
            frontImage.visible = false
            backImage.visible = false
            frontImage.source = ""
            backImage.source = ""
            frontImage.requestedSource = ""
            backImage.requestedSource = ""
            frontActive = true
        }

        function requestDisplayedImage(sourceChanged) {
            if (!bufferReady)
                return

            const source = desiredSource
            if (!overlay.appController.viewer.imageVisible || source.toString().length === 0) {
                clearLayers()
                return
            }

            if (sourceChanged)
                clearLayers()

            const tier = desiredTier
            const pixelWidth = decodeWidth(tier)
            const pixelHeight = decodeHeight(tier)
            const active = activeLayer()
            const standby = standbyLayer()

            if (active.visible && layerMatches(active, source, tier, pixelWidth, pixelHeight)) {
                if (standby.status === Image.Loading)
                    standby.source = ""
                return
            }

            // A previously decoded tier can be reused immediately when zooming back.
            if (standby.status === Image.Ready
                    && layerMatches(standby, source, tier, pixelWidth, pixelHeight)) {
                displayReadyLayer(standby)
                return
            }

            if (standby.status === Image.Loading
                    && layerMatches(standby, source, tier, pixelWidth, pixelHeight)) {
                return
            }

            standby.visible = false
            standby.source = ""
            standby.requestedSource = source
            standby.decodeTier = tier
            standby.decodeWidth = pixelWidth
            standby.decodeHeight = pixelHeight
            standby.sourceSize = Qt.size(pixelWidth, pixelHeight)
            standby.source = source
        }

        function displayReadyLayer(layer) {
            const source = desiredSource
            const tier = desiredTier
            const pixelWidth = decodeWidth(tier)
            const pixelHeight = decodeHeight(tier)
            if (!layerMatches(layer, source, tier, pixelWidth, pixelHeight))
                return

            const previous = activeLayer()
            layer.visible = true
            if (previous !== layer)
                previous.visible = false
            frontActive = layer === frontImage
        }

        function handleLayerStatus(layer) {
            if (layer.status === Image.Ready) {
                displayReadyLayer(layer)
            } else if (layer.status === Image.Error
                       && !hasDisplayedImage
                       && layer.requestedSource.toString() === desiredSource.toString()) {
                overlay.appController.viewer.reportLoadFailure(
                    "Qt Quick Image could not load the source.")
            }
        }

        onDesiredSourceChanged: requestDisplayedImage(true)
        onDesiredTierChanged: requestDisplayedImage(false)

        Timer {
            id: resizeDecodeTimer
            interval: 120
            onTriggered: canvas.requestDisplayedImage(false)
        }

        onWidthChanged: {
            if (bufferReady)
                resizeDecodeTimer.restart()
        }
        onHeightChanged: {
            if (bufferReady)
                resizeDecodeTimer.restart()
        }

        Component.onCompleted: {
            bufferReady = true
            requestDisplayedImage(true)
        }

        component ImageLayer: Image {
            property url requestedSource
            property int decodeTier: 0
            property int decodeWidth: 0
            property int decodeHeight: 0

            anchors.fill: parent
            anchors.leftMargin: 76
            anchors.rightMargin: 76
            anchors.topMargin: 96
            anchors.bottomMargin: Theme.space6
            visible: false
            asynchronous: true
            cache: false
            fillMode: Image.PreserveAspectFit
            scale: overlay.appController.viewer.zoom
            transformOrigin: Item.Center
            transform: Translate {
                x: overlay.appController.viewer.offsetX
                y: overlay.appController.viewer.offsetY
            }
            onStatusChanged: canvas.handleLayerStatus(this)
        }

        // The displayed layer remains visible while the standby layer decodes the
        // next resolution tier, so zooming never flashes a loading placeholder.
        ImageLayer { id: frontImage }
        ImageLayer { id: backImage }

        Rectangle {
            anchors.centerIn: parent
            width: loadingRow.implicitWidth + Theme.space5 * 2
            height: 44
            radius: 22
            color: Theme.viewerChrome
            border.width: 1
            border.color: Theme.viewerLine
            visible: !canvas.hasDisplayedImage
                  && (frontImage.status === Image.Loading || backImage.status === Image.Loading)

            Row {
                id: loadingRow
                anchors.centerIn: parent
                spacing: Theme.space3
                BusyIndicator {
                    width: 22
                    height: 22
                    running: parent.parent.visible
                }
                Text {
                    anchors.verticalCenter: parent.verticalCenter
                    text: "正在載入圖片"
                    color: Theme.viewerText
                    font.pixelSize: 13
                }
            }
        }

        Rectangle {
            anchors.centerIn: parent
            width: Math.min(parent.width - 96, 500)
            height: feedbackColumn.implicitHeight + Theme.space6 * 2
            radius: Theme.largeRadius
            color: Theme.viewerChrome
            border.width: 1
            border.color: Theme.viewerLine
            visible: overlay.appController.viewer.unsupportedAnimated
                  || overlay.appController.viewer.errorMessage.length > 0

            Column {
                id: feedbackColumn
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.verticalCenter: parent.verticalCenter
                anchors.margins: Theme.space6
                spacing: Theme.space3

                AppIcon {
                    anchors.horizontalCenter: parent.horizontalCenter
                    name: "image"
                    width: 32
                    height: 32
                    color: Theme.viewerSecondaryText
                }
                Text {
                    width: parent.width
                    text: overlay.appController.viewer.errorMessage.length > 0
                          ? "圖片載入失敗" : "無法預覽動畫圖片"
                    color: Theme.viewerText
                    font.pixelSize: 20
                    font.weight: Font.DemiBold
                    horizontalAlignment: Text.AlignHCenter
                }
                Text {
                    width: parent.width
                    text: overlay.appController.viewer.errorMessage.length > 0
                          ? overlay.appController.viewer.errorMessage
                          : overlay.appController.viewer.unsupportedMessage
                    color: Theme.viewerSecondaryText
                    font.pixelSize: 13
                    wrapMode: Text.Wrap
                    horizontalAlignment: Text.AlignHCenter
                }
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
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: parent.top
        anchors.margins: Theme.space4
        height: 64
        radius: Theme.largeRadius
        color: Theme.viewerChrome
        border.width: 1
        border.color: Theme.viewerLine

        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: Theme.space5
            anchors.rightMargin: Theme.space3
            spacing: Theme.space4

            Item {
                Layout.fillWidth: true
                Layout.minimumWidth: 160
                Layout.fillHeight: true

                Column {
                    anchors.left: parent.left
                    anchors.right: parent.right
                    anchors.verticalCenter: parent.verticalCenter
                    spacing: 2
                    Text {
                        width: parent.width
                        text: overlay.appController.viewer.currentName
                        color: Theme.viewerText
                        font.pixelSize: 14
                        font.weight: Font.Medium
                        elide: Text.ElideMiddle
                    }
                    Text {
                        width: parent.width
                        text: overlay.appController.viewer.zoom > 1
                              ? "拖曳或方向鍵平移" : "方向鍵切換 · 滾輪縮放"
                        color: Theme.viewerSecondaryText
                        font.pixelSize: 11
                        elide: Text.ElideRight
                    }
                }
            }

            Row {
                spacing: Theme.space1
                Layout.alignment: Qt.AlignCenter

                ViewerButton {
                    iconName: "chevron-left"
                    accessibleName: "上一張圖片"
                    enabled: overlay.appController.viewer.canGoPrevious
                    ToolTip.visible: hovered
                    ToolTip.text: "上一張（←）"
                    onClicked: overlay.appController.viewer.previous()
                }
                Rectangle {
                    anchors.verticalCenter: parent.verticalCenter
                    width: 1
                    height: 20
                    color: Theme.viewerLine
                }
                ViewerButton {
                    iconName: "zoom-out"
                    accessibleName: "縮小圖片"
                    enabled: overlay.appController.viewer.canZoomOut
                    ToolTip.visible: hovered
                    ToolTip.text: "縮小"
                    onClicked: overlay.appController.viewer.zoomOut(canvas.width, canvas.height)
                }
                ViewerButton {
                    iconName: "fit"
                    accessibleName: "重設圖片大小"
                    enabled: overlay.appController.viewer.imageVisible
                    ToolTip.visible: hovered
                    ToolTip.text: "重設縮放"
                    onClicked: overlay.appController.viewer.resetZoom()
                }
                ViewerButton {
                    iconName: "zoom-in"
                    accessibleName: "放大圖片"
                    enabled: overlay.appController.viewer.canZoomIn
                    ToolTip.visible: hovered
                    ToolTip.text: "放大"
                    onClicked: overlay.appController.viewer.zoomIn(canvas.width, canvas.height)
                }
                Rectangle {
                    anchors.verticalCenter: parent.verticalCenter
                    width: 1
                    height: 20
                    color: Theme.viewerLine
                }
                ViewerButton {
                    iconName: "chevron-right"
                    accessibleName: "下一張圖片"
                    enabled: overlay.appController.viewer.canGoNext
                    ToolTip.visible: hovered
                    ToolTip.text: "下一張（→）"
                    onClicked: overlay.appController.viewer.next()
                }
            }

            Item {
                Layout.fillWidth: true
                Layout.minimumWidth: 160
                Layout.fillHeight: true

                ViewerButton {
                    anchors.right: parent.right
                    anchors.verticalCenter: parent.verticalCenter
                    iconName: "close"
                    accessibleName: "關閉圖片檢視器"
                    ToolTip.visible: hovered
                    ToolTip.text: "關閉（Esc）"
                    onClicked: overlay.closeViewer()
                }
            }
        }
    }

    ViewerButton {
        anchors.left: parent.left
        anchors.leftMargin: Theme.space5
        anchors.verticalCenter: parent.verticalCenter
        width: 48
        height: 48
        circular: true
        iconName: "chevron-left"
        accessibleName: "上一張圖片"
        enabled: overlay.appController.viewer.canGoPrevious
        visible: enabled
        ToolTip.visible: hovered
        ToolTip.text: "上一張（←）"
        onClicked: overlay.appController.viewer.previous()
    }

    ViewerButton {
        anchors.right: parent.right
        anchors.rightMargin: Theme.space5
        anchors.verticalCenter: parent.verticalCenter
        width: 48
        height: 48
        circular: true
        iconName: "chevron-right"
        accessibleName: "下一張圖片"
        enabled: overlay.appController.viewer.canGoNext
        visible: enabled
        ToolTip.visible: hovered
        ToolTip.text: "下一張（→）"
        onClicked: overlay.appController.viewer.next()
    }
}
