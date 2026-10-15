# Tuesday for iPhone — simulator workspace

Native SwiftUI companion for Tuesday's engineering workflow. **This version uses synthetic fixtures only**, as approved by Jay on 2026-10-14. No Entra registration, live API, credentials, client documents or operational records are included.

Open `Tuesday.xcodeproj`, choose the shared **Tuesday** scheme, select an iPhone Simulator and run **Debug**. Minimum target: iOS 17. Tested OS/device/build details are in [verification](../docs/ios/verification.md). There are no external packages or code-generation prerequisites.

The synthetic workspace is available only with both `DEBUG` and `targetEnvironment(simulator)`. Release builds and physical-device app builds show a corporate-sign-in configuration gate. A fixture account is not corporate authentication.

## Try the workflow

1. Enter as **Alex Morgan**. Open *Check stormwater design* from My Work.
2. Inspect project → discipline → deliverable context. Edit the task, request independent review, post a comment with a selected mention, or record `1.25` actual hours.
3. Close an unfinished editor and resume its retained draft. Relaunching keeps it for the same synthetic identity after revalidation.
4. From Account, choose **Lose the next save response**, then save a comment. The editor shows **Outcome unknown** and locks its request. **Resolve original request** returns the original acknowledgment without adding a second comment.
5. For conflict recovery, retain an edit draft, expand **Simulator conflict scenario** in task detail, simulate another person's update, and resume the draft. Compare current values before explicitly choosing the new version and saving.
6. Sign out and enter as **Sam Chen** to start a requested review. Formal review disposition hands off to the web. **Rita Singh** has a restricted read-only project view.
7. Browse Projects (Greenway includes multiple task pages), explicitly mark Inbox items read, and change My Week to see unknown capacity preserved.

Due/work dates use `America/Halifax` regardless of the phone timezone. Comment mentions are chosen by fixture identity. The fixture service enforces versions, independent review, validation, combined actual hours, account/project visibility and durable keyed replay. These checks **do not establish the behavior of the existing ASP.NET API**.

The fixture database and account-scoped drafts are stored in the app's Application Support directory. Files request Complete protection and backup exclusion. Simulator hardware encryption/data protection remains unverified. Diagnostic output contains no comment bodies or tokens. Debug launch argument `--reset-fixtures` resets only this app's local synthetic workspace. To reset manually, remove the app from the simulator and run it again.

## Focused checks

```sh
xcodebuild test -project ios/Tuesday.xcodeproj -scheme Tuesday \
  -destination 'platform=iOS Simulator,id=<UDID>' \
  -parallel-testing-enabled NO
```

Use `-only-testing:TuesdayTests` for fixture/state regressions, or select a specific `TuesdayUITests` workflow. Native UI tests reset the synthetic workspace, interact with the app, assert results and keep screenshots. The physical-device file-protection test is deliberately skipped by Simulator.

## Production integration boundary

The original [iPhone specification](../docs/ios/spec.md) and [full acceptance gate](../docs/ios/acceptance-gate.md) remain proposed production scope. The live integration still needs:

- approved Entra native public-client registration, MSAL, tenant/API scope, bundle ID and signing/distribution decisions;
- live endpoint DTOs, server-provided permissions and filters, problem details and permission revalidation;
- the shared backend's atomic, payload-bound and authorization-safe receipt prerequisite before mobile writes;
- hosted HTTPS, associated-domain entitlements/AASA and real-device auth, VoiceOver, interruption and link tests;
- IT handling approval and human workflow acceptance.

The fixture service is not a mobile backend or production rules engine. Replace it with the existing Tuesday API boundary when those prerequisites are available; do not ship fixture behavior as corporate integration.
