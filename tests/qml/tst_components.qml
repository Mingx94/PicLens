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
            symbol: "+"
        }
    }

    Component {
        id: historyMouseHandlerComponent
        HistoryMouseHandler { }
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

    function test_designTokens() {
        compare(Theme.accent.toString(), "#4968e8")
        compare(Theme.space4, 16)
        compare(Theme.commandHeight, 56)
    }

    function test_toolbarButtonActivates() {
        const button = createTemporaryObject(toolbarButtonComponent, testCase)
        verify(button !== null)
        clickedSpy.target = button
        compare(button.implicitHeight, 38)
        compare(button.Accessible.name, "開啟資料夾")
        compare(button.Accessible.role, Accessible.Button)
        button.click()
        compare(clickedSpy.count, 1)
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
}
