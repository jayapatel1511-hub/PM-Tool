import SwiftUI

struct TaskRow: View {
    @Environment(AppStore.self) private var store
    let task: WorkTask
    var body: some View {
        Button { Task { await store.openTask(task.id) } } label: {
            VStack(alignment: .leading, spacing: 6) {
                Text(task.name).font(.headline).foregroundStyle(Color.primary).multilineTextAlignment(.leading)
                Text("\(store.project(task.projectID)?.number ?? "") · \(store.project(task.projectID)?.discipline ?? "")").font(.caption).foregroundStyle(Color.secondary)
                ViewThatFits(in: .horizontal) {
                    HStack { StatusLabel(status: task.status); due }
                    VStack(alignment: .leading) { StatusLabel(status: task.status); due }
                }
                if let blocker = task.blocker { Label(blocker, systemImage: "hand.raised").font(.caption).foregroundStyle(Color.secondary).multilineTextAlignment(.leading) }
            }.padding(.vertical, 6).frame(maxWidth: .infinity, minHeight: 44, alignment: .leading)
        }.accessibilityIdentifier("task-\(task.key)")
    }
    private var due: some View { Text(task.due.map { "Due \($0)" } ?? "No due date").font(.caption).foregroundStyle(task.due != nil && task.due! <= OrgDate.today() && task.status != .complete ? .red : .secondary) }
}

struct StatusLabel: View {
    let status: TaskStatus
    var body: some View {
        Label(status.rawValue, systemImage: status == .complete ? "checkmark.circle" : status == .onHold ? "pause.circle" : status == .ready || status == .reviewing ? "person.badge.shield.checkmark" : "circle.lefthalf.filled")
            .font(.caption.weight(.medium))
            .foregroundStyle(status == .complete ? .green : status == .ready || status == .reviewing ? .purple : .primary)
    }
}

struct DraftRow: View {
    @Environment(AppStore.self) private var store
    var isTaskContext = false
    var body: some View {
        if let draft = store.savedDraft {
            Section("Continue where you left off") {
                Button { store.editor = draft } label: {
                    Label("\(store.saveState): \(draft.kind.rawValue)", systemImage: draft.submittedAt == nil ? "square.and.pencil" : "exclamationmark.arrow.triangle.2.circlepath")
                        .frame(minHeight: 44).multilineTextAlignment(.leading)
                }.accessibilityIdentifier(isTaskContext ? "resume-task-draft" : "resume-draft")
            }
        }
    }
}

struct MyWorkView: View {
    @Environment(AppStore.self) private var store
    @State private var search = ""
    private var assigned: [WorkTask] {
        store.tasks.filter { $0.ownerID == store.account?.id && $0.status != .complete && matches($0) }
    }
    private var reviews: [WorkTask] {
        store.tasks.filter { $0.reviewerID == store.account?.id && [.ready, .reviewing].contains($0.status) && matches($0) }
    }
    var body: some View {
        List {
            ConnectionBanner()
            Section {
                Text("Your next useful step.").font(.title2.bold())
                Text("Assigned work and technical reviews, with the context to act.").foregroundStyle(.secondary)
            }
            DraftRow()
            if !reviews.isEmpty { Section("Your reviews · \(reviews.count)") { ForEach(reviews) { TaskRow(task: $0) } } }
            if !assigned.isEmpty { Section("Assigned to you · \(assigned.count)") { ForEach(assigned) { TaskRow(task: $0) } } }
            if assigned.isEmpty && reviews.isEmpty {
                ContentUnavailableView(search.isEmpty ? "No assigned work" : "No matching work", systemImage: "checkmark.circle", description: Text(search.isEmpty ? "Your assigned tasks and review requests will appear here." : "Try another task name or project number."))
            }
            if search.isEmpty && store.account?.id == Person.alex.id {
                Section("Other owned work") {
                    VStack(alignment: .leading, spacing: 8) {
                        Label("Confirm utility coordination decision", systemImage: "arrow.triangle.branch").font(.headline)
                        Text("SYN-2401 · Civil · Decision due this week").font(.caption).foregroundStyle(.secondary)
                        Text("Decisions and formal review disposition continue in Tuesday web.").font(.footnote)
                        Link("Open in Tuesday web", destination: URL(string: "https://pm.engcalchub.com/projects/\(Project.harbour.id)/coordination")!).frame(minHeight: 44)
                        Text("Synthetic identity: this fixture has no matching hosted record.").font(.caption).foregroundStyle(.secondary)
                    }.padding(.vertical, 4)
                }
            }
        }
        .searchable(text: $search, prompt: "Task or project")
        .refreshable { await store.refresh() }
        .toolbar {
            if !store.createIn.isEmpty {
                ToolbarItem(placement: .topBarLeading) { Button { store.begin(.create) } label: { Label("New task", systemImage: "plus").frame(minHeight: 44) }.accessibilityIdentifier("new-task") }
            }
        }
    }
    private func matches(_ task: WorkTask) -> Bool {
        search.isEmpty || "\(task.name) \(task.key) \(store.project(task.projectID)?.number ?? "")".localizedCaseInsensitiveContains(search)
    }
}

struct ProjectsView: View {
    @Environment(AppStore.self) private var store
    @State private var search = ""
    private var filtered: [Project] { store.projects.filter { search.isEmpty || "\($0.number) \($0.name)".localizedCaseInsensitiveContains(search) } }
    var body: some View {
        List {
            ConnectionBanner()
            ForEach(filtered) { project in
                NavigationLink { ProjectView(projectID: project.id) } label: {
                    VStack(alignment: .leading, spacing: 6) {
                        Text(project.number).font(.caption).foregroundStyle(.secondary)
                        Text(project.name).font(.headline)
                        Label(project.health, systemImage: project.health == "On track" ? "checkmark.circle" : "exclamationmark.circle").font(.subheadline)
                    }.padding(.vertical, 6)
                }.accessibilityIdentifier("project-\(project.number)")
            }
            if filtered.isEmpty { ContentUnavailableView.search(text: search) }
        }
        .searchable(text: $search, prompt: "Project name or number")
        .refreshable { await store.refresh() }
    }
}

struct ProjectView: View {
    @Environment(AppStore.self) private var store
    let projectID: UUID
    @State private var search = ""
    @State private var limit = 10
    private var project: Project? { store.project(projectID) }
    private var tasks: [WorkTask] { store.tasks.filter { $0.projectID == projectID && (search.isEmpty || "\($0.name) \($0.key)".localizedCaseInsensitiveContains(search)) }.sorted { $0.key < $1.key } }
    var body: some View {
        List {
            ConnectionBanner()
            if let project {
                Section {
                    Text(project.number).font(.caption).foregroundStyle(.secondary)
                    Text(project.name).font(.title2.bold())
                    Label(project.health, systemImage: "flag").font(.headline)
                    Text(project.attention)
                    LabeledContent("Discipline", value: project.discipline)
                    LabeledContent("Deliverable", value: project.deliverable)
                }
                Section("Tasks · \(tasks.count)") {
                    ForEach(tasks.prefix(limit)) { TaskRow(task: $0) }
                    if tasks.count > limit { Button("Load more tasks") { limit += 10 }.frame(minHeight: 44).accessibilityIdentifier("load-more") }
                    if tasks.isEmpty { ContentUnavailableView("No matching tasks", systemImage: "magnifyingglass") }
                }
                Section {
                    if store.createIn.contains(projectID) { Button("New task in this discipline") { store.begin(.create, projectID: projectID) }.frame(minHeight: 44) }
                    Link("Open project in Tuesday web", destination: URL(string: "https://pm.engcalchub.com/projects/\(projectID)")!).frame(minHeight: 44)
                    Text("Synthetic project · no matching hosted record. Document links and planner edits remain in the web workspace.").font(.footnote).foregroundStyle(.secondary)
                }
            } else { ContentUnavailableView("Project unavailable", systemImage: "lock") }
        }
        .navigationTitle(project?.number ?? "Project")
        .navigationBarTitleDisplayMode(.inline)
        .searchable(text: $search, prompt: "Find a project task")
        .onChange(of: search) { _, _ in limit = 10 }
        .refreshable { await store.refresh() }
    }
}

struct TaskDetailView: View {
    @Environment(AppStore.self) private var store
    @Environment(\.dismiss) private var dismiss
    let taskID: UUID
    @State private var scenarioMessage: String?
    private var task: WorkTask? { store.tasks.first { $0.id == taskID } }
    var body: some View {
        @Bindable var store = store
        List {
            ConnectionBanner()
            if let task {
                Section {
                    Text("\(store.project(task.projectID)?.number ?? "") → \(store.project(task.projectID)?.discipline ?? "")").font(.caption).foregroundStyle(.secondary)
                    Text(store.project(task.projectID)?.deliverable ?? "No deliverable").font(.subheadline)
                    Text(task.name).font(.title2.bold()).accessibilityIdentifier("task-title")
                    Text(task.key).font(.caption.monospaced()).foregroundStyle(.secondary)
                    StatusLabel(status: task.status).accessibilityIdentifier("task-status")
                    Text(task.description)
                    LabeledContent("Owner", value: store.person(task.ownerID))
                    LabeledContent("Reviewer", value: store.person(task.reviewerID))
                    LabeledContent("Due", value: task.due ?? "No due date")
                    LabeledContent("Priority", value: task.priority)
                    LabeledContent("Progress", value: "\(task.progress)%")
                    if let blocker = task.blocker { Label(blocker, systemImage: "hand.raised").foregroundStyle(.orange) }
                }
                DraftRow(isTaskContext: true)
                if let rights = store.permissions[taskID] {
                    Section("Actions") {
                        if let reason = rights.reason { Text(reason).foregroundStyle(.secondary) }
                        if task.requiresReview { Label("Independent technical review required", systemImage: "person.badge.shield.checkmark").font(.subheadline) }
                        if rights.edit { Button("Edit task") { store.begin(.edit, task: task) }.frame(minHeight: 44).accessibilityIdentifier("edit-task") }
                        if !rights.transitions.isEmpty {
                            Button(task.status == .ready ? "Start review" : task.requiresReview && task.status == .inProgress ? "Request review" : "Update status") { store.begin(.transition, task: task) }.frame(minHeight: 44).accessibilityIdentifier("transition-task")
                        }
                        if rights.comment { Button("Add comment") { store.begin(.comment, task: task) }.frame(minHeight: 44).accessibilityIdentifier("add-comment") }
                        if rights.time { Button("Record time") { store.begin(.time, task: task) }.frame(minHeight: 44).accessibilityIdentifier("record-time") }
                        Link("Open full review in Tuesday web", destination: AppLink.taskURL(task)).frame(minHeight: 44)
                        Text("Synthetic task: web handoff demonstrates the URL only; this identity is not hosted.").font(.caption).foregroundStyle(.secondary)
                    }.disabled(store.offline || store.saving)
                }
                Section("Comments") {
                    ForEach(store.snapshot?.comments.filter { $0.taskID == taskID } ?? []) { comment in
                        VStack(alignment: .leading, spacing: 6) {
                            Text(comment.deleted ? "Deleted comment" : store.person(comment.authorID)).font(.subheadline.bold())
                            if !comment.deleted {
                                Text(comment.body).textSelection(.enabled)
                                if let mention = comment.mentionID { Text("Mention: \(store.person(mention))").font(.caption).foregroundStyle(.secondary) }
                            }
                        }.padding(.vertical, 4)
                    }
                    if store.snapshot?.comments.contains(where: { $0.taskID == taskID }) != true { Text("No comments yet").foregroundStyle(.secondary) }
                }
                Section("Your actual time") {
                    ForEach(store.snapshot?.time.filter { $0.taskID == taskID } ?? []) { entry in
                        VStack(alignment: .leading, spacing: 6) {
                            Text("\(NSDecimalNumber(decimal: entry.hours).stringValue) h · \(entry.date)").font(.headline)
                            if !entry.note.isEmpty { Text(entry.note) }
                            Button("Correct entry") { store.begin(.correctTime, task: task, entry: entry) }.frame(minHeight: 44).disabled(store.offline || store.permissions[taskID]?.time != true)
                        }
                    }
                    Text("Recorded effort is separate from estimates and weekly plans.").font(.footnote).foregroundStyle(.secondary)
                }
                Section {
                    DisclosureGroup("Simulator conflict scenario") {
                        Text("Open an edit draft and close it to retain your input. Then simulate another person's update and resume the draft.").font(.footnote)
                        Button("Simulate another person's update") {
                            Task {
                                do { try await store.server.externalChange(taskID); await store.refresh(); scenarioMessage = "Fixture task changed to 75% with a new version." }
                                catch { scenarioMessage = error.localizedDescription }
                            }
                        }.frame(minHeight: 44)
                        if let scenarioMessage { Text(scenarioMessage).font(.footnote) }
                    }
                }
            } else { ContentUnavailableView("Task unavailable", systemImage: "lock", description: Text("Refresh and check your current access.")) }
        }
        .navigationTitle("Task context")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss(); store.selectedTask = nil } } }
        .refreshable { await store.refresh() }
        .sheet(item: $store.editor) { draft in DraftEditor(initial: draft).environment(store) }
    }
}

struct InboxView: View {
    @Environment(AppStore.self) private var store
    var body: some View {
        List {
            ConnectionBanner()
            Section {
                Text("\(store.unread) unread").font(.title2.bold())
                Text("Opening a notification keeps it unread until you mark it read.").font(.subheadline).foregroundStyle(.secondary)
                if store.unread > 0 { Button("Mark all read") { Task { await store.markRead(store.snapshot?.notices.map(\.id) ?? []) } }.frame(minHeight: 44).disabled(store.saving || store.offline) }
            }
            ForEach(store.snapshot?.notices ?? []) { notice in
                VStack(alignment: .leading, spacing: 8) {
                    Button { if let id = notice.taskID { Task { await store.openTask(id) } } } label: {
                        Label(notice.title, systemImage: notice.read ? "envelope.open" : "envelope.badge").multilineTextAlignment(.leading).frame(minHeight: 44)
                    }
                    if !notice.read { Button("Mark read") { Task { await store.markRead([notice.id]) } }.frame(minHeight: 44).disabled(store.saving || store.offline) }
                }.buttonStyle(.borderless)
            }
            if store.snapshot?.notices.isEmpty == true { ContentUnavailableView("All quiet", systemImage: "tray", description: Text("Your permitted notifications will appear here.")) }
        }.refreshable { await store.refresh() }
    }
}

struct MyWeekView: View {
    @Environment(AppStore.self) private var store
    @State private var offset = 0
    @State private var week: WeekContext?
    @State private var error: String?
    var body: some View {
        List {
            ConnectionBanner()
            Section {
                HStack {
                    Button { offset -= 1 } label: { Image(systemName: "chevron.left").frame(minWidth: 44, minHeight: 44) }.accessibilityLabel("Previous week")
                    Spacer()
                    Text(OrgDate.week(offset)).font(.headline).accessibilityLabel("Week starting \(OrgDate.week(offset))")
                    Spacer()
                    Button { offset += 1 } label: { Image(systemName: "chevron.right").frame(minWidth: 44, minHeight: 44) }.accessibilityLabel("Next week")
                }.buttonStyle(.borderless)
                Text("Your published and permitted plan").font(.title2.bold())
                Text("Week starts Monday · America/Halifax").font(.caption).foregroundStyle(.secondary)
            }
            if let week {
                Section("Planning context") {
                    LabeledContent("Planned hours", value: "\(NSDecimalNumber(decimal: week.planned).stringValue) h")
                    LabeledContent("Available hours", value: week.available.map { "\(NSDecimalNumber(decimal: $0).stringValue) h" } ?? "Unknown")
                    LabeledContent("Confidence", value: week.confidence)
                    Label(week.explanation, systemImage: "info.circle")
                    Text("Read only. Plans are not recorded actual time.").font(.footnote).foregroundStyle(.secondary)
                }
            }
            if let error { Text(error).foregroundStyle(.red) }
            Section {
                Link("Open planner in Tuesday web", destination: URL(string: "https://pm.engcalchub.com/planner?from=\(OrgDate.week(offset))&week=\(OrgDate.week(offset))")!).frame(minHeight: 44)
                Text("Synthetic week context; hosted plans are not connected.").font(.footnote).foregroundStyle(.secondary)
            }
        }.task(id: offset) { await load() }.refreshable { await store.refresh(); await load() }
    }
    private func load() async {
        guard let account = store.account, !store.offline else { error = FixtureError.offline.localizedDescription; return }
        do { week = try await store.server.week(offset, account: account.id); error = nil }
        catch { self.error = error.localizedDescription }
    }
}
