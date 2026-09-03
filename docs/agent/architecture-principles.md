# Architecture Principles

- **Layered Development:** Deliver the smallest working version first. Add features on top of a verified foundation.
- **Modularity:** Keep modules separate with clear boundaries and single concerns.
- **Dependency Direction:** Keep dependencies flowing from `piclens-desktop` to `piclens-infra` to `piclens-domain`.
- **Frontend Authority:** Use the egui and eframe versions pinned by the workspace manifest and lockfile.
- **Application Thread:** Keep filesystem work, image decode, and other blocking work off the application thread.
- **Lifecycle:** Own background workers and requests for the lifetime of the state that uses them.
- **Dependencies:** Inspect existing project dependencies and types before you add new libraries.
- **Hygiene:** Remove obsolete files and dead paths immediately.
