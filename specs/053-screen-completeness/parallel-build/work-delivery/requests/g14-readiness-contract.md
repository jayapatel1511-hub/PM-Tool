# Generation14 — root integration request

Read-side additions only, independently gated by `screen_patterns`:

- `ReadinessWindow.Work?` omitted off: `ReadinessWorkPage(Scope, EvaluatedAt, Items, Total, Page, PageSize)`. Scope `DatedOpenWork` uses the existing selected due-date window, terminal/held/undated exclusions. Independent `workPage` query; pageSize is the existing shared size. Existing constraint/Ready fields and paging remain.
- `ReadinessWorkRow(TargetType, TargetId, Key, Name, DueDate, OwnerId, DisciplineId, State, HasAssessment, Available, Presentation)`.
- `ReadinessDetail.Presentation?` omitted off: `ReadinessDeliveryPresentation(HasAssessment, Available, State, EvaluatedAt, Unknown, Blocked, Checks, Inputs, Applicability)`.
- `ReadinessDeliveryCheck(Code, Applies, Satisfied, Reason, Unknown, Blocking)`. These are aggregate canonical check facts; inputs are not individually declared failed.
- `Inputs` reuses `WorkReadinessInput` including direct-target versus constraint relationship and labelled linked source. Existing `WorkReadinessInputs`/`WorkReadinessTask` JSON fields unchanged.
- New presentation-only `ConstraintOwners`: `ReadinessDeliveryConstraint(Id, RemovalOwner:RecordContextPerson, AffectedOwner:RecordContextPerson)`. Exact same-project active constraints, with current referenced-person metadata; removal owner remains separate from the state-dependent next actor. Missing, inactive, removed and otherwise unavailable identities are explicit. No Task/Input contract addition.
- `ReadinessApplicabilityActors(Role="PMOrDisciplineLead", State=Single/Multiple/Unknown/Refused, Candidates)`; candidate `(Person:RecordContextPerson, Allowed, Refusal)`. Current primary/current member PMs and responsible active discipline lead only; actual canonical Actor/ProjectContext permission plus existing target eligibility. Viewer command guards stay separate.

Root metadata/query keys: window `workPage` is an ordinary generated endpoint query (positive integer). `inspectCheck` is an ephemeral enabled UI selection of canonical readiness check code alongside existing inspect/inspectWork. Root inspected the existing from/to-only saved-view keys and confirmed no registry/saved-view addition. Changing from/to resets workPage with the other pages. No new endpoint, entity kind, source state, command, migration or error text.

Root owns generation/API fixtures/type checks, metadata and locales. The exact locale request is adjacent `g14-readiness-locales.json`.
