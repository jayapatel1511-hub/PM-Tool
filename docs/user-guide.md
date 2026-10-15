# User guide

For project managers, discipline leads and team members, and for the testers of the review release. The application is
called **Tuesday** in its header and sign-in page; some messages still say "the Hub". Both mean this application.
Administrators: see the [administrator guide](admin-guide.md).

## Sign in

The review release and the pilot use an individual account: a user ID and a password issued to you personally.
Microsoft sign-in comes later, with the move to Azure.

1. Open the address you were given and select **Sign in**.
2. Enter your **User ID** and **Password**, then select **Sign in**.
3. When you finish, open the menu with your name (top right) and select **Sign out**.

- User IDs are not case-sensitive. Passwords have at least 12 characters.
- "User ID or password is incorrect." also appears after five attempts within one minute. Wait a minute and try again.
- You are signed out 8 hours after signing in, and after the idle time your administrator sets (8 hours by default).
- Never share your password or put it in an email, chat, ticket or document. If someone else may know it, ask your
  administrator for a new one.
- The review release contains synthetic people and projects only. Never enter real client, project or personal information.
- Email is not sent in the review release. Use the bell and **Notifications**.

## The basics on one page

**My Work** is your start page: what is due, waiting on you or held up by you.

| To… | Do this |
|---|---|
| See what to do today | **My Work → Today** (or Upcoming, Overdue). *My Tasks* are yours or ones you collaborate on; *Assigned to Me* only yours; *Created by Me* what you created for others |
| Update a task | Open it (click its key or name). Change status from the status pill, progress from the percentage, dates in the panel. Tuesday asks for a reason only when one is needed |
| Mark work ready for review | Status → **Ready for Review** (choose the reviewer if none is set). The reviewer is told; you cannot tick a reviewed task off yourself |
| Comment or mention someone | Open the item → **Comments**. Type `@` and a name to notify them |
| Say why something is stuck | Task → **Manual block → Set block** with a reason, or add a dependency on the task it waits for; it shows as Blocked everywhere until cleared |
| Find anything | The search box (press `/`). Type a key such as `2026-0417-T0012` to jump straight to it. Handoffs, review packages and change notices are found the same way |
| Look across projects | The project selector at the top: *My projects*, *All permitted projects*, or a named workspace you save. **Home**, **Boards**, **Tasks**, **Timeline**, **Calendar**, **Files** and **Team** follow it |
| Run the weekly meeting | Project → **Weekly Coordination**: what changed, what is late, blocked or due; record decisions as you go. See the [coordination view](guides/coordination-view.md) |
| Record hours | **Time → Add time**: task, date, hours. Your hours only change your own totals, never the task's estimate |
| Stay informed without noise | The bell shows what needs you; **Notification preferences** (menu with your name) sets in-app and email per event and your daily digest time |
| Share a view | **Copy link** on a workspace view keeps its projects and filters; each person sees only what they may |
| Start a project from a template | **Projects → Create project → Start from a template**: pick the template, set milestone dates (or let offsets fill them), untick disciplines you do not need, and check the summary before creating |
| Track what might go wrong | Project → **Risks → Raise risk**: choose probability and impact (each level says what it means); severity is shown as a colour and a number. Past its review date a risk shows "Review overdue" |
| Track what has gone wrong | Project → **Issues → Raise issue** with a severity and target date. A High open issue is a Critical attention item and turns health Red; resolving needs a resolution. See [location-linked issues](guides/issues.md) |
| Raise one from the work itself | On a task or deliverable panel, **Raise → Raise risk / Raise issue / Meeting action**: it is linked to that item |
| Record a meeting's actions | Project → **Meetings & Actions → Record meeting**, then **Add action** with one owner: a person, a discipline (its lead sees it in My Work) or an external party (nothing is sent to them). In Weekly Coordination's meeting mode, the action button on a row adds it to today's meeting |
| Turn an action into work | Open the action → **Convert to task**; the action then completes when the task does |
| Brief the client | Project → **Decisions → Export for the client**: the open decisions they own, overdue first, without internal notes. **Decision log** shows what was decided, deferred or cancelled and why |
| Link a decision to a whole package | Decision panel → **Link items**: tick several tasks, deliverables or milestones and link them in one go |
| Say one deliverable waits for another | Deliverable panel → **Dependencies → Linked deliverables → Add**, with a lag in days for a client review if needed |
| Choose what your digest contains | **Notification preferences → Digest contents**: untick sections you do not need. PMs can switch the **Weekly summary of my projects** on or off and preview it |
| Find a discussion | Search also looks in task and deliverable descriptions and in comments, and marks the matching words |

A task belongs to exactly one discipline, so one person is accountable for it. When two disciplines share work, create it
under the leading discipline and add collaborators from the other, or split it into two tasks linked by a dependency.
Tasks without a deliverable (such as "Book the kickoff room") are fine; they appear under **Other tasks**.

Colours always come with words and symbols. Health (Green, Yellow, Red) summarises indicators; it does not predict, and a
PM's reported health always appears next to the computed one.

## Coordination between disciplines

| Feature | Where | Guide |
|---|---|---|
| Discipline handoffs | Project → **Handoffs** | [Handoffs](guides/handoffs.md) |
| Multidisciplinary reviews | Project → **Reviews** | [Reviews](guides/reviews.md) |
| Revisions and change impact | Project → **Changes** | [Change impact](guides/change-impact.md) |
| Submission readiness | Project → **Submissions** | [Submissions](guides/submissions.md) |
| Dated allocations and availability | Project → **Allocations**; **Workload** | [Allocations](guides/allocations.md) |
| Weekly planning, confidence, visibility and time away | **Weekly Planner**; **My Work → My Week** | [Weekly planning](guides/planning.md) |
| Discipline coordination view, saved views, meeting actions | Project → **Weekly Coordination**; **My Work → Coordination** | [Coordination view](guides/coordination-view.md) |
| Design basis and assumptions | Project → **Design basis** | [Design basis](guides/design-basis.md) |
| Readiness, constraints and weekly promises | Project → **Readiness** | [Readiness](guides/readiness.md) |
| Location-linked issues | Project → **Issues** | [Issues](guides/issues.md) |

### Roles in these guides

- **PM**: the project's Project Manager or anyone with the PM role on the project. Administrators can do what a PM can,
  except where a guide says otherwise.
- **Lead**: the discipline lead of the discipline concerned (Project → **Team & Disciplines**).
- **Owner**: the person named on the record. For a task that is its **Assignee**; for a deliverable, its **Owner**.
- **Supervisor**: a person's supervisor in Tuesday, holding the Supervisor role.

### Rules for every coordination record

- Only the named person can take a named step: accept, verify, sign, adopt or confirm. A PM or lead can reassign the
  step; they cannot take it on someone else's behalf.
- Each step records your name and the time. Many ask for a reason (at least 5 characters) or an evidence link.
- You cannot review, accept, verify or confirm your own work. Your administrator can relax this for tasks, handoffs,
  reviews, change assessments and design basis with **Allow assignees to review their own tasks** (off by default).
  Issue verification and constraint removal always need someone else.
- People lists show active project participants: the PM, team members, reviewers and discipline leads. Viewers and Read
  Only accounts cannot hold coordination roles.
- "This record changed. Close this form, refresh the record and review the latest information before trying again."
  means someone saved a change while your form was open. Close it, reopen the record and decide again.
- Pressing a button twice, or retrying after a network error, does not create a duplicate.
- Read Only accounts cannot change anything. Archived and Cancelled projects are read-only. On a Complete project only the
  PM can correct records, and each correction needs a reason.
- Tuesday records links and declared revisions. It never opens, checks, stores or sends the files themselves.
- Restricted projects stay invisible to non-members in lists, search, exports and notifications.

### Notifications from coordination work

All are in-app by default; email is off unless you or your administrator turn it on. You are never notified of your own
action. Change a channel in **Notification preferences**. Events marked "still delivered when a project is muted" arrive
even if you muted the project.

| Event in Notification preferences | You receive it when |
|---|---|
| Handoff updates (still delivered when muted) | You send or receive a handoff and it changes state or its owners are reassigned |
| Multidisciplinary review updates (still delivered when muted) | A review you are a reviewer on starts or is cancelled; you are made a reviewer; a finding names you or one you resolve, verify or coordinate changes; a decision is recorded on a package you coordinate; a published revision opens a new round on a package you review or coordinate |
| Revision change assessments (still delivered when muted) | A published change creates an assessment on your work, or a source impact check on an issue you own or verify; an assessment you own, verify or whose notice you own changes; a notice with an assessment on your work is closed, reassigned or cancelled |
| Allocation updates | An allocation is proposed, edited, confirmed, declined, cancelled or completed, and you are the project's named PM, the requester or the person's supervisor |
| Submission updates | A submission you coordinate, or any submission on a project where you are the named PM, is created, edited, started, reassigned, issued or cancelled, or an optional check changes; owners of optional checks hear when checking starts and when their check is reassigned |
| Issue verification assignments (still delivered when muted) | You are proposed as an issue's independent verifier |
| Issue verification outcomes | Verified or Rejected is recorded on an issue you own or created, or on any issue where you are the project's named PM |
| Constraint removal and verification requests (still delivered when muted) | You are named to remove a constraint, or a proposed removal on your work needs your verification |
| Constraint outcomes | A constraint you remove, or one on your work, is Verified Removed or Cancelled |
| Weekly commitments proposed for you (still delivered when muted) | Someone else proposes a weekly promise for your work |
| Weekly commitment updates | A promise you perform or proposed is signed, Met, Not Met or Withdrawn |
| Design basis impact assessments (still delivered when muted) | A design basis version your work uses is replaced or withdrawn, or the decision it came from is reopened |
| Design basis conflicts | A basis entry you own now conflicts with another confirmed value |

Selecting a notification opens the record. Design basis notices open the **Design basis** tab, and constraint and weekly
promise notices open the project's **Readiness** tab; find the record there. The notification centre's type filter does
not list these events yet. **Digest contents** include **Discipline handoffs**, **Multidisciplinary reviews** and
**Revision change assessments**.

## Coming in the review release

These are being built now and are not in the current preview.

- **Task start authorisation.** Starting a task, alone or in bulk, whose readiness is Not Ready or Needs Assessment
  (including work never assessed) will need a warning acknowledgement, a reason and authorisation by the PM or the
  discipline lead. Ready and Proceed under Assumption work will start as it does now. Review, access and lifecycle rules
  will not be bypassed. Until then you can start any task you are allowed to move; a blocked task shows a warning only.
- **Coordination issue type.** Each issue will be raised as Coordination or General. Only Coordination issues will need
  at least one location or drawing/model reference, and independent verification, before they can be resolved. Issues
  that already have a location or reference will become Coordination issues; all others General. Until then, the
  verification rule applies to every issue that has a location or reference.
