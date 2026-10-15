# Weekly planning

The Weekly Planner shows a person’s available capacity, planned work and confidence for each ISO week. It is a planning layer beside resource allocations: confirmed allocations remain read-only and Workload continues to show task forecasts and committed allocation load.

## Who can do what

Supervisors can plan direct reports and record time away for them. People can add their own self-entered planning when the server grants that permission. Executives can review the people in their scope. Project Managers see planning for members of projects they manage. Administrators can plan across the organisation; correcting another owner’s entry requires a reason and is logged. Private drafts are visible only to their owner, except in the explicit, logged Administrator correction mode.

## Quick add

At desktop width, select a person-week cell and type a line such as `8 h proposal support`, then press Enter. A leading `~` records Possible confidence. Escape cancels. The server validates hours, ISO weeks, project links, permissions and duplicate retries.

## Confidence, visibility and totals

Confidence is labelled `Confidence: Confirmed`, `Confidence: Expected` or `Confidence: Possible`. Visibility is labelled Private draft, Proposed assignment, Confirmed assignment or Self-entered. These labels stay separate from source category and from an approved allocation’s `Approval status: Confirmed`. Confirmed and Expected hours reduce remaining capacity; Possible hours remain visible beside the total. Choose **See contributions** to reconcile capacity by day, the confidence bands, covered hours, task context and each indicator’s rule. An approved allocation covers linked planning hours once so the same project work is not counted twice.

## Time away

Time away uses the existing availability override record. Supervisors and Administrators select a date range, category and available hours. The command reads date versions first and refuses stale changes as one transaction. Time away explains capacity; it is not a second leave record and is never subtracted twice.

## Workload and privacy

Workload remains the task-estimate and allocation-commitment view, with its own indicators. The Planner uses the same server-authorised scope and excludes private drafts, restricted projects and hidden hours from other viewers’ totals, exports, search and contribution lists. At tablet width the planner is read-only; phones receive the desktop/tablet notice.
