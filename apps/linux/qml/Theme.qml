import QtQuick

QtObject {
    id: paletteTokens
    property bool dark: false
    readonly property color background: paletteTokens.dark ? "#09090b" : "#ffffff"
    readonly property color foreground: paletteTokens.dark ? "#fafafa" : "#18181b"
    readonly property color card: paletteTokens.dark ? "#18181b" : "#ffffff"
    readonly property color sidebar: paletteTokens.dark ? "#18181b" : "#fafafa"
    readonly property color muted: paletteTokens.dark ? "#27272a" : "#f4f4f5"
    readonly property color secondary: paletteTokens.dark ? "#a1a1aa" : "#71717a"
    readonly property color border: paletteTokens.dark ? "#3f3f46" : "#e4e4e7"
    readonly property color input: paletteTokens.dark ? "#52525b" : "#d4d4d8"
    readonly property color danger: paletteTokens.dark ? "#fca5a5" : "#b91c1c"
    readonly property color primary: paletteTokens.foreground
    // Avoid onPrimary: with primary present, QML treats it as a signal handler.
    readonly property color primaryForeground: paletteTokens.background
}
