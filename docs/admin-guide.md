# Administrator guide

For Tuesday administrators (system role **Admin**, shown as **System Administrator**). Everything here is under **Admin**
in the navigation unless stated. Every change an administrator makes is recorded in the activity log with who, when and
what changed. Users' tasks are in the [user guide](user-guide.md).

## Who can do what

- Everyone who signs in is a Standard User. System roles add abilities: **System Administrator**, **Executive**,
  **Supervisor**, **Project Manager** (may create projects) and **Read Only**. In the review release and the pilot, add
  them by hand in **Users & roles** (shown as "added in the Hub"). After the move to Azure, the Entra app roles
  `Hub.Admin`, `Hub.Executive`, `Hub.Supervisor`, `Hub.ProjectManager` and `Hub.ReadOnly` are synchronised at each
  sign-in (shown as "from directory group").
- Inside a project, people are PM, Team Member, Reviewer or Viewer, and each discipline has one lead. The project's PM
  manages its team; administrators do not need to.
- In coordination work an administrator counts as each project's PM, for example to reassign owners, cancel records,
  record readiness applicability or capture a weekly snapshot. An administrator still cannot:
  - take a named person's step (accept a handoff, verify a finding, sign a promise, adopt a revision);
  - confirm a design basis version, unless they lead its discipline or are its named approver;
  - record a submission issue, approve Not Applicable on it, reassign its coordinator or cancel it, unless they hold
    the project's PM role.
- An administrator can confirm anyone's allocation and set anyone's availability and weekly capacity.

## Review and pilot sign-in accounts

Until the move to Azure, Tuesday runs on the temporary homedev host with individual password accounts. An account is a
login ID and password mapped to one existing, active person in Tuesday. Operators issue accounts on the host; the
paths, private files and restart steps are in `docs/runbooks/homedev-review.md`.

### Issue an account

1. Check that the person exists and is **Active** in **Users & roles**, with the right supervisor, office and roles.
   - An account cannot create a person or grant project roles; add the person to project teams separately.
   - Tuesday has no screen for creating people. In the review release the people are the synthetic seed users
     (`@hub.test` addresses), which are reserved records.
   - The person's ID, which the script needs, is the identifier in the address of their **Reassign work** page.
2. An operator runs `scripts/add-review-credential.py <private credential file> <person ID> <login ID>` on the host and
   types the new password twice at the hidden prompt. It is never displayed.
   - Login IDs: 1–64 letters, numbers, dots, underscores, `@` or hyphens; not case-sensitive; one per person.
   - Passwords: at least 12 characters, no line breaks. At most 64 accounts.
3. Restart the review API so it loads the new account (runbook).
4. Give the password to the person privately, once, separately from their login ID.

Never put a password, the credential file or its contents in the repository, a ticket, chat, email or a document. The
credential file must stay private to its owner; if it is not, or it contains a duplicate, the review API refuses to start.

### Withdraw access

- Clear **Active** on the person in **Users & roles**. Their next request is refused, even in a session already open.
- Removing their login from the credential file only stops new sign-ins, after the next API restart. A session already
  open lasts up to 8 hours from sign-in, and signing out ends it only in that browser.
- The script refuses a person or login ID that is already mapped. To replace a forgotten or exposed password, an
  operator removes that person's entry from the private credential file, adds a new one with the script and restarts
  the API.

Sign-in allows five attempts per minute from one address. The person sees "User ID or password is incorrect." either
way.

## Reference data

**Disciplines, Clients, Offices, Deliverable types, Phases, Project types.** Add, rename, reorder, or deactivate. A value in
use is deactivated, never deleted: existing projects keep it and new ones cannot pick it. Offices carry a time zone.

## Settings

Thresholds and defaults the rules use (§14): due-soon days for tasks, deliverables, decisions and milestones, stale and
review-stalled days, blocked-attention days, health percentages, override expiry, dependency chain depth, self review,
capacity default, coordination lookahead, restricted projects, viewer comments, project number format, organisation time
zone and date format, digest time and weekend digests, idle sign-out, and the default channels for each notification.

During the pilot, review the attention lists weekly with the pilot PMs and adjust a threshold when a rule is too noisy or
too quiet; each change is logged (see **Activity**, category Settings). Change one threshold at a time and note why.

### Settings that affect coordination

| Setting as shown | Group | Default | Effect |
|---|---|---|---|
| `setting.coordination_lookahead_weeks` (its label is missing in this release) | Work rules | 3 | Weeks shown by default on Project → **Readiness**, counted from the current coordination week, and the window of "What can we start?" in the coordination views. 1 to 12 |
| **Allow assignees to review their own tasks** | Work rules | Off | Besides task review, lets the same person pass the independence checks for handoff receipt, review packages and findings, change assessments and design basis confirmation and impact decisions. Issue verifiers, constraint removal owners and assumption exception verifiers stay independent regardless. Keep it off for the review and the pilot |
| **Default weekly capacity (hours)** | Work rules | 40 | Capacity for people without their own weekly capacity, used by Workload, allocation confirmation and the readiness capacity checks |
| **Count thresholds in working days** | Dates and calendar | Off | When on, allocations and capacity also skip the holidays in **Holiday calendars**; when off, only weekends are skipped. A person's daily availability always replaces the day's capacity |
| A row per notification event | Notifications | Varies | The organisation default for **In-app** and **Email**. Each person can override it in **Notification preferences** |

A project's **Coordination day** (Project → **Settings**, PM only) sets where its weekly promise weeks start; Monday if
not set.

### Notification events for coordination

All default to in-app only. "Muted" means the person still receives the event when they have muted the project. These
events reach only people who can currently see the project, checked when the notice is created and again before its
email is sent. No notification is sent on Archived or Cancelled projects.

| Label in Settings and Notification preferences | Event code | Delivered when muted |
|---|---|---|
| Handoff updates | `HandoffChanged` | Yes |
| Multidisciplinary review updates | `ReviewPackageChanged` | Yes |
| Revision change assessments | `ChangeImpact` | Yes |
| Allocation updates | `AllocationChanged` | No |
| Submission updates | `SubmissionChanged` | No |
| Issue verification assignments | `IssueVerifierAssigned` | Yes |
| Issue verification outcomes | `IssueVerificationOutcome` | No |
| Constraint removal and verification requests | `ConstraintAction` | Yes |
| Constraint outcomes | `ConstraintOutcome` | No |
| Weekly commitments proposed for you | `CommitmentProposed` | Yes |
| Weekly commitment updates | `CommitmentChanged` | No |
| Design basis impact assessments | `BasisImpactPending` | Yes |
| Design basis conflicts | `BasisConflictRaised` | No |

Who receives each event is listed in the [user guide](user-guide.md#notifications-from-coordination-work). In the
review release no email is sent: messages are written to the server log by recipient and subject only, so turning email
on there changes nothing for users.

## Bootstrap and create local accounts

For a fresh Staging pilot database, the host operator sets `Auth__Local__BootstrapAdmins` in the private runtime env
file to `email|Display Name` (semicolon separated for multiple Admins). Startup creates and audits those users and
adds Admin where missing; it does not rename or reactivate existing people. Remove the setting after bootstrap.
Use the created user's GUID with `scripts/add-review-credential.py` against the pilot's private verifier file; passwords
and their temporary handoff stay outside Git. Do not copy synthetic review users or credentials into the pilot.

Once signed in, **Users & roles → Create user** creates the person's active record. Credential setup remains a host
operation: enter the returned GUID and individual user ID into the credential helper, then deliver the password privately.
Rotating or removing the verifier ends that user's existing sessions on their next request. Deactivating the user also
blocks access. Microsoft Entra provisioning replaces this local process when the Azure environment is available.

## People

**Users & roles** lists everyone with office, supervisor, roles, status and last sign-in. Filter to people without a
supervisor or include inactive people. Select **Edit** on a person to set **Supervisor**, **Office**, **Weekly capacity
(h)**, **Template editor**, **Active** and **System roles**.

A person's supervisor confirms their allocations and sets their availability, and needs the **Supervisor** role to do so.
Check that every person who will receive allocations has a supervisor.

### A leaver

1. When directory synchronisation is on, leavers become **Inactive** within a day. It is off on the homedev host used
   for the review and the pilot, so open the person in **Users & roles** and clear **Active**. Inactive people cannot
   sign in; their history stays.
2. Select **Reassign work** on the person's row. The page lists everything open that they own: tasks they are assigned to or
   review, deliverables they own or review, decisions they own, disciplines they lead.
3. Tick the items, choose who takes them, and confirm. Each PM concerned is notified; every change is logged. Reviewing
   one's own work is skipped when self review is off.
4. Coordination records are not on that page. Ask each PM or lead to reassign the person's handoffs, review assignments
   and findings, change assessments, submission roles, constraints and design basis entries in those registers, where
   the person shows as unavailable.

## Working days and holiday calendars

**Settings → Count thresholds in working days** switches due-soon, stale, review-stalled, blocked-attention and
approaching thresholds, dependency lags and workload spreading from calendar days to working days. Overdue does not
change: an item is overdue when its due date is before today.

**Holiday calendars** holds each office's statutory holidays, plus organisation-wide holidays that apply to every office.
Add them a year ahead. A project uses its office's calendar; a change takes effect at each project's next evaluation (its
next change, or overnight).

## Templates

People with **Template editor** (set in **Users & roles**) and Admins maintain project templates under **Templates**:
edit a draft, preview what it creates, publish it as the next version, or retire it. Projects keep the version they were
created from. The Appendix A reference template is seeded in development and test only; build or import your
organisation's own.

## Email summaries

Each person chooses their digest time and which digest sections they receive under **Notification preferences**; the
organisation's digest time and weekend setting are defaults under **Settings**. Project managers also get a **weekly
summary** of their Active projects on the morning of their earliest coordination day, which each PM can turn off. Neither
is ever sent to external parties, and neither is sent in the review release.

## Restricted projects

Turn on **Allow Restricted projects** in Settings to allow PMs to mark a project Restricted. A restricted project is
visible only to its members, its PM, Executives and Admins; it never appears to others in lists, search, dashboards,
calendars, reports, exports or notifications.

## Activity and exports

**Activity** is the organisation-wide log (filter by date, person, item type, category) and exports to CSV or Excel.
Excel and CSV exports from lists and reports are themselves logged; the submission **Export evidence** file and the
**My Work → Coordination** CSV are not. The log is append-only: nobody, including administrators and the database role
the app uses, can change or delete an entry.

## Operations

**Operations** shows the checks operators are alerted on — evaluation delayed, nightly run missed, a job failed, digest
late — and each background job's last run. What to do for each is in `docs/runbooks/jobs.md`; restores are in
`docs/runbooks/restore.md`.

## Data handling in the review release

- The review release holds synthetic people and projects only. Nobody may enter real client, project, personnel or
  commercial information into it.
- Never copy review data into a pilot or production environment: not the database, its dumps or backups, exports
  (Excel, CSV, the coordination CSV, submission evidence files), screenshots or notes. Pilot and production start from
  their own database.
- Never restore a production or pilot backup into the review database.
- Review accounts belong to named individuals. Do not share them, and do not reuse a review password anywhere else.
- Keep the review database, its dumps and the credential file in the private locations the runbook names.

## Support during rollout

Roll out by office or group. For each wave: a 30-minute walkthrough (see `docs/user-guide.md`), a named local champion,
and a support channel triaged daily by the Tuesday team. Record issues with the screen, the item key and the time; never
paste comment text, client documents or passwords into tickets.
