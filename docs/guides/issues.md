# Location-linked issues

An issue can record where the problem is, the exact drawings or models involved, the other disciplines affected, and an
independent check that the fix works. Project → **Issues**, then open an issue and use **Location and evidence**. Back
to the [user guide](../user-guide.md).

Tuesday stores locations exactly as entered. It does not convert coordinates or units, or interpret geometry.

## Who can do what

| Action | Who |
|---|---|
| Add a location or reference, set **Affected disciplines** | Anyone who can edit the issue: the PM, its owner, its creator, or the lead of its discipline. Being an affected discipline gives no edit rights |
| **Propose verifier** (appoint or replace) | The issue's creator or the PM, with an appointment reason |
| **Verify**, **Reject** | The appointed verifier only |
| **Mark unaffected**, **Request reopening** on a source reference impact | The issue's owner and its verifier, one decision each |

The verifier cannot be the issue's owner, its creator or the person in **Raised by**.

## Add a location

1. Under **Structured locations**, choose the **Location kind**:
   - **SiteArea**: enter **Site area**.
   - **Building**: enter **Building**, and **Level** and **Room** if useful.
   - **Alignment**: enter **Alignment**, **Start station**, **End station** and **Units**. The end must be at or after the
     start.
   - **Coordinate**: enter **Coordinate X**, **Coordinate Y**, optionally **Coordinate Z**, the **Coordinate reference
     system** and **Coordinate units**.
2. **Asset or system** can be added to any kind.
3. Select **Add**.

Station fields also appear for **Building**, but they are only saved for **Alignment**. Add several locations when one
issue spans boundaries.

## Add a drawing or model reference

1. Under **Drawing and model references**, choose the **Reference kind**: **Drawing**, **Model**, **Markup** or
   **Screenshot**.
2. Enter the **Drawing or model identifier**, its **Revision** and the **Source link**. **External topic**, **Model element
   GUID** and **Viewpoint link** are optional.
3. Leave **Available** ticked unless the source cannot be opened, then select **Add**.

Locations and references cannot be edited or removed once added. An issue with an unavailable reference cannot be
resolved ("An unavailable document reference cannot support resolution."), so check **Available** before you add.

## Verify the fix

1. The creator or PM chooses the **Verifier**, writes the **Appointment reason**, and selects **Propose verifier**. The
   verifier is notified; the issue shows "Assigned to you" to them.
2. The verifier checks the resolution, enters a **Verification evidence link**, and selects **Verify**, or **Reject**.
   The owner, the creator and the PM are notified.
3. Resolve the issue as usual with a **Resolution**.

An issue with any location or reference can be resolved only when its latest verification is Verified, was recorded
after the last location or reference was added, and every reference is Available. Otherwise: "A current independent
verification with evidence is required before resolution." Adding a location or reference after verification shows
"Needs reverification"; propose the verifier again.

## When a referenced drawing changes

When a newer revision of a source is published in **Changes** (see [change impact](change-impact.md)), each Resolved
issue that cites the old revision with the same identifier, revision and source link gets a **Source reference
impact**: "Source changed from … to …".

1. The owner and the verifier each enter a **Decision reason** and select **Mark unaffected** or **Request reopening**.
2. Nothing reopens automatically. While a decision is pending, the issue cannot be reopened. When both have decided
   and either asked for reopening, reopen it with **Reopen** as usual.

The issue keeps its original reference and revision.

## Affected disciplines

Tick the other disciplines under **Affected disciplines** in the issue panel. The issue then appears for those
disciplines in the register's discipline filter and grouping, in the [coordination view](coordination-view.md) and in
review links, always with the same issue key.

## Find issues

- Source filters: **Location**, **Drawing or model ID**, **Revision**, **Alignment**, **From station**, **To station**,
  **Units**, and verification status (**Any verification**, Proposed, Verified, Rejected, **Needs reverification**, **No
  verification**). A station range matches issues whose range overlaps it; units must match exactly.
- **Group issues by** location, discipline, owner, drawing or model, revision or verification. An issue with several
  locations, disciplines or references appears in each of its groups, with the same key.
- **Structured locations** and **Independent verification** show by default; add **Drawing and model references** and
  **Affected disciplines** from **Columns**.
- **Export** gives Excel or CSV of the filtered list, including locations, references and verification.
- A review finding can name an **Existing issue**; the review then links to the same issue rather than a copy.

## Coming in the review release

Issues will be raised as Coordination or General, and only Coordination issues will need a location or reference and
independent verification before they can be resolved. See the [user guide](../user-guide.md#coming-in-the-review-release).
