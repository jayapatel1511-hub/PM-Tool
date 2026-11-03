# S02 current-source audit · 2026-10-31

Four-fact context, canonical state selection, current authorization/recovery, terminal and unknown actors, exact child routes and new context-origin links are implemented across all twelve named detail kinds and original/discipline/workspace Weekly Coordination plus project attention. These findings are an independent source audit, not a claim that all acceptance has run.

S02 remains PARTIAL for concrete legacy relation surfaces:

1. Existing nested opens omit captureRecordContextOrigin: TaskPanel.tsx related Deliverable (around116); Deliverables.tsx related/member/dependency opens (421/432/446/517); Decisions.tsx related opens (around301); Registers.tsx related opens (649); Meetings.tsx related Task/Decision/ItemLink buttons (469–474). Closing the target currently drops the source panel and its scroll. Reuse useItemPanel(type,id,origin), gated to screen_patterns, with the clicked source element.
2. Cross-route stored-entity links in Handoffs.tsx source/target (227–228), Reviews.tsx finding linked Issue (110), and ReadinessForms.tsx stored constraint links (136) omit origin. Use the existing navigation capture/return chain, retain modified-click behavior, and open the exact record.
3. DecisionEndpoints.LinkRows at Decisions.cs245–258 orders by CreatedAt, not relationship kind/key/ID. Its query matches only SourceId and hydrates target IDs across projects. Pass the authorized project and declared source/target kind into hydration; malformed foreign/missing references stay unavailable, never reveal target key/name/status/date/person. Current normal creation validates scope; this is a legacy/malformed-read fail-safe gap, not evidence of a normal-write exploit.
4. Action direct-reference reads at Meetings.cs306–307 also lack project predicates. The ordinary create API accepts RelatedTaskId=T and Links=[Task:T]; ActionPanel renders T twice. Normalize existing displayed targets by (kind,id), preserve explicit provenance, and sort stably. No new relationship storage or generic framework is required.

File ownership: work-delivery owns TaskPanel/Handoffs/ReadinessForms; projects-reports owns Deliverables/Decisions/Registers/Meetings/Reviews and their API readers. Root owns shared origin helpers/core context. Submit shared changes through requests.

Focused acceptance: nested Task→Deliverable and Action→Task closes return to source panel with both main and panel scroll; cross-route handoff/finding opens return to exact filter/sort/project; an ordinary duplicate Action target appears once with its explicit provenance; reverse creation-order links sort by relationship/key/id; foreign, missing and mismatched-type refs expose no target metadata and do not crash the source panel. Preserve flag-off valid-record response shape and behavior.

S03 adds source-use comparison/held-up counts/Weekly blocker dedup; S05 adds complete grouped delivery/source/manifest/history locators. Keep them separate. Document URL/UNC LinksSlot entries are document pointers, not these internal entity relations. A real recipient browser session opening a copied link is distinct from existing API permission and mocked-browser evidence; do not claim that acceptance has run.



### Recipient-browser evidence added by coordinator

The checkpoint-twelve real API/Chrome Task copied-link path now passes in separate PM,
authorized recipient and denied recipient sessions; optional context off/on behavior
and restricted-project 404/privacy were exercised. See `../verification.md` and
`web/tests/record-context-recipient-real.cjs`. This closes that specific unrun evidence
gap, not all record types or legacy link/order/dedup work owned by the active lanes.
