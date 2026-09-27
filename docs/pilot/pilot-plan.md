# Pilot plan

Confirmed participant count (Q16): **50 people**. Proposed operational default: **3 PMs, 6 real projects, 8 weeks**, then a recorded go/no-go decision (§5.1, §35.1); choose the actual PM/project mix with the sponsor. The pilot
starts only after AC-VIS-01 to AC-VIS-08 pass on the integrated build and the threat-model walkthrough is done.

## Before week 1

- Select the 50 participants and confirm the proposed three PMs/six projects; set up the projects, teams and disciplines with them (a copy of structure saves time).
- A 30-minute walkthrough for each team (`docs/user-guide.md`); note who completes the core actions unaided (G6).
- Create a named workspace "Pilot" containing the six projects for the sponsor and the Hub team.

## Weekly review (30 minutes, every week)

1. Open **Reports → Pilot measures** for the six projects (last 8 weeks) and the attention lists of each project.
2. For each attention rule that proved too noisy or too quiet, agree one threshold change in **Admin → Settings**
   (logged automatically) and note why below.
3. Ask each PM: did Weekly Coordination replace your spreadsheet or email this week? (G1)
4. Check upcoming submissions: was each At Risk at least two weeks ahead if it was not on course? (G5)
5. Record the readings in the table.

## Measures

| Goal | Reading | Source | Target |
|---|---|---|---|
| G1 PMs run coordination from the Hub | weeks with a coordination review recorded; PM's answer | Pilot measures (Coordination reviews), weekly question | every pilot project, every week |
| G2 Every task has an owner and a due date | open tasks without owner; without due date | Pilot measures (from nightly snapshots) | trending to zero after week 4 |
| G3 Blocked work visible within a day | blocked tasks shown without manual entry | Pilot measures (Blocked), attention lists | blocked items appear the same day |
| G4 My Work is the daily list | share of people with open tasks who changed a task's status that week | Pilot measures (Share changing status) | everyone, weekly, without being chased |
| G5 Milestone risk two weeks ahead | submissions that were a surprise | Weekly review, milestone At Risk flags | none |
| G6 Adopted with minimal training | new users completing update, comment, ready for review unaided after the walkthrough | Walkthrough notes | all |

## Weekly log

| Week | G1 | G2 (no owner / no date) | G3 | G4 | G5 | Threshold changes and why | Notes |
|---|---|---|---|---|---|---|---|
| 1 | | | | | | | |

## Go/no-go

At the end of week 8, the sponsor records the decision as a **Decision** in the Hub (on an internal "Hub rollout"
project) with the measures above as its context, the options considered (roll out, extend the pilot, stop), the outcome,
and conditions such as the organisation's penetration-test requirement (T-18) and open threat-model items.

## Support

The Hub team answers pilot questions the same day through one channel; a local champion per office carries the rollout
after the pilot. Tickets record the screen, item key and time — never comment text or client documents.

## Coordination expansion scenarios

Packets 025–033 are specification-only. Include a packet in the pilot only after its own application acceptance and the existing hardening gates pass; record enabled capabilities for each pilot week. The participant count does not certify 50 simultaneous sessions or a server size.

Use representative permitted project data to walk through these end-to-end scenarios:

1. A sender submits an input, the receiving discipline accepts it for a purpose, then records incorporation of that exact revision.
2. A newer registered revision triggers assessment; one consumer is unaffected, another needs corrected work. Preserve the older issued history.
3. Two disciplines review a package; a blocking comment is answered and independently verified before submission readiness passes.
4. A Ready submission becomes Checking when an affected revision changes; a stale issue attempt is refused.
5. A supervisor confirms production and review allocations; demonstrate overlap handling and visibility of partial workload.
6. A shared design assumption is used with an explicit expiry, then confirmed or changed with linked impact assessments.
7. An upcoming output is not ready because an input is missing; a removal owner resolves the constraint, the consumer verifies it, and the performer commits the output for the week.
8. An issue identifies a drawing revision and station/area context; two disciplines work on the same issue and an independent verifier confirms resolution.
9. The discipline view reconciles all of the above with the source screens and permission-filtered exports.

Record observed usefulness, time-consuming fields, missed handoffs and unclear responsibility as pilot feedback. Do not claim a productivity improvement or change existing success targets without actual evidence. Continue the existing weekly review and go/no-go process.
