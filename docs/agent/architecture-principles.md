# Architecture Principles

- **Layered Development:** Deliver the smallest working version first. Add features on top of a verified foundation.
- **Modularity:** Keep modules separate with clear boundaries and single concerns.
- **Dependency Direction:** Keep dependencies flowing from `piclens-gpui` to `piclens-infra` to `piclens-domain`.
- **GPUI Authority:** Use the APIs in the locked GPUI revision. GPUI is pre-1.0, so floating examples can be incompatible.
- **Application Thread:** Keep filesystem work, image decode, and other blocking work off the application thread.
- **Lifecycle:** Own tasks and subscriptions for the lifetime of the state that uses them.
- **Dependencies:** Inspect existing project dependencies and types before you add new libraries.
- **Hygiene:** Remove obsolete files and dead paths immediately.
