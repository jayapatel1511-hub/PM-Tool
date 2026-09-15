# Engineering Project Coordination Hub

## Product Specification — Version 1.0 (Draft for Review)

| Item | Value |
|---|---|
| Document | Product and Implementation Specification |
| Product | Engineering Project Coordination Hub (working name; "the Hub" in this document) |
| Version | 1.0 — Draft for stakeholder review |
| Date | 2026-09-15 |
| Status | Ready for architecture review, wireframing, and ticket breakdown |
| Audience | Product owner, engineering team, UX design, IT security, pilot project managers |

---

### How to read this document

This specification uses a small set of consistent labels. They matter, because they tell the development team what is a requirement, what is a suggestion, and what still needs a human decision.

| Label | Meaning |
|---|---|
| **[MVP-Required]** | Must be in the first production release. The MVP is not complete without it. |
| **[MVP-Recommended]** | Strongly recommended for MVP because it is cheap relative to its coordination value. Can be cut if the schedule demands it, with the stated consequence. |
| **[Phase 2]** | Planned for the release after MVP. |
| **[Phase 3]** | Later capability. Designed for, not built. |
| **[Out of Scope]** | Will not be built. Listed to protect the product from scope creep. |
| **Recommendation:** | A recommendation by the authors, not a stated requirement. The business may choose otherwise. |
| **Assumption:** | An architectural or business assumption made to keep the specification concrete. Must be validated. |
| **TBD — Business Decision Required** | Information that only the company can provide. The specification is written so that development can proceed around it where possible. |

Canonical status names, role names, and threshold names are defined once in Section 10 (Core Data Model) and used verbatim everywhere else. If a later section appears to contradict Section 10, Section 10 wins and the later section should be corrected.

---

## 1. Executive Summary

The Engineering Project Coordination Hub is an internal web application for coordinating multidisciplinary engineering consulting projects. It exists to answer one question, continuously and without anyone compiling a status report:

> **Who is responsible for what, what is due next, what are we waiting for, and what is preventing the project from moving forward?**

The Hub is not a general-purpose task manager and it is not a scheduling engine. It is a purpose-built coordination layer for engineering projects that are organised by **discipline**, driven by **deliverables**, punctuated by **submission milestones**, and frequently held up by **cross-discipline dependencies** and **unmade decisions**.

The product is built entirely from structured data and deterministic rules. Every status, flag, and "attention required" item can be traced to a stated rule and a stated threshold. There is no AI in the base product and none is assumed.

**What the MVP delivers**

- Projects with disciplines, discipline leads, and project teams.
- Milestones, deliverables, and tasks, in a hierarchy designed for engineering work rather than generic to-do lists.
- Task status workflow with independent technical review built in.
- Task dependencies (Finish-to-Start) with automatic detection of waiting, blocked, and blocking work, and identification of affected milestones.
- A deterministic "PM Attention Required" engine with configurable thresholds.
- A Project Dashboard, a personal My Work page, and a Weekly Coordination view designed to run a multidisciplinary coordination meeting directly from the application.
- A thin Decision Register, because decisions are the most common non-task blocker in design projects. (Recommended for MVP; see Section 27 for the rationale and the fallback if it is cut.)
- Comments with @mentions, document links to SharePoint/Teams/network folders, a full activity log, in-app and email notifications, and corporate single sign-on.

**What comes later**

Risk and Issue registers, Meeting Actions, Project Templates, the Resource/Workload view, the Portfolio Dashboard, an improved Gantt, and saved views are Phase 2. Teams and SharePoint integration, ERP/Vantagepoint integration, and financial or utilisation data are Phase 3.

**Recommended technical approach**

A modular monolith: a React + TypeScript single-page application, an ASP.NET Core Web API (with Node.js/TypeScript as an acceptable alternative if the team's skills favour it), PostgreSQL, Microsoft Entra ID single sign-on, hosted on Azure App Service with Azure Database for PostgreSQL. The system is sized for hundreds of active projects, tens of thousands of tasks, and a few hundred users, and is intended to be maintained by a small internal team.

**What this document enables**

The specification is written so that a development team (or a software-development agent) can design the database (Section 24), produce wireframes (Section 13), establish the architecture (Section 23), create development tickets (Sections 11, 27, 35), implement the MVP, and test it against explicit acceptance criteria (Section 31).

---

## 2. Problem Statement

### 2.1 The coordination gap

A multidisciplinary engineering project — a municipal road reconstruction, for example — involves a project manager and five to eight disciplines, each with its own lead, its own deliverables, and its own internal review process. The disciplines depend on one another in ways that are well understood by experienced staff but rarely written down in a form the whole team can see:

```
Survey base plan
  → Civil preliminary design
    → Utility coordination
    → Geotechnical recommendations
      → Civil detailed design
        → Electrical coordination
          → QA/QC review
            → Client review
              → Final submission
```

Today this knowledge lives in the PM's head, in a spreadsheet, in a Planner board that nobody updates, and in a weekly meeting where the same questions are asked every time: *Is the survey done? Are we waiting on the client for the pavement decision? Who has the stormwater report? When is 60% due again?*

### 2.2 Why existing tools do not fit

Generic tools (Planner, Trello, Asana, Monday.com, Smartsheet) treat all work as flat tasks or cards. They have no concept of a discipline, a deliverable, a design submission, a technical review, or a decision that blocks a package. Schedule tools (Primavera P6, Microsoft Project) model dependencies well but are heavyweight, require scheduling expertise, and are not something a design team updates daily. The result is that the coordination question is answered by people compiling status, not by the system.

### 2.3 Concrete pains this product addresses

| Pain | How it shows up today | What the Hub does about it |
|---|---|---|
| Unclear ownership | "I thought Electrical was doing that." | Every task has exactly one accountable assignee and one owning discipline. Every deliverable has an owner. Unowned work is flagged. |
| Invisible dependencies | Civil detailed design starts late because geotech recommendations slipped and nobody connected the two. | Explicit task dependencies; successors are automatically shown as Waiting or Blocked; overdue predecessors are flagged as blocking others. |
| Decisions that stall work | Client has not confirmed the pavement structure; three tasks are quietly idle. | Decision Register with owner, required-by date, and links to the work it holds up; overdue decisions surface on the dashboard and coordination view. |
| Submissions that surprise people | 85% package is due Friday; two disciplines have open tasks. | Milestone status (On Track / At Risk / Overdue) computed from linked deliverables; approaching milestones with incomplete prerequisites are flagged. |
| Status compiled by hand | PM spends Monday morning building a status email. | Project Dashboard and Weekly Coordination view are always current; the meeting is run from the screen. |
| Review bottlenecks | Work sits "done" but unreviewed. | Review is part of the task workflow; stalled reviews are flagged; reviewers see a My Reviews queue. |
| No traceability | "Who moved that date?" | Immutable activity log on assignments, dates, statuses, deletions, and decisions. |

### 2.4 What success looks like

Success is measured by coordination outcomes, not feature counts:

- A PM can open the Weekly Coordination view and run the coordination meeting without any other preparation.
- A discipline lead can see, in under a minute, everything their discipline owes and everything their discipline is waiting on.
- A team member's My Work page is the single list they work from.
- A supervisor can see who is overloaded and who has capacity across projects without asking.
- Every "why is this blocked?" question can be answered by clicking the blocked indicator.

---

## 3. Product Vision

**For** project managers, discipline leads, supervisors, and engineering staff in a multidisciplinary consulting firm,
**who** need to coordinate deliverable-driven work across disciplines and keep design submissions on schedule,
**the Engineering Project Coordination Hub** is an internal coordination platform
**that** makes ownership, due dates, dependencies, blockers, and pending decisions immediately visible at project, personal, and portfolio level.
**Unlike** generic task boards and heavyweight scheduling tools,
**the Hub** is structured around disciplines, deliverables, milestones, reviews, and decisions, and it answers the coordination question through deterministic rules rather than manual status compilation.

### 3.1 What the Hub is

- A **system of record for coordination**: who owns what, when it is due, what it depends on, and what state it is in.
- A **meeting tool**: the Weekly Coordination view is designed to be projected and walked through.
- A **personal work queue**: My Work is designed to be the one list a person opens each morning.
- A **visibility layer for management**: the Portfolio Dashboard and Resource View (Phase 2) answer "which projects need help" and "who needs help".

### 3.2 What the Hub is not

- Not a scheduling engine. It does not compute critical paths, level resources, or auto-shift dates.
- Not a document management system. It links to documents; it does not store them.
- Not a timesheet, accounting, or ERP system.
- Not a replacement for Teams, email, or the engineering tools in which the actual work is produced.
- Not an AI product. Nothing in the base product depends on or includes AI.

---

## 4. Product Principles

These principles are used to make design decisions throughout the specification and should be used by the development team to resolve ambiguities. Every proposed feature must pass the first principle.

1. **Does this make coordination easier?** If a feature does not help someone answer "who, what, when, what are we waiting for, what is stuck", it does not belong. Administrative burden is a cost, not a neutral.

2. **One accountable owner.** Every task, deliverable, decision, risk, issue, and action has exactly one accountable owner. Others may collaborate, watch, or review, but accountability is never shared. Shared accountability is how work falls through gaps.

3. **Deliverables drive the work.** Tasks exist to produce deliverables; deliverables exist to meet milestones. The hierarchy in the product reflects this, and progress rolls up along it.

4. **Disciplines own their work.** Discipline leads are accountable for their discipline's deliverables and tasks within a project. The product gives them ownership views and permissions to match.

5. **Dependencies are first-class and visible.** Cross-discipline hand-offs are the main source of delay. The system makes them explicit, computes their consequences, and never hides a blocker.

6. **Deterministic and explainable.** Every computed status, flag, or health colour can be explained by a stated rule and a stated threshold. If a user asks "why is this red?", the interface shows the rule that fired.

7. **Review is part of the workflow, not an afterthought.** Engineering work requires independent technical review. The task and deliverable lifecycles model it explicitly.

8. **Decisions are work.** A pending decision is tracked with the same seriousness as a task, because it has the same power to stop a project.

9. **Minimum viable ceremony.** Fields are optional unless a rule genuinely needs them. Warnings and attention flags are preferred over hard validation, except where data integrity requires it.

10. **Information-dense, calm interface.** Desktop-first, table-oriented, low-animation, colour used with redundant text or icons. Professional engineering software, not a consumer app.

11. **Traceable.** Changes to ownership, dates, statuses, and decisions are logged and visible.

12. **Simple enough for a small team to maintain.** Modular monolith, one database, mainstream frameworks, no speculative infrastructure.

---

## 5. Goals

### 5.1 Product goals (MVP)

| ID | Goal | How we will know |
|---|---|---|
| G1 | PMs run weekly coordination meetings from the Hub. | Pilot PMs report the Weekly Coordination view replaces their status spreadsheet/email for the meeting. |
| G2 | Every active project has a current, correct answer to "what is due next and who owns it". | Attention items "task has no owner" and "task has no due date" trend toward zero on pilot projects after 4 weeks. |
| G3 | Blocked work is visible within one day of becoming blocked. | Blocked indicators are computed automatically; PM attention list shows blocked items without manual entry. |
| G4 | Team members use My Work as their daily list. | Pilot users update task status at least weekly in the Hub without being chased. |
| G5 | Milestone risk is visible at least two weeks before a submission. | Milestone At Risk rule fires on configurable lead time; no "surprise" submissions on pilot projects. |
| G6 | The system is adopted with minimal training. | New users complete core actions (update task, add comment, mark ready for review) after a 30-minute walkthrough. |

### 5.2 Technical goals

| ID | Goal |
|---|---|
| T1 | Deterministic rules are implemented as pure, unit-tested functions with near-complete test coverage. |
| T2 | Corporate SSO from day one; no separate passwords. |
| T3 | Maintainable by a team of 2–4 developers after launch. |
| T4 | Deployable to Azure with infrastructure as code and separate dev/test/prod environments. |
| T5 | All ownership, date, status, and deletion changes are captured in an immutable activity log. |

---

## 6. Non-Goals

The following are deliberately not goals of this product, at any phase. See Section 30 for the full out-of-scope list.

- Replacing Primavera P6, Microsoft Project, or any CPM scheduling tool.
- Replacing SharePoint, Teams, OneDrive, or a document management system.
- Tracking time, costs, budgets, invoices, or payroll.
- Performing or automating engineering calculations or engineering decisions.
- Providing AI assistants, summaries, recommendations, or predictions.
- Serving external clients as system users (external parties are referenced, not logged in, until at least Phase 3).
- Acting as a general-purpose work tool for non-project work (HR, IT tickets, marketing).
- Being a mobile-first product. Mobile is for reading and quick updates only.

---

## 7. User Personas

Personas are composites used to make design decisions. Names are illustrative. Most real employees combine several personas (see Section 8.4).

### 7.1 Priya — Project Manager

- **Role:** Manages 4–8 concurrent municipal infrastructure projects, each with 4–7 disciplines.
- **Goals:** Know what is due this week and next, know what is blocked and why, run a tight coordination meeting, keep clients informed, avoid submission surprises.
- **Frustrations:** Compiling status by hand every Monday. Finding out about a slipped predecessor only when the successor is late. Chasing client decisions by email with no record.
- **Uses:** Project Dashboard daily; Weekly Coordination weekly; Decision Register; Milestone view; Task list filtered by discipline.
- **Devices:** Desktop with two monitors; laptop in meeting rooms; phone for quick checks.

### 7.2 Marc — Discipline Lead (Civil)

- **Role:** Senior civil engineer leading the civil discipline on 6–10 projects, also a PM on 1–2 small ones.
- **Goals:** See everything Civil owes across all projects; know what Civil is waiting on from Survey and Geotech; assign and review Civil work; protect the team's review time.
- **Frustrations:** Being assigned tasks in tools they do not control. Learning about scope changes second-hand. Reviewing work that was "done" without being told.
- **Uses:** My Work (My Reviews); Task list filtered to Civil; Deliverables Register; Kanban for the discipline.

### 7.3 Alex — Project Team Member (Designer / EIT)

- **Role:** Works on 3–5 projects at a time across two disciplines' teams.
- **Goals:** A single list of what to do, in due-date order, with context. Clear hand-off when work is ready for review. Know when a dependency is cleared so work can start.
- **Frustrations:** Being asked for status in meetings. Not knowing a predecessor finished. Ambiguous ownership ("the Civil team").
- **Uses:** My Work almost exclusively; task detail panel; comments and mentions.
- **Devices:** Desktop; occasionally tablet on site.

### 7.4 Diane — Reviewer (Senior Technical)

- **Role:** Senior engineer who performs technical review for several disciplines' deliverables but is not on every project team.
- **Goals:** A clear queue of what needs review and by when; ability to send work back with specific comments; not being the bottleneck by surprise.
- **Uses:** My Work (My Reviews); task detail; deliverable detail.

### 7.5 Sam — Supervisor / Resource Manager (Civil Group)

- **Role:** Manages 12 civil staff across many projects. Not a member of most project teams.
- **Goals:** See each person's load across projects; spot overloaded staff and overlapping deadlines; know who has capacity when a PM asks for help; reassign work when someone is away or leaves.
- **Frustrations:** No cross-project view without asking each PM. Estimates are unreliable but something is better than nothing.
- **Uses:** Resource View (Phase 2); Portfolio Dashboard filtered by discipline; My Work of supervised staff (read).

### 7.6 Lena — Executive / Regional Manager

- **Role:** Responsible for a region or business unit with 40–100 active projects.
- **Goals:** Which projects are at risk this week and why; which PMs need support; upcoming submissions across the portfolio.
- **Frustrations:** Health reported by PMs is optimistic and inconsistent. No single place to see it.
- **Uses:** Portfolio Dashboard (Phase 2); Project Dashboard (read); reports export.

### 7.7 Jordan — System Administrator

- **Role:** IT / applications administrator.
- **Goals:** Manage user access via Entra ID groups; maintain disciplines, deliverable types, offices, clients, templates, and thresholds; deactivate leavers; audit changes.
- **Uses:** Administration screens; activity history; Entra ID.

### 7.8 The Client Contact (non-user)

- **Role:** Client project manager or approving authority. Does **not** log in.
- **Why it matters:** Client decisions and client reviews are frequently the blocker. The Hub represents clients and other external parties as **External Parties** that can own decisions and meeting actions, so "waiting on client" is visible, dated, and tracked, even though no notification is sent outside the organisation in MVP.
