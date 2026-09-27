<!--
Sync Impact Report
- Version change: none -> 1.0.0 (initial ratification)
- Principles added: I. Coordination First; II. Deterministic and Explainable (NON-NEGOTIABLE);
  III. One Accountable Owner; IV. The Product Specification Is the Source of Truth;
  V. Traceable by Construction; VI. Secure by Default; VII. Simple Enough for a Small Team
- Sections added: Product and Technical Constraints; Development Workflow; Governance
- Sections removed: none
- Templates: .specify/templates/*.md are unmodified upstream Spec Kit 1.0.8 templates and read
  this file at runtime (the plan template's Constitution Check). No template changes needed.
- Deferred TODOs: none. Open product decisions (Q1-Q20 in section 34 of the product
  specification) are product choices, not gaps in this constitution; each plan records the
  ones it depends on.
-->
# Engineering Project Coordination Hub Constitution

## Core Principles

### I. Coordination First

Every feature MUST help someone answer: who is responsible for what, what is due next, what
are we waiting for, and what is stopping the project (product specification §1, §4
principle 1). A feature that does not serve that question is rejected, however useful it
sounds. Fields are optional unless a rule needs them, and warnings are preferred over hard
validation except where data integrity requires it. The specification's out-of-scope list
(§30) and "Features That Sound Useful But Should NOT Be Built Yet" are binding until the
specification is amended.

Rationale: the product exists to replace hand-compiled status, not to become another generic
task tracker.

### II. Deterministic and Explainable (NON-NEGOTIABLE)

- The product MUST NOT contain AI, machine-learning, or LLM features in any phase: no
  assistants, summaries, predictions, extraction, or "smart" scheduling. Jay confirmed this
  on 2026-09-24.
- Every computed status, indicator, attention item, and health colour MUST trace to a rule and
  threshold stated in the specification, and the interface MUST be able to show that rule
  ("Why?").
- Rules MUST be pure functions over project data and organisation settings. The worked
  examples (§15.12) and the acceptance criteria (§31) MUST exist as automated tests.
  Thresholds live in organisation settings and are never hard-coded.

Rationale: people act on a red flag only when they can see why it is red.

### III. One Accountable Owner

Every task, deliverable, decision, risk, issue, and meeting action MUST have exactly one
accountable owner. Multiple assignees MUST NOT be introduced; collaborators, watchers, and
reviewers cover shared work (§4 principle 2, §30).

Rationale: shared accountability is how work falls through the gaps.

### IV. The Product Specification Is the Source of Truth

- `Engineering-Project-Coordination-Hub-Specification.md` defines product behaviour. Within it,
  §10 (Core Data Model) wins over later sections. Canonical status, role, and threshold names
  MUST be used verbatim in code, interface, and tests.
- Every feature spec under `specs/` MUST cite the specification sections and the requirement,
  rule, and acceptance-criteria IDs it implements (for example FR-TSK-03, D-06, AC-REV-02).
- A change in behaviour MUST amend the specification in the same change: edit `spec-parts/`,
  then run `python3 tools/build_spec.py` to regenerate the combined Markdown and the HTML page.
- Screens follow `Engineering-Project-Coordination-Hub-UI-Mockups.md`. Where the mockups and
  the specification disagree, the specification wins and the mockups are corrected.

Rationale: one written contract keeps people, agents, and reviewers aligned.

### V. Traceable by Construction

- Every change to ownership, dates, status, structure, or decisions MUST write an immutable
  activity-log entry in the same database transaction as the change, with the actor, the old
  and new values, and any required reason (§20). No application code path may update or
  delete an activity-log row.
- Work items are only ever soft-deleted, and every mutable entity uses optimistic concurrency
  (rules G-06 and G-07).

Rationale: "who moved that date?" must always have an answer, and audit is expensive to
retrofit.

### VI. Secure by Default

- Sign-in uses Microsoft Entra ID only; the product stores no passwords (§21).
- Authorisation MUST be evaluated on the server for every command and query, deny by default,
  from the permission matrix in §8. Every matrix row MUST become an automated test. The
  interface only reflects decisions the server makes.
- Secrets MUST NOT appear in code, configuration files, logs, or error messages; they live in
  a managed secret store.
- External parties are referenced, never notified and never given accounts, until the
  specification says otherwise.

Rationale: project data is internal-confidential and every employee is a user.

### VII. Simple Enough for a Small Team

The system MUST remain a modular monolith with one database and mainstream, vendor-supported
frameworks that 2 to 4 developers can maintain (§4 principle 12, §5.2 goal T3). New services,
message brokers, custom-field builders, workflow engines, or other speculative infrastructure
MUST be justified in the plan's Complexity Tracking table and approved by Jay before any
implementation.

Rationale: the quickest way to lose an internal tool is to make it expensive to keep.

## Product and Technical Constraints

- Architecture and stack follow §23 of the specification: a React and TypeScript single-page
  application, one web API, PostgreSQL, and Microsoft Entra ID, hosted on Azure and built with
  infrastructure as code. The backend language (ASP.NET Core or Node.js with TypeScript,
  decision Q1) is fixed by the first plan that needs it.
- Runtimes and frameworks MUST be on a vendor-supported release when implemented. For example,
  .NET 8 support ends in November 2026, so a .NET backend uses the current LTS release.
- Quality gates (§22): rules-engine branch coverage of at least 95 %; service-layer coverage of
  at least 70 %; end-to-end tests for the critical flows; WCAG 2.1 AA on every screen, with
  colour never the only signal; interface strings externalised from the first component.
- The performance targets in §22 are acceptance criteria: My Work, the Project Dashboard, and a
  500-task list become interactive within 2 s at p95; API list and item reads answer within
  500 ms at p95 with 100 concurrent users; re-evaluating a 2,000-task project takes under 2 s
  and never blocks a save.
- Every list is exportable, and a full project export is offered when a project is archived,
  so the data is never locked in.

## Development Workflow

- Work proceeds in Spec Kit feature packets listed in `specs/README.md`: specify, clarify
  (when needed), plan, tasks, analyze, implement, converge. Each command names its feature
  directory explicitly (`SPECIFY_FEATURE_DIRECTORY=specs/<feature>`); the machine-local
  `.specify/feature.json` pointer is checked, never trusted blindly.
- A planning request never authorises application code. An explicit implementation request
  authorises only the tasks it names, inside the plan's file boundaries.
- Every plan passes the Constitution Check before design and again after it, and lists the
  open product decisions (§34) it depends on.
- Implementation records passing, failing, skipped, and unrun checks in the packet's
  `verification.md`. A ticked checklist is not evidence that code works.
- Sub-agents and the bundled `specify workflow run` runner are used only when Jay asks for
  them. Commits, pushes, issue creation, and deployments happen only on request.
- Anything discovered outside a packet's scope becomes a proposed follow-up, never a silent
  addition.

## Governance

- This constitution supersedes other practices in this repository. Where it conflicts with an
  upstream Spec Kit template, the constitution wins; upstream templates stay unmodified.
- Amendments are made with `/speckit-constitution` on Jay's explicit direction, never to make a
  feature easier to pass. Each amendment carries a Sync Impact Report and a semantic version:
  MAJOR for removed or redefined principles, MINOR for new principles or materially expanded
  guidance, PATCH for clarifications.
- Compliance is checked at three points: the plan's Constitution Check, the `/speckit-analyze`
  consistency gate before implementation, and review of the final diff. Any justified exception
  is recorded in the plan's Complexity Tracking table.
- `CLAUDE.md` holds day-to-day guidance for agents and may not contradict this document.

**Version**: 1.0.0 | **Ratified**: 2026-09-24 | **Last Amended**: 2026-09-24
