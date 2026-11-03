# FR50 picker coverage — read-only inventory under ALL SOURCE FROZEN22

**FR50 is not yet completely covered.** The standard directory-backed PeoplePicker path has the new notices, but its one actual custom-candidate caller lacks them. Several separate native person choosers also lack or discard notice facts. A successful directory-picker browser case cannot establish all-picker acceptance or justify checking the entire S29 slice.

No C#/TS/CSS/script/API-test/metadata/fixture or other app file changed for this inventory. No app/test/build/browser/typecheck/generator/migration/helper executed. Only this own note/status changed. The zero-version guard/fixture repair remains READY and all app sources held.

## Exhaustive shared PeoplePicker call-site check

Static TypeScript JSX parsing of current web/src (excluding generated files) found **43 PeoplePicker JSX call sites in23 files**:42 with no custom candidates prop and exactly1 with it. No JSX spread attributes occur on those43 instances, so there is no hidden candidates prop supplied through a spread at those call sites. These are static implementation sites, not runtime user/person counts or browser test coverage counts; InlinePerson expands across many rows.

### Directory-backed42 sites — notices present and mapped in shared component

`hub/people.tsx:82–118` uses GET `/users` only when open and no custom candidates array. Existing project/search/exclude/ranking/limit behavior remains. API `Features/Me.cs:85–111` retains current selection predicates/order/limit, then calls ReadNotices only for the returned authorised candidate IDs. While capacity_screens is on, PickerUser.personNotices supplies nullable PersonType/StartDate/EndDate plus server-derived Starts/Ends. These are rendered by PersonDateNotice; null/omitted facts stay unknown, no certs are fetched, and dates do not change eligibility. Off keeps the old query key/name DOM and omits notices. The standard enabled query has the capacity_screens key suffix.

| Source | Current JSX lines | Input |
|---|---|---|
| components/hub/fields.tsx |314| InlinePerson, standard directory |
| components/planner/EntryPanel.tsx |63| Planner person, standard |
| pages/Planner.tsx |125(two sites)| Person filters, standard |
| pages/Portfolio.tsx |264| Standard |
| pages/ReassignWork.tsx |62| Standard |
| pages/Reports.tsx |196| Standard |
| pages/Staff.tsx |235| Standard |
| pages/Time.tsx |114| Review-person filter, standard |
| pages/Workload.tsx |149,265,454| Standard |
| pages/WorkspaceTasks.tsx |107| Standard |
| pages/projects/Coordination.tsx |183| Standard |
| pages/projects/CreateProject.tsx |138,150| Standard |
| pages/projects/Decisions.tsx |341,393,591,595,630| Standard |
| pages/projects/Deliverables.tsx |254,301,303| Standard |
| pages/projects/FromTemplate.tsx |171| Standard |
| pages/projects/Meetings.tsx |90,398| Standard |
| pages/projects/ProjectActivity.tsx |70| Standard |
| pages/projects/ProjectList.tsx |251| Standard |
| pages/projects/ProjectSettings.tsx |79| Standard |
| pages/projects/Registers.tsx |413,446| Standard owners; custom verifier below is separate |
| pages/projects/TaskPanel.tsx |475| Standard; separate pickup native select below |
| pages/projects/Tasks.tsx |273,590,780,813,883,884| Standard |
| pages/projects/Team.tsx |70,163,208| Standard lead/add/reassign controls |

Static mapping is implemented at these shared sites; only the authored directory case and root's eventual executed evidence prove actual browser behavior. Unknown facts remain legitimate unknowns even on a covered directory row.

### The one actual custom candidates prop — definite missing bridge

`pages/projects/Registers.tsx:1016–1017`, IssueMetadata verifier appointment:

```tsx
candidates={eligible.data?.people.filter(p => me.settings.allowSelfReview || p.id !== issue.ownerId && p.id !== issue.raisedById)}
```

- Source at Registers.tsx:950–951 is GET `/projects/{projectId}/changes/options`, not an issue-specific eligible-verifier endpoint. Its local query key is issue-verifier-eligible/projectId.
- `Features/Changes.cs:47` CoordinationPerson is currently exactly `(Guid Id,string DisplayName)`; Options at341–355 projects only those fields from `Coordination.People(db,p)` after project access. No PersonType/start/end/Starts/Ends/personNotices field exists.
- `.filter()` preserves all source fields, but there are presently no date facts to preserve. Shared PeoplePicker intentionally disables `/users` reads when a custom array exists. Thus all FR50 notice inputs remain **unknown/not supplied** here, and no Starts/Ends can render, even when a person's dates are recorded.
- Existing self-review exclusions must stay unchanged. No generic directory/batch fetch may fill the missing fields or expand/reorder this candidate cohort.

Small proposed follow-up, requiring a root-authorised C# window: optional null-omitted PersonNotices on CoordinationPerson, joined via PersonFacts.ReadNotices only after the existing authorised Coordination.People cohort is fixed, with SettingsStore/clock and capacity-only snapshot/flag handling. The current filter and shared PeoplePicker would then preserve/render those facts without an extra lookup. Preserve exact off bytes, permissions, ordering and self-review rules. This is a proposal only; no source change was made.

## Other person selectors outside the shared PeoplePicker contract

These are actual native/select-choice paths, so broad "all pickers" wording must also account for them or explicitly scope them out. This is an evidence-backed extension inventory, not a claim that every possible name-bearing reference is a picker.

| Chooser | Candidate source | Present versus missing/mapped |
|---|---|---|
| Task pickup assignment, TaskPanel.tsx:149 | TaskPickup.cs:22 Candidate(Id,DisplayName), Detail's eligible same-team array at205–212 | No notices at all; native option renders displayName only. Preserve canAssign, active/same-team/existing TaskPickup eligibility if later bridged. Pickup.tsx declares a compatible candidates shape at25 but its own page only links to task assignment; it does not itself render that candidate array. |
| CoordinationForms.tsx:27 peopleChoices | CoordinationOptions.people / CoordinationPerson | No dates in source today. Mapper returns only value/label, so merely adding metadata to the source would still not render native option notices. Used by Changes.tsx, Reviews.tsx, Submissions.tsx and CoordinationRegister.tsx, including reviewer/coordinator/resolver/verifier/owner selections and filters. Existing reviewer/self-review exclusions remain authoritative. |
| DesignBasis.tsx:187,190,299,301,360 | Same CoordinationOptions.people | Direct maps to value/id + displayName only; dates absent and not rendered. |
| Allocations.tsx:155,263 | Same CoordinationOptions.people | Person filter and proposal person select; dates absent and not rendered. No eligibility/calendar/capacity change is permitted by adding notices. |
| ReadinessForms.tsx:222,283 | Same CoordinationOptions.people | Removal-owner and exception-verifier arrays filter existing owners/actor then map only ID/name. Notice fields absent; preserve those exclusions. These person selections are distinct from the assumption candidates prop excluded below. |
| Handoffs.tsx:134–135 PersonSelect | HandoffOptions.people, backend HandoffPerson(Id,DisplayName), Handoffs.cs:37 | No notice DTO; native labels only. Must retain existing handoff party/authority cohort. |
| Admin.tsx:332,381 supervisor selects | Existing AdminUserRow arrays | Root's capacity-owned personFacts.notices is present in the DTO, but these options render only u.displayName; available facts are **discarded/not mapped** to labels. A UI-only permitted bridge may be sufficient, with flag-off cache/DOM handling and active/self exclusions preserved. |
| admin/Structure.tsx:27 unit leader select | GET /organisation, org.data.people | Separate organisation-person DTO has no PersonFacts/PersonNotices bridge; options render displayName only. Facts **not supplied** here. Preserve allowed leadership options. |
| DisciplineCoordinationView.tsx:97 owner filter | Existing project Team members from project/team | Team member personFacts may already contain notices, but options render only displayName; **present upstream, not rendered**. Preserve existing permitted-owner scope. |
| WorkspaceCoordination.tsx:135 owner filter | Workspace discipline-coordination result Owners; backend WorkspaceOwnerChoice(Id,DisplayName), DisciplineCoordination.cs:70,135 | Separate already-authorised source has no notices; native options displayName only. Facts **not supplied**. Preserve the actual workspace/project/discipline owner cohort. |
| Vacation.tsx:95 HR-identity person select | Availability HrIdentity(Id,DisplayName,HR mapping fields,version) | No date notices in this separate Admin identity DTO; no implicit HR/employment inference. Its inclusion in the product's FR50 picker scope needs an explicit coordinator decision before widening HR DTOs. |

`ReadinessForms.tsx`'s candidates prop is an assumption/version list, not person candidates for PeoplePicker. `DesignBasis.tsx`'s local candidates is confirmed basis versions. `readiness-delivery-presentation.tsx`'s applicability candidates are read-only actor facts, not a selection control. They are excluded from the43 shared-picker count and must not be mistaken for date-notice coverage.

## Acceptance boundary and next step

- **Implemented/static covered:** server-derived notices on the standard `/users` path and shared PeoplePicker renderer; no qualification data or arbitrary fill-in GET.
- **Definite FR50 build gap:** the one custom PeoplePicker verifier list. This alone prevents a full all-PeoplePicker claim.
- **Additional native chooser gaps:** sources above either lack notices or discard available notices. Root must settle the intended picker scope and open bounded authorised-ID DTO/presentation loans, preserving canonical candidate rights and off behavior, before claiming every person picker.
- **Runtime:** this inventory performed no browser/API execution. Prior root backend/cache passes and future Chrome results remain root evidence, separate from this static inventory. Do not check full S29 solely from the directory case or these notes.
