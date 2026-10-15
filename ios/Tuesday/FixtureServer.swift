import Foundation

// Synthetic simulator service. These rules are not the ASP.NET API or proof of its behavior.
actor FixtureServer {
    private struct SavedCommand: Codable {
        let accountID: UUID
        let draft: Draft
        let receipt: Receipt
    }
    private struct Database: Codable {
        var snapshot: Snapshot
        var receipts: [String: SavedCommand] = [:]
    }
    private var database: Database
    private let file: URL?
    private var offline = false
    private var dropReply = false
    private var revoked: Set<UUID> = []

    init(file: URL? = nil) throws {
        self.file = file
        if let file, FileManager.default.fileExists(atPath: file.path) {
            database = try JSONDecoder().decode(Database.self, from: Data(contentsOf: file))
        } else {
            let harbour = Project.harbour, greenway = Project.greenway
            let design = WorkTask(id: UUID(uuidString: "40000000-0000-0000-0000-000000000001")!, key: "SYN-2401-T001", name: "Check stormwater design", projectID: harbour.id, ownerID: Person.alex.id, reviewerID: Person.sam.id, status: .inProgress, due: OrgDate.today(), progress: 60)
            let review = WorkTask(id: UUID(uuidString: "40000000-0000-0000-0000-000000000002")!, key: "SYN-2401-T002", name: "Review drainage calculations", projectID: harbour.id, ownerID: Person.alex.id, reviewerID: Person.sam.id, status: .ready, due: OrgDate.today(), progress: 100)
            let blocked = WorkTask(key: "SYN-2401-T003", name: "Confirm pipe crossing levels", projectID: harbour.id, ownerID: Person.alex.id, due: nil, blocker: "Waiting for survey confirmation. Ask the survey lead before starting.")
            let more = (1...24).map { index in
                WorkTask(key: "SYN-2402-T\(String(format: "%03d", index))", name: index == 1 ? "Coordinate accessible path grades" : "Check greenway design segment \(index)", projectID: greenway.id, ownerID: Person.alex.id, reviewerID: Person.sam.id, requiresReview: false, due: nil)
            }
            database = Database(snapshot: Snapshot(projects: [harbour, greenway], tasks: [design, review, blocked] + more,
                comments: [Comment(taskID: design.id, authorID: Person.sam.id, body: "Please include the survey datum in the review package.", mentionID: Person.alex.id)], time: [],
                notices: [Notice(personID: Person.alex.id, title: "Sam mentioned you in stormwater design", taskID: design.id), Notice(personID: Person.sam.id, title: "Drainage calculations are ready for review", taskID: review.id)]))
            // Draft targets must keep their identity across relaunch even before the first write.
            if let file { try ProtectedStorage.write(JSONEncoder().encode(database), to: file) }
        }
    }

    func setOffline(_ value: Bool) { offline = value }
    func loseNextReply() { dropReply = true }
    func revoke(_ account: UUID) { revoked.insert(account) }

    private func authorize(_ account: UUID) throws {
        guard Person.fixtures.contains(where: { $0.id == account }), !revoked.contains(account) else { throw FixtureError.forbidden }
    }
    private func projectVisible(_ projectID: UUID, account: UUID) -> Bool {
        database.snapshot.projects.contains { $0.id == projectID } && (account != Person.rita.id || projectID == Project.harbour.id)
    }
    func fetch(_ account: UUID) async throws -> Snapshot {
        try await Task.sleep(for: .milliseconds(120))
        if offline { throw FixtureError.offline }
        try authorize(account)
        var result = database.snapshot
        result.projects = result.projects.filter { projectVisible($0.id, account: account) }
        result.tasks = result.tasks.filter { projectVisible($0.projectID, account: account) }
        let ids = Set(result.tasks.map(\.id))
        result.comments = result.comments.filter { ids.contains($0.taskID) }
        result.time = result.time.filter { $0.personID == account && ids.contains($0.taskID) }
        result.notices = result.notices.filter { $0.personID == account && ($0.taskID == nil || ids.contains($0.taskID!)) }
        return result
    }
    func permissions(_ task: WorkTask, account: UUID) -> TaskPermissions {
        let visible = projectVisible(task.projectID, account: account) && !revoked.contains(account)
        let archived = database.snapshot.projects.first { $0.id == task.projectID }?.archived ?? true
        let writable = visible && account != Person.rita.id && !archived
        let own = task.ownerID == account
        var transitions: [TaskStatus] = []
        if writable {
            if own && task.blocker == nil && task.status == .notStarted { transitions = [.inProgress] }
            if own && task.blocker == nil && task.status == .inProgress { transitions = [task.requiresReview ? .ready : .complete, .onHold] }
            if own && task.status == .onHold { transitions = [.inProgress] }
            if task.reviewerID == account && !own && task.status == .ready { transitions = [.reviewing] }
        }
        return TaskPermissions(edit: writable && own, comment: writable, time: writable, transitions: transitions,
            reason: !visible ? "Item unavailable" : archived ? "This project is archived." : !writable ? "This account has read-only access." : task.blocker)
    }
    func createProjects(_ account: UUID) -> [UUID] {
        guard !revoked.contains(account), Person.fixtures.contains(where: { $0.id == account }), account != Person.rita.id else { return [] }
        return database.snapshot.projects.filter { !$0.archived && projectVisible($0.id, account: account) }.map(\.id)
    }
    func week(_ offset: Int, account: UUID) throws -> WeekContext {
        try authorize(account)
        return WeekContext(week: OrgDate.week(offset), planned: account == Person.sam.id ? 8 : 24,
            available: offset == 0 ? 35 : nil, confidence: "Published + tentative",
            explanation: offset == 0 ? "Partial view: only plans visible to you are included. Availability source is current. No private plans or HR reasons are shown." : "Availability source has not been confirmed for this week. Capacity is unknown; do not assume zero.")
    }
    func markRead(_ ids: [UUID], account: UUID) async throws {
        if offline { throw FixtureError.offline }
        try authorize(account)
        let before = database
        for index in database.snapshot.notices.indices where database.snapshot.notices[index].personID == account && ids.contains(database.snapshot.notices[index].id) {
            database.snapshot.notices[index].read = true
        }
        do { try persist() } catch { database = before; throw error }
    }
    func externalChange(_ taskID: UUID) throws {
        guard let index = database.snapshot.tasks.firstIndex(where: { $0.id == taskID }) else { throw FixtureError.unavailable }
        let before = database
        database.snapshot.tasks[index].progress = 75
        database.snapshot.tasks[index].version += 1
        do { try persist() } catch { database = before; throw error }
    }

    func submit(_ draft: Draft, account: UUID, now: Date = Date()) async throws -> Receipt {
        try await Task.sleep(for: .milliseconds(120))
        if offline { throw FixtureError.offline }
        try authorize(account)
        guard projectVisible(draft.projectID, account: account) else { throw FixtureError.unavailable }
        guard account != Person.rita.id else { throw FixtureError.forbidden }
        guard let sent = draft.submittedAt, now.timeIntervalSince(sent) >= 0, now.timeIntervalSince(sent) < 86_400 else { throw FixtureError.expired }
        let key = account.uuidString + ":" + draft.key.uuidString
        if let saved = database.receipts[key] {
            guard saved.accountID == account, saved.draft == draft else { throw FixtureError.keyReused }
            guard let task = database.snapshot.tasks.first(where: { $0.id == saved.receipt.taskID }), projectVisible(task.projectID, account: account) else { throw FixtureError.unavailable }
            return saved.receipt
        }
        let before = database
        do {
            let receipt = try apply(draft, account: account)
            database.receipts[key] = SavedCommand(accountID: account, draft: draft, receipt: receipt)
            try persist()
            if dropReply { dropReply = false; throw FixtureError.unknown }
            return receipt
        } catch FixtureError.unknown { throw FixtureError.unknown }
        catch { database = before; throw error }
    }

    private func apply(_ draft: Draft, account: UUID) throws -> Receipt {
        guard let project = database.snapshot.projects.first(where: { $0.id == draft.projectID }), !project.archived else { throw FixtureError.forbidden }
        if draft.kind == .create {
            guard createProjects(account).contains(project.id) else { throw FixtureError.forbidden }
            try validateTask(draft)
            let count = database.snapshot.tasks.filter { $0.projectID == project.id }.count + 1
            let task = WorkTask(key: "\(project.number)-T\(String(format: "%03d", count))", name: draft.title.trimmingCharacters(in: .whitespacesAndNewlines), projectID: project.id, ownerID: draft.ownerID, reviewerID: draft.reviewerID, requiresReview: draft.requiresReview, due: draft.due, priority: draft.priority)
            database.snapshot.tasks.append(task)
            return Receipt(id: UUID(), taskID: task.id, message: "Task created in simulator")
        }
        guard let index = database.snapshot.tasks.firstIndex(where: { $0.id == draft.taskID && $0.projectID == draft.projectID }) else { throw FixtureError.unavailable }
        var task = database.snapshot.tasks[index]
        let rights = permissions(task, account: account)
        switch draft.kind {
        case .create: break
        case .edit:
            guard rights.edit else { throw FixtureError.forbidden }
            try checkVersion(draft.version, task.version)
            try validateTask(draft)
            if draft.due != task.due && draft.reason.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { throw FixtureError.validation("Explain why the due date is changing.") }
            task.name = draft.title.trimmingCharacters(in: .whitespacesAndNewlines)
            task.ownerID = draft.ownerID; task.reviewerID = draft.reviewerID
            task.due = draft.due; task.priority = draft.priority; task.progress = draft.progress
            task.version += 1
        case .transition:
            try checkVersion(draft.version, task.version)
            guard rights.transitions.contains(draft.status) else { throw FixtureError.forbidden }
            if draft.status == .onHold && draft.reason.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { throw FixtureError.validation("A reason is required to put work on hold.") }
            if draft.status == .ready {
                guard let reviewer = draft.reviewerID, reviewer != task.ownerID, [Person.alex.id, Person.sam.id].contains(reviewer) else { throw FixtureError.validation("Choose an independent, active reviewer.") }
                task.reviewerID = reviewer
                database.snapshot.notices.append(Notice(personID: reviewer, title: "\(task.key) is ready for review", taskID: task.id))
            }
            task.status = draft.status
            if draft.status == .complete { task.progress = 100 }
            task.version += 1
        case .comment:
            guard rights.comment else { throw FixtureError.forbidden }
            let body = draft.body.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !body.isEmpty, body.count <= 5000 else { throw FixtureError.validation("Enter a comment of 1–5,000 characters.") }
            if let person = draft.mentionID {
                guard Person.fixtures.contains(where: { $0.id == person }), projectVisible(project.id, account: person) else { throw FixtureError.validation("This mention recipient is unavailable.") }
                database.snapshot.notices.append(Notice(personID: person, title: "You were mentioned on \(task.key)", taskID: task.id))
            }
            database.snapshot.comments.append(Comment(taskID: task.id, authorID: account, body: body, mentionID: draft.mentionID))
        case .time, .correctTime:
            guard rights.time else { throw FixtureError.forbidden }
            guard OrgDate.parse(draft.workDate) != nil else { throw FixtureError.validation("Choose a valid organisation-local work date.") }
            let hours = try OrgDate.hours(draft.hours, locale: Locale(identifier: "en_US_POSIX"))
            var previous: TimeEntry?
            if draft.kind == .correctTime {
                guard let entry = database.snapshot.time.first(where: { $0.id == draft.entryID && $0.personID == account && $0.taskID == task.id }) else { throw FixtureError.forbidden }
                try checkVersion(draft.version, entry.version)
                previous = entry
            }
            let total = database.snapshot.time.filter { $0.personID == account && $0.date == draft.workDate && $0.id != previous?.id }.reduce(Decimal.zero) { $0 + $1.hours }
            guard total + hours <= 24 else { throw FixtureError.validation("Your combined actual hours for this date would exceed 24.") }
            let entry = TimeEntry(id: previous?.id ?? UUID(), taskID: task.id, personID: account, date: draft.workDate, hours: hours, note: draft.body, version: (previous?.version ?? 0) + 1)
            database.snapshot.time.removeAll { $0.id == entry.id }
            database.snapshot.time.append(entry)
        }
        database.snapshot.tasks[index] = task
        return Receipt(id: UUID(), taskID: task.id, message: "Saved in simulator")
    }
    private func validateTask(_ draft: Draft) throws {
        guard !draft.title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, draft.title.count <= 200 else { throw FixtureError.validation("Enter a task title of 1–200 characters.") }
        guard [Person.alex.id, Person.sam.id].contains(draft.ownerID), (0...100).contains(draft.progress), ["Low", "Normal", "High", "Urgent"].contains(draft.priority) else { throw FixtureError.validation("Choose a valid owner, progress and priority.") }
        if let reviewer = draft.reviewerID, reviewer == draft.ownerID || ![Person.alex.id, Person.sam.id].contains(reviewer) { throw FixtureError.validation("The reviewer must be independent of the owner.") }
        if let due = draft.due, OrgDate.parse(due) == nil { throw FixtureError.validation("Choose a valid due date.") }
    }
    private func checkVersion(_ expected: Int?, _ current: Int) throws {
        guard let expected else { throw FixtureError.validation("A current row version is required (428).") }
        guard expected == current else { throw FixtureError.conflict }
    }
    private func persist() throws {
        if let file { try ProtectedStorage.write(JSONEncoder().encode(database), to: file) }
    }
}

enum ProtectedStorage {
    static func write(_ data: Data, to file: URL) throws {
        try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true,
            attributes: [.protectionKey: FileProtectionType.complete])
        try data.write(to: file, options: [.atomic, .completeFileProtection])
        var url = file
        var values = URLResourceValues()
        values.isExcludedFromBackup = true
        try url.setResourceValues(values)
    }
}
