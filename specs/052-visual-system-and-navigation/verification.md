# Verification: Visual System and Navigation

Specified only, on 2026-10-31. This task writes specification documents only; no runtime implementation or acceptance check was performed. Scope awaits Jay's approval (052-D1).

## Specification checks run on 2026-10-31

The final local specification checks below were run during completion of the draft. They do not constitute runtime acceptance.

| Check | Result |
|---|---|
| `python3 tools/build_spec.py` then `python3 tools/build_spec.py --check` | PASS: combined Markdown and HTML rebuilt with pandoc and up to date |
| `python3 tools/trace_spec.py` then `python3 tools/trace_spec.py --check` | PASS: 1,545 IDs and 438 sections; zero uncited or unknown IDs in the combined specification |
| Every ID referenced in §56 exists in the specification or is defined in §56 | PASS |
| No other company's product name in the files this packet adds or changes | PASS |
| Contrast of the §56 tokens computed with the WCAG 2.1 relative-luminance formula | PASS: `#646d81` is 5.19:1 on white and 4.64:1 on `--side`; strong metric hues on white are Blue 4.70:1, Violet 5.61:1, Red 4.74:1, Amber 3.30:1, Green 4.00:1 and Teal 4.12:1; status-pill text on its stated tint is 5.66:1–6.48:1; `#9bb8ff` on `--navy` is 7.19:1. FR-VSN-21 therefore limits smaller coloured text to pairings that pass 4.5:1 and treats the other strong hues as large figures or non-text marks. |

These are token-pair calculations, not an implementation or browser audit. Final acceptance must measure the actual rendered foreground/background pair, including overlays, hover/focus states and responsive layouts.

## Acceptance

| AC ID | Area | Slice | Status |
|---|---|---|---|
| AC-VSN-01 | Token census, fonts, no third-party font request | S01, S09 | UNRUN |
| AC-VSN-02 | Count-card colours by metric | S03 | UNRUN |
| AC-VSN-03 | Lane colours and hatch | S03 | UNRUN |
| AC-VSN-04 | Project identity colour | S04 | UNRUN |
| AC-VSN-05 | One-line project header within 72 px | S02 | UNRUN |
| AC-VSN-06 | Title block from the response time | S05 | UNRUN |
| AC-VSN-07 | Count card link and definition | S06 | UNRUN |
| AC-VSN-08 | Sidebar list and foot group per role | S07 | UNRUN |
| AC-VSN-09 | Workspace and project tab rows | S07 | UNRUN |
| AC-VSN-10 | Phone drawer and bottom bar | S08 | UNRUN |
| AC-VSN-11 | Accessibility scan and contrast census | S09 | UNRUN |
| AC-VSN-12 | Flag off changes nothing | S01 to S09 | UNRUN |

No application code, browser suite, axe scan, font-origin check, route check or visual acceptance check has run. The packet is specified only; all AC rows remain UNRUN.
