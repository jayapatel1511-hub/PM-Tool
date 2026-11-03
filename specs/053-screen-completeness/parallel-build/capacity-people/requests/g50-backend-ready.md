# G50 D BACKEND READY — C# source held

Authored src/Hub.Api/Features/PersonPanel.cs, MemberResponsibility.cs, Team.cs optional read bridge, tests/Hub.Tests/Api/PersonPanelTests.cs (10 Facts), and only the additive-field Legacy normaliser in TeamCompletenessTests.cs. No runtime/build/test/generator/migration ran. Tracked diff whitespace check passed; new files reviewed as source only.

PersonPanelEndpoints.Map(api) GET people/{userId}/panel: people_screens, RR snapshot, one captured UTC instant/local day/week, self/Admin/current visible membership or validated actual Staff cohort, explicit direct/all/reports/unit context, only visible nonremoved project memberships with25/max100 stable paging. Minimal DTO excludes email/private work; org and S29 helpers reused with captured day.

MemberResponsibilityEndpoints.Map(api) PATCH projects/{projectId}/members/{memberId}/responsibility: strict required nullable note body; trim80/control validation; same-project active actual-primary lead check; root permission helper; member-version428/409; standard Complete reason/read-only/status; normal audit; no generic role-PATCH changes. Team exposes optional ResponsibilityRead(Note,CanEdit,EditReason) only people_screens.

Root seams required before compile: internal PersonRow return record for the newly internal MyWorkEndpoints.StaffScope; module maps; optional Workspace.TeamProjectRow ResponsibilityNote bridge from same current member; migration/model/audit ownership stays root. Root's SetMemberResponsibility and PersonFacts DateOnly overload already used. Root review can request exact fixes; D holds all C# until explicit reopen. Frontend/source browser tests continue under generation50.

Focused cases cover dark omission/no mutation; Rita visible-only/minimal wire; hidden/removed membership404 and self; explicit Staff authority/invalid/flag scopes; actual unit leader; stable capped/out-of-range page; org-day/cert flag; PM trim/clear/one audit/no role-capacity mutation; own active-primary DL vs wrong DL/supervisor/ReadOnly/null-primary/inactive; strict bodies/version/wrong-project/removed; Complete reason/Archived refusal. Runtime status UNPROVEN until coordinator executes.
