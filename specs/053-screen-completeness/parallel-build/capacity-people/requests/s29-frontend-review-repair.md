# S29 exact frontend P2 repairs — READY / held, runtime UNRUN

Root opened a bounded repair of Admin.tsx UserDialog callback, person-facts-editor.tsx callback/create acknowledgements and capacity-person-facts.cjs only. Those edits are finished and held; all other S29 sources and every C#/API test remained held. No process is in flight.

## Older legacy Admin form cannot borrow a newer facts version

PersonDatesForm captures `priorVersion = data.rowVersion` before awaiting the PATCH and uses that exact value for If-Match. Its callback propagates `(nextVersion, priorVersion)` through PersonFactsEditor. UserDialog advances its legacy version only in a functional setter when the legacy baseline still equals priorVersion. Otherwise the old version remains and the canonical legacy PATCH conflicts. Local legacy edits are not discarded or silently refreshed. Equal-baseline happy-path advancement remains supported.

Focused authored browser case preserves the existing successfulv1→2 path, including legacy Save atv2. Then a fresh page loads legacyv2, while a deliberately held child GET simulates another Admin's office/capacity/activation correctionv3. The child saves only dates usingv3→4. Legacy still sendsv2, receives409, and leaves the newer office,26h capacity and inactive state intact; its old null/null/true body is explicitly checked so a version-only bypass would fail this case. The held read avoids timing sleeps and query-cache assumptions. No backend change.

## Create parsing/shape failures remain unknown

Create validates a nonzero UUID-format saved ID and a **nonnegative Int32 row version (0..int.MaxValue)** before assigning savedId, setting known-saved state or showing success. Canonical newly created UserCertification starts at0; no backend version change is needed or made. The browser create/receipt fixture now starts at0, explicitly proves that valid0 acknowledges and unlocks after read, and carries0 through exact child edit/removal assertions. A truncated JSON SyntaxError, invalid acknowledgement shape or transport error remains unknown: preserve the original body/key, disable input changes and retry that exact intent. A valid retry acknowledgement plus authoritative read resolves the lock. It cannot acknowledge undefined ID or get stuck in the known-saved branch for malformed201{}.

Four authored response cases: truncated201 JSON,201{}, valid ID with fractional rowVersion and invalid ID with otherwise valid version. Each commits one synthetic server record before the bad response, asserts unknown/locked/no false Saved, retries identical body/key, then verifies one record and unlocked form after the valid receipt. Existing lost-response case is retained. The ReadOnly fixture now has a non-Admin ReadOnly role with all mutation/Admin capabilities off, then restores Admin before corrections/vocabulary. Real backend role-revocation evidence stays root-owned and unchanged.

## Root seam and static evidence

Latest s29-ui-locales.json has **35** keys: one additional `personFacts.responseUnknown` = "The server response could not confirm the saved record." Root must merge the new key; earlier34 are unchanged. No shared locale file edited here.

Final node --check for capacity-person-facts.cjs PASS; TS syntax-only parse for the two changed TSX files PASS; tracked scoped git diff --check PASS. No build/test/browser/typecheck/generator/migration/runtime/helper ran. Root owns independent re-review and final execution; these authored cases are not runtime proof. **APP/ALL SOURCE READY, C# FROZEN21 retained.**
