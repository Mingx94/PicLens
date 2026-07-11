pragma ComponentBehavior: Bound
import QtQuick
import QtQuick.Controls
import PicLens

Rectangle {
    id: pane
    required property AppController appController
    color: Theme.sidebar

    Rectangle {
        anchors.right: parent.right
        anchors.top: parent.top
        anchors.bottom: parent.bottom
        width: 1
        color: Theme.line
    }

    Column {
        anchors.fill: parent
        anchors.leftMargin: Theme.space3
        anchors.rightMargin: Theme.space3
        anchors.topMargin: Theme.space5
        anchors.bottomMargin: Theme.space4
        spacing: Theme.space3

        Row {
            width: parent.width
            spacing: Theme.space2

            Text {
                text: "資料夾"
                color: Theme.primaryText
                font.pixelSize: 15
                font.weight: Font.Bold
            }
            BusyIndicator {
                width: 22
                height: 22
                running: pane.appController.folderTree.busy
                visible: running
            }
        }

        Text {
            width: parent.width
            text: pane.appController.folderTree.rootPath || "尚未選擇瀏覽根目錄"
            color: Theme.secondaryText
            font.pixelSize: 12
            elide: Text.ElideMiddle
            leftPadding: Theme.space1
        }

        TreeView {
            id: treeView
            width: parent.width
            height: parent.height - y
            clip: true
            model: pane.appController.folderTree
            boundsBehavior: Flickable.StopAtBounds
            columnWidthProvider: function(column) {
                return column === 0 ? width : 0
            }

            delegate: Rectangle {
                id: treeDelegate
                required property TreeView treeView
                required property bool isTreeNode
                required property bool expanded
                required property int hasChildren
                required property int depth
                required property int row
                required property string treeLabel
                required property string path
                required property bool currentFolder
                required property bool loading
                required property bool childrenLoaded
                required property bool shouldExpand

                activeFocusOnTab: true
                Accessible.role: Accessible.TreeItem
                Accessible.name: treeLabel
                Accessible.description: currentFolder ? "目前資料夾" : "資料夾"
                Accessible.focusable: true
                Accessible.selectable: true
                Accessible.selected: currentFolder
                Accessible.onPressAction: pane.appController.navigateFromTree(treeDelegate.path)

                function synchronizeExpansion() {
                    if (shouldExpand && !expanded)
                        treeView.toggleExpanded(row)
                }

                width: treeView.width
                implicitHeight: 36
                radius: Theme.cornerRadius
                color: currentFolder ? Theme.selected
                                     : hoverHandler.hovered ? Theme.hover : "transparent"

                Rectangle {
                    visible: treeDelegate.currentFolder
                    anchors.left: parent.left
                    anchors.leftMargin: 2
                    anchors.verticalCenter: parent.verticalCenter
                    width: 3
                    height: 20
                    radius: 2
                    color: Theme.accent
                }

                AppIcon {
                    id: disclosure
                    x: Theme.space3 + treeDelegate.depth * 16
                    anchors.verticalCenter: parent.verticalCenter
                    width: 22
                    height: 22
                    visible: treeDelegate.isTreeNode && treeDelegate.hasChildren
                    name: treeDelegate.expanded ? "chevron-down" : "chevron-right"
                    color: treeDelegate.currentFolder ? Theme.accent : Theme.mutedText

                    TapHandler {
                        enabled: disclosure.visible
                        onTapped: treeDelegate.treeView.toggleExpanded(treeDelegate.row)
                    }
                }
                Text {
                    anchors.left: disclosure.right
                    anchors.right: loadingIndicator.left
                    anchors.leftMargin: Theme.space1
                    anchors.rightMargin: Theme.space2
                    anchors.verticalCenter: parent.verticalCenter
                    text: treeDelegate.treeLabel
                    color: treeDelegate.currentFolder ? Theme.accent : Theme.primaryText
                    font.pixelSize: 13
                    font.weight: treeDelegate.currentFolder ? Font.Medium : Font.Normal
                    elide: Text.ElideRight
                }
                BusyIndicator {
                    id: loadingIndicator
                    anchors.right: parent.right
                    anchors.rightMargin: Theme.space2
                    anchors.verticalCenter: parent.verticalCenter
                    width: 20
                    height: 20
                    running: treeDelegate.loading
                    visible: running
                }
                HoverHandler {
                    id: hoverHandler
                }
                TapHandler {
                    onTapped: pane.appController.navigateFromTree(treeDelegate.path)
                    onDoubleTapped: treeDelegate.treeView.toggleExpanded(treeDelegate.row)
                }
                Keys.onReturnPressed: function(event) {
                    pane.appController.navigateFromTree(treeDelegate.path)
                    event.accepted = true
                }
                Keys.onEnterPressed: function(event) {
                    pane.appController.navigateFromTree(treeDelegate.path)
                    event.accepted = true
                }
                Keys.onRightPressed: function(event) {
                    if (treeDelegate.hasChildren && !treeDelegate.expanded)
                        treeDelegate.treeView.toggleExpanded(treeDelegate.row)
                    event.accepted = true
                }
                Keys.onLeftPressed: function(event) {
                    if (treeDelegate.expanded)
                        treeDelegate.treeView.toggleExpanded(treeDelegate.row)
                    event.accepted = true
                }
                onExpandedChanged: {
                    if (expanded && !childrenLoaded)
                        pane.appController.folderTree.loadChildren(treeView.index(row, 0))
                }
                onShouldExpandChanged: synchronizeExpansion()
                Component.onCompleted: synchronizeExpansion()
            }
        }
    }
}
