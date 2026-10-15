# iOS simulator implementation

Requested 2026-10-14: finish iOS using a goal and simulator tests. Jay confirmed no Entra native registration exists and selected simulator fixtures for now.

## Authorized scope

Build the native iPhone daily workflow against the proposed FR-IOS-01–12 experience, with **synthetic fixtures only**. This checkout is the clean spec-only `16399682` baseline. Existing web/API worktrees are read-only references and remain untouched. The simulator build has no production sign-in, network access or company data. Release/device builds cannot enter the fixture workspace.

File ownership: `ios/` (Xcode app, fixture service, state/draft persistence, SwiftUI screens, unit/UI tests), `docs/ios/` (existing proposed spec copied intact, plan, verification), and root `.gitignore` for Xcode output. No backend, web, operational data, hosting or authentication registration changes.

## Workflow and acceptance

- Enter as a labelled synthetic assignee, reviewer or read-only person; resolve fixture identity before workspace data.
- My Work → engineering task context → permitted edit/review/comment/mention/time action → fixture acknowledgment and refresh. Create tasks inside an explicitly chosen project/discipline.
- Search and page scoped task/project lists; explicit Inbox read acknowledgment; read-only week with unknown capacity and freshness warnings; safe HTTPS handoff.
- Recover account-scoped protected drafts. Preserve conflict intent and immutable unknown-outcome requests. Do not automatically replay writes. Hide workspace when inactive and revalidate on foreground.
- Run focused simulator unit tests for rules, dates, scope, permissions, replay and persistence, plus native UI tests of the actual workflow and recovery. Inspect simulator screens, including large text/dark appearance and a larger phone.

## Evidence limits

The fixture service is deliberately separate from the existing ASP.NET API. Its validation/replay tests do not prove the server's rules or fix the known shared backend idempotency gap. No corporate sign-in, API/database/web reconciliation, APNs, universal-link association, real-device VoiceOver, performance pilot, distribution or human acceptance can pass on fixture evidence. The full proposed acceptance gate remains authoritative for a later company-data pilot.
