# Windows design intent mapped to Avalonia

Use this reference as a decision checklist. Verify current Avalonia syntax and control availability through `avalonia-docs` and the project's installed package version.

## Contents

- [Decision order](#decision-order)
- [Design principles](#design-principles)
- [Foundation checklist](#foundation-checklist)
- [Interaction patterns](#interaction-patterns)
- [Content and system states](#content-and-system-states)
- [Localization and platform behavior](#localization-and-platform-behavior)
- [WinUI concepts to translate](#winui-concepts-to-translate)
- [Review states](#review-states)
- [Definition of done](#definition-of-done)

## Decision order

1. Preserve product behavior and accessibility.
2. Follow the project's documented design system and established UI patterns.
3. Use native Avalonia controls, themes, resources, layout, and input behavior.
4. Apply Microsoft Fluent and Windows guidance where it does not conflict with cross-platform behavior.
5. Add custom visuals or controls only when the first four options cannot express the requirement.

## Design principles

- **Effortless:** Make the primary task obvious, reduce steps, and provide immediate feedback.
- **Calm:** Use hierarchy, whitespace, and restrained emphasis; avoid competing accents and gratuitous motion.
- **Personal:** Respect theme, text, input, and platform preferences without making behavior unpredictable.
- **Familiar:** Preserve platform conventions and reuse established application patterns.
- **Complete and coherent:** Keep behavior, wording, spacing, states, and component usage consistent across the app.

## Foundation checklist

### Color and themes

- Use semantic resources such as surface, text, accent, border, danger, warning, and success instead of repeated literals.
- Use `StaticResource` for stable values and `DynamicResource` only for values that can change at runtime, especially theme-aware brushes.
- Keep light and dark values in theme dictionaries when the app supports both.
- Never communicate selection, validation, or status by color alone; pair it with text, shape, icon, or state.
- Preserve sufficient foreground/background and focus-indicator contrast. Verify with tooling rather than guessing.

### Commanding

- Give each surface one visually dominant primary action at most.
- Put frequent, contextual actions near their content; move infrequent actions to a menu.
- Use verbs for commands and describe destructive scope precisely.
- Disable unavailable actions only when the reason is apparent; otherwise hide irrelevant actions.
- Confirm destructive actions when recovery is difficult, not for routine reversible operations.

### Elevation, materials, and geometry

- Use borders, surfaces, and spacing before shadows. Add elevation only to clarify hierarchy or temporary overlays.
- Use `Border.BoxShadow` sparingly for elevation.
- Request `Mica` or `AcrylicBlur` through Avalonia window transparency only when appropriate and supported. Always set a solid `TransparencyBackgroundFallback` or equivalent project fallback.
- Reuse the project's corner-radius, border, spacing, and sizing scales. Do not introduce isolated magic numbers to imitate screenshots.

### Icons

- Reuse the installed icon set or shared `PathIcon` resources; do not add an icon dependency for a handful of glyphs.
- Use one visual style and consistent optical size.
- Pair unfamiliar or destructive icons with visible labels. Give icon-only buttons an accessible name and tooltip.
- Do not use emoji or text glyphs as application icons when a stable vector icon exists.

### Layout and responsiveness

- Use `Grid` with `Auto` and `*` sizing for structure; reserve fixed sizes for intentional chrome, icons, or bounded controls.
- Use `WrapPanel` or a virtualizing/reflowing items layout for collections that should flow.
- Prefer component-level container queries over window-size event handlers when the installed Avalonia version supports them.
- Keep content reachable with `ScrollViewer` when it can overflow.
- Set sensible minimum sizes; do not require maximized windows.
- Test long Traditional Chinese and English text, display scaling, narrow windows, and empty content.

### Typography and writing

- Reuse the application font and type scale. Use weight and size to express hierarchy, not many near-duplicate styles.
- Keep body text readable, allow wrapping where meaning matters, and reserve truncation for recoverable labels with another way to reveal the full value.
- Write concise, specific labels in the user's language. Prefer action verbs, explain consequences, and avoid implementation jargon.
- Keep terminology and punctuation consistent with the localization source of truth.

### Motion

- Use motion to acknowledge input, explain spatial change, or preserve continuity.
- Prefer Avalonia transitions and page transitions over imperative animation code.
- Keep motion short and interruptible. Avoid animation that delays input or repeats without user value.
- Respect an existing reduced-motion setting. If none exists, avoid essential information that is visible only through motion.

### Navigation

- Keep top-level destinations stable and few. Preserve selection and focus when moving between views.
- Keep back navigation predictable and never use it as an unlabeled destructive action.
- Reuse the repository's navigation architecture. Before adopting newer `NavigationPage`, `TabbedPage`, or `DrawerPage` controls, verify the installed Avalonia version and platform targets.
- Use `SplitView` with an existing selection control when that is already the app pattern; do not replace navigation solely to resemble WinUI.

### Accessibility and input

- Make every interactive feature operable by keyboard; keep traversal order aligned with visual order.
- Preserve a visible focus indicator. Verify selectors such as `:focus` or `:focus-visible` against the installed Avalonia version.
- Set meaningful `AutomationProperties.Name` or visible labels for controls whose purpose is otherwise unclear. Use `AutomationId` for stable automation targeting, not as a substitute for an accessible name.
- Provide tooltips for icon-only controls, but do not put essential instructions only in tooltips.
- Support pointer, keyboard, touch, and screen reader behavior relevant to the target platforms.
- Keep click/touch targets comfortably larger than their glyph and separated from adjacent destructive actions.
- Announce or visibly present errors near their source, preserve entered data, and move focus only when it helps recovery.

### Haptics and widgets

- Treat haptics and Windows widgets as platform-specific extensions, not baseline Avalonia features.
- Add them only when explicitly required and when a supported platform API exists; preserve a non-haptic, in-app path.

## Interaction patterns

### Pointer, touch, pen, and keyboard

- Make the same essential action available without hover, right-click, or touch gestures.
- Use hover for preview and emphasis, never as the only way to reveal required information.
- Keep keyboard shortcuts discoverable in menus or tooltips and avoid overriding common text-editing or platform shortcuts.
- Let controls keep their native input behavior unless the product requirement proves it inadequate.
- Treat touch and pen as imprecise input: provide comfortable targets, spacing, and an obvious pressed state.
- Preserve a stable focus target after refresh, deletion, navigation, and dialog close.

### Forms and data entry

- Keep labels visible; do not use placeholder text as the only label.
- Group related fields, order them by task flow, and mark optional fields rather than repeating required markers everywhere.
- Choose the narrowest native control that fits the value: checkbox for independent booleans, radio buttons for a short exclusive set, combo box for longer sets, and numeric/date controls for constrained values.
- Validate at the point where the user can act. Preserve input, identify the field, explain the problem, and state how to recover.
- Keep destructive or irreversible commands away from routine form actions.

### Search, filtering, sorting, and selection

- Keep search near the collection it affects and make clear whether it searches the current view or a broader scope.
- Show active filters and provide one obvious way to clear them.
- Keep sort direction and selection state visible and keyboard operable.
- Preserve selection across harmless view changes when the underlying item remains present.
- For multi-select actions, show the selected count and apply commands to the stated scope only.

### Menus, flyouts, tooltips, and dialogs

- Use menus for compact sets of secondary commands, flyouts for lightweight contextual interaction, and dialogs for decisions that must block the current flow.
- Keep menu labels direct and stable. Do not place the only copy of an important state inside a transient surface.
- Give dialogs a specific title, concise explanation, safe default focus, and clearly ordered actions.
- Use an owned Avalonia window for desktop modal dialogs or an accessible in-window overlay when the target cannot host secondary windows.
- Restore focus to the invoking control after a transient surface closes.

### Collections and content

- Use virtualization for large or unbounded collections and keep item templates visually stable during recycling.
- Make the selected item and keyboard focus visually distinct.
- Keep row and tile actions consistent; do not move destructive actions between items.
- Use progressive disclosure for metadata. Show the information needed to identify and act on an item first.

## Content and system states

### Loading and progress

- Show determinate progress when total work is known and indeterminate progress only when it is not.
- Keep the previous useful content visible during brief refreshes when stale content is safe.
- Prevent duplicate submission while work is active, but keep cancel or close available when the operation supports it.
- Describe long-running work in plain language and report completion or failure in the same context.

### Empty states

- Distinguish first-use empty, filtered-empty, permission-denied, unavailable, and error states.
- State what happened and offer the most useful next action. Avoid decorative illustrations that displace the recovery action.
- Keep empty states consistent with the size and hierarchy of populated content.

### Errors and recovery

- Put field errors next to fields and system errors near the affected surface.
- Explain the consequence, preserve user work, and provide retry, undo, or another concrete recovery path where possible.
- Use confirmation before hard-to-reverse destructive operations and prefer undo for frequent reversible operations.
- Log diagnostic detail separately; do not expose stack traces or internal identifiers as user instructions.

### Notifications and status

- Use inline status for information tied to a surface, transient notifications for completed background actions, and dialogs only for blocking decisions.
- Do not rely on timeout-only messages for critical information.
- Keep status text selectable or otherwise recoverable when users may need to copy it.

## Localization and platform behavior

- Treat all visible strings as localizable and allow expansion without overlap or clipped controls.
- Respect right-to-left flow when the supported language requires it; do not encode direction with literal arrows when a mirrored icon or layout property exists.
- Use locale-aware formatting for dates, numbers, units, and plural forms.
- Preserve native window, file picker, clipboard, drag-and-drop, and shortcut behavior on each target platform.
- Test at least one Windows and one supported Linux environment for cross-platform desktop products; document deliberate differences.
- Keep platform-only features behind capability checks and provide a baseline path everywhere else.

## WinUI concepts to translate

| Windows / WinUI concept | Avalonia approach |
| --- | --- |
| `.xaml` | `.axaml` |
| `ThemeResource` | `DynamicResource` |
| `StaticResource` | `StaticResource` |
| `ResourceDictionary.ThemeDictionaries` | `ResourceDictionary.ThemeDictionaries` after version verification |
| `ElementTheme` | `RequestedThemeVariant` / theme variant scope after version verification |
| `VisualStateManager` and triggers | Style selectors, classes, and pseudo-classes |
| `AdaptiveTrigger` | Container queries, reflowing layouts, or the existing breakpoint pattern |
| `NavigationView` | Existing navigation pattern, often `SplitView` plus a selection control |
| `ContentDialog` | Owned `Window.ShowDialog<T>` or an accessible in-window overlay |
| `CommandBar` | Installed Avalonia command control when available, otherwise `ToolBar`, `Menu`, or the existing pattern |
| `ListView` / `GridView` | `ListBox`, `ItemsRepeater`, or an existing virtualized collection |
| `Visibility` enum | Boolean `IsVisible` |
| Storyboards for control states | Transitions, animations, and pseudo-class styles |
| `pack://` asset URI | `avares://` URI |
| Mica / Acrylic material | `TransparencyLevelHint` plus a solid fallback; support is platform-dependent |

Do not assume a named WinUI control has a one-to-one Avalonia equivalent. Search `avalonia-docs`, inspect installed packages, and reuse the current application pattern.

## Review states

For each interactive component, inspect only the states that apply:

- default
- pointer-over
- pressed
- keyboard focus
- selected or checked
- disabled
- loading or busy
- empty
- validation error
- success or completion

Check that state changes remain understandable without color, do not shift layout unexpectedly, and preserve keyboard focus.

## Definition of done

- The primary task and primary action are obvious.
- Existing project tokens and components are reused.
- Every applicable state is designed and understandable without color alone.
- Keyboard traversal, activation, escape/back behavior, and focus restoration work.
- Visible controls have accessible names and icon-only controls have tooltips.
- Narrow, wide, scaled, light, dark, localized, empty, loading, and error cases have been considered where applicable.
- Platform-dependent effects have solid fallbacks.
- The affected project builds and the smallest relevant behavior or visual check passes.
