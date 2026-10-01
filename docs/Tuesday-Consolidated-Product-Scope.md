# Tuesday — Consolidated Product Scope

## 1. Product Definition

**Tuesday is a role-based internal operating system for projects, people, resources, and delivery.**

It brings project information and resource planning into one connected place so managers, project managers, technical leads, discipline leads, and team members can work from the same underlying information without everyone needing the same interface.

The core principle is:

> **One source of truth → different views based on role.**

Tuesday should not try to become another generic Monday, Asana, Trello, or Microsoft Project. It should solve the internal problems that managers and project teams repeatedly deal with.

The product has two connected but distinct engines:

- **Project engine:** What work is happening, what needs attention, and is the project moving?
- **Resource engine:** Given everything we know about upcoming work, how should managers plan people and capacity?

They share information, but neither one should directly control the other.

---

## 2. Product North Star

> **Tuesday connects projects, people, resources, and delivery so every role can see what matters to them and managers can make better resource decisions from one shared source of truth.**

Tuesday should become the place people go to understand:

- What work exists
- Who is involved
- What is coming next
- Whether work is progressing
- Where capacity exists
- Where overload or conflicts exist
- Which projects or people require attention
- What management decisions are needed

---

## 3. Problems Tuesday Should Solve

Tuesday should help answer questions such as:

- Do our people have enough work over the next several weeks?
- Who is overloaded?
- Who has capacity?
- Which projects are creating resource pressure?
- Which employees have room to support something new?
- Which projects are moving normally?
- Which disciplines are stale, delayed, or at risk?
- Are project teams actually progressing on their assigned work?
- What work is coming that managers should plan for?
- What work is likely but not yet confirmed?
- What happens if a project is delayed, accelerated, or paused?
- Which deliverables need management attention?
- Where are resource conflicts happening across disciplines?
- What does each employee need to focus on right now?
- What decisions do managers need to make?

The system should reduce dependence on disconnected spreadsheets, workload trackers, action lists, project lists, and manual status-reporting processes.

---

## 4. Core Product Principle

Tuesday should avoid requiring users to maintain the same information twice.

If project information already tells Tuesday that a major deliverable is coming in three weeks, that information should help inform resource planning automatically.

However, resource planning should **not be controlled by project workflows**.

Project data becomes context for managers.

The manager still owns the planning decision.

The principle is:

> **Project data informs resource planning. Resource planning remains a separate manager-owned layer.**

---

## 5. Primary User Roles

### 5.1 Resource / Team Manager

Main question:

> **How should I deploy my people across all of our work?**

The manager workspace should emphasize:

- Weekly capacity
- Availability
- Future workload
- Overloaded and underallocated staff
- Leave
- Non-project work
- Resource conflicts
- Upcoming project demand
- Cross-team conflicts
- Private planning allocations
- Tentative future assignments
- Reallocation opportunities
- Staffing pressure
- Resource gaps
- Employees needing attention

Managers should be able to understand the state of their team very quickly.

---

### 5.2 Project Manager

Main question:

> **Is my project moving, and where do I need to follow up?**

The PM should **not normally allocate resources**.

The project team is generally established near project kickoff.

After that, the PM mainly needs visibility into whether each discipline or workstream is progressing.

The PM workspace should emphasize:

- Project health
- Discipline status
- Workstream status
- Deliverables
- Deadlines
- Next milestones
- Last update
- Upcoming commitments
- Risks
- Issues
- Decisions
- Actions
- Technical reviews
- Stale workstreams
- Items requiring follow-up

The PM should not have to constantly request resources if the team has already been established.

Instead, the PM needs a simple signal:

> **Is each discipline moving?**

Tuesday should help the PM follow up only where work is stale, delayed, blocked, or at risk.

---

### 5.3 Discipline / Team Lead

Main question:

> **Is my discipline delivering what the project needs, and are my people properly allocated?**

The discipline or team lead should own their discipline’s staffing decisions.

This is particularly important for multidisciplinary projects where:

- The overall PM may belong to another discipline.
- The PM may not manage the employees working in another discipline.
- The PM should not be able to directly control another discipline’s people.

The discipline lead manages:

- Capacity
- Internal assignments
- Discipline priorities
- Discipline deliverables
- Discipline staffing
- Technical progression

Status should roll up to the PM without giving the PM cross-discipline control.

---

### 5.4 Project / Technical Lead

Main question:

> **What needs my technical attention?**

Their workspace should emphasize:

- Assigned deliverables
- Technical reviews
- Dependencies
- Upcoming submissions
- Supporting staff
- Outstanding technical decisions
- Workload
- Discipline deadlines

---

### 5.5 Team Member

Main question:

> **What should I be working on?**

The team member experience should remain intentionally simple.

It should show:

- Current projects
- Current priorities
- Assigned work
- Upcoming deadlines
- Expected effort
- Important project information
- Weekly workload
- Upcoming commitments
- Employee-owned notes and allocations

Employees should not have to manage a complicated project-management interface.

---

### 5.6 Leadership

Main question:

> **Where does the organization need attention?**

Leadership can eventually see:

- Capacity trends
- Staffing pressure
- Major workload conflicts
- Project health
- Upcoming demand
- Cross-team opportunities
- Delivery risks
- Hiring pressure
- Utilization trends
- Resource shortages

Leadership should get visibility without creating additional reporting work for employees or managers.

---

## 6. Multidisciplinary Project Model

Tuesday must explicitly support multidisciplinary projects.

A project may have:

- One overall project manager
- Several disciplines
- Separate discipline leads
- Different managers
- Employees from multiple teams
- Workstreams that are controlled independently

The model should therefore include:

### Project Layer

The umbrella project contains:

- PM
- Client
- Project phase
- Project status
- Major milestones
- Major deliverables
- Overall risks
- Overall decisions
- Major deadlines
- Cross-discipline dependencies

### Discipline / Workstream Layer

Each discipline can have:

- Discipline lead
- Discipline team
- Discipline status
- Next deliverable
- Last update
- Internal milestones
- Technical decisions
- Resource needs
- Discipline-specific risks

The operating principle is:

> **Demand and status can flow across disciplines. Allocation control stays local.**

The PM gets visibility across the full project.

The PM does not get authority to directly control another discipline’s employees.

---

## 7. Projects Layer

Projects remain one of the main foundations of Tuesday.

Each project can contain:

- Project manager
- Project team
- Discipline workstreams
- Status
- Milestones
- Deliverables
- Deadlines
- Client commitments
- Project phase
- Actions
- Decisions
- Risks
- Issues
- Resource context
- Important links
- Supporting documents
- Latest updates
- Last activity date

Projects should provide context that helps the rest of the system.

---

## 8. Project Health and PM Follow-Up

The PM view should work as a **health-check dashboard**, not a resource-control dashboard.

For each discipline or workstream, the PM should see:

- Owner
- Status
- Next deliverable
- Due date
- Last update
- Current risk
- Whether the workstream is progressing
- Whether the update is stale

Tuesday should surface exceptions such as:

- No update for two weeks
- Deliverable approaching with no progress
- Discipline marked at risk
- Unresolved dependency
- Missed milestone
- Resource concern reported by discipline lead
- Technical review overdue

This reduces the need for PMs to manually chase every discipline.

---

## 9. People Layer

Each person should have a lightweight resource profile containing:

- Name
- Role
- Discipline
- Skills
- Manager
- Team
- Normal weekly capacity
- Working schedule
- Planned leave
- Active major-project assignments
- Future commitments
- Employee-owned workload entries
- Manager planning entries
- Non-project commitments

Tuesday is **not intended to become an HR system**.

It should only store the people information needed for planning and delivery.

---

## 10. Data Ownership Principle

The user who creates data should normally own that data.

Examples:

- A comment written by an employee should not be editable by someone else.
- A personal workload note should remain employee-owned.
- Another user can reply, but should not silently rewrite the original content.
- Shared project data should have clearly defined owners.
- Manager planning data can be manager-owned.

The principle is:

> **Coordinate without taking control of someone else’s content.**

This protects trust in the system.

---

## 11. Resource Planning Is a Separate Layer

Resource planning should not depend on every project being managed inside Tuesday.

Tuesday may only contain larger or strategically important projects.

Smaller work may exist outside the system.

Therefore resource planning must be able to include:

- Major projects inside Tuesday
- Smaller projects outside Tuesday
- Proposals
- Business development
- Training
- Administration
- Internal initiatives
- Supervision
- Field work
- Leave
- Temporary assignments
- Other commitments

This is critical because someone may appear underallocated if Tuesday only counts the large projects.

---

## 12. Weekly Capacity Model

Each employee should have a weekly capacity number.

Example:

**Standard capacity: 37.5 hours/week**

Tuesday then compares capacity against known commitments.

A simple model is:

> **Available capacity − known commitments = remaining capacity**

Known commitments can include:

- Major projects
- Small projects
- Other project work
- Manager-added allocations
- Employee-added allocations
- Leave
- Training
- Proposals
- Business development
- Admin
- Supervision
- Internal initiatives
- Field assignments
- Other commitments

The result should reflect a realistic weekly workload rather than only major project assignments.

---

## 13. Weekly Resource Planner

This should become one of Tuesday’s strongest manager capabilities.

Managers should see a rolling planning horizon, initially around:

**6–12 weeks**

For each employee, Tuesday should show:

- Weekly capacity
- Total planned hours
- Remaining capacity
- Overload
- Major-project work
- Other project work
- Non-project work
- Employee-added work
- Manager-added planning
- Leave
- Tentative future work

The weekly planner should be designed around very quick interaction.

---

## 14. Weekly Load Sources

Every total should be explainable.

If Tuesday says someone has 42 planned hours, the manager should be able to see where those 42 hours came from.

Possible source categories:

- Major project
- Other project
- Employee-added work
- Manager-added planning
- Leave
- Proposal
- Business development
- Training
- Admin
- Internal initiative
- Other

The system should avoid showing a single utilization number without context.

---

## 15. Employee-Added Work

Employees should be able to add lightweight weekly commitments that may not exist as formal projects.

Example entries:

- 10 h — proposal support
- 3 h — admin
- 6 h — small project
- 4 h — training
- 8 h — field work

These entries should be fast to create.

They should not require a full project record.

Employee-created entries remain employee-owned.

---

## 16. Manager Planning Layer

Managers should also be able to add work to an employee’s weekly plan.

However, manager planning should not silently overwrite employee-owned data.

Instead, Tuesday should have a separate **manager planning layer**.

A manager might add:

- 8 h — proposal support
- 16 h — possible upcoming assignment
- 12 h — internal initiative
- 20 h — likely project support

These planning entries can affect the manager’s resource view without immediately appearing to the employee.

---

## 17. Manager Planning Visibility States

A simple visibility model can support planning without unnecessary workflow.

### Private Draft

Only the manager sees it.

Useful for:

- Scenario planning
- Early forecasting
- Tentative assignment
- Reserving capacity
- Testing different resource plans

### Published / Proposed

The employee can now see the planned assignment.

Useful when:

- The manager wants to communicate upcoming work
- The assignment is likely but still adjustable

### Confirmed

The assignment becomes part of the employee’s normal planned workload.

The system should support these states without forcing the manager through a complicated approval workflow.

---

## 18. Fast Interaction Principle

The underlying planning model can be sophisticated.

The interface should not be.

A manager should be able to:

1. Click an employee’s week
2. Type something like: **8 h — proposal support**
3. Press Enter

Done.

The default can be a private manager planning entry.

Additional controls should only appear when needed.

The principle is:

> **Fast first. Control second.**

Most resource-planning actions should require only one or two interactions.

---

## 19. Optional Planning Controls

More detailed controls can exist without being required during quick entry.

For example:

- Visibility
- Tentative / confirmed
- Start week
- End week
- Repeat
- Notes
- Source
- Category
- Move to another employee
- Convert to formal assignment
- Link to project
- Link to opportunity

These should be optional.

Tuesday should not force managers to fill in unnecessary metadata every time they add work.

---

## 20. Repeat, Copy, and Drag

Managers should not have to repeatedly enter the same work.

The weekly planner should support:

- Dragging work to another week
- Dragging work to another employee
- Copying an allocation
- Repeating an allocation across multiple weeks
- Extending an allocation
- Shortening an allocation

Example:

> 12 h/week — proposal support — six weeks

This should be entered once, not six separate times.

---

## 21. Rough Allocations

Managers often do not know all details when planning.

Tuesday should therefore support rough planning.

Examples:

- ~15 h — upcoming work
- ~20 h — expected project
- ~8 h — probable review support

Managers should not be forced to know:

- Exact project code
- Exact task ID
- Exact deliverable
- Exact employee
- Exact start/end date

Planning should be useful even when information is incomplete.

---

## 22. Confirmed, Expected, and Possible Work

Resource planning should account for uncertainty.

A simple confidence model could be:

- **Confirmed**
- **Expected**
- **Possible**

This allows managers to see both committed work and future pressure without pretending every opportunity is guaranteed.

For example:

- 30 h confirmed
- 10 h expected
- 8 h possible

This is more useful than a single hard utilization percentage.

---

## 23. Cross-Team Resource Conflicts

In a multidisciplinary organization, more than one manager may assume the same employee has capacity.

Tuesday should be able to identify that.

Example:

- Civil manager plans 16 h
- Environmental manager plans 16 h
- Existing confirmed work = 20 h
- Total implied load = 52 h

Tuesday should flag the conflict.

However, it should not automatically give one manager authority over another manager’s plan.

The system provides visibility.

The managers resolve the issue.

---

## 24. Conflict Detection

Tuesday should automatically identify:

- Overallocated employees
- Underallocated employees
- Future workload gaps
- Overlapping manager plans
- Leave conflicts
- Major-deliverable pressure
- Unassigned demand
- Cross-team competition for the same person

Warnings should be visible but generally non-blocking.

Tuesday should inform decisions, not prevent managers from making them.

---

## 25. Decision-Oriented Resource Dashboard

The dashboard should focus on decisions, not just utilization percentages.

Instead of only saying:

> Jillian — 120% allocated

Tuesday should help explain:

> Jillian has two competing deadlines next week.

And ideally surface context such as:

> Bennett has 18 hours available.

The goal is to help the manager understand what action may be needed.

---

## 26. Manager Dashboard

The manager homepage should operate as an **exception dashboard**.

Managers should immediately see things such as:

### People needing attention

- 3 overloaded
- 2 significantly underallocated
- 1 returning from leave

### Projects needing attention

- 2 disciplines have stale updates
- 3 major deliverables due within 14 days

### Upcoming pressure

- Civil design demand exceeds available capacity in Week 4

### Planning conflicts

- 2 employees have overlapping manager allocations

### Decisions

- 3 resource plans need manager review

A manager should be able to understand the state of their team in roughly **30 seconds**.

---

## 27. Resource Planning Should Work Without a Project

Resource planning must support work that does not yet exist as a formal Tuesday project.

Examples:

- Proposal support
- Likely future award
- Small client task
- Internal initiative
- Corporate work
- Temporary coverage
- Training
- Business development
- Upcoming hiring need

This is another reason the resource engine should remain independent from the project engine.

---

## 28. Planning Horizon

Planning accuracy naturally decreases with distance into the future.

Tuesday should reflect that.

### Near-Term

Use:

- Person-specific planning
- Weekly hours
- Confirmed assignments
- Detailed capacity

### Mid-Term

Use:

- Approximate weekly hours
- Tentative assignments
- Expected workload

### Longer-Term

Use broader demand such as:

- Need one civil technologist in January
- Need senior engineer review support
- Need inspection support during construction season

Tuesday should not create false precision when the future is uncertain.

---

## 29. Stale Data

One of the biggest risks in a resource-planning system is old information looking current.

Tuesday should therefore track:

- Last updated
- Last confirmed
- Stale allocations
- Stale project status
- Old manager plans
- Outdated employee availability

If information becomes too old, Tuesday should signal:

> **Unknown / needs confirmation**

rather than pretending the resource picture is reliable.

---

## 30. Connected Workflows

The real value of Tuesday comes from information flowing between modules without tightly coupling them.

### Project Schedule Change

A PM moves a deliverable two weeks earlier.

Tuesday can show the manager that a major commitment is now approaching sooner.

The manager decides whether resource planning needs to change.

### Resource Reassignment

A manager shifts someone from one assignment to another.

The manager’s capacity view updates immediately.

Relevant project stakeholders can see the staffing change if it is published.

### Leave

An employee records upcoming leave.

Available capacity decreases.

Managers can immediately see the impact.

Affected projects can show a potential staffing concern.

### New Opportunity

A likely future project is added as expected work.

Its rough demand appears in resource planning.

Managers can prepare before the project starts.

### Small Project Outside Tuesday

An employee or manager adds:

> 8 h/week — small client project

The person’s weekly capacity remains realistic even though the project does not exist in Tuesday.

---

## 31. Delivery Management

Tuesday should also help projects move forward.

The system should surface:

- Upcoming deliverables
- Overdue actions
- Approaching deadlines
- Decisions required
- Unresolved risks
- Issues
- Technical reviews
- Stale workstreams
- Resource concerns
- Major milestones

Instead of making the PM search through the project, Tuesday should surface:

> **These are the things requiring your attention.**

---

## 32. Navigation Concept

A possible top-level structure is:

**Home | Projects | People | Resources | Insights**

The platform remains consistent across roles.

The content inside each area changes based on:

- Role
- Permissions
- Discipline
- Ownership
- Team
- Responsibility

This keeps Tuesday feeling like one product rather than several disconnected applications.

---

## 33. MVP Scope

The first serious version should focus on:

### Projects

- Basic project information
- PM
- Project team
- Discipline workstreams
- Status
- Major milestones
- Deliverables
- Important dates
- Project health

### People

- Role
- Discipline
- Skills
- Manager
- Team
- Capacity
- Leave
- Weekly availability

### Weekly Resource Planning

- Person × week × hours
- Major-project allocations
- Employee-added work
- Manager planning entries
- Other work
- Leave
- Repeat / drag / copy
- Remaining capacity
- Overload indication

### Resource Planning Logic

- Confirmed / expected / possible
- Private manager planning
- Published planning
- Conflict detection
- Cross-team visibility
- Stale-data warnings

### Dashboards

- Manager dashboard
- PM dashboard
- Employee view

### Ownership and Permissions

- Creator-owned comments
- Employee-owned personal entries
- Manager-owned planning entries
- Shared project data ownership
- Cross-discipline visibility without edit control

This is enough to create a genuinely useful internal product.

---

## 34. Keep Out of the Initial MVP

Tuesday should initially avoid becoming responsible for:

- Detailed timesheet entry
- Payroll
- Accounting
- Invoicing
- Full CRM
- Detailed document management
- Full HR management
- Sophisticated financial forecasting
- Enterprise scheduling
- Replacing Outlook
- Replacing Teams
- Replacing every corporate system
- AI automatically assigning employees
- Detailed individual performance tracking

Existing systems can eventually integrate with Tuesday instead of Tuesday recreating all of them.

---

## 35. Future Layers

After the core system is being used reliably, Tuesday could expand into:

- Resource forecasting
- Skills-based resource search
- Hiring-demand forecasting
- Portfolio planning
- Cross-team resource sharing
- Workload scenarios
- Automated management summaries
- AI-assisted project updates
- Intelligent conflict detection
- Organizational dashboards
- Teams integration
- Outlook integration
- Historical planning insights
- Planned versus actual effort
- Resource-demand forecasting
- Staffing recommendations

AI should primarily **surface information and options**.

Managers remain responsible for actual resource decisions.

---

## 36. Planned vs Actual — Future Use

Once Tuesday has enough trustworthy historical data, it may compare:

- Planned effort
- Actual effort
- Typical duration
- Resource demand
- Delivery timing

The purpose should be planning improvement, not employee policing.

For example:

> Projects of this type are usually planned for four weeks but historically take six.

That insight could improve future resource forecasting.

---

## 37. Success Criteria

Tuesday is succeeding when:

- Managers stop maintaining separate resource spreadsheets.
- Managers can understand team capacity quickly.
- Managers can quickly add and adjust resource plans.
- Employees can record small or non-project commitments easily.
- PMs can tell whether each discipline is moving.
- PMs spend less time manually chasing project updates.
- Resource conflicts are identified before they become urgent.
- Cross-discipline visibility improves without creating cross-discipline control.
- Project information only needs to be maintained once.
- Small projects outside Tuesday still appear in the resource picture.
- Employees have clearer priorities.
- Leadership gets useful visibility without creating extra reporting work.
- Stale information is visible rather than silently treated as current.
- Resource planning reflects real work, not just major projects.

Most importantly:

> **Tuesday becomes the place people go to understand what work exists, who is doing it, what is coming next, where capacity exists, and where attention is required.**

---

## 38. Final Product Principles

### One system, different role-based views

Everyone works from the same core information, but each role sees what matters to them.

### Shared visibility, local control

Multidisciplinary teams can see each other’s progress without taking control of another discipline’s people.

### Project context, independent resource planning

Projects inform the resource picture, but resource planning remains its own manager-owned layer.

### User-owned content

People retain ownership of the data and comments they create.

### Fast first, control second

Common actions should take one or two steps. Advanced controls remain optional.

### Explain the number

Every workload total should be traceable to its source.

### Show uncertainty

Confirmed, expected, and possible work should not be treated the same.

### Flag, don’t block

Tuesday should surface conflicts and risks without becoming unnecessarily restrictive.

### Reduce administration

The system should derive useful insights from information already entered during normal work.

### Support reality, not just the system

Resource planning must include work outside Tuesday so the capacity picture remains credible.

---

# Final One-Sentence Definition

> **Tuesday is a role-based internal operating system that connects project delivery with a lightweight, manager-owned resource-planning layer so multidisciplinary teams can see what is happening, managers can plan people realistically, and project managers can keep work moving without controlling resources outside their discipline.**
