import QtQuick
import QtQuick.Controls

ComboBox {
    id: control
    property string labelText: currentText

    implicitHeight: Theme.controlHeight
    leftPadding: Theme.space3
    rightPadding: 34
    topPadding: 0
    bottomPadding: 0
    hoverEnabled: true

    contentItem: Text {
        text: control.labelText
        color: control.enabled ? Theme.primaryText : Theme.mutedText
        font.pixelSize: 13
        font.weight: Font.Medium
        verticalAlignment: Text.AlignVCenter
        elide: Text.ElideRight
    }

    indicator: AppIcon {
        x: control.width - width - 11
        y: Math.round((control.height - height) / 2)
        width: 16
        height: 16
        name: "chevron-down"
        color: control.enabled ? Theme.primaryText : Theme.mutedText
    }

    background: Rectangle {
        radius: Theme.cornerRadius
        color: control.down ? Theme.hover : Theme.surface
        border.width: 1
        border.color: control.activeFocus ? Theme.accent
                    : control.hovered ? Theme.strongLine : Theme.line
    }
}
