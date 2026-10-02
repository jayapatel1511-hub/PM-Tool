# Revisions and change impact

Record which revision of each input your work uses. When a newer revision is published, everyone whose work is linked to
the old one gets an assessment to complete. Project → **Changes**. Back to the [user guide](../user-guide.md).

Only linked work is assessed: "Only explicitly linked work and added targets are assessed. Unlinked work has not been
checked." Tuesday never changes task dates, statuses or designs because a source changed.

## Terms

- **Current registered revision**: the latest published revision of a source (a deliverable or an external document).
- **Revision used**: the revision recorded on a task or deliverable. It changes only when its owner adopts a newer one.
- **Change notice** states: **Draft** → **Open** → **Closed**, or **Cancelled**.
- **Assessment status**: **Pending Assessment**, **Unaffected**, **Update Required**, **Clarification Needed**, **Resolved**.

## Who can do what

| Action | Who |
|---|---|
| **Register source revision** for a deliverable | PM, the lead of the deliverable's discipline, or the deliverable's owner. Naming a different accountable owner needs the PM or the lead |
| **Register source revision** for an external reference | PM, or the lead of the discipline you choose |
| **Publish change notice** | PM, the lead of the notice's discipline, or the source's owner |
| **Reassign notice owner**, **Cancel notice** | PM, or the lead of the notice's discipline |
| **Close notice** | The notice's accountable owner |
| **Record input use** | The owner of the receiving work |
| **Acknowledge receipt**, **Record assessment**, **Adopt new revision** | The assessment owner: the current owner of the affected work |
| **Reassign assessment** | PM, or the lead of the affected work's discipline |
| **Approve retaining old revision** | PM, or the lead of the affected work's discipline, who is not the work's owner |
| **Verify correction** | The assessment's named verifier |

Each assessment gets a verifier: the lead of the work's discipline, or the PM when the lead owns the work.

## 1. Register the baseline

1. **Changes → Register source revision**. Leave **Current revision being replaced (leave blank for a new source)** empty.
2. Choose **Source type**: **Deliverables** (then the **Source deliverable**, which fills most fields) or **External
   reference**.
3. Check **Accountable owner**, **Discipline**, **Source system**, **External document identifier**, **Source title**,
   **Declared revision**, **Exact source link**, **Issuer** and **Affected scope**. **Source last checked** is optional.
4. Select **Confirm**. This becomes the current registered revision. Submitting a [handoff](handoffs.md) also registers
   the source deliverable's revision.

If the source already has a registered revision, Tuesday asks you to "Select the current revision this revision
supersedes."

## 2. Record which revision your work uses (work owner)

1. Under **Inputs used by receiving work**, select **Record input use**.
2. Choose your **Receiving work**, the **Current registered revision**, the **Intended use and scope** and a **Reason or
   response**, then **Confirm**.

Recording incorporation of a handoff does this for you. To see what a task or deliverable uses, open its panel and select
**Inputs used by receiving work**, or pick it in **Receiving work** on the Changes page. Each record shows **Revision
used**, **Current registered revision** and, when they differ, "An older revision is recorded as used. Review its change
assessment."

## 3. Register and publish a replacement

1. **Register source revision** and choose the revision in **Current revision being replaced**.
2. Enter the new revision's details and **What changed and why**, **Effective date** and **Assessment due date**.
   **Confirm** creates a Draft change notice.
3. Open the notice, select **Publish change notice**, and tick any **Additional known affected work (optional)**.
4. Select **Confirm**. Publishing:
   - makes the new revision the current registered revision;
   - creates one **Pending Assessment** for each piece of work that records an earlier revision of this source, the
     receiving work of each handoff that sent one, and each item you ticked;
   - starts a new round on any review package whose manifest held the old revision;
   - returns unissued submissions that held the old revision to Checking;
   - adds a source reference impact check to resolved issues that cite the old revision with the same identifier,
     revision and link (see [issues](issues.md)).

## 4. Assess the impact (assessment owner)

1. Open the notice from the notification, or tick **Assigned to me** on the Changes page.
2. Optionally select **Acknowledge receipt**. It records "Seen" only; the assessment stays Pending Assessment.
3. Select **Record assessment**, choose the **Assessment status**, and give a **Rationale** and an **Evidence link**.
   - **Unaffected**: the change does not affect your work.
   - **Update Required**: also choose a **Correction task** that you own, and enter **Estimated effort impact
     (hours)** and **Estimated date impact (days)**. Nothing is rescheduled automatically.
   - **Clarification Needed**: the assessment stays open.
4. Then either select **Adopt new revision** (your work now records the new revision), or ask the PM or lead to
   **Approve retaining old revision** with a reason. Until one of these happens the assessment shows "Retaining the older
   revision still needs approval".

## 5. Verify a correction (verifier)

When the correction task is Complete, the named verifier selects **Verify correction** with a rationale. The assessment
becomes Resolved. The verifier must be independent of the work's owner and of the correction task's owner and creator.

## 6. Close the notice (notice owner)

An assessment is complete when it is **Unaffected** with a rationale and evidence, or **Resolved**, and the owner has
either adopted the new revision or had retention of the old one approved. Each complete assessment shows "Assessment
meets the closure requirements". When all are complete, the notice owner selects **Close notice** with a reason.
Otherwise closing is refused: "Every linked assessment needs an evidenced disposition, approved retention or adoption,
and verified correction where required."

## Reassignment

If the work's owner changes, its assessment shows "The work owner changed. A PM or responsible discipline lead must
reassign this assessment." **Reassign assessment** names the verifier, moves the assessment to the current owner and
starts it again at Pending Assessment.

## Find notices

- Columns: **Item**, **Accountable owner**, **Status**, **Assessment due date**, **Incomplete assessments**.
- Filters: **Search**, **Status**, **Accountable owner**, **Assigned to me** (you own the notice or own or verify one of
  its assessments). **Views** saves the filters; **Export** gives Excel or CSV.
- **Registered source history** lists every registered revision: current, historical or unpublished.
