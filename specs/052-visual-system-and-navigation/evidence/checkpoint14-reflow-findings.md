# Checkpoint fourteen — broader enabled-state reflow/token baseline

The existing compiled checkpoint14 API/UI ran against a fresh disposable synthetic
database at8865. All eight052/053 flags were enabled only for this audit, then their
original values were restored. 17routes × fourvariants =68observations. The four variants
are desktop1600×900, phone375×812, half-CSS-viewport800×450/DPR2 and the latter with WCAG
text-spacing overrides. The half viewport is **a reflow equivalent**, not proof of an
actual200% browser zoom setting or human visual/keyboard acceptance.

Observed:

- No document horizontal overflow, sub11px visible text, page errors or external fonts
  in these fixtures/variants. Controls/content outside the automatic detector and more
  complex interaction states remain unverified.
- Serious axe scrollable-region-focusable on Workload's discipline-context table in
  desktop and both reflow variants: scrolling region has neither a focus stop nor
  focusable descendants. Capacity lane owns the repair and must verify keyboard scrolling.
- Four project-Dashboard task-name buttons clip under text-spacing (available193–195px,
  text212px), with no full-name title. Projects lane owns a responsive/full-name repair;
  the detector records suspects for review, not an unsupported content-loss verdict.
- Rendered colour-value inventory includes older hashed accent borders/avatars and
  lavender/peach summary accents outside the new semantic palette. Some unmatched values
  are declared line tokens and opacity-derived existing tokens; the inventory does not
  prove that a source uses a named token. Remaining token-source/meaning cleanup stays
  open. Do not add arbitrary accents to the allowed set merely to get a pass.

The raw JSON retains every observed value/example, all four findings and compiled file
hashes. This is FAIL/PARTIAL evidence for052S09, not whole-packet acceptance. New source
changes in generation8 were not built for this run.
