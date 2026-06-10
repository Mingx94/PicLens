# Layout

Sources:

- Layout overview: https://learn.microsoft.com/zh-tw/windows/apps/design/layout/
- App silhouette: https://learn.microsoft.com/zh-tw/windows/apps/design/basics/app-silhouette
- Title bar: https://learn.microsoft.com/zh-tw/windows/apps/design/basics/titlebar-design
- Screen sizes and breakpoints: https://learn.microsoft.com/zh-tw/windows/apps/design/layout/screen-sizes-and-breakpoints-for-responsive-design
- Responsive design: https://learn.microsoft.com/zh-tw/windows/apps/design/layout/responsive-design

Source snapshot: Microsoft Learn zh-tw pages reviewed on 2026-06-06.

Layout should adapt to window size and organize content with clear hierarchy.

## Effective Pixels And Breakpoints

- Design in effective pixels (`epx`), not physical pixels.
- Ignore physical screen density for normal XAML sizing; the platform scales UI for viewing distance and DPI.
- Use width classes based on the app window:
  - Small: `<640` epx.
  - Medium: `641-1007` epx.
  - Large: `>=1008` epx.
- Size, margins, and positions should use multiples of 4 epx so scaled UI lands on whole pixels.
- Text does not need to follow the 4 epx multiple rule.

## Responsive Techniques

- Reposition: change element placement to use more width.
- Resize: adjust margins and containers for readability.
- Reflow: increase columns or change list/card flow at larger sizes.
- Show/hide: show more metadata or secondary controls only when space allows.
- Restructure: switch architecture at a breakpoint, such as compact navigation on small windows and tabs on larger windows.
- Use adaptive layout only when a full layout replacement is clearer than fluid adjustments.

## App Silhouettes

- Top navigation silhouette: use when vertical space matters and top-level destinations should appear near commands. Large media layouts may use wider margins such as 56 epx; dense content can use less.
- Menu bar silhouette: use for productivity or editor-style apps with many commands, often with tighter margins such as 12 epx.
- Left navigation silhouette: use for app-wide top-level destinations and content-focused experiences. Wider margins such as 56 epx can help grouped content; dense layouts may use less.
- TabView silhouette: use for document, terminal, browser, or editor workflows where tabs are central.

## Title Bar

- Standard title bar height is 32 px.
- Increase to 48 px when adding centered search or account/person controls.
- Title bar background often uses Mica, but blend it with the window when possible.
- Inactive title bar elements should appear subdued.
- Empty title bar regions and non-interactive title text should be draggable.
- Right-click or press-hold empty regions should show the system window menu.
- Double-click or double-tap the title bar should maximize/restore.
- Window icon is 16 x 16 px and placed 16 px from the leading edge, or 16 px after a back button.
- Title text uses Segoe UI Variable or Segoe UI and should truncate with ellipsis while preserving title buttons.
- Custom title buttons should match system icons and states.
