pragma ComponentBehavior: Bound
import QtQuick

Item {
    id: handler
    signal backRequested()
    signal forwardRequested()

    function handleButton(button) {
        if (button === Qt.BackButton)
            backRequested()
        else if (button === Qt.ForwardButton)
            forwardRequested()
    }

    TapHandler {
        acceptedButtons: Qt.BackButton | Qt.ForwardButton
        gesturePolicy: TapHandler.ReleaseWithinBounds
        onTapped: function(eventPoint, button) {
            handler.handleButton(button)
        }
    }
}
