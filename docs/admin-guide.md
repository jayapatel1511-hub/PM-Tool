# Administrator guide

For Hub administrators (system role **Admin**). Everything here is under **Admin** in the navigation unless stated. Every
change an administrator makes is recorded in the activity log with who, when and what changed.

## Who can do what

- Everyone who signs in with their organisation account is a Standard User. System roles add abilities: **Admin**,
  **Executive**, **Supervisor**, **ProjectManager** (may create projects) and **ReadOnly**. Assign them with the Entra app
  roles `Hub.Admin`, `Hub.Executive`, `Hub.Supervisor`, `Hub.ProjectManager`, `Hub.ReadOnly` (synchronised at each sign-in),
  or add them by hand in **Users** (shown as Manual).
- Inside a project, people are PM, Team Member, Reviewer or Viewer, and each discipline has one lead. The project's PM
  manages its team; administrators do not need to.
- There are no Hub passwords. Sign-in is Microsoft Entra ID only.

## Reference data

**Disciplines, Clients, Offices, Deliverable types, Phases, Project types.** Add, rename, reorder, or deactivate. A value in
use is deactivated, never deleted: existing projects keep it and new ones cannot pick it. Offices carry a time zone.

## Settings

Thresholds and defaults the rules use (§14): due-soon days for tasks, deliverables, decisions and milestones, stale and
review-stalled days, blocked-attention days, health percentages, override expiry, dependency chain depth, self review,
capacity default, restricted projects, viewer comments, project number format, organisation time zone and date format,
digest time and weekend digests, idle sign-out.

During the pilot, review the attention lists weekly with the pilot PMs and adjust a threshold when a rule is too noisy or
too quiet; each change is logged (see **Activity**, category Settings). Change one threshold at a time and note why.

## People

**Users** lists everyone with office, supervisor, roles, status and last sign-in. Filter to people without a supervisor
or include inactive people. Open a person to set supervisor, office, weekly capacity, template editing and Active.

### A leaver

1. When directory synchronisation is on, leavers become **Inactive** within a day. Otherwise open the person in **Users**
   and clear **Active**. Inactive people cannot sign in; their history stays.
2. Select **Reassign work** on the person's row. The page lists everything open that they own: tasks they are assigned to or
   review, deliverables they own or review, decisions they own, disciplines they lead.
3. Tick the items, choose who takes them, and confirm. Each PM concerned is notified; every change is logged. Reviewing
   one's own work is skipped when self review is off.

## Working days and holiday calendars

**Settings → Count thresholds in working days** switches due-soon, stale, review-stalled, blocked-attention and
approaching thresholds, dependency lags and workload spreading from calendar days to working days. Overdue does not
change: an item is overdue when its due date is before today.

**Holiday calendars** holds each office's statutory holidays, plus organisation-wide holidays that apply to every office.
Add them a year ahead. A project uses its office's calendar; a change takes effect at each project's next evaluation (its
next change, or overnight).

## Templates

People with **Template editing** (set in **Users**) and Admins maintain project templates under **Templates**: edit a
draft, preview what it creates, publish it as the next version, or retire it. Projects keep the version they were created
from. The Appendix A reference template is seeded in development and test only; build or import your organisation's own.

## Email summaries

Each person chooses their digest time and which digest sections they receive under **Preferences**; the organisation's
digest time and weekend setting are defaults under **Settings**. Project managers also get a **weekly summary** of their
Active projects on the morning of their earliest coordination day, which each PM can turn off. Neither is ever sent to
external parties.

## Restricted projects

Turn on **Restricted projects** in Settings to allow PMs to mark a project Restricted. A restricted project is visible only
to its members, its PM, Executives and Admins; it never appears to others in lists, search, dashboards, calendars, reports
or exports.

## Activity and exports

**Activity** is the organisation-wide log (filter by date, person, item type, category) and exports to CSV or Excel.
Exports anywhere in the Hub are themselves logged. The log is append-only: nobody, including administrators and the
database role the app uses, can change or delete an entry.

## Operations

**Operations** shows the checks operators are alerted on — evaluation delayed, nightly run missed, a job failed, digest
late — and each background job's last run. What to do for each is in `docs/runbooks/jobs.md`; restores are in
`docs/runbooks/restore.md`.

## Support during rollout

Roll out by office or group. For each wave: a 30-minute walkthrough (see `docs/user-guide.md`), a named local champion,
and a support channel triaged daily by the Hub team. Record issues with the screen, the item key and the time; never paste
comment text or client documents into tickets.
