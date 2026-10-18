# Plan: Task Pickup

Implements §53 (FR-PKP-01 to FR-PKP-13) with defaults D1 to D6. Each slice in [tasks.md](tasks.md) is one PR that merges dark behind `flag.task_pickup`.

## Approach

Reuse first. New storage is one small table.

- **Offer** (§53.2): `TaskPickupOffer(id, task_id, offered_by, assignee_id, reason, hours_remaining, needed_by, note, opened_at, closed_at, close_cause)` with a partial unique index on `task_id` where `closed_at is null`. Auto-close hooks into the task save path in `HubDb.SaveChangesAsync` when status, assignee or deletion changes, in the same transaction.
- **Same team** (§53.4): one function, `Permissions.SameTeam(a, b)`, in `src/Hub.Domain/Permissions.cs`: same home unit when packet 050's flag is on and both have one, otherwise the same `SupervisorId`.
- **Pick up and assign:** both call the existing task reassignment handler in `src/Hub.Api/Features/Tasks.cs` in one transaction (adding the picker as Team Member if needed), then close the offer. The rowVersion check makes the first of two simultaneous picks win.
- **List** (§53.3): one query in a new `src/Hub.Api/Features/TaskPickup.cs`, filtered through the existing project visibility read used by `MyWork.cs` and by `SameTeam`.
- **Notifications and log:** existing `Notifications.cs` event catalogue and `ActivityLog` writes; no new channel.
- **Report:** a catalogue entry in `Reports.cs`.

## File boundaries

| Slice | Files |
|---|---|
| S01 offer server | `src/Hub.Api/Data/Entities.cs`, `HubDb.cs`, one additive migration, `src/Hub.Api/Features/TaskPickup.cs` (new), `src/Hub.Domain/Permissions.cs`, `src/Hub.Domain/Flags.cs`, `src/Hub.Api/Text.cs` |
| S02 pick up and assign server | `src/Hub.Api/Features/TaskPickup.cs`, `src/Hub.Api/Features/Tasks.cs` (reuse only), `src/Hub.Api/Features/Team.cs` (reuse only), `src/Hub.Domain/Permissions.cs`, `src/Hub.Api/Features/Notifications.cs` |
| S03 UI | `web/src/pages/projects/TaskPanel.tsx`, `web/src/pages/MyWork.tsx`, `web/src/pages/projects/Tasks.tsx`, `web/src/pages/Pickup.tsx` (new), `web/src/i18n/en.ts`, `web/tests/task-pickup.cjs` (new) |
| S04 report | `src/Hub.Api/Features/Reports.cs`, `web/src/pages/Reports.tsx` |

Tests live in `tests/Hub.Tests/Domain/`, `tests/Hub.Tests/Api/` and `web/tests/`.

## Risks

- **Stale offers:** with no expiry (D6) offers can linger; the report's days-open column makes that visible. Revisit D6 if the median exceeds two weeks in pilot.
