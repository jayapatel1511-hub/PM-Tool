# Submission readiness

A submission package assembles the exact deliverable revisions for one external issue, shows every blocker that stops
it, and records the issue once the transmittal exists. Project → **Submissions**. Back to the
[user guide](../user-guide.md).

Recording an issue does not send files or apply a legal signature: "Confirm that the external transmission already
exists. This records its destination and link; it does not send files or apply a legal signature."

## States

**Draft** → **Checking** ⇄ **Ready** → **Issued** → **Superseded** (when a replacement is issued). **Draft**, **Checking**
and **Ready** packages can be **Cancelled**. Ready is calculated: a package is Ready when it has no readiness blockers.

## Who can do what

| Action | Who |
|---|---|
| **New submission**, **Prepare superseding issue** | PM or any discipline lead |
| **Start checking**, **Replace manifest**, **Edit submission** | The package's coordinator only |
| **Record Pass** on an optional check | That check's owner |
| **Approve Not Applicable**, **Reassign** (the coordinator or an optional check's owner), **Cancel package**, **Record issue** | The project's PM: the named PM or someone with the PM role on the project. Administrator rights alone are not enough |

## Before you start

The deliverables' current revisions must be registered in **Changes** (see [change impact](change-impact.md)), and
the submission needs an existing, non-cancelled milestone.

## Assemble and check

1. **Submissions → New submission**.
2. Enter **Title**, **Purpose**, **Recipient reference**, **Coordinator**, **Milestone** and **Target date**.
3. Under **Required revision manifest**, tick one current revision for each deliverable in this issue.
4. Optionally, **Add optional check** for anything that may not apply: describe it in **What may be inapplicable?** and
   choose its **Accountable owner**.
5. Select **Confirm**, then (as coordinator) **Start checking**.
6. Work through **Readiness blockers**. They are grouped by discipline and owner, and each opens its source record:

| Blocker | Clear it by |
|---|---|
| A manifest revision is no longer the current source revision | **Replace manifest** with the current revision |
| The required independent review is not approved for this round; the linked review round is not approved; the required review does not cover this exact deliverable revision | Completing the [review](reviews.md) of this exact revision |
| A blocking review finding still needs verified closure | Verifying or acknowledging the finding |
| A required handoff has not been accepted | The [handoff](handoffs.md) into the deliverable or one of its tasks being Accepted or Incorporated |
| An affected change assessment has not been resolved with evidence | Completing the [assessment](change-impact.md) |
| An adopted design input is no longer the current revision | The work's owner adopting the current revision |
| A coordinator or checklist owner is no longer an active project participant | The PM reassigning them |
| A required deliverable is unavailable, cancelled or deleted; a manifest source link is missing or invalid | **Replace manifest** |

The package becomes Ready on its own when the last blocker clears; the checklist rows then show "Pass (live source
check)". These derived checks are re-evaluated at issue and cannot be waived.

If a blocker still says the review is not approved for this round after a new review round was approved, select **Edit
submission** or **Replace manifest** to pick up the current round.

## Optional checks

Optional checks record evidence; they do not stop the package becoming Ready.

- The check's owner selects **Record Pass** and gives an **Evidence link**.
- The PM can **Approve Not Applicable** with a reason and an evidence link.

## Record the issue (PM)

1. Send the transmittal outside Tuesday first.
2. Open the Ready package and select **Record issue**.
3. Check **Declared destination** and enter the **External transmittal link**.
4. Select **Confirm**. Every check is re-evaluated at that moment; if anything changed since you opened the package, the
   issue is refused and nothing is recorded.

The **Issued record** keeps the date, who authorised it, the destination, the transmittal link and the manifest as
issued. It never changes.

## Corrections

- **Prepare superseding issue** on an Issued package starts a new Draft from it, with a reason. When the new package is
  issued, the earlier one becomes Superseded; both keep their history.
- **Replace manifest** and **Edit submission** (both with a reason) create a new manifest version ("Manifest version
  n"), move the package to Checking and reset optional checks to Pending.
- An unissued package, including a Draft, moves to Checking when a manifest revision is replaced, its required review
  changes, a handoff into its work changes, or a design basis used by its work is confirmed, edited or withdrawn.
  Optional check evidence is cleared. These automatic changes send no notification.
- Work that keeps an older revision with approved retention (see [change impact](change-impact.md)) still shows "An
  adopted design input is no longer the current revision" here.

## Evidence

**Export evidence** downloads a JSON file with the manifests, checks, evidence, unresolved blockers and the issue
history.
