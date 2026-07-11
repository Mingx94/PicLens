pragma ComponentBehavior: Bound

import QtQuick
import QtQuick.Controls
import QtQuick.Dialogs
import QtQuick.Layouts

ApplicationWindow {
    id: root

    width: 1280
    height: 820
    minimumWidth: 820
    minimumHeight: 540
    visible: true
    title: "PicLens · Qt Quick PoC"
    color: "#F3F5F8"

    property int tileWidth: 184
    property url previewSource: ""
    property string previewName: ""
    required property var imageModel

    readonly property color accent: "#4968E8"
    readonly property color line: "#DEE3EA"
    readonly property color primaryText: "#20242B"
    readonly property color secondaryText: "#667080"

    FolderDialog {
        id: folderDialog
        title: "選擇圖片資料夾"
        onAccepted: root.imageModel.openFolder(selectedFolder)
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 68
            color: "#FBFCFD"
            border.color: root.line

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 20
                anchors.rightMargin: 20
                spacing: 12

                Rectangle {
                    Layout.preferredWidth: 36
                    Layout.preferredHeight: 36
                    radius: 10
                    color: root.accent

                    Text {
                        anchors.centerIn: parent
                        text: "P"
                        color: "white"
                        font.bold: true
                        font.pixelSize: 18
                    }
                }

                ColumnLayout {
                    Layout.fillWidth: true
                    spacing: 1

                    Text {
                        text: "PicLens"
                        color: root.primaryText
                        font.bold: true
                        font.pixelSize: 18
                    }

                    Text {
                        Layout.fillWidth: true
                        text: root.imageModel.folderPath || "Qt 6 + Qt Quick thumbnail pipeline PoC"
                        color: root.secondaryText
                        elide: Text.ElideMiddle
                        font.pixelSize: 12
                    }
                }

                Button {
                    text: "開啟資料夾"
                    highlighted: true
                    onClicked: folderDialog.open()
                }
            }
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 52
            color: "#F8FAFC"
            border.color: root.line

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 20
                anchors.rightMargin: 20
                spacing: 12

                Text {
                    text: root.imageModel.statusText
                    color: root.secondaryText
                    font.pixelSize: 13
                }

                BusyIndicator {
                    Layout.preferredWidth: 24
                    Layout.preferredHeight: 24
                    running: root.imageModel.loading
                    visible: running
                }

                Item { Layout.fillWidth: true }

                Text {
                    text: "縮圖"
                    color: root.secondaryText
                    font.pixelSize: 12
                }

                Slider {
                    id: tileSizeSlider
                    Layout.preferredWidth: 160
                    from: 132
                    to: 260
                    stepSize: 8
                    value: root.tileWidth
                    onMoved: root.tileWidth = Math.round(value)
                }

                Text {
                    text: root.tileWidth + " px"
                    color: root.secondaryText
                    horizontalAlignment: Text.AlignRight
                    font.pixelSize: 12
                    Layout.preferredWidth: 44
                }
            }
        }

        SplitView {
            Layout.fillWidth: true
            Layout.fillHeight: true
            orientation: Qt.Horizontal

            Rectangle {
                SplitView.fillWidth: true
                SplitView.minimumWidth: 500
                color: "#F3F5F8"

                GridView {
                    id: grid
                    anchors.fill: parent
                    anchors.margins: 16
                    clip: true
                    model: root.imageModel
                    cellWidth: root.tileWidth + 16
                    cellHeight: Math.round(root.tileWidth * 0.82) + 54
                    reuseItems: true
                    cacheBuffer: height
                    boundsBehavior: Flickable.StopAtBounds

                    ScrollBar.vertical: ScrollBar { policy: ScrollBar.AsNeeded }

                    delegate: Item {
                        id: tile
                        required property string name
                        required property url fileUrl
                        required property string thumbnailUrl

                        width: grid.cellWidth
                        height: grid.cellHeight

                        Rectangle {
                            anchors.fill: parent
                            anchors.margins: 5
                            radius: 12
                            color: tileMouse.containsMouse ? "#FFFFFF" : "#F9FAFC"
                            border.color: tileMouse.containsMouse ? "#B9C7FF" : root.line

                            ColumnLayout {
                                anchors.fill: parent
                                anchors.margins: 7
                                spacing: 8

                                Rectangle {
                                    Layout.fillWidth: true
                                    Layout.fillHeight: true
                                    radius: 8
                                    color: "#E8ECF2"
                                    clip: true

                                    Text {
                                        anchors.centerIn: parent
                                        text: "IMG"
                                        color: "#9AA3B1"
                                        font.bold: true
                                        font.pixelSize: 12
                                    }

                                    Image {
                                        anchors.fill: parent
                                        source: tile.thumbnailUrl
                                        asynchronous: true
                                        cache: true
                                        fillMode: Image.PreserveAspectCrop
                                        sourceSize.width: Math.round(width * Screen.devicePixelRatio)
                                        sourceSize.height: Math.round(height * Screen.devicePixelRatio)
                                    }
                                }

                                Text {
                                    Layout.fillWidth: true
                                    text: tile.name
                                    color: root.primaryText
                                    elide: Text.ElideRight
                                    font.pixelSize: 13
                                    font.weight: Font.DemiBold
                                }
                            }

                            MouseArea {
                                id: tileMouse
                                anchors.fill: parent
                                hoverEnabled: true
                                cursorShape: Qt.PointingHandCursor
                                onClicked: {
                                    root.previewSource = tile.fileUrl
                                    root.previewName = tile.name
                                }
                            }
                        }
                    }

                    Text {
                        anchors.centerIn: parent
                        visible: !root.imageModel.loading && root.imageModel.count === 0
                        text: root.imageModel.folderPath ? "這個資料夾沒有 Qt 支援的圖片" : "開啟資料夾以測試縮圖效能"
                        color: root.secondaryText
                        font.pixelSize: 17
                    }
                }
            }

            Rectangle {
                SplitView.preferredWidth: root.previewSource.toString() ? 430 : 0
                SplitView.minimumWidth: root.previewSource.toString() ? 300 : 0
                SplitView.maximumWidth: root.previewSource.toString() ? 720 : 0
                visible: root.previewSource.toString() !== ""
                color: "#11141A"

                ColumnLayout {
                    anchors.fill: parent
                    spacing: 0

                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 54
                        color: "#1B2028"

                        RowLayout {
                            anchors.fill: parent
                            anchors.leftMargin: 16
                            anchors.rightMargin: 8

                            Text {
                                Layout.fillWidth: true
                                text: root.previewName
                                color: "#F3F5F8"
                                elide: Text.ElideMiddle
                                font.pixelSize: 13
                                font.weight: Font.DemiBold
                            }

                            ToolButton {
                                text: "✕"
                                onClicked: {
                                    root.previewSource = ""
                                    root.previewName = ""
                                }
                            }
                        }
                    }

                    Item {
                        Layout.fillWidth: true
                        Layout.fillHeight: true

                        BusyIndicator {
                            anchors.centerIn: parent
                            running: previewImage.status === Image.Loading
                            visible: running
                        }

                        Image {
                            id: previewImage
                            anchors.fill: parent
                            anchors.margins: 18
                            source: root.previewSource
                            asynchronous: true
                            cache: false
                            fillMode: Image.PreserveAspectFit
                            sourceSize.width: Math.round(width * Screen.devicePixelRatio)
                            sourceSize.height: Math.round(height * Screen.devicePixelRatio)
                        }
                    }
                }
            }
        }
    }
}
