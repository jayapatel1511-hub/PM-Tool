# Multidisciplinary reviews

A review package asks each required discipline to review a fixed set of deliverable revisions, and keeps every finding
open until it is independently verified. Project → **Reviews**. Back to the [user guide](../user-guide.md).

Before you start, the deliverables' current revisions must be registered in **Changes** (see
[change impact](change-impact.md)); a handoff submission also registers one. Otherwise the form says "No current
deliverable revisions are registered. Register them in Changes first."

## States

- Package: **Draft** → **In Review** → **Changes Required** or **Approved**. Earlier rounds become **Superseded**; a
  package can be **Cancelled**.
- Each required discipline: **Pending**, **In Review**, **Changes Required**, **Approved**.
- Each finding: **Open** → **Responded** → **Verified Closed** (or back to **Open**); or **Withdrawn**.

A package is **Approved** only when every required discipline has approved and every **Blocking** finding is Verified
Closed or its withdrawal has been acknowledged. **Advisory** findings do not hold approval.

## Who can do what

| Action | Who |
|---|---|
| **New review package** | PM, or the lead of the package's discipline |
| **Start review**, **Start a new round**, **Reassign reviewer**, **Reassign resolver** | The coordinator, the PM, or the lead of the package's discipline |
| Drop a required discipline or deliverable in a new round | PM only, with an impact statement |
| **Reassign coordinator**, **Cancel package** | PM, or the lead of the package's discipline |
| **Record decision** | The named reviewer for that discipline, once the review has started |
| **Add finding** | A named reviewer in the current round, for their own discipline |
| **Record response** | The finding's resolution owner |
| **Verify closure**, **Return to open**, **Withdraw finding** | The finding's verifier: the reviewer who raised it, unless reassigned |
| **Acknowledge withdrawal** (Blocking findings) | The coordinator |
| **Reassign verifier** | PM only. The new verifier must have the Reviewer role on the project or be a reviewer in this round |

Reviewers must be independent of the work: not the deliverable's owner or creator, and not an assignee or creator of its
tasks. A resolution owner cannot be the reviewer who raised the finding, and a verifier cannot be anyone who authored the
work or responded to the finding.

## Create and start a package

1. **Reviews → New review package**.
2. Enter **Title**, choose **Discipline** (one you manage) and **Coordinator**, and describe **Review purpose and scope**.
3. Under **Revision manifest**, tick the current revisions to review. The manifest is fixed for this round.
4. Under **Required disciplines**, tick each discipline and choose its **Reviewer** and **Review due date**.
5. Leave **Require this package to be approved before these deliverables can be issued** ticked if issue must wait for
   this review. A deliverable already gated by another package can be moved only by the PM, with a reason.
6. Select **Confirm**. The package is a Draft; reviewers are not told yet.
7. Open the package and select **Start review**. The reviewers are notified.

## Review (named reviewer)

1. Open the package and select **Record decision** on your discipline's card.
2. Choose **In Review**, **Changes Required** or **Approved**, and give a **Rationale**.
3. To raise a finding, select **Add finding**: choose the **Affected source revision**, your **Discipline**, a
   **Resolution owner** and the **Severity** (**Blocking** or **Advisory**). Optionally link an **Existing issue**, then
   describe the **Finding**. You become its verifier.

## Close a finding

1. The resolution owner selects **Record response**, explains it in **Reason or response** and gives an **Evidence link**.
2. The verifier selects **Verify closure**, or **Return to open** with a reason.
3. The verifier can **Withdraw finding** with a reason. A withdrawn Blocking finding still holds approval until the
   coordinator selects **Acknowledge withdrawal**.

Each finding's **History** shows every step with who, when and why.

## New rounds

- Changing the manifest or the required disciplines needs **Start a new round**, with a reason. Decisions start again
  at Pending; unresolved findings are carried forward ("Carried forward from an earlier round; earlier responses remain
  in its history."). The new round is a Draft; its reviewers are notified when you select **Start review**. To change
  only a reviewer, use **Reassign reviewer**; that discipline's decision returns to Pending.
- Dropping a required discipline or a deliverable needs the PM and an entry in **Impact of removing required
  disciplines**.
- Publishing a replacement for a revision in the manifest starts a new Draft round automatically, with the new
  revision, and notifies the reviewers and the coordinator. Select **Start review** again.
- Pick an earlier round in **Review round and history**. "This earlier round is preserved and cannot be edited."

## Effect on issue and submissions

- A deliverable gated by a package shows **Required before issue** on its panel. **Ready to Issue** and issue are refused
  until the package is Approved and the revision being issued matches the approved manifest.
- Readiness uses the same package for a deliverable's **Review Gate**, and unissued submissions return to Checking when the
  package changes.

## Find packages

- Columns: **Item**, **Coordinator**, **Status**, **Round**, **Outstanding disciplines**, **Blocking findings**, **Days
  waiting** (since the round started, while In Review or Changes Required).
- Filters: **Search**, **Status**, **Coordinator**, **Discipline**, **Assigned to me** (you coordinate it or review in the
  current round). **Views** saves the filters; **Export** gives Excel or CSV.
