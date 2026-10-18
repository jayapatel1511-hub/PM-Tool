# Privileged switch-on procedure

This is an operator step for a separately approved activation. No operational database or flag was changed during local implementation.

1. Quiesce the ordinary HTTP/job host and take the release backup under the existing hosting runbook.
2. Use the migration identity and private connection configuration to run the release executable with `Db:MigrateOnly=true` and `Db:InitializeAuditChains=true`. It exits before HTTP/background hosting, validates UPDATE privilege, enables the setting, seals legacy rows in `(occurred_at,id)` order and creates one retroactive-count marker per chain, in one transaction. A failed run rolls back; a repeated run does not re-chain existing evidence.
3. Run the normal host with `Db:Migrate=false` under the unchanged application role. It needs SELECT/INSERT only on activity_log; do not grant UPDATE, DELETE or TRUNCATE, function execution, ownership or membership to bypass that boundary.
4. Verify every chain and retain the verification records before reopening the host to users.

Normal application writes can start a previously empty chain but cannot initialize legacy rows. The Admin flag-on command refuses an uninitialized history with an explicit maintenance instruction. Existing seals remain immutable. Reverting this schema while awaiting-confirmation records exist requires an explicit data disposition; Down never silently rewrites their state.
