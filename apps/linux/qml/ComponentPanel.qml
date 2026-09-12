import QtQuick
import QtQuick.Controls.Basic
import QtQuick.Layouts

Dialog {
    id: panel
    required property Theme theme
    anchors.centerIn: Overlay.overlay
    title: "元件展示"
    modal: true
    standardButtons: Dialog.Close
    contentItem: ScrollView {
        clip: true
        contentWidth: availableWidth
        ColumnLayout {
            width: parent.width
            spacing: 16
            Label {
                text: "灰階介面與互動狀態"
                font.pixelSize: 24
                color: panel.theme.foreground
            }
            Label {
                Layout.fillWidth: true
                text: "展示控制項只改變本頁，不會執行檔案操作。"
                wrapMode: Text.Wrap
                color: panel.theme.secondary
            }
            Flow {
                Layout.fillWidth: true
                Layout.preferredHeight: childrenRect.height
                spacing: 8
                ActionButton {
                    theme: panel.theme
                    text: "主要操作"
                    prominent: true
                }
                ActionButton {
                    theme: panel.theme
                    text: "次要操作"
                }
                ActionButton {
                    theme: panel.theme
                    text: "危險操作"
                    destructive: true
                    glyph: "trash-2"
                }
                ActionButton {
                    theme: panel.theme
                    text: "停用操作"
                    enabled: false
                }
            }
            TextField {
                Layout.fillWidth: true
                placeholderText: "搜尋或輸入繁體中文…"
                selectByMouse: true
                Accessible.name: "展示輸入欄"
            }
            NeutralComboBox {
                theme: panel.theme
                model: ["名稱（升冪）", "名稱（降冪）", "修改時間（最舊優先）", "修改時間（最新優先）"]
                Accessible.name: "展示排序"
            }
            NeutralCheckBox {
                theme: panel.theme
                text: "包含子資料夾"
                checked: true
            }
            NeutralSlider {
                theme: panel.theme
                Layout.fillWidth: true
                from: 120
                to: 240
                stepSize: 20
                value: 160
                Accessible.name: "展示縮圖大小"
            }
            Rectangle {
                Layout.fillWidth: true
                implicitHeight: 100
                radius: 10
                color: panel.theme.muted
                border.color: panel.theme.foreground
                border.width: 2
                ColumnLayout {
                    anchors.fill: parent
                    anchors.margins: 16
                    Label {
                        text: "已選取"
                        color: panel.theme.foreground
                    }
                    Label {
                        Layout.fillWidth: true
                        text: "很長的繁體中文檔名與 Unicode 圖片名稱.png"
                        elide: Text.ElideMiddle
                        color: panel.theme.foreground
                    }
                    Label {
                        text: "無法解碼圖片"
                        color: panel.theme.danger
                    }
                }
            }
            Label {
                text: "沒有符合條件的圖片"
                font.pixelSize: 20
                color: panel.theme.foreground
            }
            Label {
                text: "試著清除搜尋，或切換資料夾。"
                color: panel.theme.secondary
            }
            TextArea {
                Layout.fillWidth: true
                readOnly: true
                selectByMouse: true
                wrapMode: TextEdit.Wrap
                text: "來源：展示圖片.png\n目標：展示圖片.webp\n狀態：略過\n說明：目標已存在。"
            }
        }
    }
}
