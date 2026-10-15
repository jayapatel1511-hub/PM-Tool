import SwiftUI

struct DraftEditor: View {
    @Environment(AppStore.self) private var store
    @Environment(\.dismiss) private var dismiss
    @State private var draft: Draft
    @State private var confirmDiscard = false
    @State private var confirmReconcile = false
    @State private var localError: String?
    private enum Field: Hashable { case title, reason, body, hours }
    @FocusState private var focused: Field?
    init(initial: Draft) { _draft = State(initialValue: initial) }
    private var submitted: Bool { store.savedDraft?.submittedAt != nil }
    private var saved: Bool { store.saveState == "Saved" }
    private var task: WorkTask? { store.tasks.first { $0.id == draft.taskID } }

    var body: some View {
        NavigationStack {
            Form {
                Section("Context") {
                    if draft.kind == .create {
                        Picker("Project and discipline", selection: $draft.projectID) {
                            ForEach(store.projects.filter { store.createIn.contains($0.id) }) { project in
                                Text("\(project.number) · \(project.discipline)").tag(project.id)
                            }
                        }.accessibilityIdentifier("project-picker")
                        Text(store.project(draft.projectID)?.name ?? "Select a permitted project")
                    } else {
                        Text(store.project(draft.projectID)?.number ?? "Project unavailable")
                        Text(task?.name ?? "Task unavailable").font(.headline).accessibilityIdentifier("draft-task-context")
                    }
                    Text("Synthetic simulator workspace").font(.caption).foregroundStyle(.secondary)
                }.disabled(submitted || saved || store.saving)

                Group {
                    switch draft.kind {
                    case .create, .edit: taskFields
                    case .transition: transitionFields
                    case .comment: commentFields
                    case .time, .correctTime: timeFields
                    }
                }.disabled(submitted || saved || store.saving)

                if store.saveState == "Conflict", let current = task {
                    Section("Current fixture state") {
                        Text("Your intended values are retained above. Current task values:")
                        LabeledContent("Title", value: current.name)
                        LabeledContent("Progress", value: "\(current.progress)%")
                        LabeledContent("Status", value: current.status.rawValue)
                        LabeledContent("Due", value: current.due ?? "No due date")
                        LabeledContent("Owner", value: store.person(current.ownerID))
                        Button("Use current version and review draft") {
                            store.rebaseDraft()
                            if let next = store.savedDraft { draft = next }
                        }.frame(minHeight: 44).accessibilityIdentifier("resolve-conflict")
                    }
                }
                Section("Save state") {
                    if let message = store.saveMessage { Text(message).accessibilityIdentifier("save-message") }
                    if let localError { Text(localError).foregroundStyle(.red) }
                    if submitted {
                        Text("The original request is retained and locked. Retry resolves that same action; it does not create a new request.").font(.footnote)
                        if let sent = store.savedDraft?.submittedAt, Date().timeIntervalSince(sent) >= 86_400 {
                            Text("The retry window has ended. Close this sheet and inspect the task, comments and actual time. Confirm the outcome before clearing the retained request.")
                            Button("I checked the records; reconcile request") { confirmReconcile = true }.frame(minHeight: 44)
                        }
                    } else if !saved { Text("Input is kept on this simulator for this account until saved, discarded or signed out. Closing this sheet keeps your draft.").font(.footnote).foregroundStyle(.secondary) }
                }
            }
            .environment(\.timeZone, OrgDate.zone)
            .navigationTitle(draft.kind.rawValue)
            .navigationBarTitleDisplayMode(.inline)
            .scrollDismissesKeyboard(.interactively)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button(saved ? "Done" : "Close") { dismiss() }.disabled(store.saving) }
                ToolbarItem(placement: .keyboard) { Button("Done entering") { focused = nil } }
                if !submitted && !saved {
                    ToolbarItem(placement: .topBarTrailing) { Button("Discard", role: .destructive) { confirmDiscard = true }.disabled(store.saving) }
                }
            }
            .safeAreaInset(edge: .bottom) {
                if focused == nil {
                VStack(spacing: 8) {
                    Label(store.saveState, systemImage: saved ? "checkmark.circle" : store.saveState == "Outcome unknown" ? "questionmark.circle" : "square.and.pencil")
                        .font(.headline).accessibilityIdentifier("save-state").accessibilityAddTraits(.updatesFrequently)
                    if store.offline { Text("Offline · Draft retained. Reconnect before saving.").font(.footnote) }
                    if saved {
                        Button("Done") { dismiss() }.buttonStyle(.borderedProminent).frame(minHeight: 44).accessibilityIdentifier("save-done")
                    } else {
                        Button {
                            focused = nil
                            Task { await store.save(draft) }
                        } label: {
                            HStack {
                                if store.saving { ProgressView().tint(.white) }
                                Text(store.saving ? "Saving…" : submitted ? "Resolve original request" : "Save in simulator")
                            }.frame(maxWidth: .infinity, minHeight: 44)
                        }
                        .buttonStyle(.borderedProminent)
                        .disabled(store.saving || store.offline || !store.verified || store.saveState == "Conflict")
                        .accessibilityIdentifier("save-draft")
                    }
                }.padding().background(.regularMaterial)
                }
            }
            .interactiveDismissDisabled(store.saving)
            .onChange(of: draft) { _, next in if !submitted && !saved { store.keep(next) } }
            .confirmationDialog("Discard this draft?", isPresented: $confirmDiscard, titleVisibility: .visible) {
                Button("Discard draft", role: .destructive) {
                    do { try store.discardDraft(); dismiss() } catch { localError = error.localizedDescription }
                }
            }
            .confirmationDialog("Confirm this request has been reconciled", isPresented: $confirmReconcile, titleVisibility: .visible) {
                Button("Clear reconciled request", role: .destructive) {
                    do { try store.reconcileExpiredDraft(); dismiss() } catch { localError = error.localizedDescription }
                }
            } message: { Text("Only continue after checking whether the original action completed. Clearing the receipt does not undo a saved action.") }
            .onDisappear {
                if saved, draft.kind == .create, let id = store.receipt?.taskID,
                   let created = store.tasks.first(where: { $0.id == id }) { store.selectedTask = created }
            }
        }
    }

    private var taskFields: some View {
        Section("Task details") {
            TextField("Task title", text: $draft.title, axis: .vertical).focused($focused, equals: .title).accessibilityIdentifier("task-name-field")
            Picker("Owner", selection: $draft.ownerID) {
                ForEach(Person.fixtures.filter { $0.id != Person.rita.id }) { Text($0.name).tag($0.id) }
            }
            Picker("Reviewer", selection: $draft.reviewerID) {
                Text("Choose a reviewer").tag(nil as UUID?)
                ForEach(Person.fixtures.filter { $0.id != draft.ownerID && $0.id != Person.rita.id }) { Text($0.name).tag(Optional($0.id)) }
            }
            Picker("Priority", selection: $draft.priority) { ForEach(["Low", "Normal", "High", "Urgent"], id: \.self) { Text($0) } }
            Toggle("Has a due date", isOn: Binding(get: { draft.due != nil }, set: { draft.due = $0 ? OrgDate.today() : nil }))
            if draft.due != nil {
                DatePicker("Due date", selection: Binding(get: { OrgDate.parse(draft.due ?? "") ?? Date() }, set: { draft.due = OrgDate.string($0) }), displayedComponents: .date)
            }
            if draft.kind == .create { Toggle("Requires independent review", isOn: $draft.requiresReview) }
            if draft.kind == .edit {
                Stepper("Progress: \(draft.progress)%", value: $draft.progress, in: 0...100, step: 5)
                if draft.due != task?.due { TextField("Reason for due-date change", text: $draft.reason, axis: .vertical).focused($focused, equals: .reason) }
            }
        }
    }
    private var transitionFields: some View {
        Section("Workflow action") {
            Picker("New status", selection: $draft.status) {
                ForEach(store.permissions[draft.taskID ?? UUID()]?.transitions ?? [], id: \.self) { Text($0.rawValue).tag($0) }
            }
            if draft.status == .ready {
                Picker("Independent reviewer", selection: $draft.reviewerID) {
                    Text("Choose a reviewer").tag(nil as UUID?)
                    ForEach(Person.fixtures.filter { $0.id != task?.ownerID && $0.id != Person.rita.id }) { Text($0.name).tag(Optional($0.id)) }
                }
                Text("This requests review. Formal review disposition stays in Tuesday web.").font(.footnote)
            }
            if draft.status == .onHold { TextField("Required reason", text: $draft.reason, axis: .vertical).focused($focused, equals: .reason) }
        }
    }
    private var commentFields: some View {
        Section("Comment") {
            TextField("Write a comment", text: $draft.body, axis: .vertical).lineLimit(4...12).focused($focused, equals: .body).accessibilityIdentifier("comment-field")
            Picker("Mention", selection: $draft.mentionID) {
                Text("No mention").tag(nil as UUID?)
                ForEach(Person.fixtures.filter { $0.id != store.account?.id && ($0.id != Person.rita.id || draft.projectID == Project.harbour.id) }) { Text($0.name).tag(Optional($0.id)) }
            }
            Text("Mention recipients are selected by identity; plain @text does not send a mention.").font(.footnote).foregroundStyle(.secondary)
        }
    }
    private var timeFields: some View {
        Section("Actual effort") {
            DatePicker("Work date", selection: Binding(get: { OrgDate.parse(draft.workDate) ?? Date() }, set: { draft.workDate = OrgDate.string($0) }), displayedComponents: .date)
            Text("Organisation date: \(draft.workDate) · America/Halifax").font(.caption).foregroundStyle(.secondary)
            TextField("Hours, e.g. 1.25", text: $draft.hours).keyboardType(.decimalPad).focused($focused, equals: .hours).accessibilityIdentifier("hours-field")
            TextField("Optional note", text: $draft.body, axis: .vertical).focused($focused, equals: .body).accessibilityIdentifier("time-note-field")
            let total = (store.snapshot?.time ?? []).filter { $0.date == draft.workDate }.reduce(Decimal.zero) { $0 + $1.hours }
            LabeledContent("Your current day total", value: "\(NSDecimalNumber(decimal: total).stringValue) h")
            Text("More than 0, at most 24 hours, with up to two decimal places. The combined day total cannot exceed 24 hours.").font(.footnote)
        }
    }
}
