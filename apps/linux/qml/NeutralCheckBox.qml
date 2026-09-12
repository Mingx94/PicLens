import QtQuick
import QtQuick.Controls.Basic

CheckBox {
    id: control
    required property Theme theme
    implicitHeight: 36
    spacing: 8
    opacity: enabled ? 1 : 0.4
    indicator: Rectangle {
        x: control.leftPadding
        y: control.topPadding + (control.availableHeight - height) / 2
        width: 18
        height: 18
        radius: 4
        color: control.checkState !== Qt.Unchecked ? control.theme.primary : control.theme.background
        border.color: control.activeFocus ? control.theme.foreground : control.theme.input
        border.width: control.activeFocus ? 2 : 1
        Item {
            anchors.centerIn: parent
            width: 10
            height: 8
            visible: control.checkState === Qt.Checked
            Rectangle {
                x: 1
                y: 3
                width: 2
                height: 5
                rotation: -45
                color: control.theme.primaryForeground
            }
            Rectangle {
                x: 6
                y: 0
                width: 2
                height: 9
                rotation: 40
                color: control.theme.primaryForeground
            }
        }
        Rectangle {
            anchors.centerIn: parent
            width: 10
            height: 2
            visible: control.checkState === Qt.PartiallyChecked
            color: control.theme.primaryForeground
        }
    }
    contentItem: Text {
        text: control.text
        font: control.font
        color: control.theme.foreground
        leftPadding: control.indicator.width + control.spacing
        verticalAlignment: Text.AlignVCenter
    }
}
