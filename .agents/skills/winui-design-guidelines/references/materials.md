# Materials

Source: https://learn.microsoft.com/zh-tw/windows/apps/design/signature-experiences/materials

Source snapshot: Microsoft Learn zh-tw page reviewed on 2026-06-06.

Windows materials add visual surface behavior. Use each for its intended job.

## Guidelines

- Mica: opaque material introduced for Windows 11. Use for app/window background surfaces that should subtly pick up desktop color and indicate active/inactive state.
- Acrylic: translucent frosted-glass material. Use only for transient dismissible UI such as flyouts and context menus.
- Smoke: translucent black overlay. Use behind blocking modal UI such as dialogs to push the underlying surface into the background.
- Mica and Acrylic adapt to light/dark mode. Smoke is always translucent black.
- Do not use materials where a flat theme resource would be clearer or more performant.
