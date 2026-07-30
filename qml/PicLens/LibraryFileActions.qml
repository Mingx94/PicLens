import QtQuick
import QtQuick.Controls
import PicLens

Item {
    id: actions
    required property AppController appController
    required property real availablePaneWidth
    property string contextMenuPath: ""
    implicitWidth: operationsButton.implicitWidth
    implicitHeight: operationsButton.implicitHeight

    function openContextMenu(sourcePath, parentItem, x, y) {
        contextMenuPath = sourcePath
        appController.prepareContextSelection(sourcePath)
        itemContextMenu.popup(parentItem, x, y)
    }

    Connections {
        target: actions.appController.fileOperations.dropRename
        function onPreviewReady() { dropRenameDialog.open() }
    }

    ToolbarButton {
        id: operationsButton
        anchors.fill: parent
        iconName: "more"
        outlined: true
        accessibleName: "更多圖庫動作"
        enabled: actions.appController.fileOperations.canProcessVisible
                 || actions.appController.fileOperations.busy
        ToolTip.text: "更多圖庫動作"
        onClicked: operationsMenu.open()

        Menu {
            id: operationsMenu
            y: parent.height
            MenuItem {
                text: "將目前顯示項目轉為 JPG（品質 100）"
                enabled: actions.appController.fileOperations.canProcessVisible
                onTriggered: {
                    if (actions.appController.fileOperations.visibleImageCount >= 50)
                        convertDialog.open()
                    else
                        actions.appController.fileOperations.convertVisible()
                }
            }
            MenuItem {
                text: "將目前顯示項目轉為無損 WebP"
                enabled: actions.appController.fileOperations.canProcessVisible
                onTriggered: {
                    if (actions.appController.fileOperations.visibleImageCount >= 50)
                        convertWebpDialog.open()
                    else
                        actions.appController.fileOperations.convertVisibleToWebp()
                }
            }
            MenuItem {
                text: "清除同名 JPG/WebP 以外的格式"
                enabled: actions.appController.fileOperations.canProcessVisible
                onTriggered: cleanupDialog.open()
            }
            MenuSeparator { }
            MenuItem {
                text: "取消目前檔案操作"
                enabled: actions.appController.fileOperations.busy
                onTriggered: actions.appController.fileOperations.cancel()
            }
        }
    }

    Dialog {
        id: renameDialog
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(420, actions.availablePaneWidth - 48)
        title: "重新命名圖片"
        modal: true
        standardButtons: Dialog.Ok | Dialog.Cancel
        closePolicy: Popup.CloseOnEscape
        onOpened: {
            renameField.text = actions.appController.fileOperations.selectedBaseName
            renameField.forceActiveFocus()
            renameField.selectAll()
        }
        onAccepted: actions.appController.fileOperations.renameSelected(renameField.text)

        contentItem: Column {
            spacing: Theme.space3
            Text {
                text: "輸入新的檔名（副檔名會保留）"
                color: Theme.primaryText
            }
            TextField {
                id: renameField
                width: Math.min(340, renameDialog.availableWidth)
                selectByMouse: true
                onAccepted: renameDialog.accept()
            }
        }
    }

    Dialog {
        id: trashDialog
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(420, actions.availablePaneWidth - 48)
        title: "將選取的圖片移至回收筒"
        modal: true
        standardButtons: Dialog.Yes | Dialog.Cancel
        closePolicy: Popup.CloseOnEscape
        onAccepted: actions.appController.fileOperations.trashSelected()

        contentItem: Text {
            width: trashDialog.availableWidth
            text: actions.appController.library.selectedCount === 1
                  ? "要將這張圖片移至回收筒嗎？"
                  : "要將選取的 " + actions.appController.library.selectedCount + " 張圖片移至回收筒嗎？"
            color: Theme.primaryText
            wrapMode: Text.Wrap
        }
    }

    Menu {
        id: itemContextMenu

        MenuItem {
            text: "在檔案管理器中顯示"
            enabled: !actions.appController.fileOperations.busy
            onTriggered: actions.appController.fileOperations.reveal(actions.contextMenuPath)
        }
        MenuSeparator { }
        MenuItem {
            text: "重新命名"
            enabled: actions.appController.fileOperations.canRename
            onTriggered: renameDialog.open()
        }
        MenuItem {
            text: "移至回收筒"
            enabled: actions.appController.fileOperations.canTrash
            onTriggered: trashDialog.open()
        }
    }

    Dialog {
        id: convertDialog
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(440, actions.availablePaneWidth - 48)
        title: "轉換為 JPG（品質 100）"
        modal: true
        standardButtons: Dialog.Yes | Dialog.Cancel
        onAccepted: actions.appController.fileOperations.convertVisible()

        contentItem: Text {
            width: convertDialog.availableWidth
            text: "要將目前顯示的 " + actions.appController.fileOperations.visibleImageCount
                  + " 張圖片轉為品質 100 的 JPG 嗎？原始檔案會保留。"
            color: Theme.primaryText
            wrapMode: Text.Wrap
        }
    }

    Dialog {
        id: cleanupDialog
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(460, actions.availablePaneWidth - 48)
        title: "清除其他同名格式"
        modal: true
        standardButtons: Dialog.Yes | Dialog.Cancel
        onAccepted: actions.appController.fileOperations.clearSameBasenameExtras()

        contentItem: Text {
            width: cleanupDialog.availableWidth
            text: "要將目前顯示圖片中，已有同名 JPG/JPEG 或 WebP 的其他格式移至回收筒嗎？JPG/JPEG 與 WebP 會保留。"
            color: Theme.primaryText
            wrapMode: Text.Wrap
        }
    }

    Dialog {
        id: convertWebpDialog
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(460, actions.availablePaneWidth - 48)
        title: "轉換為無損 WebP"
        modal: true
        standardButtons: Dialog.Yes | Dialog.Cancel
        onAccepted: actions.appController.fileOperations.convertVisibleToWebp()

        contentItem: Text {
            width: convertWebpDialog.availableWidth
            text: "要將目前顯示的 " + actions.appController.fileOperations.visibleImageCount
                  + " 張圖片轉為無損 WebP 嗎？JPG/JPEG 與既有 WebP 會略過，原始檔案會保留。"
            color: Theme.primaryText
            wrapMode: Text.Wrap
        }
    }

    Dialog {
        id: dropRenameDialog
        parent: Overlay.overlay
        anchors.centerIn: parent
        width: Math.min(560, actions.availablePaneWidth - 48)
        title: "確認拖放重新命名"
        modal: true
        closePolicy: Popup.CloseOnEscape
        onAccepted: actions.appController.fileOperations.dropRename.confirm()
        onRejected: actions.appController.fileOperations.dropRename.cancelPreview()

        footer: DialogButtonBox {
            Button {
                text: "套用重新命名"
                DialogButtonBox.buttonRole: DialogButtonBox.AcceptRole
            }
            Button {
                text: "取消"
                DialogButtonBox.buttonRole: DialogButtonBox.RejectRole
            }
            onAccepted: dropRenameDialog.accept()
            onRejected: dropRenameDialog.reject()
        }

        contentItem: Column {
            width: dropRenameDialog.availableWidth
            spacing: Theme.space3

            Text {
                width: parent.width
                text: "將重新命名 " + actions.appController.fileOperations.dropRename.renameCount
                      + " 個，略過 " + actions.appController.fileOperations.dropRename.skippedCount + " 個。"
                color: Theme.primaryText
                font.weight: Font.DemiBold
                wrapMode: Text.Wrap
            }
            Rectangle {
                width: parent.width
                height: Math.min(300, Math.max(80, previewText.implicitHeight + 24))
                radius: Theme.cornerRadius
                color: Theme.tileFrame
                border.width: 1
                border.color: Theme.line

                Text {
                    id: previewText
                    anchors.fill: parent
                    anchors.margins: 12
                    text: actions.appController.fileOperations.dropRename.previewText
                    color: Theme.secondaryText
                    font.family: "monospace"
                    wrapMode: Text.WrapAnywhere
                }
            }
        }
    }
}
