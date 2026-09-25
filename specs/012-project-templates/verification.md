# Verification: Project Templates

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (292/292) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 97.2 % branches, services 90.3 % lines |
| `dotnet build -c Release` with security analysers | Pass |
| `npm run build` and `npm run lint` | Pass |

`TemplatePlannerTests` (4): typed dates win, offsets fill the rest, blanks and anchorless milestones stay undated, a
"previous milestone" anchor follows a dated predecessor and not an undated one; deliverables and tasks follow their
milestone and deliverable (Appendix A: 85 % package M05 − 3 = 2027-05-11, "Update grading" − 12 = 2027-04-29), tasks
without a deliverable use the start date; unticked disciplines take their items and every dependency touching them, and a
task on a left-out deliverable leaves with it.

`TemplatesTests` (4):

| Test | Covers |
|---|---|
| Appendix A creates the stated structure in Setup | US2-1, US2-3, SC-002, FR-006, FR-008: 11 milestones (M01–M02 by offset, M03–M07 typed, M08–M11 undated), 28 deliverables, 120 tasks, 96 dependencies; lead, PM and unassigned roles; owners; origin recorded; one "CreatedFromTemplate" entry; no assignment notices until activation, then one per person (US2-4) |
| An unticked discipline and its dependencies are left out | US2-2: without Geotechnical, 26 deliverables, 112 tasks, 88 dependencies |
| Editors build, publish and version templates, and projects keep theirs | US1-1..3, US4, FR-001..FR-003: non-editors refused; a loop refused; stale save refused; preview dates and counts; drafts hidden from non-editors; publish v1; published cannot be edited; a project from v1; one draft per family; v2 publishes and retires v1 with v1's content kept; the project still records v1 unchanged; a retired template is refused at creation and nothing is created (US2-5); retiring hides it |
| A discipline pack is added with milestones mapped by name | US3, FR-007: Transportation proposed with "85% Design Submission" matched; a team member refused; the pack creates 1 deliverable, 4 tasks, 3 dependencies dated from the project's M05 (− 5), with the new discipline and its lead |

## Manual checks (browser pane)

| Check | Result |
|---|---|
| Templates as Jordan (Admin): the reference template, v1 Published, 11 milestones, 29 deliverables, 124 tasks; the version shows read-only with six disciplines included by default and three optional | Pass |
| New project as Priya from the template: start 2026-10-05 computes every milestone; a typed 30 % date shows "typed"; Tender Close left undated disables its date; the next step ticks the six default disciplines; the summary reads 11, 28, 120, 96 with 1 milestone, 1 deliverable and 7 tasks undated | Pass |
| Created project 2026-0501: Setup, 11 milestones, 28 deliverables, 120 tasks, "Municipal Infrastructure Design v1" | Pass |
| Team tab → Add from template → Transportation: its milestone proposed as the project's M05; added D029 due 2027-04-28 with the Transportation discipline | Pass |
| Edit (new draft) → change an offset → Save → Discard draft: back to v1 only | Pass |

## Defects found and fixed while verifying

- The wizard's "no template" option reused the empty-library message; it now says "No template".
- Counts read "1 milestones" and "1 deliverables"; summaries now use "label: number".
- Publishing ignored a row version sent in the body; it now accepts it as other actions do.
- A pack's log entry was lost because the project row itself does not change; it is now an explicit event.
- Dependency order was not stable between reads; they now list in task order.

## Decisions

- The reference template is seeded in development and test only; organisations build or import their own.
- A deliverable made from a template belongs to its discipline's lead; tasks follow the template's role.
