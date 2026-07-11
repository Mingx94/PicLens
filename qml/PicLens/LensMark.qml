import QtQuick

Item {
    id: mark
    implicitWidth: 34
    implicitHeight: 34

    Rectangle {
        anchors.fill: parent
        radius: 9
        color: Theme.brandShell
        border.width: 1
        border.color: Theme.brandOutline

        Rectangle {
            id: picture
            anchors.fill: parent
            anchors.margins: 4
            radius: 6
            color: Theme.brandSky
            border.width: 1
            border.color: Theme.surface
            clip: true

            Canvas {
                id: canvas
                objectName: "brandCanvas"
                anchors.fill: parent
                anchors.margins: 1
                antialiasing: true

                onPaint: {
                    const context = getContext("2d")
                    context.clearRect(0, 0, width, height)

                    context.beginPath()
                    context.arc(width * 0.31, height * 0.31,
                                Math.min(width, height) * 0.115,
                                0, Math.PI * 2)
                    context.fillStyle = Theme.brandSun
                    context.fill()

                    context.beginPath()
                    context.moveTo(-1, height)
                    context.lineTo(width * 0.36, height * 0.53)
                    context.lineTo(width * 0.72, height)
                    context.closePath()
                    context.fillStyle = Theme.brandHill
                    context.fill()

                    context.beginPath()
                    context.moveTo(width * 0.24, height)
                    context.lineTo(width * 0.70, height * 0.39)
                    context.lineTo(width + 1, height)
                    context.closePath()
                    context.fillStyle = Theme.brandMountain
                    context.fill()
                }
            }
        }
    }
}
