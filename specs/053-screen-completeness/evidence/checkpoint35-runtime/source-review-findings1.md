# G63 independent source review findings, before final repair

Contract: retained legacy Roles.Examples.ResultKind must remain Permission/Condition for older open clients; root repaired and regression + final21 passed.

S/W review: W31 meeting create/edit actually use RunCoordination; no meeting-cancel route. W13 issued uses a dedicated issue command allowing manager In Review, not generic DeliverableStep. S15 report-team changes use ManageTeam OR StaffOnProject, not sole StaffOnProject (Admin/PM Complete distinction).

C/P review: C04/C09 coordinate checks need stored package Coordinator rather than generic Owner; P06 copy needs target-person CreatePlanningEntry, not source edit ownership. C07/C08 missing notice close/assign/cancel; C12/C13 missing basis conflict resolution (confirmed new replacement, two versions/same-discipline and reason checks).

These are source findings with final runtime checks pending. No full83/equivalence/Accepted claim. Existing schema/API record enforcement is unchanged except separately verified Planning replay current-authority repair. Root will integrate one bounded batch after reviewers finish.
