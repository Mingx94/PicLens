import QtQuick
import QtQuick.Controls

Button {
    id: control
    property string symbol: ""
    property string iconName: ""
    property string trailingIconName: ""
    property string accessibleName: text.length > 0 ? text : symbol
    property bool primary: false
    property bool outlined: false

    Accessible.role: Accessible.Button
    Accessible.name: accessibleName
    Accessible.description: ToolTip.text
    Accessible.focusable: true
    Accessible.onPressAction: control.click()

    implicitHeight: Theme.controlHeight
    implicitWidth: text.length > 0 ? contentRow.implicitWidth + 24 : 38
    leftPadding: text.length > 0 ? 12 : 0
    rightPadding: text.length > 0 ? 12 : 0
    topPadding: 0
    bottomPadding: 0
    focusPolicy: Qt.StrongFocus

    contentItem: Item {
        implicitWidth: contentRow.implicitWidth
        implicitHeight: contentRow.implicitHeight

        Row {
            id: contentRow
            objectName: "toolbarContentRow"
            anchors.centerIn: parent
            spacing: 7

            AppIcon {
                visible: control.iconName.length > 0
                name: control.iconName
                width: 20
                height: 20
                color: !control.enabled ? Theme.mutedText
                     : control.primary ? "white" : Theme.primaryText
            }
            Text {
                visible: control.iconName.length === 0 && control.symbol.length > 0
                text: control.symbol
                color: !control.enabled ? Theme.mutedText
                      : control.primary ? "white" : Theme.primaryText
                font.pixelSize: 17
                horizontalAlignment: Text.AlignHCenter
                verticalAlignment: Text.AlignVCenter
            }
            Text {
                visible: control.text.length > 0
                text: control.text
                color: !control.enabled ? Theme.mutedText
                      : control.primary ? "white" : Theme.primaryText
                font.pixelSize: 14
                font.weight: Font.Medium
                verticalAlignment: Text.AlignVCenter
            }
            AppIcon {
                visible: control.trailingIconName.length > 0
                name: control.trailingIconName
                width: 15
                height: 15
                color: !control.enabled ? Theme.mutedText
                     : control.primary ? "white" : Theme.primaryText
            }
        }
    }

    background: Rectangle {
        radius: Theme.cornerRadius
        color: control.primary
               ? (control.down ? Theme.accentPressed
                  : control.hovered ? Theme.accentHover : Theme.accent)
               : control.down ? Theme.accentSoftPressed
                            : control.checked ? Theme.selected
                            : control.hovered ? Theme.hover : control.outlined ? Theme.surface : "transparent"
        border.width: control.activeFocus || control.outlined || control.checked ? 1 : 0
        border.color: control.activeFocus || control.checked ? Theme.accent : Theme.strongLine
    }
}
