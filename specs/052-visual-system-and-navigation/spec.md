# Feature Specification: Visual System and Navigation

**Packet**: 052 | **Feature directory**: `specs/052-visual-system-and-navigation` | **Date**: 2026-10-31
**Status**: Specified, not started. No approval is claimed; scope awaits Jay's approval (052-D1).
**Input**: the visual direction discussed and recorded on 2026-10-31 while reviewing the 33-screen colour-direction prototype (`.review/2026-10-31-unslop-home/proto/`, untracked). The prototype is the visual reference; this packet and §56 govern.
**Source**: product specification §56 (§56.1 to §56.13) in `spec-parts/31-visual-system-and-navigation.md`. Related contracts: §9.4 (navigation), §13.0 (layout, density, typography, status colour language, responsive, accessibility), §36.1 and §36.6 (FR-VIS-01, FR-VIS-02, FR-VIS-07), §36.3 (task lanes), §41 (calm workspace: R3 to R9, E1, E2, AC-CALM-04, AC-CALM-05, AC-CALM-06, AC-CALM-08), §43 (flags, packet 039: FR-DLV-07 to FR-DLV-10), §49.3 (FR-RCV-07, counts open their lists).

## User outcome

- **Everyone:** one look across every screen. Colour always means the same thing, figures are easy to read, and each screen with totals says when they were read.
- **Project Manager, Supervisor:** a sidebar of eight places plus a short foot group. Views of a page are tabs on that page, so nothing hides under More.
- **Phone users:** the same navigation in a drawer, with today's bottom bar kept.

## Scope

1. **Tokens, type and spacing** (§56.2; FR-VSN-01, FR-VSN-02, FR-VSN-03): the kit tokens with muted text corrected to `#646d81`; IBM Plex Sans and Mono served from the application's origin; a 4 px grid; restrained motion.
2. **Colour with one meaning** (§56.3; FR-VSN-04 to FR-VSN-09): six metric hues, lane colours, pill tones, the hatch, five stored project identity colours and item shapes. FR-VSN-09 states exactly how §41 R8 and AC-CALM-05 would change under 052-D2.
3. **Shell, band and buttons** (§56.4; FR-VSN-10 to FR-VSN-13): light sidebar, navy top bar and band, the one-line project header of §41 R6, and navy primary buttons.
4. **Title block** (§56.5; FR-VSN-14): "Current values" and other read times from the response's `asOf`.
5. **Count cards** (§56.6; FR-VSN-15).
6. **Sidebar** (§56.7; FR-VSN-16): one list of eight and a foot group. It states exactly how E2, §41 R4, AC-CALM-04 and FR-VIS-01 would change under 052-D3.
7. **Tab rows** (§56.8; FR-VSN-17, FR-VSN-18): the workspace row and the project groups of E1, with Activity and Settings as links (052-D4).
8. **Phone** (§56.9; FR-VSN-19) and **accessibility** (§56.10; FR-VSN-20, FR-VSN-21, FR-VSN-22).
9. **Shipping dark** (§56.11; FR-VSN-23): `flag.visual_system`.

FR-VSN-01..23 are this packet's requirements.

## Non-goals

- Any change to screen content, which is packet 053 (§57); any change to labels, status names, role names, data rules or permissions.
- A dark theme, user themes, the rejected calm variant and grid texture.
- A new interface library or icon family.
- Adding the prototype to `docs/reference/` (052-D7 decides).
- Silently editing §41: R4, R8, E2 and AC-CALM-04 and AC-CALM-05 change only through 052-D2 and 052-D3.

## Acceptance

AC-VSN-01, AC-VSN-02, AC-VSN-03, AC-VSN-04, AC-VSN-05, AC-VSN-06, AC-VSN-07, AC-VSN-08, AC-VSN-09, AC-VSN-10, AC-VSN-11 and AC-VSN-12 in §56.12 are this packet's acceptance criteria. [verification.md](verification.md) maps each one to its slice.

## Decisions and conflicts

052-D1 to 052-D10 in [DECISIONS.md](../DECISIONS.md), mirrored in §56.13. All are Open with proposed defaults; no approval is claimed in this packet. D2 and D3 carry the proposed changes to §41 R8 and decision E2; D4 conflicts with E1; D5 conflicts with the current derived project accent; D6–D10 resolve the remaining delivery, wording, flag and phone questions. Until a row is decided, the older contract wins for that conflict. The flag is not turned on in pilot until D1, D2 and D3 are decided, and any pending row blocks only that flag flip.

## Assumptions

- **Packet 039, S09:** flags exist before any slice merges.
- **Packet 037:** the calm-workspace layout (one-row toolbars, grouped project tabs, one-line headers) is built. This packet restyles it and does not undo it.
- **Packet 044, §48:** the served-file allowlist admits `woff2` font files, or S01 adds the extension to it.
- **Packet 053:** its Home and dashboard requirements use the count cards and title block defined here, but each works with `flag.visual_system` off.
