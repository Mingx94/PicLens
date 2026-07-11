import QtQuick

Item {
    id: mark
    implicitWidth: 34
    implicitHeight: 34

    Canvas {
        id: canvas
        anchors.fill: parent
        antialiasing: true

        onPaint: {
            const context = getContext("2d")
            const centerX = width / 2
            const centerY = height / 2
            const radius = Math.min(width, height) / 2 - 1
            const innerRadius = radius * 0.34
            context.clearRect(0, 0, width, height)
            context.beginPath()
            context.arc(centerX, centerY, radius, 0, Math.PI * 2)
            context.fillStyle = Theme.accent
            context.fill()
            context.strokeStyle = "#FFFFFF"
            context.lineWidth = 1.15
            context.lineCap = "round"
            for (let index = 0; index < 6; ++index) {
                const angle = -Math.PI / 2 + index * Math.PI / 3
                context.beginPath()
                context.moveTo(
                    centerX + Math.cos(angle) * innerRadius,
                    centerY + Math.sin(angle) * innerRadius)
                context.lineTo(
                    centerX + Math.cos(angle + 0.42) * radius,
                    centerY + Math.sin(angle + 0.42) * radius)
                context.stroke()
            }
            context.beginPath()
            context.arc(centerX, centerY, innerRadius, 0, Math.PI * 2)
            context.stroke()
        }
    }
}
