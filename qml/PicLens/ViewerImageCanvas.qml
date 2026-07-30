pragma ComponentBehavior: Bound
import QtQuick
import QtQuick.Controls
import QtQuick.Window
import PicLens

Item {
    id: canvas
    required property AppController appController
    required property real navigationRailWidth
    clip: true

    readonly property url desiredSource: appController.viewer.currentSourceUrl
    readonly property int desiredTier: decodeTier(appController.viewer.zoom)
    readonly property bool hasDisplayedImage: frontImage.visible || backImage.visible
    property bool frontActive: true
    property bool bufferReady: false

    function decodeTier(zoom) {
        if (zoom <= 1)
            return 1
        if (zoom <= 2)
            return 2
        return 4
    }

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
        if (!appController.viewer.imageVisible || source.toString().length === 0) {
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
            appController.viewer.reportLoadFailure(
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
        anchors.leftMargin: canvas.navigationRailWidth + Theme.space4
        anchors.rightMargin: canvas.navigationRailWidth + Theme.space4
        anchors.topMargin: 96
        anchors.bottomMargin: Theme.space6
        visible: false
        asynchronous: true
        cache: false
        fillMode: Image.PreserveAspectFit
        scale: canvas.appController.viewer.zoom
        transformOrigin: Item.Center
        transform: Translate {
            x: canvas.appController.viewer.offsetX
            y: canvas.appController.viewer.offsetY
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
        visible: canvas.appController.viewer.unsupportedAnimated
              || canvas.appController.viewer.errorMessage.length > 0

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
                text: canvas.appController.viewer.errorMessage.length > 0
                      ? "圖片載入失敗" : "無法預覽動畫圖片"
                color: Theme.viewerText
                font.pixelSize: 20
                font.weight: Font.DemiBold
                horizontalAlignment: Text.AlignHCenter
            }
            Text {
                width: parent.width
                text: canvas.appController.viewer.errorMessage.length > 0
                      ? canvas.appController.viewer.errorMessage
                      : canvas.appController.viewer.unsupportedMessage
                color: Theme.viewerSecondaryText
                font.pixelSize: 13
                wrapMode: Text.Wrap
                horizontalAlignment: Text.AlignHCenter
            }
        }
    }

    ViewerPointerSurface {
        anchors.fill: parent
        panEnabled: canvas.appController.viewer.zoom > 1
        onPanRequested: function(deltaX, deltaY) {
            canvas.appController.viewer.panBy(deltaX, deltaY)
        }
        onZoomRequested: function(pointerX, pointerY, angleDeltaY) {
            canvas.appController.viewer.zoomAt(
                pointerX,
                pointerY,
                angleDeltaY,
                canvas.width,
                canvas.height)
        }
    }
}
