import Foundation

struct Person: Identifiable, Codable, Equatable {
    let id: UUID
    let name: String
    let role: String
    static let alex = Person(id: UUID(uuidString: "10000000-0000-0000-0000-000000000001")!, name: "Alex Morgan", role: "Team member")
    static let sam = Person(id: UUID(uuidString: "10000000-0000-0000-0000-000000000002")!, name: "Sam Chen", role: "Technical reviewer")
    static let rita = Person(id: UUID(uuidString: "10000000-0000-0000-0000-000000000003")!, name: "Rita Singh", role: "Read only")
    static let fixtures = [alex, sam, rita]
}

struct Project: Identifiable, Codable, Equatable {
    let id: UUID
    let number: String
    let name: String
    let discipline: String
    let disciplineID: UUID
    let deliverable: String
    let health: String
    let attention: String
    var archived = false
    static let harbour = Project(id: UUID(uuidString: "20000000-0000-0000-0000-000000000001")!, number: "SYN-2401", name: "Harbour renewal", discipline: "Civil", disciplineID: UUID(uuidString: "30000000-0000-0000-0000-000000000001")!, deliverable: "60% design submission", health: "Attention needed", attention: "Drainage review is waiting for survey confirmation.")
    static let greenway = Project(id: UUID(uuidString: "20000000-0000-0000-0000-000000000002")!, number: "SYN-2402", name: "Greenway connection", discipline: "Civil", disciplineID: UUID(uuidString: "30000000-0000-0000-0000-000000000002")!, deliverable: "Concept design", health: "On track", attention: "No current delivery concerns.")
}

enum TaskStatus: String, Codable, CaseIterable {
    case notStarted = "Not Started", inProgress = "In Progress", ready = "Ready for Review", reviewing = "Under Review", complete = "Complete", onHold = "On Hold"
}

struct WorkTask: Identifiable, Codable, Equatable {
    var id = UUID()
    var key: String
    var name: String
    var projectID: UUID
    var ownerID: UUID
    var reviewerID: UUID?
    var status: TaskStatus = .notStarted
    var requiresReview = true
    var due: String?
    var priority = "Normal"
    var progress = 0
    var blocker: String?
    var version = 1
    var description = "Synthetic engineering task for the Tuesday simulator. No client data."
}

struct TaskPermissions {
    let edit: Bool
    let comment: Bool
    let time: Bool
    let transitions: [TaskStatus]
    let reason: String?
}

struct Comment: Identifiable, Codable, Equatable {
    var id = UUID()
    let taskID: UUID
    let authorID: UUID
    let body: String
    let mentionID: UUID?
    var deleted = false
    var created = Date()
}

struct TimeEntry: Identifiable, Codable, Equatable {
    var id = UUID()
    let taskID: UUID
    let personID: UUID
    let date: String
    var hours: Decimal
    var note: String
    var version = 1
}

struct Notice: Identifiable, Codable, Equatable {
    var id = UUID()
    let personID: UUID
    let title: String
    let taskID: UUID?
    var read = false
}

struct WeekContext: Equatable {
    let week: String
    let planned: Decimal
    let available: Decimal?
    let confidence: String
    let explanation: String
}

struct Snapshot: Codable {
    var projects: [Project]
    var tasks: [WorkTask]
    var comments: [Comment]
    var time: [TimeEntry]
    var notices: [Notice]
}

enum DraftKind: String, Codable, Identifiable {
    case create = "New task", edit = "Edit task", transition = "Update status", comment = "Add comment", time = "Record time", correctTime = "Correct time"
    var id: String { rawValue }
}

struct TaskEditBaseline: Codable, Equatable {
    let title: String
    let ownerID: UUID
    let reviewerID: UUID?
    let due: String?
    let priority: String
    let progress: Int
    init(_ task: WorkTask) {
        title = task.name; ownerID = task.ownerID; reviewerID = task.reviewerID
        due = task.due; priority = task.priority; progress = task.progress
    }
}

struct Draft: Identifiable, Codable, Equatable {
    var id = UUID()
    var kind: DraftKind
    var taskID: UUID?
    var projectID: UUID
    var version: Int?
    var entryID: UUID?
    var title = ""
    var ownerID = Person.alex.id
    var reviewerID: UUID?
    var due: String?
    var priority = "Normal"
    var progress = 0
    var status = TaskStatus.inProgress
    var reason = ""
    var body = ""
    var mentionID: UUID?
    var hours = ""
    var workDate = OrgDate.today()
    var requiresReview = true
    var baseline: TaskEditBaseline?
    // A submitted draft is immutable until acknowledged or explicitly reconciled.
    var submittedAt: Date?
    var key = UUID()
}

struct Receipt: Codable, Equatable {
    let id: UUID
    let taskID: UUID
    let message: String
}

enum FixtureError: Error, LocalizedError, Equatable {
    case offline, unavailable, forbidden, conflict, unknown, keyReused, expired
    case validation(String)
    var errorDescription: String? {
        switch self {
        case .offline: return "Connection unavailable. Your draft is kept; reconnect before saving."
        case .unavailable: return "This item is unavailable. It may have moved or your access may have changed."
        case .forbidden: return "You do not have permission to make this change."
        case .conflict: return "Conflict: this item changed. Your draft is kept. Compare the current values before retrying."
        case .unknown: return "Outcome unknown. The response was lost. Retry the original request to resolve it."
        case .keyReused: return "This request key belongs to different input. The change was refused."
        case .expired: return "The retry window has ended. Inspect current records and explicitly reconcile this request."
        case .validation(let message): return message
        }
    }
}

enum OrgDate {
    static let zone = TimeZone(identifier: "America/Halifax")!
    static var calendar: Calendar {
        var c = Calendar(identifier: .gregorian)
        c.timeZone = zone
        c.firstWeekday = 2
        return c
    }
    static func string(_ date: Date) -> String {
        let f = DateFormatter()
        f.calendar = calendar
        f.timeZone = zone
        f.locale = Locale(identifier: "en_US_POSIX")
        f.dateFormat = "yyyy-MM-dd"
        return f.string(from: date)
    }
    static func parse(_ value: String) -> Date? {
        let f = DateFormatter()
        f.calendar = calendar
        f.timeZone = zone
        f.locale = Locale(identifier: "en_US_POSIX")
        f.dateFormat = "yyyy-MM-dd"
        f.isLenient = false
        guard value.count == 10, let date = f.date(from: value), string(date) == value else { return nil }
        return date
    }
    static func today(_ now: Date = Date()) -> String { string(now) }
    static func week(_ offset: Int = 0, now: Date = Date()) -> String {
        let day = calendar.startOfDay(for: now)
        let days = (calendar.component(.weekday, from: day) + 5) % 7
        return string(calendar.date(byAdding: .day, value: offset * 7 - days, to: day)!)
    }
    static func hours(_ input: String, locale: Locale = .current) throws -> Decimal {
        let separator = locale.decimalSeparator ?? "."
        let normalized = input.trimmingCharacters(in: .whitespaces).replacingOccurrences(of: separator, with: ".")
        guard normalized.range(of: "^[0-9]+(?:\\.[0-9]{1,2})?$", options: .regularExpression) != nil,
              let value = Decimal(string: normalized, locale: Locale(identifier: "en_US_POSIX")), value > 0, value <= 24 else {
            throw FixtureError.validation("Enter more than 0 and at most 24 hours, with up to two decimal places.")
        }
        return value
    }
}

enum AppLink: Equatable {
    case project(UUID), task(UUID)
    init?(_ url: URL) {
        guard url.scheme == "https", url.host == "pm.engcalchub.com", url.port == nil,
              url.user == nil, url.password == nil, url.fragment == nil,
              let parts = URLComponents(url: url, resolvingAgainstBaseURL: false) else { return nil }
        let path = url.path.split(separator: "/")
        guard path.count == 2, path[0] == "projects", let id = UUID(uuidString: String(path[1])) else { return nil }
        if let query = parts.queryItems, !query.isEmpty {
            guard query.count == 1, query[0].name == "panel", let value = query[0].value,
                  value.hasPrefix("Task:"), let taskID = UUID(uuidString: String(value.dropFirst(5))) else { return nil }
            self = .task(taskID)
        } else { self = .project(id) }
    }
    static func taskURL(_ task: WorkTask) -> URL {
        var parts = URLComponents(string: "https://pm.engcalchub.com/projects/\(task.projectID)")!
        parts.queryItems = [URLQueryItem(name: "panel", value: "Task:\(task.id)")]
        return parts.url!
    }
}
