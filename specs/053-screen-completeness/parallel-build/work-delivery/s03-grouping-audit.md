# S03 rendered input grouping audit

Current UI repair is READY. C# remains frozen at generation 6. No source mutation or test execution is in flight.

The previous presentation split caused two separate data-loss cases: ungrouped Waiting sources were hidden, and then mixed Blocked/Waiting source membership was split by placement before deduplication. The final data flow now groups membership first and chooses placement second.

1. `shownTasks` selects cards from actually expanded Blocked, Overdue, Due This Week, discipline round, Upcoming and Held sections, using the same hide-discussed filtering as the rendered task cards. No server membership/state/count is changed.
2. `inputGroups` considers nonterminal canonical contexts with actual inputs/unavailable evidence or actual Waiting/Blocked state. Ordinary unblocked tasks with no inputs do not create a phantom unavailable source in Copy Summary.
3. `byBlocker` groups the existing canonical context inputs by kind and ID, deduplicating member tasks by task ID. `inputGroups` sorts member keys/IDs. Manual and unavailable facts keep task-local keys, not text-based shared identities.
4. One `InputPresentation` holds these complete memberships plus the visible Blocked placement keys. A Blocked source header reads its full membership from this map, including Waiting-only tasks found in other visible sections. The Waiting-on section displays the remaining successful source groups. Placement cannot remove members.
5. Both placements use `sourceFacts`, which shows the source once, its canonical resolver/date/action, the distinct member count, and links to every included task with explicit current `Blocked` / `Waiting` (or known workflow status) text. Canonical Blocked figure and the original Blocked task rows are not widened by this membership list.
6. Task cards suppress only successful source identities rendered by this shared presentation. Unknown/unavailable notices remain local. The derived Waiting-on section stays open while present. Collapsing Blocked can move its source to Waiting on if other cards still show the source; it stays one placement with the same still-visible membership.
7. The real Copy Summary action receives the exact `InputPresentation` used by the UI. It copies each group once with the same distinct task members and explicit state text. Programmatic enabled callers without this UI projection derive complete memberships over their supplied agenda. Disabled callers retain the original summary/grouping path.

The browser script now exercises:

- two Waiting-only tasks sharing a Decision, repeated in Due, discipline round and expanded Upcoming;
- visible-section contraction/removal and restoration of that group;
- a normal predecessor shape with a started/due successor Blocked and a future successor Waiting, repeated in other sections;
- exactly one predecessor source, both task links and explicit states, unchanged canonical Blocked count, exact source open/return, and one Copy Summary line retaining both members/states;
- collapse of Blocked while the two successors remain visible elsewhere, moving that same source to a single Waiting-on placement with both members.

PASS in this lane: `node --check web/tests/work-delivery-input-patterns.cjs` and scoped whitespace checks. UI build, type checks, lint, browser execution and independent runtime acceptance remain root-owned and unrun by this lane. The mixed predecessor browser data is a mock of the normal dependency state split; it is not claimed as a newly executed backend lifecycle test.
