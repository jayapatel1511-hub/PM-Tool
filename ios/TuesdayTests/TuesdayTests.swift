import XCTest
import UIKit
@testable import Tuesday

final class TuesdayTests: XCTestCase {
    private func command(_ kind: DraftKind, task: WorkTask) -> Draft {
        var d = Draft(kind: kind, taskID: task.id, projectID: task.projectID, version: task.version,
            title: task.name, ownerID: task.ownerID, reviewerID: task.reviewerID, due: task.due, priority: task.priority, progress: task.progress)
        d.submittedAt = Date()
        return d
    }
    private func expect(_ expected: FixtureError, _ action: () async throws -> Void, file: StaticString = #filePath, line: UInt = #line) async {
        do { try await action(); XCTFail("Expected \(expected)", file: file, line: line) }
        catch { XCTAssertEqual(error as? FixtureError, expected, file: file, line: line) }
    }
    private func tempDirectory() -> URL { FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString) }

    func testReviewRequiresIndependentReviewerAndCannotCompleteDirectly() async throws {
        let server = try FixtureServer()
        let task = try await server.fetch(Person.alex.id).tasks[0]
        let rights = await server.permissions(task, account: Person.alex.id)
        XCTAssertTrue(rights.transitions.contains(.ready))
        XCTAssertFalse(rights.transitions.contains(.complete))
        var direct = command(.transition, task: task); direct.status = .complete
        await expect(.forbidden) { _ = try await server.submit(direct, account: Person.alex.id) }
        var invalid = command(.transition, task: task); invalid.status = .ready; invalid.reviewerID = task.ownerID
        do { _ = try await server.submit(invalid, account: Person.alex.id); XCTFail() } catch { XCTAssertTrue(error.localizedDescription.contains("independent")) }
        var request = command(.transition, task: task); request.status = .ready
        _ = try await server.submit(request, account: Person.alex.id)
        let updated = try await server.fetch(Person.sam.id).tasks.first { $0.id == task.id }!
        XCTAssertEqual(updated.status, .ready)
        var start = command(.transition, task: updated); start.status = .reviewing
        _ = try await server.submit(start, account: Person.sam.id)
        let reviewed = try await server.fetch(Person.sam.id).tasks.first { $0.id == task.id }!
        XCTAssertEqual(reviewed.status, .reviewing)
    }

    func testReadOnlyScopeBlockersAndRevocation() async throws {
        let server = try FixtureServer()
        let visible = try await server.fetch(Person.rita.id)
        XCTAssertEqual(visible.projects.map(\.id), [Project.harbour.id])
        XCTAssertTrue(visible.tasks.allSatisfy { $0.projectID == Project.harbour.id })
        let task = visible.tasks[0]
        let rights = await server.permissions(task, account: Person.rita.id)
        XCTAssertFalse(rights.edit); XCTAssertFalse(rights.comment); XCTAssertFalse(rights.time)
        XCTAssertTrue(rights.transitions.isEmpty)
        var comment = command(.comment, task: task); comment.body = "Refused"
        await expect(.forbidden) { _ = try await server.submit(comment, account: Person.rita.id) }
        let blocked = visible.tasks.first { $0.blocker != nil }!
        let blockedRights = await server.permissions(blocked, account: Person.alex.id)
        XCTAssertTrue(blockedRights.transitions.isEmpty)
        await server.revoke(Person.alex.id)
        await expect(.forbidden) { _ = try await server.fetch(Person.alex.id) }
    }

    func testLostReplyAndOverlappingReplayPersistExactlyOnce() async throws {
        let directory = tempDirectory(); defer { try? FileManager.default.removeItem(at: directory) }
        let file = directory.appendingPathComponent("fixture.json")
        let server = try FixtureServer(file: file)
        let task = try await server.fetch(Person.alex.id).tasks[0]
        var draft = command(.comment, task: task); draft.body = "A single synthetic comment"; draft.mentionID = Person.sam.id
        await server.loseNextReply()
        await expect(.unknown) { _ = try await server.submit(draft, account: Person.alex.id) }
        let restarted = try FixtureServer(file: file)
        let immutable = draft
        async let first = restarted.submit(immutable, account: Person.alex.id)
        async let second = restarted.submit(immutable, account: Person.alex.id)
        let results = try await [first, second]
        XCTAssertEqual(results[0], results[1])
        let snapshot = try await restarted.fetch(Person.alex.id)
        XCTAssertEqual(snapshot.comments.filter { $0.body == draft.body }.count, 1)
        let inbox = try await restarted.fetch(Person.sam.id).notices
        XCTAssertEqual(inbox.filter { $0.title.contains("mentioned") }.count, 1)
        var changed = draft; changed.body = "Different bytes"
        await expect(.keyReused) { _ = try await restarted.submit(changed, account: Person.alex.id) }
        changed = draft; changed.projectID = Project.greenway.id
        await expect(.keyReused) { _ = try await restarted.submit(changed, account: Person.alex.id) }
        await restarted.revoke(Person.alex.id)
        await expect(.forbidden) { _ = try await restarted.submit(draft, account: Person.alex.id) }
    }

    func testCreateReturnsOneIdentityAndValidatesScopeAndOwner() async throws {
        let server = try FixtureServer()
        var draft = Draft(kind: .create, projectID: Project.harbour.id, title: "Synthetic new task", reviewerID: Person.sam.id)
        draft.submittedAt = Date()
        let first = try await server.submit(draft, account: Person.alex.id)
        let replay = try await server.submit(draft, account: Person.alex.id)
        XCTAssertEqual(first, replay)
        let created = try await server.fetch(Person.alex.id).tasks.filter { $0.id == first.taskID }
        XCTAssertEqual(created.count, 1); XCTAssertEqual(created[0].name, draft.title)
        var blank = draft; blank.key = UUID(); blank.title = "  "
        do { _ = try await server.submit(blank, account: Person.alex.id); XCTFail() } catch { XCTAssertTrue(error.localizedDescription.contains("title")) }
        var wrong = draft; wrong.key = UUID(); wrong.ownerID = Person.rita.id
        do { _ = try await server.submit(wrong, account: Person.alex.id); XCTFail() } catch { XCTAssertTrue(error.localizedDescription.contains("owner")) }
        wrong = draft; wrong.key = UUID(); wrong.projectID = UUID()
        await expect(.unavailable) { _ = try await server.submit(wrong, account: Person.alex.id) }
    }

    func testStaleAndMissingVersionsPreserveCurrentTask() async throws {
        let server = try FixtureServer()
        let task = try await server.fetch(Person.alex.id).tasks[0]
        var draft = command(.edit, task: task); draft.progress = 90
        try await server.externalChange(task.id)
        await expect(.conflict) { _ = try await server.submit(draft, account: Person.alex.id) }
        let current = try await server.fetch(Person.alex.id).tasks.first { $0.id == task.id }!
        XCTAssertEqual(current.progress, 75)
        draft.version = nil
        do { _ = try await server.submit(draft, account: Person.alex.id); XCTFail() } catch { XCTAssertTrue(error.localizedDescription.contains("428")) }
    }

    func testTimeValidationCombinedCapAndVersionedCorrection() async throws {
        let server = try FixtureServer()
        let tasks = try await server.fetch(Person.alex.id).tasks
        var first = command(.time, task: tasks[0]); first.hours = "23.5"
        _ = try await server.submit(first, account: Person.alex.id)
        var second = command(.time, task: tasks[1]); second.hours = "1.25"
        do { _ = try await server.submit(second, account: Person.alex.id); XCTFail() } catch { XCTAssertTrue(error.localizedDescription.contains("exceed 24")) }
        let entries = try await server.fetch(Person.alex.id).time
        XCTAssertEqual(entries.count, 1)
        var correction = command(.correctTime, task: tasks[0]); correction.entryID = entries[0].id; correction.version = entries[0].version; correction.hours = "1.25"
        _ = try await server.submit(correction, account: Person.alex.id)
        var stale = correction; stale.key = UUID(); stale.hours = "2"
        await expect(.conflict) { _ = try await server.submit(stale, account: Person.alex.id) }
        let actual = try await server.fetch(Person.alex.id).time
        XCTAssertEqual(actual[0].hours, Decimal(string: "1.25")); XCTAssertEqual(actual[0].version, 2)
        for input in ["0", "-1", "24.01", "1.234", "nan", "1e2", "1,000", ".", ""] { XCTAssertThrowsError(try OrgDate.hours(input, locale: Locale(identifier: "en_CA"))) }
        XCTAssertEqual(try OrgDate.hours("1,25", locale: Locale(identifier: "fr_CA")), Decimal(string: "1.25"))
    }

    func testConcurrentTimeCannotDefeatDailyCap() async throws {
        let server = try FixtureServer()
        let tasks = try await server.fetch(Person.alex.id).tasks
        var first = command(.time, task: tasks[0]); first.hours = "20"
        var second = command(.time, task: tasks[1]); second.hours = "5"
        async let a: Receipt? = try? server.submit(first, account: Person.alex.id)
        async let b: Receipt? = try? server.submit(second, account: Person.alex.id)
        let receipts = await [a, b].compactMap { $0 }
        XCTAssertEqual(receipts.count, 1)
        let total = try await server.fetch(Person.alex.id).time.reduce(Decimal.zero) { $0 + $1.hours }
        XCTAssertLessThanOrEqual(total, 24)
    }

    func testInboxOnlyExplicitAcknowledgmentAndOfflineFailure() async throws {
        let server = try FixtureServer()
        let first = try await server.fetch(Person.alex.id)
        let again = try await server.fetch(Person.alex.id)
        XCTAssertEqual(first.notices.filter { !$0.read }.count, again.notices.filter { !$0.read }.count)
        await server.setOffline(true)
        await expect(.offline) { try await server.markRead(first.notices.map(\.id), account: Person.alex.id) }
        await server.setOffline(false)
        try await server.markRead(first.notices.map(\.id), account: Person.alex.id)
        let read = try await server.fetch(Person.alex.id)
        XCTAssertEqual(read.notices.filter { !$0.read }.count, 0)
    }

    func testOrganisationDatesDSTAndUnknownCapacity() async throws {
        let day = "2026-03-08"
        XCTAssertEqual(OrgDate.parse(day).map(OrgDate.string), day)
        XCTAssertNil(OrgDate.parse("2026-02-30"))
        XCTAssertNil(OrgDate.parse("2026-1-1"))
        let instant = ISO8601DateFormatter().date(from: "2026-03-08T03:30:00Z")!
        XCTAssertEqual(OrgDate.today(instant), "2026-03-07")
        XCTAssertEqual(OrgDate.week(now: instant), "2026-03-02")
        let server = try FixtureServer()
        let week = try await server.week(1, account: Person.alex.id)
        XCTAssertNil(week.available)
        XCTAssertTrue(week.explanation.contains("unknown"))
    }

    func testStrictLinksRejectBroadenedViewsAndArbitraryHosts() {
        let project = Project.harbour.id
        let task = UUID()
        XCTAssertEqual(AppLink(URL(string: "https://pm.engcalchub.com/projects/\(project)")!), .project(project))
        XCTAssertEqual(AppLink(URL(string: "https://pm.engcalchub.com/projects/\(project)?panel=Task:\(task)")!), .task(task))
        for url in ["http://pm.engcalchub.com/projects/\(project)", "https://evil.test/projects/\(project)", "https://pm.engcalchub.com.evil.test/projects/\(project)", "https://pm.engcalchub.com/projects/\(project)?savedView=anything", "https://pm.engcalchub.com/projects/not-a-guid", "https://pm.engcalchub.com/projects/\(project)?panel=Task:bad", "https://pm.engcalchub.com/projects/\(project)?panel=Task:\(task)&redirect=https://evil.test", "https://pm.engcalchub.com:444/projects/\(project)"] { XCTAssertNil(AppLink(URL(string: url)!), url) }
    }

    @MainActor func testDraftRecoveryIsolationAndBackupExclusion() async throws {
        let directory = tempDirectory(); defer { try? FileManager.default.removeItem(at: directory) }
        let store = AppStore(directory: directory, restoreAccount: false)
        await store.signIn(.alex)
        let task = store.tasks[0]
        store.begin(.comment, task: task)
        var draft = store.savedDraft!; draft.body = "Retained synthetic draft"
        store.keep(draft)
        let file = directory.appendingPathComponent("draft-\(Person.alex.id).json")
        XCTAssertEqual(try file.resourceValues(forKeys: [.isExcludedFromBackupKey]).isExcludedFromBackup, true)
        let otherAccount = AppStore(directory: directory, restoreAccount: false)
        await otherAccount.signIn(.sam)
        XCTAssertNil(otherAccount.savedDraft)
        let restarted = AppStore(directory: directory, restoreAccount: false)
        XCTAssertFalse(restarted.verified); XCTAssertNil(restarted.snapshot)
        await restarted.signIn(.alex)
        XCTAssertEqual(restarted.savedDraft?.body, draft.body)
        try restarted.signOut()
        XCTAssertFalse(FileManager.default.fileExists(atPath: file.path))
        XCTAssertNil(restarted.snapshot); XCTAssertNil(restarted.savedDraft)
    }

    func testDeviceFileProtection() throws {
        #if targetEnvironment(simulator)
        throw XCTSkip("iOS Simulator does not expose device data-protection attributes. Verify Complete protection on a physical iPhone before company-data use.")
        #else
        let file = tempDirectory().appendingPathComponent("draft.json")
        defer { try? FileManager.default.removeItem(at: file.deletingLastPathComponent()) }
        try ProtectedStorage.write(Data("Synthetic draft".utf8), to: file)
        let attributes = try FileManager.default.attributesOfItem(atPath: file.path)
        XCTAssertEqual(attributes[.protectionKey] as? FileProtectionType, .complete)
        #endif
    }

    @MainActor func testStoreUnknownOutcomeIsImmutableAndRecoveredAcrossRelaunch() async throws {
        let directory = tempDirectory(); defer { try? FileManager.default.removeItem(at: directory) }
        let store = AppStore(directory: directory, restoreAccount: false)
        await store.signIn(.alex)
        store.begin(.time, task: store.tasks[0])
        var draft = store.savedDraft!; draft.hours = "1.25"
        await store.server.loseNextReply()
        await store.save(draft)
        XCTAssertEqual(store.saveState, "Outcome unknown")
        let original = store.savedDraft!
        let restarted = AppStore(directory: directory, restoreAccount: false)
        await restarted.signIn(.alex)
        XCTAssertEqual(restarted.savedDraft, original)
        var changed = original; changed.hours = "5"
        await restarted.save(changed)
        XCTAssertEqual(restarted.saveState, "Saved")
        XCTAssertNil(restarted.savedDraft)
        XCTAssertEqual(restarted.snapshot?.time.count, 1)
        XCTAssertEqual(restarted.snapshot?.time[0].hours, Decimal(string: "1.25"))
    }

    @MainActor func testOfflineForegroundLocksUntilRevalidatedAndConflictKeepsInput() async throws {
        let directory = tempDirectory(); defer { try? FileManager.default.removeItem(at: directory) }
        let store = AppStore(directory: directory, restoreAccount: false)
        await store.signIn(.alex)
        let task = store.tasks[0]
        store.begin(.edit, task: task)
        var draft = store.savedDraft!; draft.title = "Intended task title"
        try await store.server.externalChange(task.id)
        await store.save(draft)
        XCTAssertEqual(store.saveState, "Conflict")
        XCTAssertEqual(store.savedDraft?.title, draft.title)
        XCTAssertEqual(store.tasks[0].progress, 75)
        store.rebaseDraft()
        XCTAssertEqual(store.savedDraft?.version, 2)
        XCTAssertEqual(store.savedDraft?.progress, 75)
        XCTAssertEqual(store.savedDraft?.title, draft.title)
        await store.save(store.savedDraft!)
        XCTAssertEqual(store.tasks[0].progress, 75)
        XCTAssertEqual(store.tasks[0].name, draft.title)
        await store.setOffline(true)
        await store.refresh()
        XCTAssertNotNil(store.snapshot)
        XCTAssertNotNil(store.error)
        store.lock()
        XCTAssertNil(store.editor)
        await store.refresh()
        XCTAssertFalse(store.verified); XCTAssertNil(store.snapshot)
        await store.setOffline(false)
        XCTAssertTrue(store.verified)
    }

    @MainActor func testExplicitAccountChangeAndRevocationPurgeDrafts() async throws {
        let directory = tempDirectory(); defer { try? FileManager.default.removeItem(at: directory) }
        let store = AppStore(directory: directory, restoreAccount: false)
        await store.signIn(.alex)
        store.begin(.comment, task: store.tasks[0])
        await store.signIn(.sam)
        XCTAssertNil(store.savedDraft)
        await store.signIn(.alex)
        XCTAssertNil(store.savedDraft)
        store.begin(.comment, task: store.tasks[0])
        await store.server.revoke(Person.alex.id)
        await store.refresh()
        XCTAssertNil(store.snapshot); XCTAssertNil(store.savedDraft); XCTAssertFalse(store.verified)
        XCTAssertFalse(FileManager.default.fileExists(atPath: directory.appendingPathComponent("draft-\(Person.alex.id).json").path))
    }

    @MainActor func testPrivacyShieldCoversPresentedWorkspaceOnSceneDeactivation() throws {
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.first as? UIWindowScene)
        let delegate = PrivacyDelegate()
        delegate.conceal(Notification(name: UIScene.willDeactivateNotification, object: scene))
        let shield = try XCTUnwrap(scene.windows.first { $0.windowLevel > .alert && !$0.isHidden })
        XCTAssertEqual((shield.rootViewController?.view.subviews.first as? UILabel)?.text, "Tuesday · Workspace locked")
        delegate.reveal(Notification(name: UIScene.didActivateNotification, object: scene))
        XCTAssertTrue(shield.isHidden)
    }

    func testRetryWindowRequiresReconciliation() async throws {
        let server = try FixtureServer()
        let task = try await server.fetch(Person.alex.id).tasks[0]
        var draft = command(.comment, task: task); draft.body = "Expired request"; draft.submittedAt = Date().addingTimeInterval(-86_401)
        await expect(.expired) { _ = try await server.submit(draft, account: Person.alex.id) }
    }

    @MainActor func testExpiredDraftRequiresExplicitReconciliation() async throws {
        let directory = tempDirectory(); defer { try? FileManager.default.removeItem(at: directory) }
        let store = AppStore(directory: directory, restoreAccount: false)
        await store.signIn(.alex)
        store.begin(.comment, task: store.tasks[0])
        var draft = store.savedDraft!; draft.body = "Synthetic expired command"; draft.submittedAt = Date().addingTimeInterval(-86_401)
        store.keep(draft)
        await store.save(draft)
        XCTAssertEqual(store.saveState, "Outcome unknown")
        XCTAssertNotNil(store.savedDraft)
        XCTAssertThrowsError(try store.reconcileExpiredDraft(now: draft.submittedAt!))
        try store.reconcileExpiredDraft()
        XCTAssertNil(store.savedDraft)
        XCTAssertEqual(store.snapshot?.comments.filter { $0.body == draft.body }.count, 0)
    }

    @MainActor func testUnreadableRetainedInputCannotBeOverwritten() async throws {
        let directory = tempDirectory(); defer { try? FileManager.default.removeItem(at: directory) }
        let file = directory.appendingPathComponent("draft-\(Person.alex.id).json")
        let damaged = Data("unreadable synthetic draft".utf8)
        try ProtectedStorage.write(damaged, to: file)
        let store = AppStore(directory: directory, restoreAccount: false)
        await store.signIn(.alex)
        XCTAssertTrue(store.hasRetainedInput)
        XCTAssertNil(store.savedDraft)
        store.begin(.comment, task: store.tasks[0])
        XCTAssertNil(store.editor)
        XCTAssertEqual(try Data(contentsOf: file), damaged)
        try store.signOut()
        XCTAssertFalse(FileManager.default.fileExists(atPath: file.path))
    }

    @MainActor func testUnsentDraftKeepsTaskIdentityBeforeFirstWrite() async throws {
        let directory = tempDirectory(); defer { try? FileManager.default.removeItem(at: directory) }
        let store = AppStore(directory: directory, restoreAccount: false)
        await store.signIn(.alex)
        let task = store.tasks.first { $0.projectID == Project.greenway.id }!
        store.begin(.comment, task: task)
        var draft = store.savedDraft!; draft.body = "Unsent draft for a generated fixture identity"
        store.keep(draft)
        let restarted = AppStore(directory: directory, restoreAccount: false)
        await restarted.signIn(.alex)
        XCTAssertTrue(restarted.tasks.contains { $0.id == task.id })
        XCTAssertEqual(restarted.savedDraft?.taskID, task.id)
        await restarted.save(restarted.savedDraft!)
        XCTAssertEqual(restarted.saveState, "Saved")
        XCTAssertEqual(restarted.snapshot?.comments.filter { $0.taskID == task.id && $0.body == draft.body }.count, 1)
    }
}
