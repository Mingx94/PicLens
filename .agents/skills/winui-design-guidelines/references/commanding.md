# Commanding

Source: https://learn.microsoft.com/zh-tw/windows/apps/design/basics/commanding-basics

Source snapshot: Microsoft Learn zh-tw page reviewed on 2026-06-06.

Commands are interactive UI elements that let users act, submit, search, filter, create, edit, save, delete, or choose settings.

## Guidelines

- Start with the task flow: what the user is trying to accomplish, how many steps are needed, and which inputs are likely.
- Choose the right control:
  - `Button` for immediate actions.
  - Lists or grids for item selection and display.
  - `CheckBox`, `RadioButton`, and `ToggleSwitch` for options.
  - Date/time pickers for date or time values.
  - `AutoSuggestBox` when suggestions help while typing.
- Put critical, frequent commands on the canvas near the affected object.
- Keep infrequent or secondary commands in `CommandBar`, `MenuBar`, flyouts, menus, or context menus.
- Avoid crowding the canvas with commands that compete with content.
- Prefer direct manipulation when it is clearer, such as drag/drop for reordering.
- Give command feedback only when it helps. Good surfaces include `CommandBar` content, flyouts, and dialogs.
- Use dialogs cautiously because they interrupt the workflow.
- Confirm actions that are irreversible and high consequence: overwrite, permanent delete, unsaved close, purchase, or important form submission.
- For reversible actions such as non-permanent delete, rename, or content edits, prefer a clear undo command.
