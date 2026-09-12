import QtQuick
import QtQuick.Controls.Basic

Slider {
    id: control
    required property Theme theme
    implicitHeight: 36
    opacity: enabled ? 1 : 0.4
    background: Rectangle {
        x: control.leftPadding
        y: control.topPadding + control.availableHeight / 2 - height / 2
        width: control.availableWidth
        height: 4
        radius: 2
        color: control.theme.border
        Rectangle {
            x: control.mirrored ? parent.width - width : 0
            width: control.position * parent.width
            height: parent.height
            radius: 2
            color: control.theme.secondary
        }
    }
    handle: Rectangle {
        x: control.leftPadding + control.visualPosition * (control.availableWidth - width)
        y: control.topPadding + control.availableHeight / 2 - height / 2
        width: 18
        height: 18
        radius: 9
        color: control.pressed ? control.theme.muted : control.theme.card
        border.color: control.activeFocus ? control.theme.foreground : control.theme.secondary
        border.width: control.activeFocus ? 2 : 1
    }
}
