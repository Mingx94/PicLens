import QtQuick
import QtQuick.Shapes

Item {
    id: icon
    property string name: ""
    property color color: Theme.primaryText
    property real strokeWidth: 1.8
    property bool multisampled: false
    implicitWidth: 20
    implicitHeight: 20
    readonly property bool usesFilledPath: name === "grid-filled"
                                        || name === "refresh-filled"
                                        || name === "search-filled"
    readonly property int pathSize: name === "refresh-filled" || name === "search-filled"
                                   ? 20 : 24
    readonly property real pathScale: Math.min(icon.width / icon.pathSize,
                                               icon.height / icon.pathSize)

    function pathForName() {
        switch (name) {
        case "chevron-left":
            return "M15 18 L9 12 L15 6"
        case "chevron-right":
            return "M9 18 L15 12 L9 6"
        case "chevron-down":
            return "M6 9 L12 15 L18 9"
        case "refresh":
            return "M20 11 C19.5 7 16.1 4 12 4 C7.6 4 4 7.6 4 12 C4 16.4 7.6 20 12 20 C15.4 20 18.3 17.9 19.4 14.8 M20 4 L20 11 L13 11"
        case "refresh-filled":
            return "M9.89 3.75a6.25 6.25 0 0 0-3.63 11.26.75.75 0 0 1-.9 1.2 7.75 7.75 0 0 1 4-13.93l-.6-.59A.75.75 0 0 1 9.82.63l2.12 2.12c.3.3.3.77 0 1.06L9.82 5.93a.75.75 0 0 1-1.06-1.06L9.9 3.75Zm.22 12.5a6.25 6.25 0 0 0 3.63-11.26.75.75 0 0 1 .9-1.2 7.75 7.75 0 0 1-4 13.93l.6.59a.75.75 0 1 1-1.06 1.06l-2.12-2.12a.75.75 0 0 1 0-1.06l2.12-2.13a.75.75 0 1 1 1.06 1.07l-1.13 1.12Z"
        case "search":
            return "M11 19 A8 8 0 1 1 19 11 A8 8 0 0 1 11 19 M17 17 L21 21"
        case "search-filled":
            return "M12.54 13.6a6.5 6.5 0 1 1 1.06-1.06l3.43 3.43a.75.75 0 0 1-.98 1.13l-.08-.07-3.43-3.43Zm.96-5.1a5 5 0 1 0-10 0 5 5 0 0 0 10 0Z"
        case "plus":
            return "M12 5 L12 19 M5 12 L19 12"
        case "zoom-in":
            return "M10.5 18 A7.5 7.5 0 1 1 18 10.5 A7.5 7.5 0 0 1 10.5 18 M16 16 L21 21 M10.5 7.5 L10.5 13.5 M7.5 10.5 L13.5 10.5"
        case "zoom-out":
            return "M10.5 18 A7.5 7.5 0 1 1 18 10.5 A7.5 7.5 0 0 1 10.5 18 M16 16 L21 21 M7.5 10.5 L13.5 10.5"
        case "fit":
            return "M9 4 L4 4 L4 9 M15 4 L20 4 L20 9 M4 15 L4 20 L9 20 M20 15 L20 20 L15 20"
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
        case "folder-open":
            return "M3 7 L10 7 L12 9 L21 9 L19 19 L3 19 Z M3 7 L3 5 L9 5 L11 7"
        case "sidebar-collapse":
            return "M3 4 L21 4 L21 20 L3 20 Z M9 4 L9 20 M17 8 L13 12 L17 16"
        case "sidebar-expand":
            return "M3 4 L21 4 L21 20 L3 20 Z M9 4 L9 20 M13 8 L17 12 L13 16"
        default:
            return ""
        }
    }

    Shape {
        objectName: "appIconShape"
        width: icon.pathSize
        height: icon.pathSize
        anchors.centerIn: parent
        scale: icon.pathScale
        layer.enabled: icon.multisampled
        layer.samples: 4

        ShapePath {
            strokeColor: icon.usesFilledPath ? "transparent" : icon.color
            strokeWidth: icon.usesFilledPath ? 0 : icon.strokeWidth
            fillColor: icon.usesFilledPath ? icon.color : "transparent"
            capStyle: ShapePath.RoundCap
            joinStyle: ShapePath.RoundJoin

            PathSvg { path: icon.pathForName() }
        }
    }
}
