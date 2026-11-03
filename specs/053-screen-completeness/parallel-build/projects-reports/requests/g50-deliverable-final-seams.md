# C50 / S33 — final root integrations

The Settings helper exists at `web/src/pages/projects/deliverable-numbering.tsx`, exporting the agreed `DeliverableNumberingSettings({projectId}:{projectId:string})`. Root's import/render is acknowledged. It uses the existing `['p',projectId,'deliverable-numbering']` cache prefix, so ordinary `refreshProject` covers it; it explicitly refetches only on the person's Reload action. A background read never silently resets typed pattern or its original project baseline.

Merge the four UI-only dropped-view labels in `g50-deliverable-register-text-delta.json` if they do not already exist. No other locale delta is required. The generic layout label is “View layout” so it remains appropriate to other lists.

Add `deliverable-register.fixture.cjs` to root-owned `web/tests/tsconfig.json` include list before fixture typechecking. It types the three new DTO constructor defaults and does not edit existing fixtures or generated contracts. New `web/tests/deliverable-register.cjs` consumes it and the existing platform/deliverable/reference/view fixtures. C did not execute the script, parser, typechecker or browser.

UI expects the exact approved OpenAPI DTO names and optional fields already requested: NumberingRead, DeliverableRegisterInfo, DeliverableIssueRegisterInfo, RegisterInfo on row/history, RecordIssueResponse permission, new query/body fields, numbering GET/PUT and nested response PATCH. Root must generate those from the held backend before frontend compilation. No cast or duplicate transport bypass is introduced for new APIs.

Preserve earlier source changes inside Deliverables.tsx (RecordContext, existing Work origin/related navigation, baseline/revision/issue guards). Current loan only adds register semantics, local date-list UI, optional numbering and per-real-issue recipient response. The original source/UI test suites remain intact.
