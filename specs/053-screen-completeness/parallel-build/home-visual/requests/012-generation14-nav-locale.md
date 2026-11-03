# Generation14 supplemental shared nav/locale contract

Root acknowledged/applied both shared edits before dependent wiring. Shell now supplies pathname only to its visualItems callback; the scoped Vacation subtitle uses the supplied locale. Root did not edit Shell concurrently.

Before dependent wiring, request root's two small shared edits:

1. `web/src/app/nav.ts`: extend only the existing `visualAlso` callback signature to `(me: Me, pathname: string) => string[]`. Add Staff's `visualAlso: (_, pathname) => /^\/people\/[^/]+\/reassign-work$/.test(pathname) ? [pathname] : []`. Existing Time callback can ignore the second argument. Home's loaned Shell will pass the current pathname to that callback inside the **visualItems-only** map. No broad `/people` prefix, new nav destination, permission change or legacy-off alias. Staff remains governed by its existing capability gate. Tests cover an allowed exact reassign route, hidden Staff, and nearby nonmatching paths.
2. `web/src/i18n/en.ts`: add `availability.projectScope`: `Availability for people you can view across projects.` Home's Vacation-only loan will use this subtitle only when embedded in a project and visual_system is enabled, retaining the existing range/unit/user access and existing off/global subtitle. This is a scope disclosure, not a new project filter.

Home owns the listed Shell call site and other supplemental application/browser-script files. Root should finish nav/locale changes before dependent Home wiring, and avoid concurrent Shell writes until Home's READY/held acknowledgement.

For the legacy embedded route, Home will add a visual-only breadcrumb alias exported by ProjectLayout, select People only on the existing embedded Vacation URL when permitted, and keep Vacation absent from the displayed project sibling list. Existing off groups/breadcrumbs and the global Resources/Time fallback stay unchanged. Global utilities remain unparented pending the proposed-policy conflict.
