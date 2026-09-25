# Implementation Plan: Notification and Search Enhancements

**Branch**: `020-notification-and-search-enhancements` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

A weekly PM summary email with a preview, digest sections that each person can switch off, unread counts that update
within seconds, and search across task and deliverable descriptions and comments with the matching text marked.

## Technical Context

As packet 001. `user_setting` already holds `weekly_summary_enabled`, `digest_sections_off` and
`last_weekly_summary_at`. One migration, `SearchTextIndexes`: trigram GIN indexes on `task.description`,
`deliverable.description` and live `comment.body` (partial on `deleted_at IS NULL`).

New: `WeeklySummary` and `WeeklySummaryJob` beside the digest in `Infrastructure/Digest.cs`; `Digest.SectionCodes` and
the switched-off sections in `Digest.Build`; `GET /me/weekly-summary` (preview), `GET /me/notifications/pulse`; the digest
preference accepts `sectionsOff` and `weeklySummary`; search adds description matches, a `comments` group and
risk/issue/action keys to the exact lookup. Web: `useUnread` polls the pulse and re-reads counts, the bell list and the
Following feed only when it changes; the Preferences digest section gains section switches, the weekly summary switch
and preview; search results show a marked snippet.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Counts, text matching and fixed schedules only | Pass |
| II | The summary counts with the dashboard's own task filters and the same decision and health reads | Pass |
| III | No ownership changes | Pass |
| IV | No status changes | Pass |
| V | Preference changes are stored per person; emails go through the existing outbox with a dedup key | Pass |
| VI | Search keeps the caller's visible projects, restricted ones included only for members; deleted comments never match; the summary and preview cover only projects the person manages | Pass |
| VII | No new infrastructure: live counts use a light poll instead of a push service; indexes only | Pass |

## Design notes

- Live counts: the pulse is four cheap reads (unread count, newest notification, newest change by others on followed
  projects, newest read marker) polled every 4 seconds while the tab is visible; the expensive Following count is
  computed only when the pulse changes. Background tabs pause and refresh on focus.
- The weekly summary goes out once a week at the person's digest time on the earliest coordination day among their
  Active projects (a project without one meets on Monday). Changes count everything done by others since the last
  summary (seven days the first time). PMs with no Active projects get nothing; anyone can turn it off.
- Switching a digest section off drops it and its count from the subject; its items can still appear in a later
  section, since sections de-duplicate in severity order.
- A description match ranks after name and key matches; snippets are about 80 characters with mention tokens as names.

## Complexity Tracking

| Deviation | Why | Simpler alternative rejected because |
|---|---|---|
| Polling rather than server push for FR-003 | Bearer-token auth rules out `EventSource`; a push hub is new infrastructure | A 4-second pulse meets the 5-second target with one small indexed query set per visible tab |
