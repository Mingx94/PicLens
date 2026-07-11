import QtQuick
import QtQuick.Shapes

Item {
    id: icon
    property string name: ""
    property color color: Theme.primaryText
    property real strokeWidth: 1.8
    implicitWidth: 20
    implicitHeight: 20

    function pathForName() {
        switch (name) {
        case "menu":
            return "M4 6.5 L20 6.5 M4 12 L20 12 M4 17.5 L20 17.5"
        case "chevron-left":
            return "M15 18 L9 12 L15 6"
        case "chevron-right":
            return "M9 18 L15 12 L9 6"
        case "chevron-down":
            return "M6 9 L12 15 L18 9"
        case "refresh":
            return "M20 11 C19.5 7 16.1 4 12 4 C7.6 4 4 7.6 4 12 C4 16.4 7.6 20 12 20 C15.4 20 18.3 17.9 19.4 14.8 M20 4 L20 11 L13 11"
        case "search":
            return "M11 19 A8 8 0 1 1 19 11 A8 8 0 0 1 11 19 M17 17 L21 21"
        case "plus":
            return "M12 5 L12 19 M5 12 L19 12"
        case "close":
            return "M6 6 L18 18 M18 6 L6 18"
        case "grid":
        case "grid-filled":
            return "M4 4 L10 4 L10 10 L4 10 Z M14 4 L20 4 L20 10 L14 10 Z M4 14 L10 14 L10 20 L4 20 Z M14 14 L20 14 L20 20 L14 20 Z"
        case "list":
            return "M9 6 L20 6 M9 12 L20 12 M9 18 L20 18 M4 6 L4.01 6 M4 12 L4.01 12 M4 18 L4.01 18"
        case "more":
            return "M6 12 L6.01 12 M12 12 L12.01 12 M18 12 L18.01 12"
        case "image":
            return "M3 5 L21 5 L21 19 L3 19 Z M3 16 L8.5 10.5 L13 15 L16 12 L21 17 M16 8.5 L16.01 8.5"
        case "sidebar":
            return "M4 4 L20 4 L20 20 L4 20 Z M9 4 L9 20"
        default:
            return ""
        }
    }

    Shape {
        width: 24
        height: 24
        anchors.centerIn: parent
        scale: Math.min(icon.width / 24, icon.height / 24)

        ShapePath {
            strokeColor: icon.name === "grid-filled" ? "transparent" : icon.color
            strokeWidth: icon.strokeWidth
            fillColor: icon.name === "grid-filled" ? icon.color : "transparent"
            capStyle: ShapePath.RoundCap
            joinStyle: ShapePath.RoundJoin

            PathSvg { path: icon.pathForName() }
        }
    }
}
