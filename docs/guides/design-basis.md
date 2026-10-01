# Design basis and assumptions

The register holds the criteria (authority or client requirements) and the assumptions a project designs to, in numbered
versions, and the exact version each task or deliverable uses. Entries are recorded text and values, not calculations.
Project → **Design basis**. Back to the [user guide](../user-guide.md).

## States

Each version is **Proposed**, **Confirmed**, **Superseded** (replaced by a newer confirmed version) or **Withdrawn**. A
confirmed version never changes; a change needs a new version. An unconfirmed assumption is never shown as confirmed.

## Who can do what

"PM or lead" means the PM or the lead of the entry's responsible discipline.

| Action | Who |
|---|---|
| **New basis entry** | PM or lead, naming any owner and approver. A team member can create one in their primary discipline, as its owner and without an approver |
| **Edit proposal** | The entry's owner, or the PM or lead. Only while Proposed, and before any work links it or a proceed disposition exists |
| Reassign owner and approver (the **Accountable owner** button, before the first confirmation) | PM or lead |
| **Confirm version** | The lead of the responsible discipline, or the named **Independent approver**; not the entry's owner. Being PM is not enough |
| **Propose replacement**, **Withdraw version**, **Proceed under assumption**, **Resolve conflict** | PM or lead |
| **Link consuming work** | The owner of the task or deliverable |
| **Decide impact → Adopt new version** | The owner of the consuming work |
| **Decide impact → Unaffected by change** | PM or lead, who is not the consuming work's owner |

## Create an entry

1. **Design basis → New basis entry**.
2. Choose **Kind** (**Criterion** or **Assumption**) and enter the **Title**. PMs and leads also choose the **Accountable
   owner** and, optionally, an **Independent approver**.
3. Choose the **Responsible discipline** and enter **Location or system scope** and **Value or statement**.
4. For a number, enter **Numeric value** and **Units**.
5. Record the source: **Source system**, **Stable source ID**, **Source link**, **Declared revision**. Add a
   **Confirmation due date** for a provisional entry, and a **Source decision** if a decision set it.
6. Select **Save**. The entry is Proposed.

If an entry with the same kind, title, discipline and scope exists, the form says "An entry with this title and scope
exists. Open it before acknowledging a separate entry." Use **Open existing entry**; to save anyway, tick **I inspected
the existing entry and need a separate record**.

## Confirm it

1. Open the entry and select **Confirm version**.
2. Give a **Rationale** and select **Confirm**.

Confirmation is refused without a source system, declared revision and source link ("Source evidence and rationale are
required for confirmation."), and for a number without units ("Numeric design criteria need explicit units before
confirmation."). Fix a proposal with **Edit proposal** and a rationale.

## Link the work that uses it (work owner)

1. Open the entry and select **Link consuming work**.
2. Choose the **Version**, your **Task or deliverable** and the **Intended use**, then **Confirm**.

You can link the current confirmed version, or a Proposed assumption that has an unexpired **Proceed under assumption**
for you. Each piece of work links an entry once. The entry lists the link under **Consuming work** with "Current use" or
"Historical use".

## Change or withdraw it

1. **Propose replacement** (PM or lead) with a rationale. This needs a confirmed current version, no other open
   proposal and no pending impact assessments.
2. Confirm the new version as above. The old version becomes Superseded, and each consuming work gets an impact
   assessment. Its recorded version stays the old one until it is adopted; the entry shows "Uses superseded version".
3. **Withdraw version** (PM or lead) with a rationale also creates an assessment for each consumer. A confirmed version
   cannot be withdrawn while a replacement is proposed.
4. Reopening a linked source decision (from Decided to Pending) also creates an assessment for each consumer.

## Decide the impact

Under **Impact assessments**, select **Decide impact** on a pending assessment:

- The work's owner chooses **Adopt new version**; the work then records the new version.
- The PM or lead chooses **Unaffected by change**.

Both need a **Rationale** and an **Evidence link**. After a withdrawal or a reopened decision there is no new version to
adopt, so only the PM or lead can decide it. Until it is decided, the work's readiness **Basis** check is not satisfied.
Work that uses a withdrawn version stays unsatisfied on **Basis** even after the decision, because the version it uses
is no longer confirmed.

## Proceed under an assumption

For a Proposed **Assumption** that work must use before it is confirmed:

1. Select **Proceed under assumption** (PM or lead).
2. Choose the **Accountable owner** who may proceed (not yourself), the **Expires on** date and a **Rationale**. The
   scope is the version's scope.
3. That person can then link the assumption to their work. The work's readiness can become Proceed under Assumption only
   through an approved [assumption exception](readiness.md#assumption-exceptions).

"This permits use of the unconfirmed assumption only for the named owner and scope until its expiry. It does not
confirm the assumption."

## Conflicts

When two confirmed entries have the same kind, title, discipline and scope but different values, both show under
**Unresolved conflicts**, and their owners are notified. Tuesday does not pick one. To resolve, confirm a replacement
version of one entry with the agreed value, then select **Resolve conflict**, choose that **Resolution version** and give a
rationale. Both histories stay.

## Find and export

- Filters: **Kind**, **Status**, **Responsible discipline**, **Location or system scope**, **Affected task or deliverable**,
  **Overdue confirmation only** (proposals past their confirmation due date).
- Columns: **Item**, **Kind**, **Responsible discipline**, **Accountable owner**, **Current registered version** (or "Not
  confirmed"), **Latest proposal**, **Unresolved conflicts**.
- **Export** gives Excel or CSV with one row per version, including units and source details.
- Entries that come with a project template start as Proposed; no confirmation is carried over.
