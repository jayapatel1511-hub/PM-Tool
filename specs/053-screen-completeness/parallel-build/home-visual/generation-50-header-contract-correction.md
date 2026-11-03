# Generation50 header contract correction

Source/evidence audit only. Root explicitly authorized this test-only correction; no product/header CSS changed, and A executed no checks.

The CountCard overlap and project identity header have separate contracts. `count-card-overlap.cjs` continues to measure actual card overlap at exactly72px on desktop and64px on phone, within the existing1px measurement tolerance. Its Dashboard header check now preserves the previously measured64px desktop result within1px, and the established64–72px phone box.

Evidence:

- `spec-parts/31-visual-system-and-navigation.md:119` FR-VSN-12 and `:191` AC-VSN-05 require the complete one-line project identity/header/meta within72px. Neither specifies exact64px on phones.
- `parallel-build/generation45-brief.md:9` carries the same72px maximum. `home-visual/generation-45-overlap-and-density-proposal.md:22` explicitly describes a natural64–72px identity header.
- `web/tests/project-header-compact.cjs:61` begins the desktop name/group loop. Its logged64px observations in `evidence/checkpoint21-runtime/browser-project-header-compact-repair1.log` come from that loop. The later1440/375 loop at `:111–114` enforces72px maximum and does not log an exact phone64px result.
- `web/src/visual-layout.css:44–45` sets min64/max72; `:54` uses4px vertical desktop padding, and `:108` uses6px phone padding. The required40px main control plus16px metadata and12px phone padding explains the measured68px phone header. The desktop equivalent is64px. No generation50 overlap change altered these header rules.
- Root's `evidence/checkpoint22-runtime/count-card-overlap-run3.log` reports the settled375px Dashboard header as68px. Root accepted the distinction before authorizing this assertion correction.

The prior exact64px phone assertion over-generalized a desktop observation. This correction restores the existing normative phone bound; it does not waive CountCard overlap, facts, control sizes, containment or the separate strict Task/report/Staff density checks. All existing layout-readiness and initial/settled diagnostics remain. New execution and acceptance stay with root.
