# Tuesday iOS — Acceptance gate

**Contract:** [iPhone specification](spec.md).

Date: 2026-10-13. This is a runnable scenario checklist, not an executed test report. All native/API/hosted/human scenarios are **UNPROVEN** until evidence is attached. Documentation validation can pass independently.

2026-10-14 update: Jay selected a synthetic simulator workspace while native Entra registration is unavailable. See [simulator verification](verification.md) for executed native fixture checks. Those results do not pass the real API, real-device or company-pilot requirements below. The documentation PASS recorded at the end is the historical 2026-10-13 validation, not a new validation of this checkout.

## Verdict rules and test setup

Use **PASS** for an executed scenario with supporting evidence, **FAIL** for an observed mismatch, **BLOCKED** when a named prerequisite prevents the check, and **UNPROVEN** when it has not run. A build, screenshot or mocked response does not prove API persistence, corporate sign-in or engineering acceptance. Do not mark the mobile release accepted until every P0 scenario passes and IT/product acceptance is recorded.

Create labelled synthetic fixtures in an approved test environment: an assignee, an independent reviewer, a PM, a supervisor and a read-only user; one visible and one restricted project; permitted/denied disciplines; a review-required task, a blocked task, an ordinary task and archived/inaccessible work. Include published/tentative weekly entries, another manager's private draft and a stale availability source. Never use real client documents or private HR reasons as test data.

Record for each execution: app/API commit, environment URL, authentication mode, device and OS, fixture revision, scenario, actual result, trace/receipt IDs without tokens, and evidence location. For writes, inspect the API/database and the matching web page. Record denied actions and any unexpected extra audit or notification effects as failures.

## P0 scenarios

### AC-IOS-01 — Corporate authentication and account lifecycle

**Covers:** FR-IOS-01.

- Given the approved Entra native registration, when the user signs in on a real iPhone, then the API accepts the correct audience/scope and `/me` resolves the expected active identity.
- Cancelled sign-in returns to a usable sign-in screen. Wrong tenant/audience, inactive identity and expired credentials expose no project data.
- No client secret, development identity header or staging review cookie is used by the production configuration. A fixture-only sign-in run cannot pass this corporate-auth gate.

### AC-IOS-02 — Account isolation, logout and revoked access

**Covers:** FR-IOS-01, FR-IOS-11.

- Given account A's open task and unsent draft, when the person changes environment/account or signs out, then protected screen state, drafts and unresolved receipts cannot appear for account B. Sign-out warns about unsent drafts before clearing them.
- Background/app-switcher snapshots conceal protected content. A token-expired draft remains locked until the same identity reauthenticates; a known inactive/revoked identity is purged.
- After foreground return or relaunch while offline, protected entities stay locked pending online revalidation. A 403/404 revalidation evicts that entity and shows a neutral unavailable message.

### AC-IOS-03 — My Work truth and useful empty/error states

**Covers:** FR-IOS-02.

- Given assigned tasks, collaborations, task/deliverable review requests and owned decisions, when My Work loads, then its sections match `/me/work`; assignment and review responsibility remain distinct.
- Blocker identity/explanation is preserved only within permitted scope. Unsupported native entities retain context and a working web handoff.
- No assigned work shows a true empty state; a failed request shows an error and last successful timestamp. The app must not display a failed fetch as zero work.

### AC-IOS-04 — Task edits and technical-review safety

**Covers:** FR-IOS-03, FR-IOS-04.

- Given a review-required In Progress task, when the assignee requests review, then an eligible independent reviewer is required and the server result becomes Ready for Review. A direct Complete shortcut is absent and its attempted API command is refused.
- Given a task assigned for review, when the reviewer starts review, then the result matches the server's permitted transition. Formal disposition opens the matching full web review workflow in v1.
- Allowed owner/due-date/priority/progress edits persist through their existing commands; required reasons are requested. No task status is written by PATCH. A mobile write appears correctly on the web and in the audit record.

### AC-IOS-05 — Permission, readiness and read-only rules

**Covers:** FR-IOS-03, FR-IOS-04.

- Given a read-only user, denied discipline, blocked readiness condition or archived project, when detail is loaded and a write is attempted, then UI and API respect the current permissions and return actionable reasons.
- A stale enabled button cannot bypass a server denial. Changing a local role label does not create permission. No private/restricted data appears in explanation text or counts.

### AC-IOS-06 — Concurrency preserves the other person's work

**Covers:** FR-IOS-04, FR-IOS-11.

- Given a task loaded at version N, when the web updates it and the phone submits N, then the server refuses with 409. The other person's change remains intact; the phone retains its intended values and shows current server values before an explicit retry.
- Missing version is refused with 428. The client never automatically repeats a review transition or overwrites fields with a fresh version after conflict.

### AC-IOS-07 — Scoped task capture

**Covers:** FR-IOS-05.

- Given an authorized project/discipline, when a person submits a valid task, then exactly one returned task opens with correct owner, dates and parent context, and the web shows the same record.
- Missing title, invalid owner, parent in another project, denied discipline and connection failure leave a useful validation/error state and recoverable input. No guessed default bypasses required ownership or shared task rules.

### AC-IOS-08 — Duplicate-safe writes and unknown outcomes

**Covers:** FR-IOS-05, FR-IOS-06, FR-IOS-07, FR-IOS-11.

- Repeat for task creation, comment posting, time creation and a task transition: drop the response after the server commits, then retry the original bytes/key/path. Exactly one business action, receipt and appropriate audit/notification effects must exist.
- Send two overlapping identical keyed requests from controlled API clients. Both must resolve to the same action with no duplicate side effects. The inspected middleware currently does not establish this guarantee; resolve that shared backend gap before PASS.
- Reusing a key with a different payload or path is explicitly refused; it must not return a misleading successful result. Check behavior after the deduplication retention window too.
- Revoke project access after a successful write, then request receipt replay. Current authorization must prevent return of protected response content; active identity alone is insufficient.
- On double tap the app sends one logical in-flight command. On a timed-out result it displays Outcome unknown, preserves the original receipt and does not mint a fresh key. After the retry window it requires reconciliation rather than automatic resubmission.

### AC-IOS-09 — Comments, mentions and confidential visibility

**Covers:** FR-IOS-06.

- Given a permitted task, when a comment includes a selected mention, then body/recipient identity match the server representation and exactly the intended permitted recipient receives the existing notification behavior.
- Blank and over-limit comments fail without losing entered text. Deleted comments use the server's redacted representation. An unavailable recipient or revoked task access cannot leak hidden record content.
- The comment composer, mention picker and error message work with VoiceOver and the software keyboard.

### AC-IOS-10 — Time is actual effort with a shared daily cap

**Covers:** FR-IOS-07.

- Save `1.25` hours to an allowed task and work date; verify own daily total and the web record. Test localized decimal input without changing the wire value.
- Reject zero, negative hours, `24.01`, more than two fractional digits and a combined daily total above 24. Concurrent entries on phone/web must not defeat the daily cap.
- Correct an own entry with its row version; a stale correction fails safely. Archived/denied work is refused according to server rules. Actual hours never overwrite planned or estimated hours.

### AC-IOS-11 — Dates, weeks and timezone boundaries

**Covers:** FR-IOS-07, FR-IOS-10.

- Given organisation-local due/work dates, when the phone timezone changes across midnight or a DST boundary, then those dates and week selection remain unchanged and UTC activity timestamps display correctly.
- My Week uses the server's week convention and organisation settings. A nil due date, no plan and unknown capacity remain distinct from today, zero hours and zero capacity.

### AC-IOS-12 — Search, filtering and pagination cannot broaden scope

**Covers:** FR-IOS-08.

- Given more than one page of permitted tasks, when the user searches/filters and loads more, then every result and sort matches the API, without duplicates or omitted later pages.
- Change project scope while the prior request is delayed. Its late response cannot replace the new list. Inaccessible projects never appear in search, aggregate counts or local histories.
- A saved-view link with unsupported predicates offers an explicit explanation/web handoff; the app does not ignore predicates and present an apparently equivalent wider view. Saved-view authoring is absent in v1.

### AC-IOS-13 — Inbox acknowledgment and safe target opening

**Covers:** FR-IOS-09.

- Given unread notifications over several pages, when Inbox opens, then counts and items match the server. Opening an item alone does not mark it read.
- Explicit Mark read/Mark all read changes the count only after acknowledgment. A failed command retains a recoverable state. Foreground return refetches current counts/items.
- A notification whose target became restricted/deleted opens a neutral unavailable state without disclosing cached title/body. Duplicate foreground refreshes do not create extra commands.

### AC-IOS-14 — My Week planning privacy and source freshness

**Covers:** FR-IOS-10.

- Given published, tentative and private planning records, when the current person views My Week, then hours/confidence and permission-filtered totals agree with the planner/API. Another manager's private draft is absent.
- A partial view is labelled; unknown capacity and stale leave source are explained without fabricated available hours. HR reasons and balances never appear.
- No native edit, publish, approve-leave or rebalance action exists in v1. Open web planner preserves the relevant permitted week/context.

### AC-IOS-15 — Draft recovery and truthful offline behavior

**Covers:** FR-IOS-11.

- Enter task/comment/time input, interrupt with a network loss, and terminate/relaunch the app. After the same identity reauthenticates, the draft is recoverable with its original task/project context and unsent/sent-unknown status.
- Already loaded content in the current foreground session is labelled with its fetch timestamp after connectivity fails. Writes remain unavailable offline; entering a draft does not claim a server save.
- Reconnection does not autonomously replay mutations. Submitted unknown-outcome receipts cannot be edited into a new request before resolution. Expired/revoked permissions are rechecked before a recovered draft is sent.
- Inspect storage/logs: drafts/receipts have required protection and backup exclusion; full project caches, client files, tokens and comment text are absent from diagnostic logs.

### AC-IOS-16 — Accessible native daily workflow

**Covers:** FR-IOS-12.

- On the smallest and a large supported iPhone, complete sign-in, task selection, task edit/review request, comment/mention, time entry and error recovery at default and maximum accessibility text size.
- Repeat with VoiceOver; verify labels, actions, focus after sheet dismissal/error and readable status/reason text. All hit targets meet the specified 44 × 44 pt minimum. Status remains understandable without color.
- Check light/dark appearance, Reduce Motion, keyboard overlap, long project/task names and no hidden required action or clipped validation message. Attach device evidence; a web accessibility scan cannot pass this native gate.

### AC-IOS-17 — Deep links, web handoff and interruption

**Covers:** FR-IOS-12.

- Open an approved project/task HTTPS link on a real device from cold and warm app states, signed in and signed out. Login returns to the intended permitted item; installed/uninstalled behavior follows the approved web fallback.
- Unknown/malformed identifiers, unapproved hosts and inaccessible targets produce safe results. URL parameters cannot execute a write or become an arbitrary external redirect.
- Open a supported document link in its existing tool. A network-share/unsupported link explains the limitation. Returning from web/document apps revalidates native permissions/data without losing unsent input.

## Performance and pilot verdicts

Execute the budgets and stress inputs in §9 of the spec: at least 30 recorded runs for p95 first My Work and detail timings; record latency/cache/reference-device conditions. Check responsive scrolling and text input with the large work/comment fixtures. A fast mock-server result does not pass the deployed-path performance gate.

| Layer | Initial verdict | Required evidence |
|---|---|---|
| Documentation | PASS — local checks, 2026-10-13 | 12 unique requirements, 17 unique scenarios, complete bidirectional mappings, 29 valid file links, balanced fences, matching archive hashes and clean whitespace checks |
| Native implementation | UNPROVEN | Build identity, focused tests and accessible simulator/device flows |
| Real API behavior | UNPROVEN | Shared rule, duplicate-safety, concurrency, data/audit reconciliation scenarios |
| Approved hosted environment | UNPROVEN | HTTPS, real corporate identity, distribution and link association evidence |
| Real iPhone behavior | UNPROVEN | Actual-device auth, interruption, accessibility and link tests |
| Human/engineering acceptance | UNPROVEN | Representative users complete the core workflow; technical reviewer verifies QA boundaries; Jay accepts scope; IT accepts handling/distribution |

No production rollout follows merely from passing documentation checks. Push notification and a persistent offline outbox require follow-up requirements and gates before their behavior can be claimed.
