# Discipline handoffs

A handoff records one input sent from one discipline to another for a stated use: the exact revision, who sends it, who
receives it, what it must satisfy and when it is needed. Project → **Handoffs**. Back to the [user guide](../user-guide.md).

Accepting a handoff confirms the input is usable for the stated purpose. It is not technical approval, and it does not
complete or unblock the receiving task.

## States

**Draft** → **Submitted** → **Accepted** → **Incorporated**. From Submitted the receiver can instead request
clarification (**Clarification Requested**) or return it (**Returned**); the sender then resubmits. Any handoff that is
not Incorporated or Cancelled can be **Cancelled**.

## Who can do what

| Action | Who |
|---|---|
| **New handoff** | PM, the lead of the sending or receiving discipline, or the source deliverable's owner when the sending discipline is their primary discipline |
| **Edit draft** (Draft, Clarification Requested, Returned) | Sending owner, PM, or either discipline's lead. Changing an owner needs the PM or a lead, and a reason |
| **Submit input**, including resubmitting | Sending owner only |
| **Accept for use**, **Request clarification**, **Return input**, **Record incorporation** | Receiving owner only. The receiver cannot be the sender, or anyone who earlier submitted it |
| **Reassign owners** | PM or either discipline's lead, with a reason |
| **Cancel handoff** | PM or either discipline's lead, with a reason |

A step you may not take is shown greyed out; point at it to see why.

## Create a handoff

1. Start from one of these:
   - the source deliverable's panel → **New handoff** (fills the source, revision, link and sending owner);
   - the receiving task's panel → **New handoff** (fills the receiving discipline, work, owner and needed date);
   - **Handoffs → New handoff**.
2. Complete **Handoff** (a title), **Source deliverable**, **Declared revision**, **Exact source link**, **Receiving
   discipline**, **Receiving work** (a task or deliverable of that discipline), **Sending owner**, **Receiving owner**,
   **Intended use and scope**, **Acceptance criteria** and **Needed by**.
3. Enter **Promised by** now or later; it is needed before you submit.
4. Select **Save draft**.

If **Promised by** is later than **Needed by**, the handoff shows "Promised after needed date". Both dates are kept and
no other date moves.

## Send it (sending owner)

1. Open the handoff and select **Submit input**. A note in **Reason or response** is optional the first time and
   required when you resubmit after a clarification request or return.
2. The declared revision and link are preserved as a submitted revision under **Preserved submission revisions**.
   After the first submission the receiving work, intended use and acceptance criteria can no longer be edited.

"The source record changed. Refresh and confirm its revision before submitting." means the source deliverable was
edited after you saved the draft. Open **Edit draft**, check **Declared revision** and **Exact source link**, select
**Save draft**, then submit.

## Receive it (receiving owner)

1. Open it from the notification, or set **Direction** to **Incoming to me**.
2. Use **Open external source** to check the input. Tuesday does not fetch the file or check that the link holds that
   revision.
3. Choose one:
   - **Accept for use**, and describe **How the acceptance criteria were met** (the criteria are shown as a hint).
   - **Request clarification** or **Return input**, with a reason.
4. Once you have used the input, select **Record incorporation** and describe **What was incorporated into the receiving
   work**. This records the revision as the input used by the receiving work (see [change impact](change-impact.md)).

**Record incorporation** is refused when the receiving deliverable is already Issued or Accepted, or when a newer
revision of the source is registered, unless a change assessment approved keeping the older revision.

## When the source changes

Publishing a newer revision of the source (see [change impact](change-impact.md)) keeps the accepted or incorporated
revision as recorded and creates a pending assessment on the receiving work. The handoff then lists it under **Affected
work and assessments**. A row marked "Source record changed or is unavailable" means the source deliverable has
changed or been removed since the handoff was saved; confirm the intended input before going on.

## Effect on other records

- The receiving work's readiness **Handoff** check is satisfied only when all its handoffs are Accepted or Incorporated.
- An unissued submission that contains the receiving work returns to Checking whenever its handoff changes, and cannot
  be issued until the handoff is Accepted or Incorporated.

## Find handoffs

- Filters: **Search**, **Direction** (**All**, **Incoming to me**, **Outgoing from me**), **Status**, **Discipline** (either
  side) and **Overdue only**. "Past needed date" means the needed date has passed and the handoff is not Accepted,
  Incorporated or Cancelled; it is not flagged while the project is On Hold.
- "An owner is inactive or no longer an eligible project participant. A PM or discipline lead must reassign." means use
  **Reassign owners**.
- **Views** saves the filters. **Export** gives Excel or CSV of the filtered list.
- **Receipt history** on the handoff lists every step with who, when, the reason and the revision.
