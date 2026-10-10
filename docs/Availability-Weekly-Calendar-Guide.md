# Vacation, weekly planning and personal calendars

Tuesday plans leave already approved in HR. HR retains requests, approval, balances and cancellation authority.

## Approved leave

Open **Vacation & Coverage**. Day view shows remaining daily hours; Year overview provides monthly totals and month drill-down; Leave list shows source dates and record provenance. Coverage concerns link to the existing work-item panels. Unknown due dates remain unknown. Coverage reassignment uses the existing project permissions and review rules.

An administrator maps each employee's exact HR source and stable person key, then previews and applies a CSV. Use this UTF-8 header, in this order:

```csv
source_system,external_id,person_key,status,start_date,end_date,hours_per_day,source_updated_at
Synthetic HR,SYNTHETIC-001,employee-1,Approved,2026-10-12,2026-10-13,,2026-10-01T12:00:00Z
```

The example is synthetic. Confirm the mapping against the actual HR export before using operational data. Dates use `yyyy-MM-dd`; status is `Approved` or `Cancelled`; the source timestamp includes its timezone. Empty hours means a full working day; positive hours apply uniformly per working day. Uneven daily absence needs separate HR records. Identical duplicate source rows deduplicate; contradictory duplicates, overlapping partial-day records, excessive hours, unmapped identities and stale HR timestamps reject the batch. Do not include reasons, health information or balances. Maximum: 256 KB and 1,000 source rows.

Preview shows new, changed, cancelled and unchanged records, manual matches and affected weeks. Apply uses a private, expiring preview receipt and current versions. A failure leaves existing leave and staffing unchanged. Reimporting unchanged rows changes no leave records. Missing rows never cancel leave; include an explicit newer cancellation. Successful import time and latest HR source time are shown separately.

A supervisor may copy an approval for a direct report using its stable source/reference when an export is unavailable. Administrators may do this for any employee. These records are labelled **ManualHR** and reconcile in place when the matching HR export arrives. Imported leave is read-only; correct it in HR and import again. Legacy absence remains provisional until a manager explicitly links its dates to the matching HR approval or cancellation. That link preserves the original audit record.

Working schedules apply from their effective date. Weekly contracted hours are split equally over selected days. Office holidays reduce availability without redistributing hours to other days. Leave and holiday overlap counts once. Capacity and dated reservation spreading use the same effective schedule, including weekend schedules.

## Weekly planning

In **Team Planner**, use People or Projects view. **Copy last week** previews your manager-owned allocations, lets you select records, and creates private Drafts for the selected week. It preserves confidence and flags continuing/duplicate work, inactive projects, provisional capacity and overload. Employee self entries and leave are not copied.

**Publish weekly plan** publishes selected manager Drafts without changing confidence. Each affected person receives one summary and sees the published work in **My Week**. A changed preview rejects the whole selection; reload and select again. Current manager-entry editing follows packet 034: administrators or supervisors acting on their direct reports. PMs use the existing project reservation proposal/confirmation workflow. The Projects view displays the visible weekly plan alongside confirmed project reservations.

## Calendar subscription

Open **Calendar → Sync my calendar → Create private link**, then add that URL as an Internet calendar subscription in the chosen calendar client. The URL is shown only once; keep it private. Rotate to obtain a replacement; revoke to stop future access.

The feed contains your approved leave, personally assigned dated tasks/reviews/deliverables and permitted project milestones. Events use all-day dates and link to My Week. Weekly hours stay in My Week. Stable event identities and sequences support updates and sanitized cancellations; account state and permissions are checked on every fetch. Tuesday stores only a hash of the access token and omits it from application audit/receipt/request logs.

Refresh is controlled by the calendar client. Outlook is the intended first client; real-client update/cancellation acceptance remains unverified. Configure the designated `pm.engcalchub.com` host, HTTPS and proxy query redaction before operational subscription use. Revocation stops future fetches; it does not erase a calendar client's cached copy. Inbound free/busy, two-way sync, HR APIs and leave approvals remain deferred.
