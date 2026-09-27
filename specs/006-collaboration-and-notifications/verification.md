# Verification: Collaboration and Notifications

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (219/219) |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |

`CollaborationTests` (8, all pass):

| Test | Covers |
|---|---|
| Mention notifies and watches, edit window, PM removal | AC-COM-01 (Mention notice, watcher, "mentioned non-member" logged, not added to the team), AC-COM-02 (edit at 10 minutes shows edited, refused at 16), AC-COM-03 (PM removes another's comment, placeholder, text visible to Admin only, deletion logged), C-03 author-only delete |
| Status notes, viewer switch | AC-COM-04 (Status Note comment from a transition, not editable), AC-COM-05 (Viewer can comment until the project turns viewer comments off; then refused) |
| Document links | AC-DOC-01 (SharePoint detected, title from last segment), AC-DOC-02 (network path flagged for Copy path), AC-DOC-03 (deliverable link inherited read-only on its task), DOC-01 refusal, DOC-03 soft delete and log, project header links through the same table |
| Assignment notifies but never the actor | AC-NOT-01 (one in-app, one email row), AC-NOT-02 (own status change creates nothing), AC-NOT-05 pattern (email turned off for Mention: in-app only), preference reset, unread count, filters, mark one and mark all read |
| Following feed | AC-ASG-02 (others' changes unread, own changes absent, unread count), AC-ASG-06 (bulk shift of five tasks is one collapsed entry), AC-ASG-03 (manual level kept after a role change), AC-ASG-04 (manual follow kept on removal from an open project; assignment follow removed) |
| Setup assignments arrive as one batch | AC-NOT-07, AC-ASG-01, ASG-07 (read marker moved to the activation time, no unread flood) |
| Digest | AC-NOT-03 (subject "Hub digest — 2 overdue, 1 review, 1 blocked", sections with blockers), AC-NOT-04 (nothing to say, nothing sent), one per day, no weekend digest |
| Project updates exclude notified changes | AC-ASG-05 (three changes by others, the one that assigned the person is excluded: "2 project updates") |

AC-REV-01 (reviewer notified in-app and by email) is covered by `TasksTests.Review_flow_requires_reviewer_comment_and_counts_rounds`
together with the channel defaults (ReviewRequested: app and email).

## Manual checks (browser pane)

| Check | Result |
|---|---|
| Task panel Comments tab (default when available): empty state, composer placeholder | Pass |
| Typing "@Ma" lists Jill Martin and Marc Dubois (team) before Omar Haddad ("not on the team"); arrow + Enter inserts the mention; Ctrl+Enter posts | Pass |
| Posted comment renders bold and the @Marc Dubois chip; time in the viewer's local time | Pass |
| Marc's bell shows 8 unread; notification centre grouped Today/Yesterday with icons, project and relative time | Pass |
| Following tab: followed project with unread count and mark-as-read, entries grouped by project and day, "already notified" on the mention | Pass |
| Preferences: digest switch and time, event table with in-app/email and "still delivered when muted" | Pass |
| Project header follow control "Following: All activity" | Pass |

## Defects found and fixed while verifying

- Putting a waiting task On Hold sent its assignee "no longer waiting — you can start". Held tasks no longer get the
  unblocked notice; regression test `Putting_a_waiting_task_on_hold_does_not_say_you_can_start`.
- Project header links (packet 002) were hard-deleted from their own table. They are now document links on the
  project: soft-deleted and logged (DOC-03, constitution V). Migration `ProjectLinksAsDocumentLinks` copies existing
  rows before dropping `project_link`.
- A comment appeared in History and Following as "Created … Comment type: General"; it is now logged as "Commented"
  (the text itself stays out of the log).
- The notification centre title used a label with an unfilled "{n}".

## Decisions

- On a Restricted project only people who can see it can be mentioned; C-05's "a mentioned non-member can open the
  item" would need a per-item access grant, which the constitution's deny-by-default model does not have. On Open
  projects anyone active can be mentioned and can read the item.
- Following de-duplication (ASG-06) matches on the action's correlation id: if a request notified the person, none of
  its log rows count again.

## Deferred (cross-packet rule)

- The digest's My staff section and supervisor notices in the digest: packet 007 (FR-ASG-07).
- Weekly PM summary, digest content preferences and live unread counts: packet 020.
- Decision events (DecisionAssigned, DecisionRecorded) are raised by packet 008 through the same notifier.
