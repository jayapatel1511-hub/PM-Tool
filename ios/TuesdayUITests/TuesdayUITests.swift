import XCTest

@MainActor
final class TuesdayUITests: XCTestCase {
    private var app: XCUIApplication!
    override func setUp() async throws {
        try await super.setUp()
        await MainActor.run { launchClean() }
    }
    private func launchClean() {
        continueAfterFailure = false
        app = XCUIApplication()
        app.launchArguments = ["--reset-fixtures"]
        app.launch()
    }
    private func signIn(_ suffix: String = "1") {
        tap(app.buttons["signin-10000000-0000-0000-0000-00000000000\(suffix)"])
        XCTAssertTrue(app.tabBars.buttons["My Work"].waitForExistence(timeout: 8))
    }
    private func reveal(_ element: XCUIElement, file: StaticString = #filePath, line: UInt = #line) {
        for _ in 0..<20 {
            if element.exists {
                if isBodyElement(element) {
                    if element.frame.height > 0 && element.frame.intersection(contentRect()).height >= min(44, element.frame.height) { break }
                } else if element.isHittable { break }
            }
            scroll(down: element.exists && element.frame.midY < app.frame.midY)
        }
        if element.exists && isBodyElement(element) {
            XCTAssertGreaterThan(element.frame.height, 0, file: file, line: line)
            XCTAssertGreaterThanOrEqual(element.frame.intersection(contentRect()).height, min(44, element.frame.height), file: file, line: line)
        } else { XCTAssertTrue(element.isHittable, file: file, line: line) }
    }
    private func tap(_ element: XCUIElement, file: StaticString = #filePath, line: UInt = #line) {
        reveal(element, file: file, line: line)
        if isBodyElement(element) {
            let visible = element.frame.intersection(contentRect())
            app.coordinate(withNormalizedOffset: .zero).withOffset(CGVector(dx: visible.midX, dy: visible.midY)).tap()
        } else { element.tap() }
    }
    private func isInput(_ element: XCUIElement) -> Bool { [.textField, .textView, .searchField].contains(element.elementType) }
    private func isBodyElement(_ element: XCUIElement) -> Bool {
        if isInput(element) || element.elementType == .staticText { return true }
        return !["save-draft", "save-done", "new-task"].contains(element.identifier)
            && !["Close", "Done", "Discard", "Account and simulator settings", "My Work", "Projects", "Inbox", "My Week"].contains(element.label)
    }
    private func contentRect() -> CGRect {
        let top = (app.navigationBars.allElementsBoundByIndex.last?.frame.maxY ?? app.frame.minY) + 12
        var bottom = app.frame.maxY - 24
        for edge in [app.tabBars.firstMatch, app.keyboards.firstMatch] where edge.exists && edge.isHittable {
            bottom = min(bottom, edge.frame.minY - 8)
        }
        let state = app.staticTexts["save-state"]
        if state.exists && state.isHittable { bottom = min(bottom, state.frame.minY - 32) }
        XCTAssertGreaterThan(bottom - top, 44, "A scrollable content region must remain visible")
        return CGRect(x: app.frame.minX, y: top, width: app.frame.width, height: bottom - top)
    }
    private func scroll(down: Bool) {
        let area = contentRect()
        let origin = app.coordinate(withNormalizedOffset: .zero)
        let upper = origin.withOffset(CGVector(dx: area.midX, dy: area.minY + area.height * 0.2))
        let lower = origin.withOffset(CGVector(dx: area.midX, dy: area.minY + area.height * 0.8))
        (down ? upper : lower).press(forDuration: 0.05, thenDragTo: down ? lower : upper)
    }
    private func openDesign() {
        tap(app.buttons["task-SYN-2401-T001"])
        reveal(app.staticTexts["task-title"])
    }
    private func save() {
        tap(app.buttons["save-draft"])
        XCTAssertTrue(app.staticTexts["Saved"].waitForExistence(timeout: 5))
        tap(app.buttons["save-done"])
    }
    private func attach(_ name: String) {
        let shot = XCTAttachment(screenshot: app.screenshot())
        shot.name = name; shot.lifetime = .keepAlways; add(shot)
    }

    func testCommentAndActualTimeDailyWorkflow() {
        signIn(); openDesign()
        tap(app.buttons["add-comment"])
        let comment = app.textFields["comment-field"]
        tap(comment); comment.typeText("Simulator field update: datum checked.")
        if app.buttons["Done entering"].exists { app.buttons["Done entering"].tap() }
        save()
        reveal(app.staticTexts["Simulator field update: datum checked."])
        for _ in 0..<12 where !app.buttons["record-time"].exists { scroll(down: true) }
        tap(app.buttons["record-time"])
        let hours = app.textFields["hours-field"]
        tap(hours); hours.typeText("1.25")
        if app.buttons["Done entering"].exists { app.buttons["Done entering"].tap() }
        save()
        reveal(app.staticTexts.matching(NSPredicate(format: "label BEGINSWITH '1.25 h'")).firstMatch)
        attach("Comment and actual time saved")
    }

    func testCreateTaskOpensReturnedIdentity() {
        signIn()
        tap(app.buttons["new-task"])
        let title = app.textFields["task-name-field"]
        tap(title); title.typeText("Confirm synthetic utility clearance")
        if app.buttons["Done entering"].exists { app.buttons["Done entering"].tap() }
        save()
        XCTAssertTrue(app.staticTexts["Confirm synthetic utility clearance"].waitForExistence(timeout: 5))
        attach("Created task context")
    }

    func testRequestAndStartReviewAcrossAccounts() {
        signIn(); openDesign()
        tap(app.buttons["transition-task"])
        save()
        for _ in 0..<12 where !app.staticTexts["task-status"].exists { scroll(down: true) }
        reveal(app.staticTexts["task-status"])
        XCTAssertEqual(app.staticTexts["task-status"].label, "Ready for Review")
        tap(app.navigationBars.buttons["Done"])
        tap(app.buttons["Account and simulator settings"])
        tap(app.buttons["Sign out"])
        signIn("2")
        tap(app.buttons["task-SYN-2401-T001"])
        tap(app.buttons["transition-task"])
        save()
        for _ in 0..<12 where !app.staticTexts["task-status"].exists { scroll(down: true) }
        reveal(app.staticTexts["task-status"])
        XCTAssertEqual(app.staticTexts["task-status"].label, "Under Review")
        attach("Independent review started")
    }

    func testDraftSurvivesTerminationWithoutAutomaticSubmission() {
        signIn()
        tap(app.tabBars.buttons["Projects"])
        tap(app.buttons["project-SYN-2402"])
        tap(app.buttons["task-SYN-2402-T001"])
        tap(app.buttons["add-comment"])
        let comment = app.textFields["comment-field"]
        tap(comment); comment.typeText("Synthetic unsent draft survives restart")
        if app.buttons["Done entering"].exists { app.buttons["Done entering"].tap() }
        tap(app.buttons["Close"])
        app.terminate()
        app.launchArguments = []
        app.launch()
        tap(app.buttons["resume-draft"])
        reveal(app.staticTexts["draft-task-context"])
        XCTAssertEqual(app.staticTexts["draft-task-context"].label, "Coordinate accessible path grades")
        reveal(app.textFields["comment-field"])
        XCTAssertEqual(app.textFields["comment-field"].value as? String, "Synthetic unsent draft survives restart")
        XCTAssertTrue(app.staticTexts["Draft"].exists)
        attach("Draft recovered after relaunch")
    }

    func testLostAcknowledgmentResolvesOriginalRequest() {
        signIn()
        tap(app.buttons["Account and simulator settings"])
        tap(app.buttons["Lose the next save response"])
        tap(app.navigationBars.buttons["Done"])
        openDesign()
        tap(app.buttons["add-comment"])
        let comment = app.textFields["comment-field"]
        tap(comment); comment.typeText("One logical comment after lost response")
        if app.buttons["Done entering"].exists { app.buttons["Done entering"].tap() }
        tap(app.buttons["save-draft"])
        XCTAssertTrue(app.staticTexts["Outcome unknown"].waitForExistence(timeout: 5))
        XCTAssertFalse(comment.isEnabled)
        save()
        reveal(app.staticTexts["One logical comment after lost response"])
        XCTAssertEqual(app.staticTexts.matching(identifier: "One logical comment after lost response").count, 1)
        attach("Lost acknowledgment resolved once")
    }

    func testReadOnlyCannotMutateTasks() {
        signIn("3")
        XCTAssertTrue(app.staticTexts["No assigned work"].exists)
        XCTAssertFalse(app.buttons["new-task"].exists)
        tap(app.tabBars.buttons["Projects"])
        tap(app.buttons["project-SYN-2401"])
        tap(app.buttons["task-SYN-2401-T001"])
        for _ in 0..<4 where !app.staticTexts["This account has read-only access."].isHittable { app.swipeUp() }
        XCTAssertTrue(app.staticTexts["This account has read-only access."].exists)
        XCTAssertFalse(app.buttons["edit-task"].exists)
        XCTAssertFalse(app.buttons["transition-task"].exists)
        XCTAssertFalse(app.buttons["add-comment"].exists)
        attach("Read-only task context")
    }

    func testProjectPaginationAndUnknownWeekCapacity() {
        signIn()
        tap(app.tabBars.buttons["Projects"])
        tap(app.buttons["project-SYN-2402"])
        tap(app.buttons["load-more"])
        for _ in 0..<8 where !app.buttons["task-SYN-2402-T020"].exists { app.swipeUp() }
        XCTAssertTrue(app.buttons["task-SYN-2402-T020"].exists)
        tap(app.tabBars.buttons["My Week"])
        tap(app.buttons["Next week"])
        XCTAssertTrue(app.staticTexts["Available hours, Unknown"].waitForExistence(timeout: 5))
        attach("Unknown capacity remains unknown")
    }

    func testInboxOpenDoesNotMarkRead() {
        signIn()
        tap(app.tabBars.buttons["Inbox"])
        XCTAssertTrue(app.staticTexts["1 unread"].exists)
        tap(app.buttons.matching(NSPredicate(format: "label CONTAINS 'Sam mentioned you'")).firstMatch)
        tap(app.navigationBars.buttons["Done"])
        XCTAssertTrue(app.staticTexts["1 unread"].waitForExistence(timeout: 5))
        tap(app.buttons["Mark read"])
        XCTAssertTrue(app.staticTexts["0 unread"].waitForExistence(timeout: 5))
    }

    func testConflictRequiresExplicitReviewBeforeRetry() {
        signIn(); openDesign()
        tap(app.buttons["edit-task"])
        let title = app.textFields["task-name-field"]
        tap(title); title.typeText(" updated on phone")
        let intendedTitle = title.value as? String
        if app.buttons["Done entering"].exists { app.buttons["Done entering"].tap() }
        tap(app.buttons["Close"])
        tap(app.buttons["Simulator conflict scenario"])
        tap(app.buttons["Simulate another person's update"])
        for _ in 0..<12 where !app.buttons["resume-task-draft"].exists { scroll(down: true) }
        tap(app.buttons["resume-task-draft"])
        tap(app.buttons["save-draft"])
        XCTAssertTrue(app.staticTexts["Conflict"].waitForExistence(timeout: 5))
        XCTAssertFalse(app.buttons["save-draft"].isEnabled)
        tap(app.buttons["resolve-conflict"])
        XCTAssertTrue(app.buttons["save-draft"].isEnabled)
        save()
        for _ in 0..<12 where !app.staticTexts["task-title"].exists { scroll(down: true) }
        reveal(app.staticTexts["task-title"])
        XCTAssertEqual(app.staticTexts["task-title"].label, intendedTitle)
        attach("Conflict explicitly reviewed and saved")
    }

    func testOfflineForegroundLocksProtectedWorkspace() {
        signIn()
        tap(app.buttons["Account and simulator settings"])
        let toggle = app.switches["offline-toggle"]
        XCTAssertTrue(toggle.waitForExistence(timeout: 5))
        toggle.coordinate(withNormalizedOffset: CGVector(dx: 0.9, dy: 0.5)).tap()
        XCTAssertEqual(toggle.value as? String, "1")
        tap(app.navigationBars.buttons["Done"])
        XCUIDevice.shared.press(.home)
        app.activate()
        XCTAssertTrue(app.staticTexts["Workspace locked"].waitForExistence(timeout: 5))
        XCTAssertFalse(app.buttons["task-SYN-2401-T001"].exists)
        tap(app.buttons["Reconnect simulator"])
        XCTAssertTrue(app.tabBars.buttons["My Work"].waitForExistence(timeout: 5))
        attach("Foreground revalidation after reconnection")
    }
}
