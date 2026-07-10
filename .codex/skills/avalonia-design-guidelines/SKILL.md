---
name: avalonia-design-guidelines
description: Apply Microsoft Windows and Fluent design guidance to Avalonia applications without assuming WinUI APIs. Use when designing, implementing, or reviewing Avalonia AXAML interfaces, design systems, themes, controls, navigation, responsive layouts, visual states, accessibility, interaction, motion, icons, typography, or UI copy.
---

# Avalonia Design Guidelines

Create calm, familiar, accessible Avalonia interfaces while preserving the project's own product and visual identity.

## Establish context

1. Read the nearest `AGENTS.md` and the project's documentation entry point. In PicLens, start at `docs/README.md`.
2. Read existing design sources such as `DESIGN.md`, theme dictionaries, application resources, shared styles, and representative views.
3. Reuse existing tokens, controls, icons, and interaction patterns before adding anything.
4. Use the bundled reference as the complete local design baseline. Call `avalonia-docs` `get_avalonia_expert_rules` at the start of Avalonia work and use that MCP to obtain current Avalonia syntax, controls, APIs, and version-sensitive patterns.
5. Treat Windows and Fluent concepts as design intent, not Avalonia API documentation. Never paste WinUI/UWP XAML into an Avalonia project.

## Choose the task

- For a design request, state the user task, hierarchy, interaction states, and responsive behavior before suggesting visuals.
- For a review request, report evidence and actionable issues; do not edit unless asked.
- For an implementation request, make the smallest coherent change in existing resource and style locations, then verify it.

## Apply the guideline

1. Start from the user's primary task. Remove decoration or commands that compete with it.
2. Preserve familiar platform behavior for windows, focus, keyboard input, selection, context menus, dialogs, and back navigation.
3. Express color, typography, spacing, shape, elevation, and motion through the existing token system. Add a token only when a value is reused or semantically important.
4. Use Avalonia native controls and styling before custom controls. Use selectors, classes, pseudo-classes, themes, and resources instead of WinUI `VisualStateManager` patterns.
5. Design every applicable state: default, pointer-over, pressed, focused, selected, disabled, loading, empty, error, and success.
6. Keep the interface usable with keyboard alone, visible focus, accessible names, sufficient contrast, text scaling, and non-color status cues.
7. Make layouts adapt using `Auto`/`*` sizing, reflowing panels, or container queries supported by the installed Avalonia version. Avoid fixed window-size assumptions.
8. Keep platform-dependent materials, haptics, and window effects optional and provide a solid fallback.

Read [references/guideline.md](references/guideline.md) for every design or review task. For implementation or AXAML review, use `avalonia-docs` and the installed package version instead of maintaining local API examples.

## Verify

- Build the affected project and run the smallest relevant UI or behavior check.
- Exercise keyboard traversal and focus restoration.
- Check narrow and wide window sizes, light and dark themes when supported, long/localized text, empty data, and error states.
- Confirm platform-specific effects degrade safely on every target platform in scope.
- For visual changes, inspect a rendered screenshot or the running view when tooling is available.

## Report

Lead with the result. Name reused project conventions, checks performed, and any deliberate deviation from the reference guideline. Do not claim pixel-perfect Windows parity; report behavioral and design-system alignment.
