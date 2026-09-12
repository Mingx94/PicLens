# Arch 本機交付

此目錄是 4.0.0 Qt 工作樹交付，不是已發布版本。`PKGBUILD` 含明確 checksum 佔位符，不能直接 makepkg。沒有虛構的 tag、commit source URL 或 `SKIP`。

最終來源包由下方腳本產生，檔名與 checksum 以該次交付目錄的 `SHA256SUMS` 為準。2026-09-12 已在 Omarchy 4.0.3 建置 Linux 主套件與 debug 套件，放於 `dist/piclens-4.0.0-arch-validation-20260912/`，連同固定來源、manifest 與 SHA256SUMS。makepkg check() 4/4、成品 Wayland 啟閉與隔離 pacman 檔案生命週期通過；乾淨 Arch、真正舊版升級與主機桌面整合仍待驗。詳見 [Arch 驗收紀錄](../../docs/engineering/arch-validation.md)。

## Windows 產生快照

需要 PowerShell 7、Git、Python 3.10 以上。輸出目錄必須不存在，可在 repo 外或 repo 的 `dist/` 下新建子目錄；不覆寫既有交付。`dist` 同時受 Git ignore 與 exporter 允許清單排除，不會遞迴打包先前成品。

```powershell
pwsh -File F:/PicLens/packaging/arch/New-Handoff.ps1 -OutputDirectory F:/PicLens/dist/piclens-4.0.0-handoff
```

產生 source tarball、填入真實 SHA-256 的 `PKGBUILD`、`SHA256SUMS` 與逐檔 `SOURCE-MANIFEST.json`。Linux 也可執行 `python packaging/arch/handoff.py --output /tmp/piclens-handoff`。

來源透過 `git ls-files --cached --others --exclude-standard -z` 取得，讀取目前磁碟內容，包含未提交變更。固定 tar 順序、mode、uid/gid、mtime 與 gzip 時間；相同檔案位元組在相同 Python/zlib 工具鏈下產生相同 checksum。base commit 只是來源追溯，不代表內容等於 HEAD；SHA-256 鎖定的是 tarball 本身。

允許清單只接受 Linux 原始碼／QML／CMake／測試、Arch 封裝、已 tracked 的共用 assets／圖片 fixtures、test-data 的 JSON／TXT／Markdown、LICENSE、TODO.arch.md、tracked 規格文件及新的 Arch 驗收文件。排除 ignored 檔案、隱藏目錄、個人 profile、build、cache、binary 與 symlink／reparse 路徑。不讀個人設定。新共用圖片 fixture 必須先經主 agent 納入 tracked 清單，或明確審查並調整允許清單；不自動打包任意本機圖片。程式無法判斷原始碼內容是否含個資，交付前必須核對 manifest 的檔名與內容。

請在各 agent 停止修改後產生；產生器重讀比對已選檔案以偵測同時修改，但不是檔案系統交易快照。新增必要路徑或 build 產物變更後重新產生至新目錄。不要修改已交付 tarball 或把 checksum 改成 SKIP。

## Arch 本機 makepkg

將整個交付目錄搬至 Arch；依 [Linux README](../../apps/linux/README.md)先自行安裝工具，再以一般帳號執行：

```bash
cd /path/to/piclens-4.0.0-handoff
sha256sum -c SHA256SUMS
makepkg --verifysource
makepkg
makepkg --packagelist
```

PKGBUILD 將 `qt6-wayland>=6.8` 列為必要執行相依，`pkgconf` 列為建置相依。

不使用 `-s`／`-i`，不自動安裝相依或成品。`check()` 執行隔離 offscreen CTest，沒有測試會失敗；`package()` 只寫 makepkg 的 `$pkgdir`。完成後人工指定實際套件檔，以 `pacman -Qip`、`pacman -Qlp`、`bsdtar -tvf` 唯讀核對 metadata、內容與 mode。需要正式乾淨建置時，由使用者建立乾淨 Arch VM／chroot 並重做；此處不啟動容器或安裝流程。

## CMake 安裝整合表

已核對目前 `apps/linux/CMakeLists.txt` 的安裝來源與下表一致；Omarchy 本次已核對 DESTDIR 與實際 `.pkg.tar.zst` 的安裝位置及 mode。本目錄不修改 CMake。使用 GNUInstallDirs、`CMAKE_INSTALL_PREFIX=/usr` 及 DESTDIR；禁止硬編開發機路徑。

| 來源／target | 安裝位置 | mode |
|---|---|---|
| target `piclens` | `/usr/bin/piclens` | 755 |
| target `piclens-worker` | `/usr/libexec/piclens/piclens-worker` | 755 |
| `packaging/arch/piclens.desktop` | `/usr/share/applications/piclens.desktop` | 644 |
| `packaging/arch/piclens.png` | `/usr/share/icons/hicolor/48x48/apps/piclens.png` | 644 |
| `packaging/arch/io.github.Mingx94.PicLens.metainfo.xml` | `/usr/share/metainfo/io.github.Mingx94.PicLens.metainfo.xml` | 644 |
| `LICENSE`、`apps/linux/THIRD-PARTY.md` | `/usr/share/licenses/piclens/` | 644 |
| `assets/Icons/Lucide/LICENSE.txt` | `/usr/share/licenses/piclens/Lucide-LICENSE.txt` | 644 |

desktop entry 暫不宣告 MIME handler／`%F`，因 CLI 契約目前只指定 `--folder`。圖示取自既有 48×48 PNG，未縮放或生成；不將 300×300 原圖錯標為 256×256。AppStream 不列未發布的 release 日期。若 App 內嵌字型，另安裝 OFL。

GitHub 發布流程已建立於 `.github/workflows/arch-native.yml`。推送 `arch/v<version>` annotated tag 後，`handoff.py --release-tag` 核對 CMake／pkgver／pkgrel、乾淨 checkout 與 tag commit，產生 `piclens-<version>-source.tar.gz` 及真實 checksum。版本直接讀取 CMake，不再寫死在 exporter。

容器內的 `build-release.sh` 以一般帳號執行 `makepkg --nocheck`，設定 `PICLENS_BUILD_TESTING=OFF`；本機一般 makepkg 仍保留測試。GitHub Release 包含未簽署套件、來源、PKGBUILD、manifest、套件版本與 SHA256SUMS。沒有 PR／main 自動測試，也不發布至 AUR。實際 hosted 成功及桌面／安裝驗收須另留證據。
