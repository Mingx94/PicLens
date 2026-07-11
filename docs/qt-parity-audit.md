# Qt runtime contract audit

Final cutover audit for the production Qt runtime.

| Product contract | Production owner | Gate result |
|---|---|---|
| Folder scan, recursive mode, format filtering and sort | Core + Infrastructure | Implemented; unit/integration gates pass |
| Search, grid/list, selection and thumbnail sizing | Presentation + QML | Implemented; controller and Quick Test gates pass |
| Lazy folder tree and navigation history | Presentation + QML | Implemented; model/controller gates pass |
| Bounded thumbnail decode, cache and stale-request rejection | Infrastructure + Presentation | Implemented; concurrency/cache gates pass |
| Rename, delete/trash, reveal and drag/drop | Infrastructure + Presentation + QML | Implemented; Windows and Linux adapter gates pass |
| Inline viewer, zoom, pan and input parity | Core + Presentation + QML | Implemented; controller/QML/runtime gates pass |
| Settings, logging and profile continuity | Infrastructure | Implemented; persistence and copied-profile gates pass |
| Portable deployment | Qt scripts | Windows and Ubuntu clean-runner gates pass |
| MSI / DEB / RPM lifecycle | WiX + CPack | Windows, Ubuntu and Fedora lifecycle gates pass |
| Licensing | Root MIT + third-party notices | Payload audit passes |
| Large-library performance | App diagnostics + performance script | Local representative and hosted Windows 10,000-image gates pass |

No production contract has a legacy runtime owner. Historical schema names remain only where tests protect existing user data compatibility.
