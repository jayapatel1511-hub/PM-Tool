# Verification: Operations and Security Hardening

Slice builds in progress; see the build notes below.

| AC | Status |
|---|---|
| AC-OPS-01 | UNRUN |
| AC-OPS-02 | UNRUN |
| AC-OPS-03 | UNRUN |
| AC-OPS-04 | UNRUN |
| AC-OPS-05 | UNRUN |
| AC-OPS-06 | UNRUN |
| AC-OPS-07 | UNRUN |
| AC-OPS-08 | UNRUN |
| AC-OPS-09 | UNRUN |
| AC-OPS-10 | UNRUN |
| AC-OPS-11 | UNRUN |
| AC-OPS-12 | PASS (local) |
| AC-OPS-13 | UNRUN |
| AC-OPS-14 | UNRUN |
| AC-OPS-15 | UNRUN |
| AC-OPS-16 | UNRUN |
| AC-OPS-17 | UNRUN |
| AC-OPS-18 | UNRUN |
| AC-OPS-19 | UNRUN |
| AC-OPS-20 | UNRUN |
| AC-OPS-21 | UNRUN |

## Build 044-S06

Mocked review-probe copied-cookie test and a SKIP line in the probe's social mode.

- Checks on the merged tree: `python3 -m unittest tools/test_social_activation.py tools/test_pilot_operations.py` 17 passed; `dotnet build Hub.slnx` 0 errors; `dotnet test --filter LocalSessionTests|SocialAuthTests` 19 passed.
- AC-OPS-12: PASS (local). Server tests unchanged and pass. Removing the review probe's copied-cookie check fails the new mocked test (mutation checked by the builder). In social mode the probe prints a SKIP line naming the server test. The pilot probe's test is unchanged and passes.
- Assumptions: no 044 decision (D1-D11) affects this slice; the packet ships without a flag (§48.1), so no flag was added; the SKIP line is printed only with `--social` and does not count as a failure.
- Left for Jay: nothing. The next social-mode run of the review probe on homedev will print the new SKIP line.
