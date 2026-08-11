Always use ASD-STE100 Simplified Technical English. 
Follow Zinsser's four principles of quality writing: 1. Simplicity, 2. Brevity, 3. Clarity, 4. Humanity

---

- Delete obsolete paths. Ship only current code.
- Use simplest code that meets needs now.
- Build in layers. Ship smallest working version first. Add on top of working product.
- Keep modules separate with clear concerns.
- Prefer mature libraries that simplify or stabilize.
- Check existing deps, docs, and types first.
- Design for the long term.
- Study proven products. Adopt their patterns.
- Use the UTC release date and two-digit serial as every release version: `YY.MMDD.NN` (for example, `26.0806.01`).
- Release: verify packages, push the matching `v<version>` tag to GitHub, and confirm the release Action succeeds.

---

File changes:

1. `git status`. Preserve existing work.
2. Change and stage task work only.
3. Review the diff. Run fitting checks.
4. Commit on `main`. Short message. Report hash.

Push/amend/rewrite need explicit request.
Read-only work: report.
