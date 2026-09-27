# Verification: Dated Capacity and Project Allocations

**Date**: 2026-09-26
**State**: Pure domain arithmetic implemented; schema, API, screen and product acceptance remain open.

`dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --filter 'FullyQualifiedName~AllocationsTests' -v:q -p:WarningLevel=0 -m:1 -nodeReuse:false` passed 4/4 on the isolated Mac worktree. These pure-function tests cover the 12-versus-8 plus unlinked-3 arithmetic in AC-CAP-01, a four-hour day override and holiday capacity in AC-CAP-02, exact decimal spread and a status reset on material edit. They do not exercise persistence, permissions, concurrent confirmation, project visibility or browser behavior. AC-CAP-03 through AC-CAP-05 are not accepted on this evidence.

The next bounded work is the additive schema and versioned commands. Product acceptance remains UNPROVEN.
