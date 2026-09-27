# Implementation Plan: Collaboration and Notifications

**Branch**: `006-collaboration-and-notifications` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

Comments with @mentions on tasks, deliverables, milestones, decisions and the Phase 2 registers; document links on
projects, deliverables and tasks (pointers only); the notification centre with a Following tab; per-event channel
preferences and the daily digest; follow levels per project with the rule that assignment follows and the user's
choice sticks. Notification rows and immediate emails are created by the existing `Notifier`, which already applies
self-suppression, Muted follows, preferences, the 5-minute collapse, the 24-hour email de-duplication and the Setup
hold (packets 002–005).

## Technical Context

As packet 001. Tables `comment`, `comment_mention`, `item_watcher`, `document_link`, `notification`,
`notification_preference`, `user_setting`, `project_follow` and `email_message` exist in the initial schema. Migration
`ProjectLinksAsDocumentLinks` moves the project header links into `document_link` and drops `project_link`.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | No chat, threads, reactions, attachments or read receipts (§12.8); no file storage (FR-DOC-03); no Teams notifications (Phase 3) | Pass |
| II | Nothing derived is stored as a status; the Following feed is a query over `activity_log` (ASG-05), not a notification row per follower | Pass |
| III | Notifications go to named people only: owners, watchers, mentioned people, followers; nobody can set another person's follow level (ASG-02) | Pass |
| IV | Event codes and follow levels verbatim (§17.2, §12.18) | Pass |
| V | Comment create/edit/delete, mentions of non-members, link add/remove all logged in the same transaction; comment text is not copied into the log; links are soft-deleted (DOC-03) | Pass |
| VI | `Permissions.Comment/AddLink/EditComment/DeleteComment`, PM-only project links, follow only on visible projects | Pass |
| VII | Digest runs as one more advisory-lock job in the monolith; mail through the existing sender adapters; no new infrastructure | Pass |

## Design notes

- **Mentions** are stored in the text as `@[Display Name](user-id)`, inserted by the composer's people picker (project
  members first, then other active people). The server resolves them, adds watchers (C-06), notifies with the Mention
  event, and logs "mentioned non-member" for people outside the team (C-05). On a Restricted project only people who
  can see it may be mentioned (decision: the spec's "can open the item" would need an item-level grant).
- **Deleted comments** keep their text for Admin audit; others see "deleted by author" or "removed by PM" (C-03, C-04).
  Only General comments are edited; Review and Status Note comments are records of a transition.
- **Following de-duplication** (ASG-06): a log row whose correlation id produced a personal notification for the user
  is marked "notified" and not counted as unread or in the digest's Project updates.
- **Digest**: runs every 5 minutes and sends each person's digest once their local time passes their chosen time
  (default the organisation's `digest_send_time_local`); weekends follow `weekend_digests`; only Active projects the
  person can see; each item once in its most severe section (Overdue, Due soon, Blocked, Reviews, Decisions,
  Attention, Milestones), 10 rows per section with "and n more"; subject "Hub digest — 2 overdue, 1 review, 1
  blocked · n project updates". The My staff section is packet 007.

## Project Structure

```text
src/Hub.Api/Features/Comments.cs, DocumentLinks.cs, Notifications.cs
src/Hub.Api/Infrastructure/Digest.cs
src/Hub.Api/Data/Migrations/*_ProjectLinksAsDocumentLinks.cs
tests/Hub.Tests/Api/CollaborationTests.cs
web/src/components/hub/comments.tsx, links.tsx, notifications.tsx, richtext.tsx
web/src/pages/Notifications.tsx, Preferences.tsx, projects/Follow.tsx
```

## Checks

AC-COM-01..05, AC-DOC-01..03, AC-NOT-01..07, AC-REV-01, AC-ASG-01..06 as API tests; browser checks of the comment
box, mention picker, bell, centre, Following tab, preferences and follow control.

## Complexity Tracking

None.
