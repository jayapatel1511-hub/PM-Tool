# G55 / checkpoint27 — D shared document-link handoff

**READY / HOLD, 2026-11-02.** Source authoring and the narrowly authorized browser-driver repair are complete. D has run no parser, type checker, lint, generator, build, provider test, browser, service or helper. This is a source handoff, not a runtime PASS or acceptance verdict. Root owns integration, frozen execution and final evidence. Application and fixture sources remain held.

Implementation root: `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`, branch `codex/build-052-053`. The root-frozen [G55 contract](../../g55-file-link-freeze.md) governs scope under `people_screens`, with FR/AC-SCR-77 and AC-SCR-84. No commit, push, deployment, operational flag, dependency, schema, backend, generated-contract, locale or acceptance-status change was made by D.

## Exact delivered source

Hashes below were reread from disk when this handoff was written. Later root changes require a new manifest; do not treat these hashes as a moving baseline.

| File | Lines | SHA-256 |
|---|---:|---|
| `web/src/components/hub/links.tsx` | 180 | `23e19ed5be1d77bc32f593279169329604cfa6b18b8b717dad1292149b9a0d24` |
| `web/src/components/hub/document-link-reference.tsx` | 123 | `8a5da7e54765fc33fe5dbd517dea734a9ded02e6ed63e20c183feef2521fc2cd` |
| `web/tests/document-link-reference.cjs` | 267 | `b640b10da61c42dcd53691a805f8d165d17bf741208dd6107e2eee31c12da0c3` |
| `web/tests/document-link-reference.fixture.cjs` | 54 | `190fe0c0c38899054dffca5b2f9131139c541c7a31d2522ae6401e36f4bd6fc7` |

Read-only source, trailing-whitespace, final-newline and SHA-256 checks passed. A read-only substring comparison against `git show HEAD:web/src/components/hub/links.tsx` established that the entire former `AddLink` function body, now named `LegacyAddLink`, and the following `detect()` source are identical. The off branch retains that form, its IDs, body and behavior; its old query key and row classes are preserved. New registration fields and idempotency headers occur only in the enabled branch. This source parity does not substitute for root's flag-off browser/provider checks.

## Shared interfaces and safety boundaries

- `LinkReferenceSummary({context,focusKey})` uses generated `DocumentLinkReferenceContext`. It distinguishes all seven frozen comparison states, shows neutral manual matches, and labels the warning **Declared C, current D**. Missing values remain unknown; RowVersion is not displayed as a drawing revision. A registrar trigger requires a known User descriptor whose ID actually equals `registeredBy`; no raw identifier, guessed person, or inherited mutation action is introduced. Each caller supplies a distinct source key.
- `LinkReferenceEditor({link,onClose,onDone})` uses the frozen generated `LinkRow` Pick. It PATCHes only `{reference: replacement}` or `{reference: null}`, with the originally displayed If-Match. Definite stale refusal retains inputs and locks Save. Once uncertain, its ref-held intent remains immutable through later refusals; no current-version promotion occurs. It validates the five-field reply and exact target ID before clearing input or notifying success. Inherited rows never mount it in LinksPanel; callers remain responsible for current own-link change permission.
- Enabled `AddLink` adds explicit optional manual registration, preserves existing URL/path/type detection, and retains one original body/path/Idempotency-Key after uncertainty or an in-progress receipt. It validates the old five-field reply and expected address before clearing/callback. A definite refusal can be corrected with a new key only before uncertainty. No registrar, time or comparison-target field is sent. Raw reference text reaches the canonical control-before-trim validator; client trimming cannot hide forbidden controls.
- Enabled editor/create state is partitioned by viewer and owner; a current own-link permission check governs editor mounting. Snapshot versions are not replaced by a fresher row. Standard safety/Cancel and Dialog focus mechanics are reused, with no Person-navigation exception or new global guard. The new shared module does not import `links.tsx`, avoiding a direct module cycle.
- A owns Files and consumes the two frozen exports. Root owns text, cache, schema, registry, generated contracts and test inventory. Root confirmed A's successful editor callback invalidates DocumentLink before closing. Root also added the conditional task hint so the frozen owner-less editor Pick does not assert that every owner is a task.

## Browser source and observed repair

The driver cites AC-SCR-77 / AC-SCR-84 and uses finite GET and business-write guards. Authored cases cover comparison/provenance states, actual registrar source/query/nonzero-scroll/natural-focus return, inherited read-only behavior, HTTP action and narrowly observed UNC Copy path, clean/dirty Cancel, replacement/explicit-null clear, named refusals, original-version uncertain PATCH, idempotent uncertain creates, reply validation, optional registration, 375px layout and the exact legacy off form/body/header boundary. Its mocks establish UI behavior only; they do not establish real access, persistence, external file contents or canonical comparison correctness.

Root's initial [browser-reference1.log](../../../evidence/checkpoint27-runtime/browser-reference1.log) stopped in `editor-normal` while filling an optional field. It recorded two completed source cases, zero commands/mutations, an unexpected `planning/grid` read and a timeout waiting for `Stable source ID`. **No full driver PASS follows from that run.**

The authorized CJS-only repair changed these exact boundaries:

1. Canonical `Field` appends ` (optional)` to accessible labels. The fill helper, retained-value check and phone loop now use exact **Stable source ID (optional)** and **Declared revision (optional)** labels. Required **Source system** remains exact and unchanged. No application label was altered.
2. Source inspection identified the actual planning read as `PersonPanel -> usePeopleCapacity`, not the separate self MyWeekStrip. The driver permits only `planning/grid` with the exact object `{personIds: actualRegistrarId, from: actualFixturePerson.week, weeks: '1'}` while enabled, then delegates to the existing platform fixture. It records and finally asserts exactly that one scoped read. Unknown reads/writes remain refused; no broad planning fallback was added.
3. Root's optional `TUESDAY_SCREENSHOT_DIR` hooks and explicit AC77/84 header remain intact. Diagnostics include the finite planning reads alongside source/dialog/focus/scroll/case/counter evidence.

Application and fixture hashes did not change during this driver repair. Subsequent root execution may exist; use the latest root checkpoint evidence/manifest for its verdict rather than inferring one from this handoff.

## Checks still owned by root, all unrun by D

These are explicitly unrun **by D**; this list does not assert that root has not already executed some of them on its frozen batch.

- Parse both CJS files: `node --check web/tests/document-link-reference.cjs` and `node --check web/tests/document-link-reference.fixture.cjs` from the implementation root.
- After root's canonical generation and fixture inventory integration, run `npm run typecheck:tests` from `web`; run the relevant application type check (`tsc -b`), focused `oxlint` for the two delivered TSX files, and root's production build/SSR checks. Root alone decides whether to reuse its already-generated frozen inputs or invoke `npm run build`, which also generates contracts.
- Run the repaired driver once through the single browser lane, using the frozen verified preview and Chrome: `TUESDAY_UI_BASE_URL=<verified-local-base> CHROME_EXECUTABLE_PATH=<verified-Chrome> node web/tests/document-link-reference.cjs`. The optional screenshot directory is root-controlled. Preserve the first failed log and record the final source/build hashes alongside any rerun.
- Run A's `web/tests/file-screen.cjs` against the same frozen integration; preserve its independent source/filter/permission/refusal/Person-return assertions. Run adjacent legacy link/Files checks required by root's impact review, including flag-off behavior and unchanged old response shapes.
- C owns canonical provider evidence for `DocumentLinkReferenceTests` and `DocumentLinkReferenceReadTests`; B owns `FileScreenTests`. Root owns their actual provider execution, legacy collaboration/Files coverage, schema/registry/parity checks and final counts. No synthetic driver response grants permission, proves receipt persistence, or verifies the remote file.

No further source writing is authorized by this handoff. Root must reopen an exact loan for any observed repair. Prior QA24 data/services and all preserved evidence remain untouched. Jay's acceptance and deployment are separate from local implementation and verification.
