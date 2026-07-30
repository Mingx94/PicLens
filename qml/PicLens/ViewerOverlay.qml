pragma ComponentBehavior: Bound
import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import PicLens

Rectangle {
    id: overlay
    required property AppController appController
    visible: appController.viewer.open
    z: 100
    color: Theme.viewerCanvas
    focus: visible
    readonly property real navigationRailWidth: Theme.viewerRailWidthFor(width)

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

    function panVertical(deltaY) {
        appController.viewer.panBy(0, deltaY)
    }

    component ViewerButton: Button {
        id: control
        property string iconName: ""
        property string accessibleName: ""

        implicitWidth: 38
        implicitHeight: 38
        padding: 0
        focusPolicy: Qt.StrongFocus
        Accessible.role: Accessible.Button
        Accessible.name: accessibleName
        Accessible.description: ToolTip.text
        Accessible.focusable: true
        Accessible.onPressAction: control.click()
        ToolTip.visible: hovered && ToolTip.text.length > 0

        contentItem: AppIcon {
            name: control.iconName
            width: 20
            height: 20
            color: control.enabled ? Theme.viewerText : Theme.viewerDisabledText
        }

        background: Rectangle {
            radius: Theme.cornerRadius
            color: control.down ? Theme.viewerPressed
                 : control.hovered || control.activeFocus ? Theme.viewerHover
                 : "transparent"
            border.width: control.activeFocus ? 1 : 0
            border.color: Theme.viewerText
        }
    }

    component ViewerNavigationRail: Button {
        id: rail
        required property string iconName
        required property string accessibleName
        required property bool leftEdge

        implicitWidth: overlay.navigationRailWidth
        padding: 0
        focusPolicy: Qt.StrongFocus
        Accessible.role: Accessible.Button
        Accessible.name: accessibleName
        Accessible.description: ToolTip.text
        Accessible.focusable: true
        Accessible.onPressAction: rail.click()
        ToolTip.visible: hovered && enabled && ToolTip.text.length > 0

        contentItem: Item {
            AppIcon {
                anchors.centerIn: parent
                name: rail.iconName
                width: Theme.viewerRailIconSize
                height: Theme.viewerRailIconSize
                color: rail.enabled ? Theme.viewerText : Theme.viewerDisabledText
            }
        }

        background: Rectangle {
            color: rail.down ? Theme.viewerPressed
                 : rail.enabled && (rail.hovered || rail.activeFocus) ? Theme.viewerHover
                 : Theme.viewerRailSurface

            Rectangle {
                anchors.top: parent.top
                anchors.bottom: parent.bottom
                x: rail.leftEdge ? parent.width - width : 0
                width: 1
                color: Theme.viewerLine
            }
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
        onActivated: overlay.panVertical(48)
    }
    Shortcut {
        sequence: "Down"
        context: Qt.WindowShortcut
        onActivated: overlay.panVertical(-48)
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
        panVertical(48)
        event.accepted = true
    }
    Keys.onDownPressed: function(event) {
        panVertical(-48)
        event.accepted = true
    }

    onVisibleChanged: {
        if (visible)
            forceActiveFocus(Qt.OtherFocusReason)
    }
    Component.onCompleted: forceActiveFocus(Qt.OtherFocusReason)

    ViewerImageCanvas {
        id: canvas
        anchors.fill: parent
        appController: overlay.appController
        navigationRailWidth: overlay.navigationRailWidth
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
                    ToolTip.text: "縮小"
                    onClicked: overlay.appController.viewer.zoomOut(canvas.width, canvas.height)
                }
                ViewerButton {
                    iconName: "fit"
                    accessibleName: "重設圖片大小"
                    enabled: overlay.appController.viewer.imageVisible
                    ToolTip.text: "重設縮放"
                    onClicked: overlay.appController.viewer.resetZoom()
                }
                ViewerButton {
                    iconName: "zoom-in"
                    accessibleName: "放大圖片"
                    enabled: overlay.appController.viewer.canZoomIn
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
                    ToolTip.text: "下一張（→）"
                    onClicked: overlay.appController.viewer.next()
                }
            }

            Item {
                Layout.fillWidth: true
                Layout.minimumWidth: 160
                Layout.fillHeight: true

                Row {
                    anchors.right: parent.right
                    anchors.verticalCenter: parent.verticalCenter
                    spacing: Theme.space1

                    ViewerButton {
                        iconName: "folder-open"
                        accessibleName: "在檔案管理器中顯示"
                        enabled: overlay.appController.viewer.currentPath.length > 0
                            ToolTip.text: "在檔案管理器中顯示"
                        onClicked: overlay.appController.fileOperations.reveal(
                            overlay.appController.viewer.currentPath)
                    }
                    ViewerButton {
                        iconName: "close"
                        accessibleName: "關閉圖片檢視器"
                            ToolTip.text: "關閉（Esc）"
                        onClicked: overlay.closeViewer()
                    }
                }
            }
        }
    }

    ViewerNavigationRail {
        anchors.left: parent.left
        anchors.top: commandBar.bottom
        anchors.bottom: parent.bottom
        anchors.topMargin: Theme.space4
        iconName: "chevron-left"
        accessibleName: "上一張圖片"
        leftEdge: true
        enabled: overlay.appController.viewer.canGoPrevious
        ToolTip.text: "上一張（←）"
        onClicked: overlay.appController.viewer.previous()
    }

    ViewerNavigationRail {
        anchors.right: parent.right
        anchors.top: commandBar.bottom
        anchors.bottom: parent.bottom
        anchors.topMargin: Theme.space4
        iconName: "chevron-right"
        accessibleName: "下一張圖片"
        leftEdge: false
        enabled: overlay.appController.viewer.canGoNext
        ToolTip.text: "下一張（→）"
        onClicked: overlay.appController.viewer.next()
    }
}
