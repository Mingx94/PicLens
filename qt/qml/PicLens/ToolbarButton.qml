import QtQuick
import QtQuick.Controls

Button {
    id: control
    property string symbol: ""
    property string accessibleName: text.length > 0 ? text : symbol

    Accessible.role: Accessible.Button
    Accessible.name: accessibleName
    Accessible.description: ToolTip.text
    Accessible.focusable: true
    Accessible.onPressAction: control.click()

    implicitHeight: 38
    implicitWidth: text.length > 0 ? contentRow.implicitWidth + 24 : 38
    leftPadding: 12
    rightPadding: 12
    focusPolicy: Qt.StrongFocus

    contentItem: Row {
        id: contentRow
        spacing: 7
        anchors.centerIn: parent

        Text {
            visible: control.symbol.length > 0
            text: control.symbol
            color: control.enabled ? Theme.primaryText : Theme.mutedText
            font.pixelSize: 17
            horizontalAlignment: Text.AlignHCenter
            verticalAlignment: Text.AlignVCenter
        }
        Text {
            visible: control.text.length > 0
            text: control.text
            color: control.enabled ? Theme.primaryText : Theme.mutedText
            font.pixelSize: 14
            font.weight: Font.Medium
            verticalAlignment: Text.AlignVCenter
        }
    }

    background: Rectangle {
        radius: Theme.cornerRadius
        color: control.down ? Theme.accentSoftPressed
                            : control.checked ? Theme.selected
                            : control.hovered ? Theme.hover : "transparent"
        border.width: control.activeFocus ? 1 : 0
        border.color: Theme.accent
    }
}
