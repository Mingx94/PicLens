# PicLens Agent Guide

- Write in Traditional Chinese with Taiwan usage. Use short sentences, common words, and clear verbs. Preserve code, identifiers, API names, and required technical terms.
- Use `zhtw-mcp` to check uncertain regional terms. If unavailable, use known Taiwan terms and continue.
- Before changing behavior, read the relevant sections of the [product specification](docs/product/product-spec.md) and [runtime invariants](docs/engineering/runtime-invariants.md).
- After runtime changes, run Cargo checks and a real-app smoke test that cover the affected behavior. Follow the isolation requirements in the runtime invariants; use disposable copies for file-operation tests.
- Use Computer Use only when the user explicitly requests it.
- If interactive verification is unavailable or not authorized, complete other applicable checks and report the unverified behavior. Do not treat compilation as UI verification.
- Complete necessary local changes and relevant checks within the authorized task. Use existing conventions for routine implementation choices. If required input or authorization is missing, pause only the dependent work and complete unaffected authorized work.
- Fix failures caused by the requested changes and rerun affected checks without asking again. Once relevant checks pass, broaden or repeat them only for new changes, failures, or unresolved concerns.
- Explicit user instructions take precedence over skill guidance. Apply design recommendations in the product's context. If a skill causes a pause, an approval request, or a departure from the task, link the file, quote the instruction, and distinguish its requirement from your interpretation.
