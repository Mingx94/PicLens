import QtQuick
import QtQuick.Controls.Basic

ComboBox {
    id: control
    required property Theme theme
    implicitHeight: 36
    leftPadding: 12
    rightPadding: 32
    opacity: enabled ? 1 : 0.4
    contentItem: Text {
        text: control.displayText
        font: control.font
        color: control.theme.foreground
        verticalAlignment: Text.AlignVCenter
        elide: Text.ElideRight
    }
    indicator: ActionButton {
        theme: control.theme
        x: control.width - width - 4
        y: (control.height - height) / 2
        width: 24
        height: 24
        glyph: "chevron-right"
        rotation: 90
        enabled: false
        Accessible.ignored: true
        opacity: 1
        background: Item {}
    }
    background: Rectangle {
        radius: 6
        color: control.down ? control.theme.muted : control.theme.card
        border.width: control.activeFocus ? 2 : 1
        border.color: control.activeFocus ? control.theme.secondary : control.theme.input
    }
    delegate: ItemDelegate {
        id: option
        required property int index
        required property var modelData
        width: control.width
        height: 36
        highlighted: control.highlightedIndex === index
        Accessible.name: modelData
        Accessible.selectable: true
        Accessible.selected: control.currentIndex === index
        contentItem: Text {
            text: option.modelData
            font: control.font
            color: control.theme.foreground
            verticalAlignment: Text.AlignVCenter
            elide: Text.ElideRight
        }
        background: Rectangle {
            radius: 4
            color: option.highlighted ? control.theme.muted : "transparent"
        }
    }
    popup: Popup {
        y: control.height + 4
        width: control.width
        padding: 4
        implicitHeight: Math.min(contentItem.implicitHeight + 8, 240)
        contentItem: ListView {
            clip: true
            implicitHeight: contentHeight
            model: control.popup.visible ? control.delegateModel : null
            currentIndex: control.highlightedIndex
            ScrollIndicator.vertical: ScrollIndicator {}
        }
        background: Rectangle {
            radius: 6
            color: control.theme.card
            border.color: control.theme.border
        }
    }
}
