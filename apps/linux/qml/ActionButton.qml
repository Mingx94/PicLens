import QtQuick
import QtQuick.Controls.Basic
import QtQuick.Controls.impl

ToolButton {
    id: control
    property string glyph: ""
    property string hint: text
    property bool prominent: false
    property bool destructive: false
    required property Theme theme
    readonly property color ink: control.destructive ? control.theme.danger : control.prominent ? control.theme.primaryForeground : control.theme.foreground
    implicitHeight: 36
    implicitWidth: text.length ? Math.max(72, contentItem.implicitWidth + 24) : 36
    icon.source: glyph ? "qrc:/icons/" + glyph + ".svg" : ""
    icon.width: 16
    icon.height: 16
    icon.color: control.ink
    palette.buttonText: control.ink
    palette.windowText: control.ink
    palette.text: control.ink
    // Basic ToolButton uses highlight for its label when visualFocus is true.
    palette.highlight: control.ink
    opacity: enabled ? 1 : 0.4
    hoverEnabled: true
    Accessible.name: hint
    ToolTip.visible: hovered && hint.length > 0
    ToolTip.text: hint
    ToolTip.delay: 650
    contentItem: IconLabel {
        spacing: control.spacing
        mirrored: control.mirrored
        display: control.display
        icon: control.icon
        defaultIconColor: control.ink
        text: control.text
        font: control.font
        color: control.ink
    }
    background: Rectangle {
        radius: 6
        color: control.prominent ? control.theme.primary : control.down || control.hovered ? control.theme.muted : "transparent"
        border.width: control.activeFocus ? 2 : 1
        border.color: control.activeFocus ? control.theme.secondary : control.prominent ? control.theme.primary : control.theme.border
    }
}
