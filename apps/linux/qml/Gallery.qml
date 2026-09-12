import QtQuick
import QtQuick.Controls.Basic
import QtQuick.Layouts

Item {
    id: gallery
    required property Theme theme
    signal contextRequested(string path, bool folder, real x, real y)
    property bool dragging: false
    property real dragY: 0
    property string dragPath: ""
    property var platformHints: Qt.styleHints
    property var materialized: ({})
    function focusGrid() {
        grid.forceActiveFocus();
    }
    onVisibleChanged: if (!visible) {
        endDrag();
        app.setVisible([]);
    }

    function reportVisible() {
        let paths = [];
        let materializedCount = 0;
        for (let key in materialized) {
            let item = materialized[key];
            if (!item)
                continue;
            ++materializedCount;
            if (!item.pooled && item.y + item.height > grid.contentY && item.y < grid.contentY + grid.height)
                paths.push(item.path);
        }
        app.reportMaterialized(materializedCount);
        app.setVisible(paths);
    }
    function endDrag(commit) {
        if (dragging) {
            if (commit)
                ghost.Drag.drop();
            else
                ghost.Drag.cancel();
        }
        dragging = false;
        app.setDragActive(false);
        dragPath = "";
    }
    Component.onDestruction: app.setVisible([])
    GridView {
        id: grid
        anchors.fill: parent
        anchors.rightMargin: 16
        clip: true
        focus: true
        model: app.library
        reuseItems: true
        cacheBuffer: Math.min(600, cellHeight * 2)
        cellWidth: Math.max(112, app.thumbnailSize + 16)
        cellHeight: cellWidth + 48
        boundsBehavior: Flickable.StopAtBounds
        ScrollBar.vertical: ScrollBar {
            x: grid.width + 4
            width: 12
        }
        Keys.onPressed: event => {
            let columns = Math.max(1, Math.floor(grid.width / grid.cellWidth));
            let delta = event.key === Qt.Key_Left ? -1 : event.key === Qt.Key_Right ? 1 : event.key === Qt.Key_Up ? -columns : event.key === Qt.Key_Down ? columns : 0;
            if (delta) {
                app.moveSelection(delta, !!(event.modifiers & Qt.ShiftModifier));
                event.accepted = true;
            } else if (event.key === Qt.Key_Return || event.key === Qt.Key_Enter) {
                app.openViewer();
                event.accepted = true;
            } else if (event.key === Qt.Key_Escape) {
                gallery.endDrag();
                app.clearSelection();
                event.accepted = true;
            } else if (event.key === Qt.Key_Delete) {
                app.requestOperation("trash");
                event.accepted = true;
            } else if (event.key === Qt.Key_F2) {
                app.requestOperation("rename");
                event.accepted = true;
            }
        }
        delegate: Item {
            id: tile
            required property int index
            required property string path
            required property string name
            required property bool folder
            required property bool animated
            required property bool selected
            required property string imageKey
            required property string error
            required property string detail
            property bool pooled: false
            property string registryKey: ""
            width: grid.cellWidth
            height: grid.cellHeight
            function registerItem() {
                if (registryKey)
                    delete gallery.materialized[registryKey];
                registryKey = path;
                gallery.materialized[registryKey] = tile;
            }
            Component.onCompleted: registerItem()
            Component.onDestruction: {
                if (gallery.materialized[registryKey] === tile)
                    delete gallery.materialized[registryKey];
            }
            onPathChanged: registerItem()
            GridView.onPooled: {
                if (gallery.dragPath === path)
                    gallery.endDrag();
                pooled = true;
            }
            GridView.onReused: {
                pooled = false;
                registerItem();
            }
            Rectangle {
                anchors.fill: parent
                anchors.margins: 4
                radius: 10
                color: tile.selected || pointer.containsMouse ? theme.muted : theme.card
                border.color: tile.selected ? theme.foreground : theme.border
                border.width: tile.selected ? 2 : 1
                ColumnLayout {
                    anchors.fill: parent
                    anchors.margins: 8
                    spacing: 4
                    Item {
                        Layout.fillWidth: true
                        Layout.preferredHeight: width
                        Image {
                            id: preview
                            anchors.fill: parent
                            // Keep one binding alive across initialization and reuse.
                            // Pooling switches this to empty without imperative writes.
                            source: !tile.pooled && !tile.folder && tile.imageKey ? "image://thumb/" + tile.imageKey : ""
                            cache: false
                            asynchronous: false
                            fillMode: Image.PreserveAspectCrop
                            clip: true
                        }
                        ActionButton {
                            anchors.centerIn: parent
                            theme: gallery.theme
                            glyph: tile.folder ? "folder" : tile.error ? "circle-alert" : "image"
                            visible: tile.folder || preview.status !== Image.Ready
                            enabled: false
                            width: 56
                            height: 56
                            icon.width: 40
                            icon.height: 40
                            background: Item {}
                        }
                        Label {
                            anchors.right: parent.right
                            anchors.bottom: parent.bottom
                            text: tile.selected ? "已選取" : tile.animated ? "動態" : ""
                            visible: text.length > 0
                            padding: 4
                            color: theme.foreground
                            background: Rectangle {
                                color: theme.card
                                radius: 4
                            }
                        }
                    }
                    Label {
                        Layout.fillWidth: true
                        text: tile.name
                        elide: Text.ElideMiddle
                        color: theme.foreground
                    }
                    Label {
                        Layout.fillWidth: true
                        text: tile.error || tile.detail
                        elide: Text.ElideRight
                        font.pixelSize: 12
                        color: tile.error ? theme.danger : theme.secondary
                    }
                }
                DropArea {
                    anchors.fill: parent
                    enabled: !tile.folder && !app.busy
                    onEntered: drag => {
                        drag.accepted = gallery.dragging && tile.path !== gallery.dragPath;
                    }
                    onDropped: drop => {
                        if (gallery.dragging && tile.path !== gallery.dragPath) {
                            app.dropRename(tile.path);
                            drop.acceptProposedAction();
                        }
                    }
                    Rectangle {
                        anchors.fill: parent
                        radius: 10
                        color: "transparent"
                        border.width: 3
                        border.color: theme.foreground
                        visible: parent.containsDrag
                    }
                }
                MouseArea {
                    id: pointer
                    anchors.fill: parent
                    acceptedButtons: Qt.LeftButton | Qt.RightButton
                    hoverEnabled: true
                    property point pressPoint
                    property bool moved: false
                    property bool deferredSelection: false
                    property int pressModifiers: 0
                    onPressed: mouse => {
                        grid.forceActiveFocus();
                        pressPoint = Qt.point(mouse.x, mouse.y);
                        moved = false;
                        pressModifiers = mouse.modifiers;
                        deferredSelection = false;
                        if (mouse.button === Qt.RightButton) {
                            app.select(tile.index, mouse.modifiers, true);
                            let p = mapToItem(gallery, mouse.x, mouse.y);
                            gallery.contextRequested(tile.path, tile.folder, p.x, p.y);
                        } else if (!tile.folder) {
                            deferredSelection = tile.selected && !(mouse.modifiers & (Qt.ControlModifier | Qt.ShiftModifier));
                            if (!deferredSelection)
                                app.select(tile.index, mouse.modifiers, false);
                        }
                    }
                    onPositionChanged: mouse => {
                        if (!(pressedButtons & Qt.LeftButton) || app.busy)
                            return;
                        let dx = mouse.x - pressPoint.x, dy = mouse.y - pressPoint.y;
                        if (!moved && Math.sqrt(dx * dx + dy * dy) >= gallery.platformHints.startDragDistance) {
                            moved = true;
                            if (tile.folder)
                                return;
                            gallery.dragPath = tile.path;
                            gallery.dragging = true;
                            app.setDragActive(true);
                        }
                        if (gallery.dragging) {
                            let p = mapToItem(gallery, mouse.x, mouse.y);
                            ghost.x = p.x - 16;
                            ghost.y = p.y - 16;
                            gallery.dragY = p.y;
                        }
                    }
                    onReleased: mouse => {
                        let click = !moved && containsMouse && mouse.button === Qt.LeftButton;
                        gallery.endDrag(true);
                        if (click && tile.folder) {
                            let path = tile.path;
                            Qt.callLater(function () {
                                app.navigate(path);
                            });
                        } else if (click && deferredSelection) {
                            app.select(tile.index, pressModifiers, false);
                        }
                        deferredSelection = false;
                    }
                    onCanceled: {
                        deferredSelection = false;
                        gallery.endDrag();
                    }
                    onDoubleClicked: mouse => {
                        if (mouse.button === Qt.LeftButton && !moved) {
                            if (!tile.folder)
                                app.openViewer(tile.index);
                        }
                    }
                    ToolTip.visible: containsMouse && !pressed
                    ToolTip.delay: 900
                    ToolTip.text: tile.name + (tile.error ? "\n" + tile.error : "")
                    Accessible.name: tile.name
                    Accessible.role: Accessible.ListItem
                }
            }
        }
    }
    Rectangle {
        id: ghost
        width: 32
        height: 32
        radius: 6
        color: theme.foreground
        opacity: 0.65
        visible: gallery.dragging
        z: 100
        Drag.active: gallery.dragging
        Drag.source: gallery
        Drag.hotSpot.x: 16
        Drag.hotSpot.y: 16
    }
    Timer {
        interval: 120
        running: gallery.visible
        repeat: true
        onTriggered: gallery.reportVisible()
    }
    Timer {
        interval: 16
        running: gallery.dragging
        repeat: true
        onTriggered: {
            let speed = gallery.dragY < 48 ? -12 : gallery.dragY > gallery.height - 48 ? 12 : 0;
            grid.contentY = Math.max(0, Math.min(Math.max(0, grid.contentHeight - grid.height), grid.contentY + speed));
        }
    }
    Connections {
        target: app
        function onScrollTo(row) {
            grid.positionViewAtIndex(row, GridView.Contain);
        }
        function onFocusGallery() {
            gallery.focusGrid();
        }
    }
}
