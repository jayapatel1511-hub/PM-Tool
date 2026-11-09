<!--
Sync Impact Report
- Version change: 1.1.0 -> 2.0.0 (MAJOR: principle VI redefined; a technical constraint changed)
- Modified sections: Product and Technical Constraints, first bullet. On Jay's direction of
  2026-11-09 ("keep everything tailored to homedev"), hosting is homedev: Docker Compose
  stacks behind a Cloudflare tunnel, defined by the repository's hosting/ files and activated
  by its scripts, in place of "hosted on Azure and built with infrastructure as code".
  Specification §21 to §23 were rewritten to match in the same change.
- Modified principles: VI. Secure by Default, first bullet only. On Jay's direction of
  2026-11-09 ("rewrite sign-in"), "Sign-in uses Microsoft Entra ID only" becomes an individual
  account per person through the deployment's sign-in mode: local-password accounts on
  homedev (his 2026-10-01 decision, docs/runbooks/environments.md), Microsoft and Google
  sign-in where he authorises it (2026-10-14, docs/runbooks/social-sign-in.md), or Entra. No
  stored passwords stays. Principles I to V and VII, the other bullets of VI, Development
  Workflow and Governance are unchanged, word for word.
- Sections added: none. Sections removed: none.
- Templates: no template changes needed.
- Follow-up files: README.md, docs/admin-guide.md, docs/user-guide.md and docs/decisions.md
  updated to name homedev.
- Deferred TODOs: principle IV's sentence that screens follow the mockups file is older than
  CLAUDE.md's visual-reference rule; not changed here. Decision RPT-07 (which sign-in the
  pilot's real users get) stays open in specs/DECISIONS.md.
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

- Every person signs in with an individual account, through the mode the deployment selects
  (§23.6, FR-AUTH-01): local-password accounts on homedev, Microsoft and Google sign-in where
  Jay authorises it, or Microsoft Entra ID. The product stores no passwords, only salted
  verifiers held in private files outside Git. Shared accounts are not allowed, and
  development sign-in never runs outside Development and Testing (§21).
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
  application, one web API, PostgreSQL, and individual sign-in (§23.6), hosted on homedev as Docker Compose
  stacks behind a Cloudflare tunnel. The stacks are defined by the repository's `hosting/` files
  and activated by its scripts; no other host or paid service is assumed. The backend language (ASP.NET Core or Node.js with TypeScript,
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

- Work proceeds in Spec Kit feature packets listed in `specs/README.md`: specify, clarify (when
  needed), plan, tasks, analyze, implement, converge. Each command names its feature directory
  explicitly (`SPECIFY_FEATURE_DIRECTORY=specs/<feature>`); the machine-local
  `.specify/feature.json` pointer is checked, never trusted blindly.
- A packet is one spec pull request, which Jay approves and merges, followed by slice pull
  requests. The spec pull request holds the `spec-parts/` delta, a `spec.md` of at most about
  1,500 words, a short `plan.md`, a `tasks.md` that lists the slices (each slice is one pull
  request) and the packet's rows in `specs/DECISIONS.md`.
- A planning request never authorises application code. An explicit implementation request
  authorises only the slices or tasks it names, inside the plan's file boundaries.
- Boy-scout rule: within the files a slice already changes, code may be converted to the
  project's current patterns when tests stay unchanged and behaviour does not change. This is
  not an addition, and it never widens a pull request to other files. Anything else discovered
  outside a packet's scope becomes a proposed follow-up, never a silent addition.
- Every plan passes the Constitution Check before design and again after it, and lists the open
  product decisions (§34) it depends on.
- Decisions: open product and process decisions live in one log, `specs/DECISIONS.md`, each
  with a default. Defaults apply while an addition is dark. A pending decision never blocks a
  merge or a review deployment; it blocks only turning the addition on for pilot users. Only
  Jay confirms or overrules a decision.
- Trunk: `origin/main` is the only integration branch, protected and always deployable. Pull
  requests merge by squash after the required checks pass on GitHub, and that remote CI run is
  the evidence for the change. Unreleased additions that change what pilot users see merge
  dark behind organisation-setting flags, unless their packet lists them as unflagged
  corrections or fixes, each with a decision row; such unflagged slices are Tier B. An
  existing feature is never put behind a flag.
- Merge tiers: Jay gives a standing request for Tier A pull requests to merge automatically
  once the required checks pass and the reviewer agent reports no open findings. Tier A covers
  dark additions, fixes and guards that change nothing users see, tests, documentation,
  refactors with unchanged tests, and patch or minor dependency updates. Jay merges Tier B pull
  requests himself: specifications and decision statuses, permission and authentication code,
  contract migrations, hosting, deployment scripts, CI configuration, any change to approved
  behaviour, any unflagged change users can see, and anything labelled `large` or
  `replace-test`.
- Deployment: Jay gives a standing request for each green main image to be deployed
  automatically to the homedev review stack, with a database dump before activation, probes
  after it and an automatic return to the previous image on failure. Pilot and production
  deployments happen only on request.
- Verification: every acceptance criterion added or changed is cited by an automated test,
  unless it is listed as human-only. `tools/trace_spec.py --report` joins each specification ID
  to its tests and to the CI results of a commit; the report of the deployed commit's run is
  the packet's verification. Each packet keeps one hand-written `acceptance.md` of at most one
  page for human sessions, visual review, sweep defects and sign-off. A ticked checklist is not
  evidence that code works.
- Transition: until `tools/trace_spec.py --report` is published on main, implementation records
  passing, failing, skipped, and unrun checks in the packet's `verification.md`, as before.
  Existing `verification.md` files are kept and not rewritten.
- Agents: sub-agents, parallel agents and the bundled `specify workflow run` runner are used
  only when Jay asks for them, within his limit on concurrent agents. Each agent works on one
  slice branch from `origin/main`. At most five agents write at once, on disjoint paths; one
  schema-lane writer alone adds migrations; a separate reviewer agent reviews every pull
  request. Agents commit, push and open pull requests only for work Jay has requested. Agents
  never deploy, never turn a flag on in pilot, never apply a Tier B label and never confirm a
  decision. Issue creation happens only on request.

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

**Version**: 2.0.0 | **Ratified**: 2026-09-24 | **Last Amended**: 2026-11-09
