# S04 shared CommandForm guard

Please add optional `submitDisabled?: boolean` to `CommandForm` in `web/src/pages/projects/CoordinationForms.tsx`; the existing Confirm button should be disabled when `busy || submitDisabled`. Default false/undefined preserves all current callers and server error/idempotency behavior.

Reviews.tsx will pass this only for the enabled review assignment form while read-only guidance for its currently selected source IDs is pending/unavailable, or a selected reviewer is excluded by the canonical author set. This prevents the user saving from a form with unresolved eligibility facts and does not change server permissions/refusals. The owned new guidance GET uses existing source publication/current-head/author/person rules, scoped to the authorized project. Existing ValidateRound remains the authority for Start.
