- Delete obsolete paths. Ship only current code.
- Use simplest code that meets needs now.
- Build in layers. Ship smallest working version first. Add on top of working product.
- Keep modules separate with clear concerns.
- Prefer mature libraries that simplify or stabilize.
- Check existing deps, docs, and types first.
- Design for the long term.
- Study proven products. Adopt their patterns.
- Use the build date as every release version: `YYYY.M.D` (for example, `2026.8.6`).

---

File changes:

1. `git status`. Preserve existing work.
2. Change and stage task work only.
3. Review the diff. Run fitting checks.
4. Commit on `main`. Short message. Report hash.

Push/amend/rewrite need explicit request.
Read-only work: report.
