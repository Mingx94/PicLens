# Qt redistribution inventory

這是工程 release gate inventory，不是法律意見。公開或商業散佈前，專案 owner 必須依實際 Qt edition、取得方式與產品授權模式完成法律審查。

## Build sources and linking

- Windows 本機 candidate：MSYS2 UCRT64 open-source Qt 6.11.1。
- Windows/Ubuntu clean-runner candidate：`install-qt-action` / `aqtinstall` 取得的 open-source Qt 6.8.3。
- Fedora 44 RPM candidate：Fedora distribution Qt 6 packages；RPM 不複製 system Qt libraries，一般 shared libraries 由 auto-generated dependencies 表達，動態載入的 WebP plugin 則明確依賴 `qt6-qtimageformats`。
- PicLens 與 Qt、compiler/runtime、image plugins 均採 dynamic linking；Qt DLL 與 plugins 是 executable 旁的獨立檔案。
- Root `VERSION` 仍是 PicLens package version authority。
- PicLens 自身程式碼採 MIT License；root `LICENSE` 是授權文字 authority。Windows portable/MSI 以 `LICENSE.txt` 攜帶，Linux install tree 以 `share/licenses/PicLens/LICENSE` 攜帶。

## Qt runtime inventory

目前 Windows `windeployqt` artifact 直接包含：

- Core/GUI：Core、Gui、Network、OpenGL。
- QML/Quick：Qml、QmlMeta、QmlModels、QmlWorkerScript、Quick、QuickLayouts、QuickShapes、QuickEffects。
- Controls/dialogs：QuickControls2、QuickTemplates2 與 Basic implementation；deploy scanner 帶入但程式未使用的 Windows、FluentWinUI3、Fusion、Imagine、Material、Universal style modules 會在 smoke 前移除。
- Platform plugins：正式 artifact 只包含 `qwindows`；`qoffscreen` 僅暫時用於 isolated release smoke，測試通過後即從散佈目錄移除。
- Image plugins：`qgif`、`qico`、`qjpeg`、`qwebp`。
- TLS/network information：Windows certificate/Schannel 與 network-information plugins；不強制打包 OpenSSL backend。

## MSYS2 UCRT64 dependency closure

本機 MinGW artifact 另包含 compiler runtime 與 Qt shared dependencies：

```text
libgcc_s_seh-1.dll
libstdc++-6.dll
libwinpthread-1.dll
libb2-1.dll
libbrotlicommon.dll
libbrotlidec.dll
libbz2-1.dll
libdouble-conversion.dll
libffi-8.dll
libfreetype-6.dll
libgio-2.0-0.dll
libglib-2.0-0.dll
libgmodule-2.0-0.dll
libgobject-2.0-0.dll
libgraphite2.dll
libharfbuzz-0.dll
libiconv-2.dll
libicudt78.dll
libicuin78.dll
libicuuc78.dll
libintl-8.dll
libjpeg-8.dll
libmd4c.dll
libpcre2-16-0.dll
libpcre2-8-0.dll
libpng16-16.dll
libwebp-7.dll
libwebpdemux-2.dll
libwebpmux-3.dll
libzstd.dll
zlib1.dll
```

`build-portable.ps1` 從實際 PE imports 遞迴建立 closure，不以這份文字清單代替 dependency resolution。MSVC 與 Linux artifacts 由各自 Qt deployment toolchain 決定 compiler/shared runtime，不能假設與 MSYS2 清單相同。

## Fonts and notices

- PicLens 不內嵌或散佈應用程式字型；執行時優先選擇作業系統已安裝的繁中文字型，找不到時回退至 Qt general system font。
- Windows artifact 包含 PicLens MIT `LICENSE.txt`、`THIRD_PARTY_NOTICES.txt` 與 Qt base/declarative/imageformats、libwebp license directories；MSYS2 builds 使用 distro license packages，official MSVC CI 同步取得對應 Qt source `LICENSES`。
- Linux portable/Ubuntu DEB tree 包含 PicLens MIT license、PicLens notice，以及 `qtbase`/`qtdeclarative`/`qtimageformats` source `LICENSES`。Fedora RPM metadata 宣告 MIT 並明確依賴 `qt6-qtimageformats`；Qt 與 libwebp license texts 由 Fedora Qt packages 提供。
- MSI database audit 會確認 notice、font license payload tree、Qt runtime 與 compiler runtime 沒有因 incremental build 遺失。

## Release blockers

- 在實際用於正式 release 的 Qt edition/toolchain 上重新產生 inventory。
- 對 Windows MSI、DEB、RPM 的最終 file list 與 notices 做 release review。
- 確認 LGPL/GPL、第三方 codecs/fonts 與動態連結散佈義務；工程團隊不得把本文件當成法律核准。
