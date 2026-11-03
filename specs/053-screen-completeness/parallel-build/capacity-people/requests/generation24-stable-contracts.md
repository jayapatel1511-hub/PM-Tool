# Generation24 D stable picker contracts

Implementing only the FR50 presentation/test loans in generation24-brief.md. Root owns all six C# read bridges, shared generated types/cache and execution.

- New pure `personOptionLabel(name, notices, enabled): string` requires an explicit boolean; off/missing notices returns the exact original name. `personNoticeWords(notices)` reuses the existing server starts/ends booleans, existing translations and `fmtDate`, with no local calendar/eligibility logic.
- `peopleChoices(options, enabled)` receives that explicit flag from each owning React component. IDs/order/filter exclusions and command payloads remain unchanged. Existing direct option maps use the same formatter.
- Expect generated DTO optional `personNotices` on CoordinationPerson, Candidate, HandoffPerson, OrganisationPersonRow, WorkspaceOwnerChoice and HrIdentity exactly as brief. Existing Admin/Team supplies `personFacts?.notices`. No certification data, extra lookup or new locale requested. Registers custom candidates retain optional property naturally and need no implementation write.
- Cache prefixes confirmed: coord-options, issue-verifier-eligible, pickup-offers, handoff-options, organisation, workspace-coordination, hr-identities, admin, project p and people. Root extends invalidation. No lane shared-key changes.
- HR identity selection currently reads its write version from the latest query cache. Notice invalidation would lend a new version to dirty old fields. The scoped presentation update will pin the version when the person is selected and advance it only from the successful HrIdentitySaved acknowledgement, preserving stale-write refusal.
- Dedicated API tests: CapacityPersonPickerNoticeTests.cs. Dedicated rendered synthetic browser checks: capacity-person-picker-notices.cjs. Source only here; root runs all tests/build/browser/generation after freezes.

No schema/settings/registry migration, feature activation, decision closure, commit/push/deploy or helper/goal work.
