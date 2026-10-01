# Readiness, constraints and weekly promises

Readiness asks whether a task or deliverable can start: are its inputs, decisions, basis, owner, capacity and gates in
place? A weekly promise is the performer's own commitment to deliver a stated output in one coordination week. Project →
**Readiness**. Back to the [user guide](../user-guide.md).

Readiness never changes the work's status. Today you can start a task whatever its readiness; see
[coming in the review release](../user-guide.md#coming-in-the-review-release).

## Readiness states

| State | Meaning |
|---|---|
| **Needs Assessment** | At least one check is unassessed or its result is unknown. Work never assessed is never Ready |
| **Not Ready** | A required input is not satisfied, or a constraint is still open |
| **Ready** | Every check is satisfied or reasoned Not applicable, and no constraint is open |
| **Proceed under Assumption** | The only blocker is a Proposed assumption covered by an approved, unexpired [assumption exception](#assumption-exceptions) |

## The nine checks

Tuesday works each check out from the linked records every time you look. Applicability alone never satisfies a check.

| Check | Satisfied when |
|---|---|
| **Handoff** | Every [handoff](handoffs.md) into this work is Accepted or Incorporated |
| **Predecessor** | Every task it depends on is Complete (for a deliverable: every linked deliverable is Issued or Accepted) |
| **Decision** | Every decision linked as "Blocked by it" is Decided |
| **Basis** | Every [design basis](design-basis.md) version it uses is Confirmed, with no unresolved conflict or pending impact assessment |
| **Production Owner** | The work has an active owner who is a project participant. Always required |
| **Production Capacity** | The owner's dated workload fits their capacity, availability and confirmed [allocations](allocations.md). Missing estimates or dates leave it unknown |
| **Review Capacity** | For a deliverable gated by a review package: each reviewer's confirmed Review allocation fits their capacity |
| **Review Gate** | For a deliverable gated by a [review package](reviews.md): the package and its current round are Approved and include this deliverable. Marked "Requires review" with no package means not satisfied |
| **Submission Gate** | Every [prerequisite submission package](#prerequisite-submission-packages) is Issued |

Where a check has no linked source, the PM or lead records its applicability: **Not applicable** with a reason lets it
pass; **Required** keeps it unknown until a source exists. An open constraint in the Handoff, Decision, Basis, Capacity
or Review category makes the matching check unsatisfied.

## Who can do what

"PM or lead" means the PM or the lead of the work's discipline. The work's owner is the performer.

| Action | Who |
|---|---|
| **Define output** | The work's owner |
| **Record applicability** | PM or lead |
| **Record constraint** | The work's owner, or the PM or lead |
| **Propose resolution** | The constraint's removal owner |
| **Verify removal** | The affected work owner, while still the work's owner |
| **Cancel constraint** | PM or lead |
| **Approve assumption exception**, **Link prerequisite package**, **Remove link** | PM or lead |
| **Propose promise** | The work's owner, or the PM or lead |
| **Sign promise**, **Record Met** | The performer only |
| **Withdraw promise** before the snapshot | The performer |
| **Capture weekly snapshot** | PM |
| **Record Not Met**, **Withdraw promise** after the snapshot | The performer or the PM |

## Inspect readiness

1. **Readiness → Inspect readiness**, then choose the **Linked work**.
2. If it says "Readiness has not been assessed. The named performer must first define the intended output and
   completion criteria.", the work's owner selects **Define output** and enters the **Intended output** and
   **Completion criteria**.
3. Read the state, then the **Needs assessment:** and **Blocking checks:** lines. Each check shows its
   **Applicability**, **Current result** and the reason from its source.
4. As PM or lead, select **Record applicability · {check}** for a check that needs it, choose **Required** or **Not
   applicable**, and give a **Rationale** (and an **Evidence link** if you have one). **Production Owner** cannot be Not
   applicable.

## Constraints

A constraint is something that must be removed before the work can start, with one person named to remove it.

1. In the inspector, select **Record constraint**.
2. Choose the **Constraint category** (**Handoff**, **Decision**, **Basis**, **Capacity**, **Review**, **Scope** or
   **Other**), describe it, and choose the **Removal owner** (not the work's owner), **Removal needed by** and the
   **Constraint source link** (the decision, issue or handoff that is the constraint, or other evidence).
3. The removal owner selects **Propose resolution** with a rationale and an evidence link. It stays a blocker: "Proposed
   resolution remains a blocker until the affected work owner checks the evidence and verifies removal."
4. The affected work owner checks the evidence and selects **Verify removal**. The constraint becomes **Verified
   Removed** with their name and the time.
5. The PM or lead can **Cancel constraint** with a reason while it is Open or Resolution Proposed.

## Assumption exceptions

When the only blocker is **Basis** because the work uses a Proposed assumption, a PM or lead can let limited work go
ahead. First the assumption needs a **Proceed under assumption** for the performer in [design basis](design-basis.md),
and the performer must have linked it to this work.

1. In the inspector, under **Assumption exceptions**, select **Approve assumption exception**.
2. Choose the **Proposed assumption**, and enter the **Limited work allowed**, the **Risk**, a **Responsible verifier**
   (not the performer and not yourself) and **Expires on** (no later than the proceed disposition).
3. Select **Approve assumption exception**. The work becomes Proceed under Assumption.

When the exception expires or the assumption changes, the work returns to Needs Assessment. Every exception stays listed
as **In effect**, **Expired** or **Not in effect**. Review and submission gates are never overridden.

## Prerequisite submission packages

The Submission Gate links work to submissions that must be issued before it can start, such as an earlier stage's
submission.

1. In the inspector, under **Prerequisite submissions**, select **Link prerequisite package** (PM or lead).
2. Choose the **Submission package** and give a **Rationale**.

- The gate passes only when every linked package is Issued.
- A Superseded package is followed to the package that replaced it, which must be Issued.
- A Cancelled package, or one still Draft, Checking or Ready, keeps the work Not Ready.
- A package whose manifest lists this deliverable (or the task's deliverable) cannot be linked. If one later lists it,
  the gate becomes unknown: "This package now lists this output, so it cannot gate it. Remove or replace the link."
- **Remove link** needs a reason; the link stays in the list as "Removed" with who, when and why.
- With no link, the gate stays unassessed until a PM or lead records a reasoned Not applicable.
- Proceed under Assumption never overrides the Submission Gate.

## The readiness window

The page shows the coordination weeks from the current one onward: 3 by default (your administrator sets it), up to 12.
Change **From** and **To** to look elsewhere; **Clear** resets them.

- **Constraints to remove**: open constraints needed by the end of the window, including overdue ones.
- **Ready outputs**: assessed work due in the window whose readiness is Ready now.
- One section per week, **Week of {date}**, lists that week's promises. Each shows its work as a type and the start of
  an internal ID (for example "Task 01a0e4a0"); select it to open the work.

## Weekly promises

Weeks start on the project's coordination day (Project → **Settings** → **Coordination day**; Monday if not set). "Weeks
start on {day}, the project coordination day." A week recorded under an earlier coordination day keeps its start date
and appears as its own section.

### Propose

1. **Readiness → Propose promise**.
2. Choose the **Linked work**; its owner is the **Performer**.
3. Choose the **Week start** (it must be a coordination day) and a **Target date** within that week.
4. Enter the **Intended output** and **Completion criteria**, then **Save**.

A proposal stays Proposed until the performer signs it. If you propose for someone else, they are notified.

### Sign (performer)

1. Select **Review promise** on the promise, then **Sign promise**, and give a rationale.
2. Signing needs: the work's readiness is **Ready** now (Proceed under Assumption is not enough), you defined its
   output, no constraint is open, and the week has no snapshot yet.

The readiness at signing is shown on the promise. Before the snapshot you can **Withdraw promise** only while it is
still Proposed.

### Snapshot (PM)

On the week's section, select **Capture weekly snapshot** and give a rationale. The snapshot fixes that week's signed
promises; their count is the denominator from then on. After it, no promise can be proposed or signed for that week.

### Close (after the snapshot)

- The performer selects **Record Met** with a rationale and an evidence link.
- The performer or the PM selects **Record Not Met** or **Withdraw promise** with a reason, such as missing input,
  decision delay, changed scope or unavailable capacity.

The original output, criteria and date never change; **Promise history** lists every step with who and when. The week
shows "Original committed snapshot: n", "Met: x of n originally committed" and "Withdrawn later: n". A later withdrawal
never shrinks the original count. These figures describe the plan; they are not a measure of individual productivity.

On a Complete project, only the PM can propose, and the form asks for a **Reason**.
