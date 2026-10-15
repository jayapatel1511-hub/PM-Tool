import SwiftUI
import UIKit

@main
struct TuesdayApp: App {
    @UIApplicationDelegateAdaptor(PrivacyDelegate.self) private var delegate
    @Environment(\.scenePhase) private var phase
    #if DEBUG && targetEnvironment(simulator)
    @State private var store = AppStore()
    #endif

    var body: some Scene {
        WindowGroup {
            #if DEBUG && targetEnvironment(simulator)
            RootView()
                .environment(store)
                .tint(.purple)
                .onOpenURL { store.open($0) }
                .task { if store.account != nil { await store.refresh() } }
                .onChange(of: phase) { _, next in
                    if next == .background { store.lock() }
                    if next == .active, store.account != nil { Task { await store.refresh() } }
                }
            #else
            ContentUnavailableView("Corporate sign-in is not configured", systemImage: "lock.shield", description: Text("This build has no approved Entra registration. Use the Debug iPhone Simulator build for the synthetic Tuesday workspace."))
            #endif
        }
    }
}

// A separate window also covers presented sheets in the app-switcher snapshot.
@MainActor final class PrivacyDelegate: NSObject, UIApplicationDelegate {
    private var shields: [String: UIWindow] = [:]
    func application(_ application: UIApplication, didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]? = nil) -> Bool {
        NotificationCenter.default.addObserver(self, selector: #selector(conceal), name: UIScene.willDeactivateNotification, object: nil)
        NotificationCenter.default.addObserver(self, selector: #selector(reveal), name: UIScene.didActivateNotification, object: nil)
        NotificationCenter.default.addObserver(self, selector: #selector(reveal), name: UIScene.didDisconnectNotification, object: nil)
        return true
    }
    @objc func conceal(_ notification: Notification) {
        guard let scene = notification.object as? UIWindowScene, shields[scene.session.persistentIdentifier] == nil else { return }
            let window = UIWindow(windowScene: scene)
            window.windowLevel = .alert + 1
            let controller = UIViewController()
            controller.view.backgroundColor = .systemBackground
            let label = UILabel()
            label.text = "Tuesday · Workspace locked"
            label.font = .preferredFont(forTextStyle: .headline)
            label.translatesAutoresizingMaskIntoConstraints = false
            controller.view.addSubview(label)
            NSLayoutConstraint.activate([label.centerXAnchor.constraint(equalTo: controller.view.centerXAnchor), label.centerYAnchor.constraint(equalTo: controller.view.centerYAnchor)])
            window.rootViewController = controller
            window.isHidden = false
            shields[scene.session.persistentIdentifier] = window
    }
    @objc func reveal(_ notification: Notification) {
        guard let scene = notification.object as? UIWindowScene else { return }
        shields.removeValue(forKey: scene.session.persistentIdentifier)?.isHidden = true
    }
}

struct RootView: View {
    @Environment(AppStore.self) private var store
    @State private var accountSheet = false

    var body: some View {
        @Bindable var store = store
        Group {
            if store.account == nil { SignInView() }
            else if !store.verified {
                ContentUnavailableView {
                    Label(store.loading ? "Checking access…" : "Workspace locked", systemImage: "lock.shield")
                } description: {
                    Text(store.error ?? "Revalidating your synthetic account before showing the workspace.")
                } actions: {
                    if store.loading { ProgressView() }
                    else {
                        Button("Try again") { Task { await store.refresh() } }.buttonStyle(.borderedProminent)
                        if store.offline { Button("Reconnect simulator") { Task { await store.setOffline(false) } } }
                        Button("Sign out") { try? store.signOut() }
                    }
                }
            } else {
                TabView(selection: $store.selectedTab) {
                    tab("My Work", icon: "checklist", value: 0) { MyWorkView() }
                    tab("Projects", icon: "folder", value: 1) { ProjectsView() }
                    tab("Inbox", icon: "tray", value: 2) { InboxView() }.badge(store.unread)
                    tab("My Week", icon: "calendar", value: 3) { MyWeekView() }
                }
            }
        }
        .sheet(isPresented: $accountSheet) { AccountView() }
        .sheet(item: $store.selectedTask) { task in
            NavigationStack { TaskDetailView(taskID: task.id) }
                .environment(store)
        }
        .sheet(item: $store.selectedProject) { project in
            NavigationStack { ProjectView(projectID: project.id) }
                .environment(store)
        }
        .sheet(item: Binding(get: { store.verified && store.selectedTask == nil ? store.editor : nil }, set: { store.editor = $0 })) { draft in
            DraftEditor(initial: draft).environment(store)
        }
        .alert("Tuesday link", isPresented: Binding(get: { store.linkError != nil }, set: { if !$0 { store.linkError = nil } })) {
            Button("OK") { store.linkError = nil }
        } message: { Text(store.linkError ?? "") }
    }

    private func tab<Content: View>(_ title: String, icon: String, value: Int, @ViewBuilder content: () -> Content) -> some View {
        NavigationStack {
            content()
                .navigationTitle(title)
                .toolbar {
                    ToolbarItem(placement: .topBarTrailing) {
                        Button { accountSheet = true } label: { Image(systemName: "person.crop.circle").frame(minWidth: 44, minHeight: 44) }
                            .accessibilityLabel("Account and simulator settings")
                    }
                }
        }
        .tabItem { Label(title, systemImage: icon) }.tag(value)
    }
}

struct SignInView: View {
    @Environment(AppStore.self) private var store
    @State private var signingIn = false
    var body: some View {
        NavigationStack {
            List {
                Section {
                    Image(systemName: "square.stack.3d.up.fill").font(.largeTitle).foregroundStyle(.purple).accessibilityHidden(true)
                    Text("Tuesday").font(.largeTitle.bold())
                    Text("Keep engineering work moving.").font(.title3)
                    Text("Simulator workspace · Synthetic data only").font(.headline).foregroundStyle(.purple)
                    Text("Try the daily workflow as a team member, technical reviewer or read-only colleague. No company account or network connection is used.")
                }
                Section("Choose a synthetic account") {
                    ForEach(Person.fixtures) { person in
                        Button {
                            signingIn = true
                            Task { await store.signIn(person); signingIn = false }
                        } label: {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(person.name).font(.headline)
                                Text(person.role).font(.subheadline).foregroundStyle(Color.secondary)
                            }.frame(maxWidth: .infinity, minHeight: 44, alignment: .leading).contentShape(Rectangle())
                        }.accessibilityIdentifier("signin-\(person.id)")
                    }
                }.disabled(signingIn)
                if let error = store.error {
                    Section("Unable to open workspace") { Text(error).foregroundStyle(.red) }
                }
                Section {
                    Text("Corporate sign-in is unavailable until an approved Entra iOS registration is supplied. This workspace cannot connect to the live API.").font(.footnote).foregroundStyle(.secondary)
                }
            }.navigationTitle("Welcome")
        }
    }
}

struct ConnectionBanner: View {
    @Environment(AppStore.self) private var store
    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Label("Simulator · Synthetic data", systemImage: "testtube.2").font(.caption.bold())
            if let date = store.lastFetch {
                Text("\(store.offline || store.error != nil ? "Stale · Last checked" : "Last checked") \(date.formatted(date: .omitted, time: .shortened))").font(.caption).foregroundStyle(.secondary)
            }
            if let error = store.error { Text(error).font(.caption).foregroundStyle(.red) }
        }
        .frame(maxWidth: .infinity, alignment: .leading).padding(.horizontal).padding(.vertical, 8)
        .background(.thinMaterial)
    }
}

struct AccountView: View {
    @Environment(AppStore.self) private var store
    @Environment(\.dismiss) private var dismiss
    @State private var confirmSignOut = false
    @State private var message: String?
    var body: some View {
        NavigationStack {
            Form {
                Section("Synthetic account") {
                    Text(store.account?.name ?? "Signed out").font(.headline)
                    Text(store.account?.role ?? "")
                    Text("Environment: simulator fixtures v1")
                    Text("Organisation dates: America/Halifax")
                }
                Section("Simulator scenarios") {
                    Toggle("Simulate connection loss", isOn: Binding(get: { store.offline }, set: { value in Task { await store.setOffline(value) } }))
                        .accessibilityIdentifier("offline-toggle")
                    Button("Lose the next save response") {
                        Task { await store.server.loseNextReply(); message = "The next write will commit in the fixture service, then lose its acknowledgment. Retry its original request to resolve it." }
                    }
                    Text("These controls exercise native recovery with a local fixture service. They do not test the live server.").font(.footnote).foregroundStyle(.secondary)
                    if let message { Text(message) }
                }
                Section {
                    Button("Sign out", role: .destructive) {
                        if store.hasRetainedInput { confirmSignOut = true }
                        else { signOut() }
                    }.disabled(store.saving)
                }
            }
            .navigationTitle("Account")
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } } }
            .confirmationDialog("Discard retained input and sign out?", isPresented: $confirmSignOut, titleVisibility: .visible) {
                Button("Discard and sign out", role: .destructive) { signOut() }
            } message: { Text("Unsent drafts and unresolved request receipts on this device will be removed. A submitted action may already have completed in the fixture service.") }
        }
    }
    private func signOut() {
        do { try store.signOut(); dismiss() } catch { message = error.localizedDescription }
    }
}
