import QtQuick
import QtTest
import PicLens 1.0

TestCase {
    id: testCase
    name: "PicLensComponents"
    when: windowShown
    width: 360
    height: 200

    Component {
        id: toolbarButtonComponent
        ToolbarButton {
            text: "開啟資料夾"
            iconName: "plus"
        }
    }

    Component {
        id: historyMouseHandlerComponent
        HistoryMouseHandler { }
    }

    Component {
        id: iconButtonComponent
        ToolbarButton {
            iconName: "menu"
            accessibleName: "選單"
        }
    }

    Component {
        id: compactComboComponent
        CompactComboBox {
            width: 126
            model: ["名稱"]
            labelText: "名稱"
        }
    }

    Component {
        id: lensMarkComponent
        LensMark { }
    }

    Component {
        id: viewerPointerSurfaceComponent
        ViewerPointerSurface {
            width: 240
            height: 160
        }
    }

    SignalSpy {
        id: clickedSpy
        signalName: "clicked"
    }

    SignalSpy {
        id: backSpy
        signalName: "backRequested"
    }

    SignalSpy {
        id: forwardSpy
        signalName: "forwardRequested"
    }

    SignalSpy {
        id: panRequestedSpy
        signalName: "panRequested"
    }

    function test_designTokens() {
        compare(Theme.accent.toString(), "#4968e8")
        compare(Theme.space4, 16)
        compare(Theme.commandHeight, 64)
        compare(Theme.controlHeight, 38)
        compare(Theme.statusHeight, 48)
        compare(Theme.viewerRailWidth, 80)
        compare(Theme.brandMountain.toString(), "#155dbb")
    }

    function test_lensMarkUsesCompactBrandGeometry() {
        const mark = createTemporaryObject(lensMarkComponent, testCase)
        verify(mark !== null)
        compare(mark.implicitWidth, 34)
        compare(mark.implicitHeight, 34)
        verify(findChild(mark, "brandCanvas") !== null)
    }

    function test_toolbarButtonActivates() {
        const button = createTemporaryObject(toolbarButtonComponent, testCase)
        verify(button !== null)
        clickedSpy.target = button
        compare(button.implicitHeight, 38)
        compare(button.Accessible.name, "開啟資料夾")
        compare(button.Accessible.role, Accessible.Button)
        compare(button.iconName, "plus")
        button.click()
        compare(clickedSpy.count, 1)
    }

    function test_controlContentUsesSingleCenteringInset() {
        const iconButton = createTemporaryObject(iconButtonComponent, testCase)
        verify(iconButton !== null)
        compare(iconButton.implicitWidth, 38)
        compare(iconButton.leftPadding, 0)
        compare(iconButton.rightPadding, 0)

        const combo = createTemporaryObject(compactComboComponent, testCase)
        verify(combo !== null)
        compare(combo.leftPadding, Theme.space3)
        compare(combo.contentItem.leftPadding, 0)
        compare(combo.contentItem.rightPadding, 0)
    }

    function test_historyMouseButtonsAreIsolated() {
        const handler = createTemporaryObject(historyMouseHandlerComponent, testCase, {
            width: 160,
            height: 100
        })
        verify(handler !== null)
        backSpy.target = handler
        forwardSpy.target = handler

        handler.handleButton(Qt.LeftButton)
        compare(backSpy.count, 0)
        compare(forwardSpy.count, 0)

        handler.handleButton(Qt.BackButton)
        handler.handleButton(Qt.ForwardButton)
        compare(backSpy.count, 1)
        compare(forwardSpy.count, 1)
    }

    function test_viewerPointerSurfaceBlocksGalleryAndStillPans() {
        const surface = createTemporaryObject(viewerPointerSurfaceComponent, testCase)
        verify(surface !== null)
        verify(surface.blockedButtons & Qt.LeftButton)
        verify(surface.blockedButtons & Qt.RightButton)
        compare(surface.preventsStealing, true)
        panRequestedSpy.target = surface

        surface.panEnabled = true
        surface.beginPointer(80, 80)
        surface.updatePointer(112, 96, Qt.LeftButton)
        verify(panRequestedSpy.count > 0)
    }
}
