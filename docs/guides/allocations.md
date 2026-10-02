# Dated allocations and availability

An allocation reserves a person's hours on a project for set dates, for production work or for a review. The person's
supervisor confirms it after seeing the dated capacity. Availability records a person's hours for one date. Project →
**Allocations**, and **Workload**. Back to the [user guide](../user-guide.md).

Allocations are a staffing commitment. They never change task estimates, progress, due dates or recorded time.

## States

**Proposed** → **Confirmed**, **Declined** or **Cancelled**; **Confirmed** → **Completed** or **Cancelled**. Editing a
Confirmed allocation returns it to Proposed for the supervisor to confirm again.

## Who can do what

| Action | Who |
|---|---|
| **Propose allocation** | PM or any discipline lead |
| **Edit allocation**, **Cancel allocation**, **Complete allocation** | The PM, or the lead who proposed it |
| **Review capacity** and **Confirm allocation**, **Decline allocation** | The person's supervisor, or an Admin |
| See a project's allocations | The PM sees all; others see those they proposed; supervisors see their direct reports'; Admins see all |
| Set a person's daily availability or weekly capacity | Their supervisor, or an Admin |
| Open **Workload** | Admins, Executives, Supervisors and holders of the Project Manager system role |

## Propose an allocation

1. Project → **Allocations → Propose allocation**.
2. Choose **Purpose**: **Production** (linked to tasks) or **Review** (linked to review assignments).
3. Choose the **Person**, the dates (**From**, **Through**) and the total **Hours**.
4. Under **Linked work**, choose each item and its **Linked work date**:
   - Production: the person's open assigned tasks ("No assigned open tasks for this person." if none).
   - Review: the person's current, unapproved review assignments, with **Linked review effort (hours)** for each.
   - **Add linked work** for more.
5. Optionally, under **Specific day hours**, **Add day** to fix the hours on a date. Other hours spread over the
   person's working days in the range.
6. Select **Confirm**. The person's supervisor is notified.

## Confirm or decline (supervisor)

1. Open the allocation from the notification, or from the project's **Allocations** list.
2. Select **Review capacity**. For each date the table shows **Available**, **Existing committed**, **Resulting committed**
   and **Over by**.
3. Select **Confirm**. If any date is over capacity, a **Reason** is required.
4. Or select **Decline allocation** and give a reason.

If the person also has work on a project you cannot see, the capacity preview is refused ("You cannot open the workload
view."). Ask an Admin to confirm it.

## Change it later (PM or proposer)

- **Edit allocation** needs a reason. Editing a Confirmed allocation returns it to Proposed.
- **Cancel allocation** (Proposed or Confirmed) and **Complete allocation** (Confirmed) need a reason.

## Set availability (supervisor)

1. **Workload**, find the person, and select the calendar icon next to their capacity ("Set daily availability for
   …").
2. Choose the **Date**, enter **Available hours** (0–24) and a **Category**: **Unavailable**, **Reduced** or
   **Additional**.
3. Select **Save**.

The value replaces that day's capacity, including on a holiday. "Use a general category; do not enter personal or
medical details." The pencil icon sets **Hours per week**; leave it empty to use the organisation default.

## Read the workload grid

Each week shows committed hours over available hours, with the task forecast, reserved (confirmed) hours and proposed
hours beneath. Committed load counts, for each confirmed allocation, the larger of its reserved hours and its linked
remaining work, plus remaining work not linked to any allocation. Proposed hours never count as committed. "Partial
project scope; spare capacity is unknown" means some of the person's work is on projects you cannot see. **How this is
calculated** explains the method.

Readiness uses the same figures for a work item's **Production Capacity** and **Review Capacity** checks (see
[readiness](readiness.md)).
