pragma ComponentBehavior: Bound
import QtQuick

Item {
    id: surface
    property bool panEnabled: false
    readonly property bool dragging: pointerArea.pressed && panEnabled
    readonly property int blockedButtons: pointerArea.acceptedButtons
    readonly property bool preventsStealing: pointerArea.preventStealing
    signal panRequested(real deltaX, real deltaY)
    signal zoomRequested(real pointerX, real pointerY, real angleDeltaY)

    function beginPointer(pointerX, pointerY) {
        pointerArea.previousX = pointerX
        pointerArea.previousY = pointerY
    }

    function updatePointer(pointerX, pointerY, buttons) {
        if (!panEnabled || !(buttons & Qt.LeftButton))
            return
        panRequested(
            pointerX - pointerArea.previousX,
            pointerY - pointerArea.previousY)
        pointerArea.previousX = pointerX
        pointerArea.previousY = pointerY
    }

    // Take the initial grab so an input-transparent viewer overlay cannot pass
    // the press through to gallery delegates underneath it.
    MouseArea {
        id: pointerArea
        anchors.fill: parent
        acceptedButtons: Qt.LeftButton | Qt.RightButton | Qt.MiddleButton
        preventStealing: true
        property real previousX: 0
        property real previousY: 0

        onPressed: function(mouse) {
            surface.beginPointer(mouse.x, mouse.y)
            mouse.accepted = true
        }
        onPositionChanged: function(mouse) {
            surface.updatePointer(mouse.x, mouse.y, mouse.buttons)
        }
        onWheel: function(event) {
            surface.zoomRequested(event.x, event.y, event.angleDelta.y)
            event.accepted = true
        }
    }
}
