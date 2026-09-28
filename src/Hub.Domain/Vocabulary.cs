namespace Hub.Domain;

// Canonical names from §10 of the product specification, stored and displayed verbatim.

public static class SystemRole
{
    public const string Admin = "Admin", Executive = "Executive", Supervisor = "Supervisor",
        ProjectManager = "ProjectManager", ReadOnly = "ReadOnly";
    public static readonly string[] All = [Admin, Executive, Supervisor, ProjectManager, ReadOnly];
    // Entra app role value → system role (§23.6).
    public static readonly IReadOnlyDictionary<string, string> AppRoles = new Dictionary<string, string>
    {
        ["Hub.Admin"] = Admin, ["Hub.Executive"] = Executive, ["Hub.Supervisor"] = Supervisor,
        ["Hub.ProjectManager"] = ProjectManager, ["Hub.ReadOnly"] = ReadOnly,
    };
}

public static class RoleSource { public const string Group = "Group", Manual = "Manual"; }

public static class ProjectRole
{
    public const string PM = "PM", TeamMember = "TeamMember", Reviewer = "Reviewer", Viewer = "Viewer";
    public static readonly string[] All = [PM, TeamMember, Reviewer, Viewer];
}

public static class ProjectStatus
{
    public const string Setup = "Setup", Active = "Active", OnHold = "On Hold", Complete = "Complete",
        Archived = "Archived", Cancelled = "Cancelled";
    public static readonly string[] All = [Setup, Active, OnHold, Complete, Archived, Cancelled];
    public static bool IsReadOnly(string s) => s is Archived or Cancelled;
    public static bool IsLive(string s) => s is Setup or Active or OnHold;
}

public static class TaskStatuses
{
    public const string NotStarted = "Not Started", InProgress = "In Progress", ReadyForReview = "Ready for Review",
        InReview = "In Review", RevisionRequired = "Revision Required", Complete = "Complete",
        OnHold = "On Hold", Cancelled = "Cancelled";
    public static readonly string[] All = [NotStarted, InProgress, ReadyForReview, InReview, RevisionRequired, Complete, OnHold, Cancelled];
    public static bool IsTerminal(string s) => s is Complete or Cancelled;
    public static bool IsOpen(string s) => !IsTerminal(s);
    public static bool IsReview(string s) => s is ReadyForReview or InReview;
    // "In Progress or later" for rules that ask whether someone is working the task (D-06 d, A-08, A-09).
    public static bool IsStarted(string s) => s is InProgress or ReadyForReview or InReview or RevisionRequired;
}

public static class DeliverableStatus
{
    public const string NotStarted = "Not Started", InProgress = "In Progress", InReview = "In Review",
        RevisionRequired = "Revision Required", ReadyToIssue = "Ready to Issue", Issued = "Issued",
        Accepted = "Accepted", OnHold = "On Hold", Cancelled = "Cancelled";
    public static readonly string[] All = [NotStarted, InProgress, InReview, RevisionRequired, ReadyToIssue, Issued, Accepted, OnHold, Cancelled];
    // G-04 / DL-11: Issued, Accepted and Cancelled are terminal for overdue purposes.
    public static bool IsTerminal(string s) => s is Issued or Accepted or Cancelled;
}

public static class MilestoneStatus
{
    public const string OnTrack = "On Track", AtRisk = "At Risk", Overdue = "Overdue", Complete = "Complete", Cancelled = "Cancelled";
    public static readonly string[] All = [OnTrack, AtRisk, Overdue, Complete, Cancelled];
}

public static class DecisionStatus
{
    public const string Pending = "Pending", UnderReview = "Under Review", Decided = "Decided", Deferred = "Deferred", Cancelled = "Cancelled";
    public static readonly string[] All = [Pending, UnderReview, Decided, Deferred, Cancelled];
    public static bool IsOpen(string s) => s is Pending or UnderReview or Deferred;
}

public static class RiskStatus
{
    public const string Open = "Open", Monitoring = "Monitoring", Closed = "Closed", Realised = "Realised";
    public static readonly string[] All = [Open, Monitoring, Closed, Realised];
    public static bool IsOpen(string s) => s is Open or Monitoring;
}

public static class IssueStatus
{
    public const string Open = "Open", InProgress = "In Progress", Resolved = "Resolved", Cancelled = "Cancelled";
    public static readonly string[] All = [Open, InProgress, Resolved, Cancelled];
    public static bool IsOpen(string s) => s is Open or InProgress;
}

public static class ActionStatus
{
    public const string Open = "Open", InProgress = "In Progress", Complete = "Complete", Cancelled = "Cancelled";
    public static readonly string[] All = [Open, InProgress, Complete, Cancelled];
    public static bool IsOpen(string s) => s is Open or InProgress;
}

public static class Health
{
    public const string Green = "Green", Yellow = "Yellow", Red = "Red", Grey = "Grey";
    public static readonly string[] All = [Green, Yellow, Red, Grey];
    public static readonly string[] Overridable = [Green, Yellow, Red];
    public static int Severity(string? h) => h switch { Red => 0, Yellow => 1, Green => 2, _ => 3 };
}

public static class Priority
{
    public const string Low = "Low", Medium = "Medium", High = "High", Critical = "Critical";
    public static readonly string[] All = [Low, Medium, High, Critical];
    public static int Rank(string? p) => p switch { Critical => 0, High => 1, Medium => 2, _ => 3 };
}

public static class Impact
{
    public const string Low = "Low", Medium = "Medium", High = "High";
    public static readonly string[] All = [Low, Medium, High];
}

public static class Severity
{
    public const string Critical = "Critical", Warning = "Warning", Info = "Info";
    public static int Rank(string s) => s switch { Critical => 0, Warning => 1, _ => 2 };
}

public static class MilestoneType
{
    public const string Kickoff = "Kickoff", FieldWork = "Field Work", DesignSubmission = "Design Submission",
        ClientWorkshop = "Client Workshop", PermitSubmission = "Permit Submission", Tender = "Tender",
        Construction = "Construction", IFC = "IFC", RecordDrawings = "Record Drawings", Closeout = "Closeout", Other = "Other";
    public static readonly string[] All = [Kickoff, FieldWork, DesignSubmission, ClientWorkshop, PermitSubmission, Tender, Construction, IFC, RecordDrawings, Closeout, Other];
    public static bool IsSubmission(string t) => t is DesignSubmission or PermitSubmission or Tender or IFC;
}

public static class BlockType
{
    public const string Client = "Client", ExternalParty = "External Party", Internal = "Internal",
        Decision = "Decision", Information = "Information", Other = "Other";
    public static readonly string[] All = [Client, ExternalParty, Internal, Decision, Information, Other];
}

public static class LinkType
{
    public const string SharePoint = "SharePoint", OneDrive = "OneDrive", Teams = "Teams",
        NetworkFolder = "Network Folder", ExternalDms = "External DMS", ClientPortal = "Client Portal", Other = "Other";
    public static readonly string[] All = [SharePoint, OneDrive, Teams, NetworkFolder, ExternalDms, ClientPortal, Other];
}

public static class CommentKind
{
    public const string General = "General", Review = "Review", StatusNote = "Status Note", System = "System";
}

public static class FollowLevel
{
    public const string AllActivity = "AllActivity", MyItemsOnly = "MyItemsOnly", Muted = "Muted";
    public static readonly string[] All = [AllActivity, MyItemsOnly, Muted];
}

public static class FollowSource { public const string Assignment = "Assignment", Manual = "Manual"; }

public static class Visibility { public const string Open = "Open", Restricted = "Restricted"; }

public static class ItemRelation
{
    public const string BlockedByDecision = "blocked_by_decision", Related = "related", RealisedAs = "realised_as", ConvertedToTask = "converted_to_task";
}

public static class ItemType
{
    public const string Project = "Project", Milestone = "Milestone", Deliverable = "Deliverable", Task = "Task",
        Decision = "Decision", Risk = "Risk", Issue = "Issue", Meeting = "Meeting", Action = "Action",
        Comment = "Comment", DocumentLink = "DocumentLink", Dependency = "Dependency", Member = "Member",
        Discipline = "Discipline", ExternalParty = "ExternalParty", User = "User", ReferenceData = "ReferenceData",
        Setting = "Setting", TimeEntry = "TimeEntry", CalendarEvent = "CalendarEvent", Template = "Template",
        Handoff = "Handoff", ChangeNotice = "ChangeNotice",
        Report = "Report", Snooze = "Snooze", ProjectLink = "ProjectLink", Holiday = "Holiday";
    // Items that can carry comments (C-01; Phase 2 registers included).
    public static readonly string[] Commentable = [Task, Deliverable, Milestone, Decision, Risk, Issue, Action];
    public static readonly string[] Linkable = [Project, Deliverable, Task];
}

public static class CalendarEventType
{
    public const string Meeting = "Meeting", SiteWork = "Site Work", InternalTask = "Internal Task";
    public static readonly string[] All = [Meeting, SiteWork, InternalTask];
}

public static class EventVisibility { public const string Project = "Project", Private = "Private"; }

public static class MeetingType
{
    public static readonly string[] All = ["Coordination", "Client", "Design Review", "Site", "Other"];
}

public static class ActionOwnerType { public const string User = "User", Discipline = "Discipline", ExternalParty = "External Party"; }

public static class TemplateStatus { public const string Draft = "Draft", Published = "Published", Retired = "Retired"; }

public static class AssignToRole { public const string DisciplineLead = "DisciplineLead", PM = "PM", Unassigned = "Unassigned"; }

public static class Weekday
{
    public static readonly string[] All = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
    public static DayOfWeek Parse(string? d) => d switch
    {
        "Tuesday" => DayOfWeek.Tuesday, "Wednesday" => DayOfWeek.Wednesday, "Thursday" => DayOfWeek.Thursday,
        "Friday" => DayOfWeek.Friday, "Saturday" => DayOfWeek.Saturday, "Sunday" => DayOfWeek.Sunday, _ => DayOfWeek.Monday,
    };
}
