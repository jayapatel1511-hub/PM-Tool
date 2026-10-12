## 42. Supervisor Capacity Signals Amendment

Approved scope: on 2026-10-12 a gap review of the October 2026 product research against §39–§41 found two of its recommendations still open, delivered by packet 038 with the default for every open decision (§42.5). This section records the product contract; implementation and acceptance remain pending. It puts the Weekly Planner's figures for the current week on My Staff, where a Supervisor already looks at their people, and holds the daily digest on days a person has no availability. Both reuse records, rules and reads that exist: §39's planning entries and capacity, the FR-PICK-01 batch read, §12.18's approved leave and office holidays, and §17.3's digest. Nothing here adds a rule, threshold, record, permission or endpoint.

Where this section adds to §13.19 (My Staff), §17.3 (daily digest) and §17.4 (preferences), this section governs. §39, §40 and §41 are unchanged. The research's other open recommendations stay deferred as §40.10 lists them.

### 42.1 Scope and boundary

Part A (§42.2) is one column on My Staff. Part B (§42.3) is one condition in the daily digest and one user preference. Outside this amendment: a Manager Home or exception dashboard; changes to the planner's figures, indicators or scope (PLN-05 to PLN-16); new capacity signals or thresholds; holding immediate in-app or email notices; redirecting notices to a delegate; an "away" marker beside names elsewhere in the product; changes to the Staff Assignments report or any export.

### 42.2 Capacity on My Staff

- **FR-SUP-01.** My Staff (§13.19) MUST show, for each listed person, a "This week" column with their remaining capacity for the current ISO week as "N h free" and, when that week has time away, "N h away". The current week is the ISO week identified by its Monday in `org_time_zone` (PLN-01, G-03), on every day of the week. The figures are the Weekly Planner's own for that person and week: remaining capacity by PLN-09 and time away by PLN-06, computed by the same functions, under the viewer's visibility rules (FR-PLN-18), so the two screens never disagree. This column is the one exception to §13.19's "counts only, no hours" rule.

- **FR-SUP-02.** The figures MUST come from the FR-PICK-01 batch planner read (at most 50 people for one week, answered inside the viewer's planner scope, PLN-14). My Staff loads them with one request per 50 listed rows, after the rows render, never one request per person. A person outside the viewer's planner scope is left out of the answer silently and shows no figure. A viewer without planner access sees no column and makes no capacity request.

- **FR-SUP-03.** The column MUST follow the person picker's partial-view rule (FR-PICK-02): whenever the picker would show "Partial view" instead of a figure for that person, so does My Staff, with the PLN-15 label as its tooltip and accessible description. The two surfaces share one rule, including any later change to it.

- **FR-SUP-04.** The planner's indicators for that person and the current week, Over-planned (PLN-10) and the count of Stale plan entries (PLN-12), MUST join the row's signal set and follow §41 R7: the row shows its most severe signal plus "+n", and the capacity chips name their week (for example "Over-planned · W42"). Under-planned is not shown on My Staff. A week with neither indicator shows no chip.

- **FR-SUP-05.** The column MUST be information only. It is not a filter, sort key or export column; the Staff Assignments report (§19) is unchanged; it stores nothing, notifies no one and changes no figure, indicator or response of the Weekly Planner, Workload, My Week or readiness. On phones the column appears in the read-only card as one line of text.

### 42.3 Digest hold during time away

- **FR-DIG-01.** The daily digest (§17.3) MUST NOT be sent to a person on a day on which their daily capacity, by the planner's day rule (FR-PLN-08, with the approved leave and office holidays of §12.18), is 0 h although their calendar counts the day as a working day (normal day capacity above 0 h). A Reduced day with any available hours sends as usual. The condition is evaluated per person when their digest is due, for the calendar date in `org_time_zone` (G-03). The weekend rule of §17.3 is unchanged and applies first; a day that is non-working by the person's calendar is governed by that rule alone.

- **FR-DIG-02.** A held digest MUST NOT be queued, merged or sent later, and the held day does not count as a sent digest. The next digest sent covers its normal window: the dated sections as of its send day, and the Project updates and My staff sections "since the last digest" sent, so the first digest after time away summarises what changed while the person was away, under the existing caps of 10 rows and "and n more" per section (§17.3, §17.5).

- **FR-DIG-03.** The hold MUST be a per-user preference, "Hold my digest on days I'm away", default on for new and existing users, shown on the Preferences page (§13.17) beside the digest on/off and time. Turning it off sends the digest on away days as before. There is no organisation setting, and the preference changes no other notification.

- **FR-DIG-04.** Immediate in-app and email notices, the weekly summary, the Following feed and the in-app notification centre MUST be unchanged by the hold. The hold reads only the day's available hours; it never reads, logs or shows a leave reason, type, balance or HR reference (§12.18), writes no record about the person, and notifies no one. The digest job reports the number of digests held beside the numbers sent and empty.

### 42.4 Acceptance criteria

- **AC-SUP-01.** Given Taylor supervises Yagmur, and the Weekly Planner shows Yagmur's current-week cell as 12.0 h remaining with 8.0 h time away, when Taylor opens My Staff, then Yagmur's row shows "12 h free · 8 h away", and the figures equal the planner's for that person and week.

- **AC-SUP-02.** Given 12 direct reports, then My Staff loads their figures with one request naming those 12 people; given an Admin on the all-staff scope with 120 people, then three requests of at most 50 people each; in neither case one request per person. A person outside the viewer's planner scope shows no figure, and the response contains no data for them.

- **AC-SUP-03.** Given a person for whom the owner picker shows "Partial view", then My Staff shows "Partial view" for that person, with the PLN-15 label as its tooltip and accessible description, and no figure.

- **AC-SUP-04.** Given a person who is Over-planned in the current week with two Stale plan entries, then the row's signal set includes "Over-planned · W42" and "Stale plan 2", shown as the most severe signal plus "+n" under §41 R7; a person with neither shows no capacity chip.

- **AC-SUP-05.** Given My Staff is opened with the column visible, then the Weekly Planner grid, My Week, Workload and readiness responses, and the Staff Assignments report, are identical to their values before packet 038, and the existing packet 034 and 036 automated tests pass unchanged.

- **AC-DIG-01.** Given Yagmur has digest on and an Unavailable override of 0 h on Tuesday, a working day of her calendar, when 07:00 Tuesday organisation time passes, then no digest email is queued for her, the job counts one held digest, and an immediate notice that day is sent as usual.

- **AC-DIG-02.** Given a Reduced override of 4 h on Wednesday, then Wednesday's digest is sent.

- **AC-DIG-03.** Given Yagmur is away Monday to Friday and returns the following Monday, then none of the five digests is sent, nothing is sent on Saturday or Sunday under the existing weekend rule, and Monday's digest is sent with its dated sections as of that Monday and Project updates since the last digest sent before her leave, each section capped at 10 rows with "and n more".

- **AC-DIG-04.** Given Yagmur turns "Hold my digest on days I'm away" off, then her next away day's digest is sent; the preference is hers alone, defaults to on for new and existing users, and changes no other notification.

- **AC-DIG-05.** Given a full-day office holiday for Yagmur's office on Thursday, then Thursday's digest is held; given instead that Friday is a non-working day of her calendar, then Friday is governed by the weekend rule alone and this hold does not apply.

- **AC-DIG-06.** Given any held digest, then no leave reason, type, balance or HR reference is read, logged or shown, no record is written about the person, and her Supervisor receives nothing.

### 42.5 Decisions and deferred proposals

Each decision below uses its default until Jay decides otherwise.

| # | Decision | Default used |
|---|---|---|
| D1 | Which week My Staff shows | The current ISO week (Monday in `org_time_zone`), on every day of the week. No next-week switch or horizon. |
| D2 | Partial-view rule on My Staff | The person picker's (FR-PICK-02), including any later decision on it, so the two surfaces never disagree. |
| D3 | Admins and Executives on the all-staff scope | Figures for everyone, loaded one request per 50 rows (FR-SUP-02). |
| D4 | What holds the digest | A working day of the person's calendar with 0 h available, whatever the cause: a full-day Unavailable override, approved full-day leave or an office holiday. A Reduced day sends. |
| D5 | What happens to a held digest | Nothing is queued or merged. The next digest's "since the last digest" sections cover the time away under the existing caps. |
| D6 | Where the hold is controlled | A per-user preference, default on. No organisation setting. |
| D7 | Exports and reports | Unchanged. The column is screen-only. |

Deferred follow-up proposals, outside packet 038: sorting or filtering My Staff by free hours; a next-week column or a horizon on My Staff; Under-planned on My Staff; holding or redirecting immediate notices during time away; an "away" marker beside names in team lists, pickers and comments; showing the hold in Vacation & Coverage; holding the weekly summary. The Manager Home exception dashboard, People and Person Profile, Overlapping Plans and Deliverable Pressure stay deferred as §39.12 lists them.
