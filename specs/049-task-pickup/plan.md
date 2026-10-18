# Plan: Task Pickup

Implements §53 (FR-PKP-01 to FR-PKP-13) with defaults D1 to D6. Each slice in [tasks.md](tasks.md) is one PR that merges dark behind `flag.task_pickup`.

## Approach

Reuse first. New storage is two small tables.

- **Offer** (§53.2): `TaskPickupOffer(id, task_id, offered_by, assignee_id, reason, hours_remaining, needed_by, note, opened_at, closed_at, close_cause)` with a partial unique index on `task_id` where `closed_at is null`. Auto-close hooks into the task save path in `HubDb.SaveChangesAsync` when status, assignee or deletion changes, in the same transaction.
- **Request** (§53.4): `TaskPickupRequest(id, offer_id, requester_id, note, requested_at, decided_at, decided_by, outcome, decline_reason)`.
- **Approve:** calls the existing task reassignment handler in `src/Hub.Api/Features/Tasks.cs` inside the same transaction, then closes the offer and declines the rest. Permission is one function, `Permissions.ApprovePickup`, in `src/Hub.Domain/Permissions.cs`.
- **List** (§53.3): one query in a new `src/Hub.Api/Features/TaskPickup.cs`, filtered through the existing project visibility read used by `MyWork.cs`.
- **Notifications and log:** existing `Notifications.cs` event catalogue and `ActivityLog` writes; no new channel.
- **Report:** a catalogue entry in `Reports.cs`.

## File boundaries

| Slice | Files |
|---|---|
| S01 offer server | `src/Hub.Api/Data/Entities.cs`, `HubDb.cs`, one additive migration, `src/Hub.Api/Features/TaskPickup.cs` (new), `src/Hub.Domain/Permissions.cs`, `src/Hub.Domain/Flags.cs`, `src/Hub.Api/Text.cs` |
| S02 request and approve server | `src/Hub.Api/Features/TaskPickup.cs`, `src/Hub.Api/Features/Tasks.cs` (reuse only), `src/Hub.Domain/Permissions.cs`, `src/Hub.Domain/Settings.cs`, `src/Hub.Api/Features/Notifications.cs` |
| S03 UI | `web/src/pages/projects/TaskPanel.tsx`, `web/src/pages/MyWork.tsx`, `web/src/pages/projects/Tasks.tsx`, `web/src/pages/Pickup.tsx` (new), `web/src/i18n/en.ts`, `web/tests/task-pickup.cjs` (new) |
| S04 report | `src/Hub.Api/Features/Reports.cs`, `web/src/pages/Reports.tsx` |

Tests live in `tests/Hub.Tests/Domain/`, `tests/Hub.Tests/Api/` and `web/tests/`.

## Risks

- **Stale offers:** with no expiry (D6) offers can linger; the report's days-open column makes that visible. Revisit D6 if the median exceeds two weeks in pilot.
