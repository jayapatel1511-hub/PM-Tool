# Runbook: background jobs and operator alerts

The Hub's background work runs inside the API process (§23.5). Every run is recorded in `hub.job_run` and guarded by a
PostgreSQL advisory lock, so scaled-out instances never run a job twice. Jobs run independently: a long nightly run does
not hold up email, digests or the watchdog. Admins see the same information under **Admin → Operations**.

## Jobs

| Job | Schedule | What it does | If it stops |
|---|---|---|---|
| Evaluation worker (hosted service) | continuously, a moment after each save | Re-evaluates changed projects from the outbox (`hub.outbox_event`): task, deliverable, milestone and decision states, health, attention | Indicators go stale; `EvaluationDelayed` alert after 10 minutes; `/health` reports Degraded |
| `evaluation-retry` | every 15 minutes | Retries failed outbox events; evaluates Active projects that have no state yet | Failures stay unretried |
| `nightly-evaluation` | daily after 00:15 organisation time | Date rollover (overdue, due soon), health snapshots with the day's counts, override and snooze expiry; prunes job records older than 90 days | Overdue flags lag a day; `NightlyMissed` alert after 26 hours |
| `directory-sync` | daily after 01:00 | Leavers become Inactive; job title, office and manager follow Entra ID (when `Graph:DirectorySync` is on) | Leavers keep access until the next run |
| `daily-digest` | every 5 minutes | Builds each person's digest once their digest time has passed (default 07:00) | `DigestLate` alert after 08:00 |
| `weekly-summary` | every 15 minutes | Sends each PM one weekly summary of their Active projects on their earliest coordination day (Monday when none is set), at their digest time; skips PMs who turned it off or manage no Active project | PMs miss that week's summary; the next week's is unaffected |
| `email-send` | every 30 seconds | Sends queued email with retries (up to 8 attempts, doubling delay) | Mail queues; digest mail triggers `DigestLate` |
| `ops-watchdog` | every 5 minutes | Checks the conditions below; logs `OpsAlert <kind>` errors and an `OpsHeartbeat` | Heartbeat alert after 30 minutes |

Evaluation targets (§22): a change is reflected in its project's derived state within 2 seconds; the nightly run for 500
active projects and 100,000 tasks finishes within 30 minutes (measured: see `specs/011-hardening-and-pilot/verification.md`).

## Alerts

Alert rules live in `infra/main.bicep` and are deployed when `operatorEmail` is set. Each fires to the operators' action
group.

| Alert | Signal | First response |
|---|---|---|
| Hub operations alert — `EvaluationDelayed` | oldest unprocessed outbox event older than 10 minutes | Check the app is running (`/health`); look for repeated evaluation errors in the logs (`traces` where message contains "Evaluation"); restart the app if the worker is stuck. Events are kept, so nothing is lost; evaluation catches up after the restart |
| `NightlyMissed` | no successful `nightly-evaluation` for 26 hours | Admin → Operations shows the last run; check the job's error in the logs. After fixing, the run starts on its own within a minute (it is due while no success exists for today) |
| `JobFailed` | any job run recorded as Failed since the last check | Read the exception in the logs (`exceptions` table, "Job {Job} failed"). Transient database or mail errors recover on the next run; repeated failures need a fix |
| `DigestLate` | after 08:00 on a digest day, digests due by 08:00 not built, or built but not sent | Check `email-send` failures and the mail route (Graph or SMTP relay credentials in Key Vault). Unsent mail retries on its own once the route works |
| Hub background jobs stopped | no `OpsHeartbeat` for 30 minutes | The job host is not running: check `Jobs:Enabled` is not `false`, and restart the app |
| Failed-request rate above 5 % | more than 5 % of at least 20 requests in 15 minutes answered 5xx | Check `/health`, recent deployments, and the database; roll back the last deployment if it coincides |

## Running a job by hand

There is no manual trigger in the interface. To force a rerun, delete the job's success record for today (only for
`nightly-evaluation`, `directory-sync`, `daily-digest`) with the database administrator role, and the job host runs it within
20 seconds:

```sql
DELETE FROM hub.job_run WHERE job_name = 'nightly-evaluation' AND started_at > now() - interval '1 day' AND status = 'Succeeded';
```

`hub.job_run` is operational data, not history; the audit trail is the append-only activity log, which no role can change.

## Logs

Logs go to Application Insights (workspace-based) when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set: requests (query
strings redacted), dependencies and application logs. They never contain tokens, secrets or comment text, and people are
identified by internal ID. Retention is 90 days in production and 30 elsewhere (to be confirmed by the organisation).
