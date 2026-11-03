# Root-owned narrow pre-replay seam required for S29 creates

Observed current generic IdempotencyMiddleware returns a live cached POST receipt before running the endpoint/filter. S29's per-handler flag/Admin/self guards therefore cannot make an off-flag or newly denied same-key retry404/403. Do not add a receipt schema or bypass existing stable-claim infrastructure.

D authored metadata `PersonFacts.CertificationCreateGuard(bool Admin)` only on the two certification POSTs plus `public static Task PersonFacts.AuthoriseCreateReplay(HttpContext,Access,HubDb,SettingsStore)`. It is a pure guard: no-op for every other route; checks capacity_screens, Admin target or active nonReadOnly own current target, reveals no facts. Reader interfaces remain fixed. It performs current request authority before returning a cached reply, while initial command still rechecks under its transaction/target lock.

Root exact shared patch request: in existing IdempotencyMiddleware.Invoke after non-POST/blank-key/unresolved short-circuit and before Claim/seen lookup, if endpoint has this metadata, await `PersonFacts.AuthoriseCreateReplay(ctx, ctx.RequestServices.GetRequiredService<Access>(), db, ctx.RequestServices.GetRequiredService<SettingsStore>())`. Apply only the marked certification creates; preserve the existing atomic/planning paths and all other generic receipt semantics. Root owns the middleware file; D did not edit it.

Dedicated API cases will assert identical-key replay succeeds once while permitted, the same key404 after flagoff, and proxy/self rights remain current. Root must merge this seam before the full S29 tests can pass. A handler-only or RequireFlag endpoint filter cannot intercept the existing cached-return branch.
