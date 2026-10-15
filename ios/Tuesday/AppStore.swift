import Foundation
import Observation

@MainActor @Observable
final class AppStore {
    let server: FixtureServer
    private let directory: URL
    private var generation = UUID()
    private var storageFailed = false
    var account: Person?
    var snapshot: Snapshot?
    var permissions: [UUID: TaskPermissions] = [:]
    var createIn: [UUID] = []
    var verified = false
    var loading = false
    var saving = false
    var offline = false
    var lastFetch: Date?
    var error: String?
    var saveMessage: String?
    var savedDraft: Draft?
    var editor: Draft?
    var saveState = "Draft"
    var receipt: Receipt?
    var selectedTask: WorkTask?
    var selectedProject: Project?
    var pendingLink: AppLink?
    var linkError: String?
    var selectedTab = 0

    init(directory: URL? = nil, restoreAccount: Bool = true) {
        self.directory = directory ?? FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("TuesdaySimulator-v1", isDirectory: true)
        #if DEBUG && targetEnvironment(simulator)
        if ProcessInfo.processInfo.arguments.contains("--reset-fixtures"), directory == nil {
            try? FileManager.default.removeItem(at: self.directory)
            UserDefaults.standard.removeObject(forKey: "TuesdaySimulatorAccount")
        }
        #endif
        do { server = try FixtureServer(file: self.directory.appendingPathComponent("synthetic-database.json")) }
        catch {
            server = try! FixtureServer()
            storageFailed = true
            self.error = "Simulator storage could not be read. Remove this app from the simulator and rebuild to reset its synthetic storage."
        }
        if restoreAccount, let id = UserDefaults.standard.string(forKey: "TuesdaySimulatorAccount"), let uuid = UUID(uuidString: id) {
            account = Person.fixtures.first { $0.id == uuid }
        }
    }

    var tasks: [WorkTask] { snapshot?.tasks ?? [] }
    var projects: [Project] { snapshot?.projects ?? [] }
    var unread: Int { snapshot?.notices.filter { !$0.read }.count ?? 0 }
    var hasRetainedInput: Bool {
        savedDraft != nil || account.map { FileManager.default.fileExists(atPath: draftFile($0.id).path) } == true
    }
    func person(_ id: UUID?) -> String { Person.fixtures.first { $0.id == id }?.name ?? "Unassigned" }
    func project(_ id: UUID) -> Project? { projects.first { $0.id == id } }

    func signIn(_ person: Person) async {
        guard !storageFailed else { return }
        if let current = account, current.id != person.id {
            do { try signOut() }
            catch { self.error = error.localizedDescription; return }
        }
        generation = UUID()
        account = person
        snapshot = nil; permissions = [:]; verified = false; savedDraft = nil; editor = nil
        lastFetch = nil; error = nil; saveMessage = nil; receipt = nil; saveState = "Draft"
        UserDefaults.standard.set(person.id.uuidString, forKey: "TuesdaySimulatorAccount")
        await refresh()
        if verified { loadDraft() }
    }
    func refresh() async {
        guard let account, !storageFailed else { return }
        let request = UUID()
        generation = request
        loading = true
        do {
            let next = try await server.fetch(account.id)
            var rights: [UUID: TaskPermissions] = [:]
            for task in next.tasks { rights[task.id] = await server.permissions(task, account: account.id) }
            let create = await server.createProjects(account.id)
            guard generation == request, self.account?.id == account.id else { return }
            snapshot = next; permissions = rights; createIn = create
            verified = true; lastFetch = Date(); error = nil; loading = false
            if savedDraft == nil { loadDraft() }
            routePendingLink()
        } catch {
            guard generation == request, self.account?.id == account.id else { return }
            loading = false
            self.error = error.localizedDescription
            if error as? FixtureError == .forbidden || error as? FixtureError == .unavailable {
                snapshot = nil; permissions = [:]; verified = false; selectedTask = nil; selectedProject = nil
                let file = draftFile(account.id)
                if FileManager.default.fileExists(atPath: file.path) {
                    do { try FileManager.default.removeItem(at: file) }
                    catch { storageFailed = true; self.error = "Access was revoked, but local draft cleanup failed. Remove this simulator app to clear its protected state." }
                }
                savedDraft = nil; editor = nil
            }
        }
    }
    func lock() {
        generation = UUID()
        verified = false; snapshot = nil; permissions = [:]; createIn = []
        selectedTask = nil; selectedProject = nil; editor = nil; loading = false
    }
    func signOut() throws {
        guard !saving else { throw FixtureError.validation("Wait for the current save to finish before signing out.") }
        if let account, FileManager.default.fileExists(atPath: draftFile(account.id).path) {
            try FileManager.default.removeItem(at: draftFile(account.id))
        }
        lock()
        account = nil; savedDraft = nil; editor = nil; receipt = nil
        selectedTab = 0
        lastFetch = nil; error = nil; saveMessage = nil; saveState = "Draft"
        UserDefaults.standard.removeObject(forKey: "TuesdaySimulatorAccount")
    }
    func setOffline(_ value: Bool) async {
        offline = value
        await server.setOffline(value)
        if !value { await refresh() }
    }
    func openTask(_ id: UUID) async {
        guard !offline else { error = FixtureError.offline.localizedDescription; return }
        await refresh()
        guard verified, error == nil, let current = tasks.first(where: { $0.id == id }) else {
            selectedTask = nil; linkError = FixtureError.unavailable.localizedDescription; return
        }
        selectedTask = current
    }
    func open(_ url: URL) {
        guard let link = AppLink(url) else {
            linkError = "This link is not a supported Tuesday project or task link. Open the original view in Tuesday web."
            return
        }
        pendingLink = link
        if verified { Task { await refresh() } }
    }
    private func routePendingLink() {
        guard let pendingLink else { return }
        self.pendingLink = nil
        switch pendingLink {
        case .task(let id):
            if let task = tasks.first(where: { $0.id == id }) { selectedTask = task }
            else { linkError = FixtureError.unavailable.localizedDescription }
        case .project(let id):
            if let project = projects.first(where: { $0.id == id }) { selectedProject = project }
            else { linkError = FixtureError.unavailable.localizedDescription }
        }
    }

    func begin(_ kind: DraftKind, task: WorkTask? = nil, projectID: UUID? = nil, entry: TimeEntry? = nil) {
        guard savedDraft == nil else { editor = savedDraft; return }
        guard !hasRetainedInput else { error = "Retained input could not be read. It will not be overwritten. Sign out only if you intend to discard it."; return }
        guard let pid = task?.projectID ?? projectID ?? projects.first(where: { createIn.contains($0.id) })?.id else { return }
        var draft = Draft(kind: kind, taskID: task?.id, projectID: pid, version: task?.version,
            entryID: entry?.id, title: task?.name ?? "", ownerID: task?.ownerID ?? account?.id ?? Person.alex.id,
            reviewerID: task?.reviewerID, due: task?.due, priority: task?.priority ?? "Normal", progress: task?.progress ?? 0,
            status: task.flatMap { permissions[$0.id]?.transitions.first } ?? .inProgress,
            requiresReview: task?.requiresReview ?? true)
        if let entry {
            draft.version = entry.version; draft.hours = NSDecimalNumber(decimal: entry.hours).stringValue
            draft.workDate = entry.date; draft.body = entry.note
        }
        if kind == .edit, let task { draft.baseline = TaskEditBaseline(task) }
        saveState = "Draft"; saveMessage = nil; receipt = nil
        keep(draft)
        editor = draft
    }
    private func draftFile(_ accountID: UUID) -> URL { directory.appendingPathComponent("draft-\(accountID).json") }
    func keep(_ draft: Draft) {
        guard let account else { return }
        do {
            try ProtectedStorage.write(JSONEncoder().encode(draft), to: draftFile(account.id))
            savedDraft = draft
        } catch { self.error = "Draft could not be protected and saved locally. Keep this screen open and try again." }
    }
    private func loadDraft() {
        guard let account else { return }
        let file = draftFile(account.id)
        guard FileManager.default.fileExists(atPath: file.path) else { return }
        do {
            savedDraft = try JSONDecoder().decode(Draft.self, from: Data(contentsOf: file))
            saveState = savedDraft?.submittedAt == nil ? "Draft" : "Outcome unknown"
        } catch { self.error = "The retained draft could not be read. It has not been overwritten." }
    }
    func discardDraft() throws {
        guard !saving else { throw FixtureError.validation("Wait for the current save to finish.") }
        guard savedDraft?.submittedAt == nil else { throw FixtureError.validation("Resolve or explicitly reconcile the submitted request before discarding it.") }
        if let account, FileManager.default.fileExists(atPath: draftFile(account.id).path) { try FileManager.default.removeItem(at: draftFile(account.id)) }
        savedDraft = nil; editor = nil; saveState = "Draft"; saveMessage = nil
    }
    func save(_ input: Draft) async {
        guard !saving, verified, !offline, let account else { saveMessage = FixtureError.offline.localizedDescription; return }
        saving = true; saveState = "Saving"; saveMessage = nil
        defer { saving = false }
        var command = savedDraft?.submittedAt != nil ? savedDraft! : input
        do {
            if command.submittedAt == nil {
                if command.kind == .time || command.kind == .correctTime {
                    command.hours = NSDecimalNumber(decimal: try OrgDate.hours(command.hours)).stringValue
                }
                command.submittedAt = Date()
            }
            // Durably retain the exact command before sending it; storage failure stops submission.
            try ProtectedStorage.write(JSONEncoder().encode(command), to: draftFile(account.id))
            savedDraft = command
            let result = try await server.submit(command, account: account.id)
            guard self.account?.id == account.id else { return }
            receipt = result
            saveState = "Saved"
            saveMessage = "\(result.message). Receipt \(result.id.uuidString.prefix(8))."
            do { try FileManager.default.removeItem(at: draftFile(account.id)) }
            catch {
                // Keep the acknowledged request immutable if local cleanup fails.
                saveMessage = "Saved in simulator. Local receipt cleanup failed; resolve the retained request after reopening."
                await refresh()
                return
            }
            savedDraft = nil
            await refresh()
            if error != nil { saveMessage = "Saved in simulator; refresh unavailable. Receipt \(result.id.uuidString.prefix(8))." }
        } catch {
            guard self.account?.id == account.id else { return }
            let problem = error as? FixtureError
            saveState = problem == .conflict ? "Conflict" : problem == .unknown || problem == .expired ? "Outcome unknown" : "Failed"
            saveMessage = error.localizedDescription
            if problem != .unknown && problem != .expired {
                command.submittedAt = nil
                keep(command)
            }
            if problem == .conflict { await refresh() }
        }
    }
    func rebaseDraft() {
        guard saveState == "Conflict", var draft = savedDraft, let task = tasks.first(where: { $0.id == draft.taskID }) else { return }
        if draft.kind == .correctTime {
            guard let entry = snapshot?.time.first(where: { $0.id == draft.entryID }) else { return }
            draft.version = entry.version
        } else {
            if draft.kind == .edit, let base = draft.baseline {
                // Retain intended edits while adopting other people's changes to untouched fields.
                if draft.title == base.title { draft.title = task.name }
                if draft.ownerID == base.ownerID { draft.ownerID = task.ownerID }
                if draft.reviewerID == base.reviewerID { draft.reviewerID = task.reviewerID }
                if draft.due == base.due { draft.due = task.due }
                if draft.priority == base.priority { draft.priority = task.priority }
                if draft.progress == base.progress { draft.progress = task.progress }
                draft.baseline = TaskEditBaseline(task)
            }
            draft.version = task.version
        }
        draft.key = UUID(); draft.submittedAt = nil
        keep(draft); editor = draft; saveState = "Draft"; saveMessage = "Current version selected. Review your intended values, then explicitly save."
    }
    func reconcileExpiredDraft(now: Date = Date()) throws {
        guard !saving, let sent = savedDraft?.submittedAt, now.timeIntervalSince(sent) >= 86_400 else {
            throw FixtureError.validation("Resolve the original request within its retry window.")
        }
        if let account { try FileManager.default.removeItem(at: draftFile(account.id)) }
        savedDraft = nil; editor = nil; saveState = "Draft"; saveMessage = nil
    }
    func markRead(_ ids: [UUID]) async {
        guard let account, verified, !saving else { return }
        saving = true
        defer { saving = false }
        do { try await server.markRead(ids, account: account.id); await refresh() }
        catch { self.error = error.localizedDescription }
    }
}
