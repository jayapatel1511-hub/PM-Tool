# C50 — exact browser mutation-guard repair, RE-READY / HELD

The coordinator's test-only loan is complete. Only `web/tests/deliverable-register.cjs` changed; application source, fixtures and all C# remain held.

Actual outgoing API mutations are now observed at request level, independently of mocked endpoint responses. A last-registered top interceptor rejects every unlisted mutation with501 before the platform fixture can return its permissive200 fallback. The METHOD + PATH allowlist uses the exact synthetic project/deliverable/issue identities and only the intended sign-in, create, client-number PATCH, actual issue POST, recipient-response PATCH, project-numbering PUT and view POST commands. No general deliverable wildcard, transition, lifecycle, bulk, DELETE or unrelated ID is accepted.

Final assertions require both unexpected and rejected collections empty, and reconcile the actual product mutation count with the intended handlers' write collections after accounting for legitimate sign-ins. Existing response body/dual-version and numbering/parent-version assertions remain; client-number body keys additionally exclude lifecycle/issue changes.

PASS: `node --check web/tests/deliverable-register.cjs` (exit0), run under the coordinator's explicit optional Node-check exception. This is the sole lane execution exception to the earlier handoff's unrun statement. No browser/build/typecheck/lint/fixture/C# execution was run here. Chrome behavior remains UNPROVEN by this lane; root separately reports all33 focused API cases passed, including C's13.

**ALL SOURCE RE-READY / HELD.** No further source edit without another explicit repair loan.
