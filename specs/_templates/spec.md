# Feature Specification: <Name>

**Packet**: NNN | **Feature directory**: `specs/NNN-<name>` | **Date**: YYYY-MM-DD
**Status**: Specified, not started.
**Source**: product specification §NN in `spec-parts/NN-<name>.md`.

<!-- Packet shape v2 (§43.9, FR-DLV-21): at most about 1,500 words. One spec pull request (Tier B) holds
this file, plan.md, tasks.md, the spec-parts delta and the packet's rows in specs/DECISIONS.md. -->

## User outcome

<What changes for which user, in two or three sentences.>

## Scope

1. **<Area>** (§NN.N): <what is built>. FR-XXX-01, FR-XXX-02.

## Non-goals

- <What this packet does not do.>

## Decisions (defaults apply until Jay decides)

| ID | Question | Default |
|---|---|---|
| D1 | <question> | <default; the same row goes in specs/DECISIONS.md as NNN-D1> |

## Acceptance

- <Area>: AC-XXX-01, AC-XXX-02.

## Depends on

- Packet NNN (§NN): <what it needs first>.

## Flag name

`flag.<name>` (declared in `src/Hub.Domain/Flags.cs`), off by default. With it off, behaviour is today's.
