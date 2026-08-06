- Delete obsolete paths. Ship only current code.
- Use simplest code that meets needs now.
- Build in layers. Ship smallest working version first. Add on top of working product.
- Keep modules separate with clear concerns.
- Prefer mature libraries that simplify or stabilize.
- Check existing deps, docs, and types first.
- Design for the long term.
- Study proven products. Adopt their patterns.
- Use the UTC build timestamp as every release version: `YYMMDD.HHmm` (for example, `260806.1530`).

---

File changes:

1. `git status`. Preserve existing work.
2. Change and stage task work only.
3. Review the diff. Run fitting checks.
4. Commit on `main`. Short message. Report hash.

Push/amend/rewrite need explicit request.
Read-only work: report.
