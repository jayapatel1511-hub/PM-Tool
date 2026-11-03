# Generation20 initial 052 inventory and bounded implementation

Read current FR-VSN-10–13/16–19, AC05/08/09/10 and the maintained Shell/nav/workspace/layout/title/tab sources and existing four visual scripts. These already implement the preferred shell, one-list rail, project groups/quiet links, normal long-name truncation and basic phone trap/return. Do not rewrite them. A owns no C#/API/metadata files in this batch: BACKEND READY, all previous C# held.

Concrete source gaps within this loan:

1. Desktop and phone Search inputs share the same React ref. Unmounting the phone sheet can clear the still-mounted desktop input's ref, breaking the desktop `/` shortcut after phone search. Give the sheet its own ref. The existing `/` handler cannot open phone search when no sheet exists; add a v2 phone branch and keep modal/text editing isolated, retaining off behavior.
2. Media changes unmount the sheets without clearing menu/search state; returning below768 can resurrect them. Clear only the shell sheets when leaving phone/visual mode. A history/pop navigation can also leave an open shell sheet; close it on v2 route-key changes. Existing Sheet focus mechanics remain shared/held.
3. Phone title-block inner columns use max-content for the label and can overflow with long reference labels/text spacing; its text container is not guaranteed to span the band. Make only title-bearing phone containers/full-width reference blocks shrink/wrap correctly without changing desktop compact geometry/server times. Keep the target-date part of phone project metadata from being squeezed away by long PM/client names; full PM/client already live in Details.
4. Actual input/select/search/segmented boundaries use the correct `--input` token, but existing tests do not measure adjacent rendered surfaces at3:1. Author meaningful measured checks first, without inventing a new colour or relaxing allowance. Promote only existing white/ink alpha mixes to narrow named v2 roles where necessary; preserve computed appearance.

Meaningful missing evidence: the canonical six baseline roles plus independent capability variants; all registered route families and former project group aliases; real drawer list parity, resize/history/shortcut transitions; exact120-character/unbroken title and metadata extremes; rendered control-boundary and mobile reference-block geometry. New source-serving Shell/ProjectLayout route probes may isolate the route shell from heavy page DTOs, but must explicitly distinguish that from loading the actual destination pages/API permission proof.

Still unresolved/outside this loan:

- Search, Notifications and Preferences intentionally retain zero rail selection pending the earlier explicit utility-policy decision. Do not fake a Tasks/Projects parent to satisfy the unrestricted FR16 sentence. NotFound/feature-denied pages are explicit non-permitted destinations. Legacy global Vacation keeps its verified unmapped baseline; enabled Resources/Time follows Jay's special case.
- Dashboard's verified normal flow versus FR11's literal overlap wording remains a recorded conflict; keep the fix. My Staff/other remaining CountCard adoption is S06 and page-owned, excluded here. No CountCard/page/ProjectLayout/planner/Reports/person-picker/API edit is authorized by this brief.
- Complete every-screen real-page/load/band ordering, permission admission, universal first-data geometry, all-page reflow, native200% zoom, full S09, formal sidebar/budget decisions and pilot cleanup require separate evidence/ownership. None follows from a route-shell fixture or a static source audit. Root decides acceptance after runtime checks.

No check/runtime/build/test/browser/helper/goal ran for this inventory. No full S02/S07/S08 completion claim.
