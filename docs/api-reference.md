# Technical interface reference

The Hub's single-page application uses the same HTTP API that integrations may use. This page gives the conventions; the
full, generated description of every operation is the OpenAPI document at **`/api/v1/openapi.json`** (signed-in users
outside development).

## Access

- Base path `/api/v1`. HTTPS only; plain HTTP is redirected and HSTS is sent (§21).
- Every request carries an Entra ID access token for the Hub API (`Authorization: Bearer …`, audience and scope from the
  environment's app registration). There are no API keys or passwords. What a caller can see and do is exactly what the
  signed-in person can in the interface; permissions are checked on the server for every request.
- Each person is limited to 600 requests a minute (HTTP 429 beyond it, with `Retry-After` in seconds).
- `GET /health` (anonymous) answers `Healthy`, `Degraded` (evaluation backlog above 10 minutes) or `Unhealthy`.

## Requests and responses

- JSON with camelCase names. Calendar dates are `yyyy-MM-dd` in the organisation's time zone; timestamps are ISO 8601 UTC.
- Lists page with `page` and `pageSize` (at most 200) and answer `{ items, page, pageSize, totalCount, sort }`. Filters are
  query parameters that match the interface's URLs, so a filtered screen and its API call use the same names
  (for example `GET /api/v1/tasks?projects=mine&overdue=true&sort=dueDate:asc`).
- Workspace endpoints take `projects` = `mine`, `all`, or a comma-separated list of project ids; restricted projects the
  caller cannot see are silently left out.
- Exports: `…/export?format=csv|xlsx` on lists and reports; files carry the filters used and the time of export.

## Changing data

- Create with `POST`, change with `PATCH` (only the fields sent), and move through a workflow with the item's
  `…/transition` operation; status is never set by `PATCH`.
- A `POST` may carry `Idempotency-Key: <up to 100 characters>`. Retrying it within a day returns the first successful
  response (marked `Idempotent-Replayed: true`) instead of creating a second item; the same key on a different path is
  refused with 422 `idempotency_key_reused`. Keys belong to the signed-in person.
- Every changeable item has a `rowVersion`. Send it as `If-Match: "<rowVersion>"` (or `rowVersion` in the body). Missing:
  **428**; someone changed it first: **409** `concurrency_conflict` with who and when, so the client can reload.
- Work items are never hard-deleted; `DELETE` marks them deleted and they can be restored where the interface allows.
- Some changes need a reason (for example a PM moving a due date); the API answers 400 with an error on `reason` until one
  is given.

## Errors

Problem details (`application/problem+json`):

```json
{ "type": "https://hub.local/errors/validation", "title": "Validation failed", "status": 400, "code": "validation",
  "detail": "…", "errors": { "dueDate": ["…"] }, "traceId": "…" }
```

| Status | Meaning |
|---|---|
| 400 | Invalid input; `errors` names the fields |
| 401 | Not signed in |
| 403 | Signed in but not permitted; `detail` says what is needed |
| 404 | Not found, or not visible to the caller (the two are indistinguishable on purpose) |
| 409 | Stale `rowVersion` or a duplicate |
| 422 | A business rule refused it (for example completing a task that needs review) |
| 428 | `If-Match` missing |
| 429 | Rate limit |
| 500 | Unexpected; nothing internal is returned — quote the `traceId` to support |

## Main groups

| Area | Paths |
|---|---|
| Me, preferences, notifications | `/me`, `/me/work`, `/me/preferences`, `/me/notifications`, `/me/dashboard-layout` |
| Projects and teams | `/projects`, `/projects/{id}`, `/projects/{id}/team`, `/projects/{id}/members`, `/projects/{id}/disciplines` |
| Work | `/projects/{id}/tasks`, `/tasks`, `/tasks/{id}`, `/projects/{id}/deliverables`, `/projects/{id}/milestones`, `/projects/{id}/decisions` |
| Coordination | `/projects/{id}/dashboard`, `/projects/{id}/coordination`, `/projects/{id}/timeline`, `/timeline`, `/home`, `/calendar` |
| Collaboration | `/items/{type}/{id}/comments`, `/items/{type}/{id}/links`, `/items/{type}/{id}/watchers`, `/files` |
| Workspace | `/workspaces`, `/views`, `/team` |
| Time | `/time` |
| Search and reports | `/search`, `/reports`, `/reports/{code}`, `/reports/{code}/export` |
| Portfolio and people | `/portfolio`, `/workload`, `/staff`, `/users/{id}/open-work`, `/users/{id}/reassign` |
| Templates | `/templates`, `/templates/{id}`, `/templates/{id}/structure`, `…/draft`, `…/publish`, `…/retire`, `…/preview`, `/projects/{id}/template-packs` |
| Registers | `/projects/{id}/decisions`, `/decisions/{id}/links/bulk`, `/projects/{id}/decision-log`, `/projects/{id}/decisions/client-export`, `/projects/{id}/risks`, `/risks/{id}`, `/projects/{id}/issues`, `/issues/{id}` (each with `…/transition`, `…/links`, `…/export`) |
| Meetings and actions | `/projects/{id}/meetings`, `/projects/{id}/meetings/current`, `/meetings/{id}/actions`, `/projects/{id}/actions`, `/actions/{id}`, `/actions/{id}/transition`, `/actions/{id}/convert` |
| Dependencies | `/tasks/{id}/dependencies` (with `lagDays`), `/dependencies/{id}`, `/deliverables/{id}/dependencies`, `/deliverable-dependencies/{id}` |
| Notifications extras | `/me/notifications/pulse` (a cheap stamp for live counts), `/me/notifications/unread-count`, `/me/weekly-summary` (preview) |
| Administration | `/admin/settings`, `/admin/users`, `/admin/activity`, `/admin/operations`, `/admin/holidays`, `/admin/<reference kind>` |
