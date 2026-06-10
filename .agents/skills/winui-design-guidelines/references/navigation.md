# Navigation

Source: https://learn.microsoft.com/zh-tw/windows/apps/design/basics/navigation-basics

Source snapshot: Microsoft Learn zh-tw page reviewed on 2026-06-06.

Good navigation is consistent, simple, and clear.

## Principles

- Model user paths before choosing controls.
- Keep navigation predictable with standard controls, locations, icons, and styles.
- Reduce destinations to the important ones and hide secondary items.
- Label destinations clearly and show where the user is.
- Avoid more than two navigation levels without breadcrumbs.
- Avoid forcing users to navigate up and down to reach related content.

## Structure

- Use a flat structure when pages can be visited in any order, are clearly distinct, have no parent-child relationship, and the group has fewer than eight pages.
- Use a hierarchical structure when pages have required order, parent-child relationships, or more than seven pages.
- Combine flat top-level navigation with hierarchy under complex sections when needed.
- In multi-level structures, same-level navigation should link within the current subtree, not unrelated branches.

## Control Selection

- `Frame`: use for most multi-page apps with a shell containing the main navigation.
- Top `NavigationView`: use for sibling pages when options should remain visible, content needs more space, or icons are not clear enough.
- `TabView`: use for dynamic tabs or document-style workflows.
- `BreadcrumbBar`: use to show the path to the current location and allow returning to parent levels.
- Left `NavigationView`: use for top-level pages, especially with more than five nav items or less frequent switching.
- List/details: use when users frequently switch among items and inspect or edit details.
- Hyperlinks/buttons inside content may be page-specific, but global navigation should stay consistent.

## Back Navigation

- Add navigation history when moving across peer groups or when no visible navigation element represents the peer pages.
- Usually do not add history for transient UI such as dialogs, flyouts, startup screens, on-screen keyboard, or selection modes; back should dismiss the transient surface.
- Avoid history entries for item enumeration within list/details; back should return to the item list.
