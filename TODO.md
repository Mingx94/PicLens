# TODO

`experiment/gpui` 的功能實作待辦已完成。產品規格與 [runtime invariants](docs/runtime-invariants.md) 仍是行為權威。

下列項目需要外部平台、明確的系統變更授權，或產品決策。完成前不可宣稱對應的 package、互動驗證或 production release 已完成。

## 原生平台驗證

- [ ] 在 clean Windows runner 執行 MSI install、launch、replace、uninstall 與 profile preservation lifecycle。腳本：`scripts/test-msi-lifecycle.ps1`；需明確傳入 `-ConfirmSystemChanges`。
- [ ] 在 clean Ubuntu runner 執行 DEB build 與 lifecycle。腳本：`scripts/build-deb.sh`、`scripts/test-linux-package-lifecycle.sh`。
- [ ] 在 clean Fedora runner 執行 RPM build 與 lifecycle。腳本：`scripts/build-rpm.sh`、`scripts/test-linux-package-lifecycle.sh`。
- [ ] 使用原生 UI automation 檢查 1280×800 與 800×600 的 delayed tooltip、UIA name、focus restore、dialog、drag、scroll 與檔案操作結果。自動 screenshot 只能證明 render，不可取代輸入與 UIA 驗證。
- [ ] 在大型 disposable image library 執行 Release metrics，記錄 CPU/GPU、storage、display scale，並驗證持續捲動、search 與 viewer open。腳本：`scripts/measure-performance.ps1`。

## 產品與 release authority

- [ ] 產品核准正式效能門檻後，才加入自動 performance gate。現有 metrics 不設未經核准的門檻。
- [ ] 若要簽署 release assets，設定受保護的 code-signing identity 與 timestamp service；未設定前，所有 assets 必須標示 unsigned。
- [ ] 經使用者明確授權後，建立匹配 Cargo version 的 annotated `v<version>` tag 並 push。
- [ ] 確認 hosted release workflow 的 Windows、Ubuntu、Fedora lifecycle jobs 全部成功，且 GitHub Release 包含 MSI、DEB、RPM、portable archives 與 SHA-256 checksum files。

本機 compilation、test、MSI build 或 launch-only smoke 都不會自動勾選上述外部驗證項目。
