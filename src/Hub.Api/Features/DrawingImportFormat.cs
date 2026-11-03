// Pure CSV/XLSX format reader; domain validation and atomic creation are separate stages.
// Pure bytes -> raw cells/locations/diagnostics. No domain IDs/defaults, database, filesystem extraction,
// uploads, source identity inference, permissions, keys, receipts, membership, notifications or HTTP.
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualBasic.FileIO;

namespace Hub.Api.Features;

public enum DrawingImportFormatKind { Csv, Xlsx }
public enum DrawingImportCellKind { Missing, Blank, Text, Number, Boolean, Date, DateTime, Time, Duration, Formula, Error, Unknown }
public enum DrawingImportDiagnosticSeverity { Warning, Error }
public sealed record DrawingImportDiagnostic(string Code, DrawingImportDiagnosticSeverity Severity, string Message,
    string? Sheet = null, int? SourceRow = null, int? Column = null);
public sealed record DrawingImportRawCell(int Column, string Reference, DrawingImportCellKind Kind, string? Raw,
    string? Text = null, bool? Boolean = null, DateTime? CalendarValue = null, DateTimeOffset? ZonedValue = null,
    TimeSpan? TimeValue = null, bool CoordinateInferred = false, string? EncodedText = null);
public sealed record DrawingImportRawHeader(int Column, string Raw, string Normalized, DrawingImportRawCell Cell);
public sealed record DrawingImportRawRecord(int SourceRow, bool SourceRowInferred, bool Hidden, IReadOnlyList<DrawingImportRawCell> Cells);
public sealed record DrawingImportSheet(string Name, string State, string Part);
public sealed record DrawingImportFormatInput(DrawingImportFormatKind Format, byte[] Content, string? Worksheet = null);
public sealed record DrawingImportFormatResult(DrawingImportFormatKind Format, string? ContentSha256, string? SelectedSheet, bool? Date1904,
    IReadOnlyList<DrawingImportSheet> Sheets, IReadOnlyList<DrawingImportRawHeader> Headers, IReadOnlyList<DrawingImportRawRecord> Records,
    IReadOnlyList<DrawingImportDiagnostic> Diagnostics, bool Complete)
{
    public bool Success => Complete && Diagnostics.All(value => value.Severity != DrawingImportDiagnosticSeverity.Error);
}
public sealed record DrawingImportFormatLimits
{
    public int CsvBytes { get; init; } = 256 * 1024;
    public int XlsxBytes { get; init; } = 2 * 1024 * 1024;
    public int ExpandedBytes { get; init; } = 16 * 1024 * 1024;
    public int Records { get; init; } = 1000;
    public int Columns { get; init; } = 100;
    public int Entries { get; init; } = 128;
    public int CellCharacters { get; init; } = 16_000;
    public int XmlNodes { get; init; } = 250_000;
    public int SharedStrings { get; init; } = 100_000;
    public int SharedStringCharacters { get; init; } = 4 * 1024 * 1024;
    public int Styles { get; init; } = 4096;
    public int Diagnostics { get; init; } = 2000;
}

public static class DrawingImportFormat
{
    const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    const string StrictMain = "http://purl.oclc.org/ooxml/spreadsheetml/main";
    const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    const string StrictRel = "http://purl.oclc.org/ooxml/officeDocument/relationships";
    const string PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
    static readonly UTF8Encoding Utf8 = new(false, true);
    enum DrawingImportNumberStyle { Number, Date, DateTime, Time, Duration, Unknown }
    sealed record DrawingImportRelation(string Id, string Type, string Target);
    sealed record DrawingImportTextValue(string Raw, string Text);
    sealed class DrawingImportFault(string code, string message, int? row = null, int? column = null) : Exception(message)
    { public string Code { get; } = code; public int? Row { get; } = row; public int? Column { get; } = column; }

    sealed class DrawingImportBuilder(DrawingImportFormatInput input, DrawingImportFormatLimits limits)
    {
        public readonly List<DrawingImportRawHeader> Headers = [];
        public readonly List<DrawingImportRawRecord> Records = [];
        public readonly List<DrawingImportDiagnostic> Diagnostics = [];
        public readonly List<DrawingImportSheet> Sheets = [];
        public string? SelectedSheet;
        public string? Hash;
        public bool? Date1904;
        public bool Complete;
        public int XmlNodeCount;
        public void Add(string code, string message, bool error = true, int? row = null, int? column = null)
        {
            if (Diagnostics.Count >= limits.Diagnostics) throw new DrawingImportFault("diagnostic_limit", "Diagnostic budget exceeded; parsing did not complete.", row, column);
            Diagnostics.Add(new(code, error ? DrawingImportDiagnosticSeverity.Error : DrawingImportDiagnosticSeverity.Warning, message, SelectedSheet, row, column));
        }
        public string Bound(string value, int row, int column)
        {
            if (value.Length > limits.CellCharacters) throw new DrawingImportFault("cell_limit", "Cell text exceeds the bounded format-stage limit.", row, column);
            return value;
        }
        public void TableRow(int row, bool inferred, bool hidden, List<DrawingImportRawCell> cells)
        {
            if (hidden) Add("hidden_row", "Hidden row is included; it has not been silently skipped.", false, row);
            if (cells.All(cell => cell.Kind is DrawingImportCellKind.Blank or DrawingImportCellKind.Missing))
            { Add("blank_row", "Wholly blank row is not a data record.", false, row); return; }
            if (Headers.Count == 0)
            {
                if (cells.Count == 0) throw new DrawingImportFault("headers_missing", "A nonempty header row is required.", row);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in cells)
                {
                    var raw = cell.EncodedText ?? cell.Text ?? cell.Raw ?? "";
                    var normalized = (cell.Text ?? cell.Raw ?? "").Trim().ToLowerInvariant();
                    Headers.Add(new(cell.Column, raw, normalized, cell));
                    if (cell.Kind != DrawingImportCellKind.Text) Add("header_not_text", "Headers must be literal text.", row: row, column: cell.Column);
                    if (normalized.Length == 0) Add("blank_header", "A header is blank.", row: row, column: cell.Column);
                    else if (!seen.Add(normalized)) Add("duplicate_header", "Header names are duplicated after trimming and case normalization.", row: row, column: cell.Column);
                }
                return;
            }
            if (Records.Count >= limits.Records) throw new DrawingImportFault("record_limit", "Too many nonblank data records; parsing did not complete.", row);
            if (cells.Count > Headers.Count) Add("row_wider_than_header", "Data extends beyond the header columns.", row: row, column: Headers.Count + 1);
            while (cells.Count < Headers.Count) cells.Add(Missing(cells.Count + 1, row));
            Records.Add(new(row, inferred, hidden, cells.ToArray()));
        }
        public DrawingImportFormatResult Finish() => new(input.Format, Hash, SelectedSheet, Date1904, Sheets.ToArray(), Headers.ToArray(), Records.ToArray(), Diagnostics.ToArray(), Complete);
    }

    public static DrawingImportFormatResult Parse(DrawingImportFormatInput input, DrawingImportFormatLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        limits ??= new();
        if (new[] { limits.CsvBytes, limits.XlsxBytes, limits.ExpandedBytes, limits.Records, limits.Columns, limits.Entries, limits.CellCharacters,
            limits.XmlNodes, limits.SharedStrings, limits.SharedStringCharacters, limits.Styles, limits.Diagnostics }.Any(value => value <= 0))
            throw new ArgumentOutOfRangeException(nameof(limits), "Parser limits are trusted server configuration and must be positive.");
        var result = new DrawingImportBuilder(input, limits);
        try
        {
            if (input.Content is null || input.Content.Length == 0) throw new DrawingImportFault("empty_file", "No file bytes were supplied.");
            var bound = input.Format switch { DrawingImportFormatKind.Csv => limits.CsvBytes, DrawingImportFormatKind.Xlsx => limits.XlsxBytes,
                _ => throw new DrawingImportFault("unsupported_format", "Only explicit CSV or XLSX format is supported.") };
            if (input.Content.Length > bound) throw new DrawingImportFault("file_limit", "Compressed/original file bytes exceed the configured format budget.");
            result.Hash = Convert.ToHexString(SHA256.HashData(input.Content));
            if (input.Format == DrawingImportFormatKind.Csv) Csv(input, limits, result); else Xlsx(input, limits, result);
            if (result.Headers.Count == 0) result.Add("headers_missing", "No nonblank header row was found.");
            if (result.Records.Count == 0) result.Add("records_missing", "No nonblank data records were found.");
            result.Complete = true;
        }
        catch (DrawingImportFault fault)
        {
            // A fatal budget/format error preserves bounded partial data, never a claim that every row was checked.
            result.Diagnostics.Add(new(fault.Code, DrawingImportDiagnosticSeverity.Error, fault.Message, result.SelectedSheet, fault.Row, fault.Column));
        }
        catch (DecoderFallbackException) { result.Diagnostics.Add(new("invalid_utf8", DrawingImportDiagnosticSeverity.Error, "CSV must be valid UTF-8.")); }
        catch (XmlException) { result.Diagnostics.Add(new("invalid_xml", DrawingImportDiagnosticSeverity.Error, "Workbook XML is malformed, contains a DTD or exceeds XML limits.", result.SelectedSheet)); }
        catch (InvalidDataException) { result.Diagnostics.Add(new("invalid_xlsx", DrawingImportDiagnosticSeverity.Error, "Invalid, encrypted, unsupported or corrupt XLSX archive.", result.SelectedSheet)); }
        catch (IOException) { result.Diagnostics.Add(new("invalid_file", DrawingImportDiagnosticSeverity.Error, "The bounded input stream could not be read.", result.SelectedSheet)); }
        return result.Finish();
    }

    static void Csv(DrawingImportFormatInput input, DrawingImportFormatLimits limits, DrawingImportBuilder result)
    {
        if (input.Worksheet is not null) throw new DrawingImportFault("worksheet_not_csv", "CSV has no worksheet selection.");
        var text = Utf8.GetString(input.Content); if (text.StartsWith('\uFEFF')) text = text[1..];
        foreach (var (raw, row) in CsvRecords(text))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string[] fields;
            using (var parser = new TextFieldParser(new StringReader(raw)) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false })
            {
                parser.SetDelimiters(",");
                try { fields = parser.ReadFields() ?? []; }
                catch (MalformedLineException) { throw new DrawingImportFault("invalid_csv", "Malformed quoted CSV record.", row); }
                if (!parser.EndOfData) throw new DrawingImportFault("invalid_csv", "CSV record boundaries are inconsistent.", row);
            }
            if (fields.Length > limits.Columns) throw new DrawingImportFault("column_limit", "Too many CSV columns.", row);
            var cells = fields.Select((value, index) => {
                result.Bound(value, row, index + 1);
                return new DrawingImportRawCell(index + 1, Coordinate(index + 1, row), value.Length == 0 ? DrawingImportCellKind.Blank : DrawingImportCellKind.Text, value, value.Length == 0 ? null : value);
            }).ToList();
            if (result.Headers.Count > 0 && fields.Length < result.Headers.Count) result.Add("short_csv_record", "CSV record has fewer fields than its headers.", row: row);
            result.TableRow(row, false, false, cells);
        }
    }

    // Only record boundaries/source lines are scanned here. TextFieldParser remains the CSV value/escaping parser.
    static IEnumerable<(string Text, int Row)> CsvRecords(string text)
    {
        var start = 0; var line = 1; var row = 1; var quoted = false; var fieldStart = true; var afterQuote = false;
        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (quoted)
            {
                if (ch == '"') { if (index + 1 < text.Length && text[index + 1] == '"') index++; else { quoted = false; afterQuote = true; } }
                else if (ch == '\r') { line++; if (index + 1 < text.Length && text[index + 1] == '\n') index++; }
                else if (ch == '\n') line++;
                continue;
            }
            if (ch is '\r' or '\n')
            {
                yield return (text[start..index], row); line++;
                if (ch == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                start = index + 1; row = line; fieldStart = true; afterQuote = false; continue;
            }
            if (ch == ',') { fieldStart = true; afterQuote = false; continue; }
            if (ch == '"')
            {
                if (!fieldStart || afterQuote) throw new DrawingImportFault("invalid_csv", "Quotes in unquoted values must use normal CSV quoting.", row);
                quoted = true; fieldStart = false; continue;
            }
            if (afterQuote && !char.IsWhiteSpace(ch)) throw new DrawingImportFault("invalid_csv", "Unexpected text after a closing quote.", row);
            if (!char.IsWhiteSpace(ch)) fieldStart = false;
        }
        if (quoted) throw new DrawingImportFault("invalid_csv", "A quoted CSV field is not closed.", row);
        if (start < text.Length) yield return (text[start..], row);
    }

    static void Xlsx(DrawingImportFormatInput input, DrawingImportFormatLimits limits, DrawingImportBuilder result)
    {
        using var zip = new ZipArchive(new MemoryStream(input.Content, false), ZipArchiveMode.Read);
        if (zip.Entries.Count > limits.Entries) throw new DrawingImportFault("entry_limit", "Too many archive entries.");
        var parts = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase); long expanded = 0;
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName; ValidatePartName(name);
            if (parts.ContainsKey(name)) throw new DrawingImportFault("duplicate_part", "Archive entry names collide, including case collisions.");
            if (name.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".xlsb", StringComparison.OrdinalIgnoreCase))
                throw new DrawingImportFault("macro_or_binary_workbook", "Macros/binary workbooks are not supported.");
            if (entry.Length < 0 || entry.Length > limits.ExpandedBytes - expanded) throw new DrawingImportFault("expanded_limit", "Declared expanded archive bytes exceed the budget.");
            using var stream = entry.Open(); using var target = new MemoryStream(); var buffer = new byte[8192];
            int count; while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                expanded += count; if (expanded > limits.ExpandedBytes) throw new DrawingImportFault("expanded_limit", "Actual expanded archive bytes exceed the budget.");
                target.Write(buffer, 0, count);
            }
            parts.Add(name, target.ToArray());
        }
        // Inspect all XML/relationship parts without resolving any external resource. Data is read only from the explicitly selected sheet.
        foreach (var (name, bytes) in parts.Where(part => part.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || part.Key.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
            ScanXml(bytes, limits, result);
        var contentTypes = Document(Part(parts, "[Content_Types].xml"), limits);
        XNamespace contentTypeNs = "http://schemas.openxmlformats.org/package/2006/content-types";
        if (contentTypes.Root?.Name != contentTypeNs + "Types") throw new DrawingImportFault("invalid_content_types", "Invalid package content-types root.");
        if (contentTypes.Descendants().Attributes("ContentType").Any(value => value.Value.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) || value.Value.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)))
            throw new DrawingImportFault("macro_or_binary_workbook", "Macro-enabled workbooks are not supported.");
        var office = OneRelation(Relations(parts, "", limits), "/officeDocument", true)!;
        var workbookPart = ResolvePart("", office.Target); var workbook = Document(Part(parts, workbookPart), limits);
        var workbookTypes = contentTypes.Root.Elements(contentTypeNs + "Override")
            .Where(value => (string?)value.Attribute("PartName") == "/" + workbookPart).ToArray();
        if (workbookTypes.Length != 1 || (string?)workbookTypes[0].Attribute("ContentType") != "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")
            throw new DrawingImportFault("unsupported_workbook_type", "The package must declare one ordinary XLSX workbook.");
        var ns = SpreadsheetNamespace(workbook, "workbook"); var workbookRels = Relations(parts, workbookPart, limits);
        var properties = workbook.Root!.Elements(ns + "workbookPr").ToArray();
        if (properties.Length > 1) throw new DrawingImportFault("invalid_workbook", "Duplicate workbook properties.");
        result.Date1904 = XmlBoolean((string?)properties.SingleOrDefault()?.Attribute("date1904"), false);
        var sheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var sheetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sheet in workbook.Root.Elements(ns + "sheets").Elements(ns + "sheet"))
        {
            var name = (string?)sheet.Attribute("name") ?? ""; var state = (string?)sheet.Attribute("state") ?? "visible";
            var relationshipIds = sheet.Attributes().Where(value => value.Name.LocalName == "id" && value.Name.NamespaceName is Rel or StrictRel).ToArray();
            var relationship = relationshipIds.Length == 1 ? relationshipIds[0].Value : null;
            if (name.Length is < 1 or > 128 || name.Any(char.IsControl) || !sheetNames.Add(name) || state is not ("visible" or "hidden" or "veryHidden") || relationship is null || !sheetIds.Add(relationship))
                throw new DrawingImportFault("invalid_sheet", "Worksheet names/state/relationship identities are invalid or duplicated.");
            var link = workbookRels.SingleOrDefault(value => value.Id == relationship && value.Type.EndsWith("/worksheet", StringComparison.Ordinal));
            if (link is null) throw new DrawingImportFault("invalid_sheet", "Only actual worksheets can be selected; missing/chartsheet references are unsupported.");
            var part = ResolvePart(workbookPart, link.Target); Part(parts, part); result.Sheets.Add(new(name, state, part));
            if (result.Sheets.Count > limits.Entries) throw new DrawingImportFault("sheet_limit", "Too many worksheet declarations.");
        }
        if (result.Sheets.Count == 0) throw new DrawingImportFault("worksheet_missing", "Workbook has no selectable worksheet.");
        DrawingImportSheet? selected;
        if (input.Worksheet is null)
        {
            if (result.Sheets.Count != 1) throw new DrawingImportFault("worksheet_required", "Multiple sheets require explicit worksheet selection.");
            selected = result.Sheets[0];
            if (selected.State != "visible") throw new DrawingImportFault("worksheet_required", "A hidden sheet requires explicit worksheet selection.");
        }
        else selected = result.Sheets.SingleOrDefault(sheet => sheet.Name == input.Worksheet);
        if (selected is null) throw new DrawingImportFault("worksheet_missing", "The selected worksheet name does not exist.");
        result.SelectedSheet = selected.Name;
        if (selected.State != "visible") result.Add("hidden_sheet", "Explicitly selected hidden sheet is included.", false);
        var sharedLink = OneRelation(workbookRels, "/sharedStrings", false);
        DrawingImportTextValue[] shared = sharedLink is null ? [] : SharedStrings(Document(Part(parts, ResolvePart(workbookPart, sharedLink.Target)), limits), limits);
        var styleLink = OneRelation(workbookRels, "/styles", false);
        var styles = styleLink is null ? new[] { DrawingImportNumberStyle.Number } : Styles(Document(Part(parts, ResolvePart(workbookPart, styleLink.Target)), limits), limits);
        Worksheet(Document(Part(parts, selected.Part), limits), shared, styles, result.Date1904.Value, limits, result);
    }

    static void ValidatePartName(string name)
    {
        var segments = name.TrimEnd('/').Split('/');
        if (name.Length is < 1 or > 512 || name.StartsWith('/') || name.Contains('\\') || name.Contains(':') || name.Contains('%') || name.Any(char.IsControl)
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new DrawingImportFault("unsafe_part", "Archive part name is absolute, ambiguous or traverses directories.");
    }
    static string ResolvePart(string source, string target)
    {
        if (target.Length is < 1 or > 512 || target.Contains(':') || target.Contains('\\') || target.Contains('#') || target.Contains('?') || target.StartsWith("//") || target.Any(char.IsControl))
            throw new DrawingImportFault("unsafe_relationship", "Relationship target is not an internal package part.");
        foreach (var encoded in new[] { "%2f", "%5c", "%2e" }) if (target.Contains(encoded, StringComparison.OrdinalIgnoreCase)) throw new DrawingImportFault("unsafe_relationship", "Encoded path separators/dots are unsupported.");
        string decoded;
        try { decoded = Uri.UnescapeDataString(target); }
        catch (UriFormatException) { throw new DrawingImportFault("unsafe_relationship", "Invalid encoded relationship target."); }
        if (decoded.Contains('%') || decoded.Contains('\\') || decoded.Contains(':')) throw new DrawingImportFault("unsafe_relationship", "Ambiguous encoded relationship target.");
        var stack = target.StartsWith('/') || source.Length == 0 ? new List<string>() : source.Split('/').SkipLast(1).ToList();
        foreach (var segment in decoded.TrimStart('/').Split('/'))
        {
            if (segment.Length == 0 || segment == ".") continue;
            if (segment == "..") { if (stack.Count == 0) throw new DrawingImportFault("unsafe_relationship", "Relationship escapes the package root."); stack.RemoveAt(stack.Count - 1); }
            else stack.Add(segment);
        }
        var path = string.Join('/', stack); ValidatePartName(path); return path;
    }
    static byte[] Part(Dictionary<string, byte[]> parts, string name)
    {
        // Every interpreted XML part must have passed the aggregate node/security scan above.
        if (!name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            throw new DrawingImportFault("unsupported_xml_part_name", "Required XML parts need an explicit .xml/.rels package name.");
        return parts.TryGetValue(name, out var bytes) ? bytes : throw new DrawingImportFault("missing_part", "A required workbook part is absent.");
    }
    static XmlReaderSettings Settings(DrawingImportFormatLimits limits) => new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = limits.ExpandedBytes, MaxCharactersFromEntities = 1, IgnoreComments = true };
    static XDocument Document(byte[] bytes, DrawingImportFormatLimits limits) { using var reader = XmlReader.Create(new MemoryStream(bytes, false), Settings(limits)); return XDocument.Load(reader, LoadOptions.None); }
    static void ScanXml(byte[] bytes, DrawingImportFormatLimits limits, DrawingImportBuilder result)
    {
        using var reader = XmlReader.Create(new MemoryStream(bytes, false), Settings(limits));
        while (reader.Read())
        {
            if (++result.XmlNodeCount > limits.XmlNodes) throw new DrawingImportFault("xml_node_limit", "Workbook XML node budget exceeded.");
            if (reader.NodeType != XmlNodeType.Element) continue;
            if (reader.LocalName is "externalLink" or "externalReferences") throw new DrawingImportFault("external_relationship", "External workbook links are not supported.");
            if (reader.LocalName == "Relationship")
            {
                var target = reader.GetAttribute("Target") ?? ""; var type = reader.GetAttribute("Type") ?? "";
                if (reader.GetAttribute("TargetMode")?.Equals("External", StringComparison.OrdinalIgnoreCase) == true || target.Contains(':') || target.StartsWith("//") || type.Contains("externalLink", StringComparison.OrdinalIgnoreCase))
                    throw new DrawingImportFault("external_relationship", "External relationships are not supported.");
            }
        }
    }
    static XNamespace SpreadsheetNamespace(XDocument document, string root)
    {
        if (document.Root is null || document.Root.Name.LocalName != root || document.Root.Name.NamespaceName is not (Main or StrictMain))
            throw new DrawingImportFault("invalid_spreadsheet_part", "Unsupported spreadsheet part root/namespace.");
        return document.Root.Name.Namespace;
    }
    static List<DrawingImportRelation> Relations(Dictionary<string, byte[]> parts, string source, DrawingImportFormatLimits limits)
    {
        var name = source.Length == 0 ? "_rels/.rels" : string.Join('/', source.Split('/').SkipLast(1).Append("_rels").Append(source.Split('/')[^1] + ".rels"));
        var document = Document(Part(parts, name), limits);
        if (document.Root?.Name != XName.Get("Relationships", PackageRel)) throw new DrawingImportFault("invalid_relationships", "Invalid package relationships root.");
        var seen = new HashSet<string>(StringComparer.Ordinal); var list = new List<DrawingImportRelation>();
        foreach (var row in document.Root.Elements(XName.Get("Relationship", PackageRel)))
        {
            var id = (string?)row.Attribute("Id") ?? ""; var type = (string?)row.Attribute("Type") ?? ""; var target = (string?)row.Attribute("Target") ?? "";
            if (id.Length == 0 || type.Length == 0 || target.Length == 0 || !seen.Add(id)) throw new DrawingImportFault("invalid_relationships", "Missing/duplicate relationship attributes.");
            list.Add(new(id, type, target));
        }
        return list;
    }
    static DrawingImportRelation? OneRelation(List<DrawingImportRelation> relations, string suffix, bool required)
    {
        var matches = relations.Where(row => row.Type.EndsWith(suffix, StringComparison.Ordinal)).ToArray();
        if (matches.Length > 1 || required && matches.Length != 1) throw new DrawingImportFault("invalid_relationships", "A required relationship is missing or ambiguous.");
        return matches.SingleOrDefault();
    }
    // ST_Xstring is decoded once per source <t>, before runs are joined. Output is never rescanned.
    // Raw preserves the XML-decoded, still-Xstring-encoded source text; XML entity escaping is handled by XmlReader.
    static DrawingImportTextValue RichText(XElement root, XNamespace ns, DrawingImportFormatLimits limits, int? row = null, int? column = null)
    {
        var raw = new StringBuilder(); var text = new StringBuilder();
        foreach (var node in root.Elements().Where(value => value.Name == ns + "t" || value.Name == ns + "r")
            .SelectMany(value => value.Name == ns + "t" ? new[] { value } : value.Elements(ns + "t")))
        {
            var value = node.Value;
            if (value.Length > limits.CellCharacters - raw.Length) throw new DrawingImportFault("cell_limit", "Encoded rich text exceeds the bounded cell limit.", row, column);
            raw.Append(value); text.Append(DecodeXstring(value));
        }
        return new(raw.ToString(), text.ToString());
    }
    static string DecodeXstring(string raw)
    {
        // A seven-code-unit escape emits one UTF-16 code unit; output cannot exceed bounded input.
        // _x005F_x0041_ emits literal _x0041_, not A. Malformed escape-looking text stays literal.
        var text = new StringBuilder(raw.Length);
        for (var index = 0; index < raw.Length;)
        {
            if (index + 6 < raw.Length && raw[index] == '_' && raw[index + 1] == 'x' && raw[index + 6] == '_'
                && ushort.TryParse(raw.AsSpan(index + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var codeUnit))
            { text.Append((char)codeUnit); index += 7; }
            else text.Append(raw[index++]);
        }
        return text.ToString();
    }
    static DrawingImportTextValue[] SharedStrings(XDocument document, DrawingImportFormatLimits limits)
    {
        var ns = SpreadsheetNamespace(document, "sst"); var list = new List<DrawingImportTextValue>(); long characters = 0;
        foreach (var item in document.Root!.Elements(ns + "si"))
        {
            if (list.Count >= limits.SharedStrings) throw new DrawingImportFault("shared_string_limit", "Too many shared strings.");
            var value = RichText(item, ns, limits); characters += value.Raw.Length;
            if (characters > limits.SharedStringCharacters) throw new DrawingImportFault("shared_string_limit", "Encoded shared string text exceeds its budget.");
            list.Add(value);
        }
        return list.ToArray();
    }
    static DrawingImportNumberStyle[] Styles(XDocument document, DrawingImportFormatLimits limits)
    {
        var ns = SpreadsheetNamespace(document, "styleSheet"); var custom = new Dictionary<int, string>();
        foreach (var format in document.Root!.Elements(ns + "numFmts").Elements(ns + "numFmt"))
        {
            var id = Integer((string?)format.Attribute("numFmtId"), "invalid_style", 0, 65535); var code = (string?)format.Attribute("formatCode") ?? "";
            if (code.Length > 512 || !custom.TryAdd(id, code) || custom.Count > limits.Styles) throw new DrawingImportFault("style_limit", "Number formats are invalid, duplicated or exceed the budget.");
        }
        var styles = new List<DrawingImportNumberStyle>();
        foreach (var format in document.Root.Elements(ns + "cellXfs").Elements(ns + "xf"))
        {
            if (styles.Count >= limits.Styles) throw new DrawingImportFault("style_limit", "Too many cell styles.");
            var id = Integer((string?)format.Attribute("numFmtId") ?? "0", "invalid_style", 0, 65535);
            styles.Add(id switch {
                >= 14 and <= 17 => DrawingImportNumberStyle.Date, 22 => DrawingImportNumberStyle.DateTime,
                >= 18 and <= 21 or 45 or 47 => DrawingImportNumberStyle.Time, 46 => DrawingImportNumberStyle.Duration,
                _ => custom.TryGetValue(id, out var code) ? CustomStyle(code) : id is >= 27 and <= 36 or >= 50 and <= 58 ? DrawingImportNumberStyle.Unknown : DrawingImportNumberStyle.Number
            });
        }
        return styles.Count == 0 ? [DrawingImportNumberStyle.Number] : styles.ToArray();
    }
    static DrawingImportNumberStyle CustomStyle(string code)
    {
        var visible = new StringBuilder(); var quoted = false; var duration = false;
        for (var index = 0; index < code.Length; index++)
        {
            var ch = char.ToLowerInvariant(code[index]);
            if (ch == '"') { quoted = !quoted; continue; }
            if (quoted) continue;
            if (ch is '\\' or '_' or '*') { index++; continue; }
            if (ch == ';') { if (code[(index + 1)..].Trim() != "@") return DrawingImportNumberStyle.Unknown; break; }
            if (ch == '[')
            {
                var end = code.IndexOf(']', index + 1); if (end < 0) return DrawingImportNumberStyle.Unknown;
                var bracket = code[(index + 1)..end].ToLowerInvariant();
                if (bracket.Length > 0 && bracket.All(value => value is 'h' or 'm' or 's')) duration = true;
                if (bracket.Contains('>') || bracket.Contains('<') || bracket.Contains('=')) return DrawingImportNumberStyle.Unknown;
                index = end; continue;
            }
            visible.Append(ch);
        }
        if (quoted) return DrawingImportNumberStyle.Unknown;
        if (duration) return DrawingImportNumberStyle.Duration;
        var text = visible.ToString().Replace("am/pm", "", StringComparison.Ordinal).Replace("a/p", "", StringComparison.Ordinal);
        if (text.Equals("general", StringComparison.Ordinal)) return DrawingImportNumberStyle.Number;
        var letters = text.Where(char.IsLetter).ToArray();
        if (letters.Any(ch => ch is not ('y' or 'm' or 'd' or 'h' or 's' or 'e'))) return DrawingImportNumberStyle.Unknown;
        var date = text.Contains('y') || text.Contains('d'); var time = text.Contains('h') || text.Contains('s');
        return date && time ? DrawingImportNumberStyle.DateTime : date ? DrawingImportNumberStyle.Date : time ? DrawingImportNumberStyle.Time
            : text.Contains('m') ? DrawingImportNumberStyle.Unknown : DrawingImportNumberStyle.Number;
    }

    static void Worksheet(XDocument document, DrawingImportTextValue[] shared, DrawingImportNumberStyle[] styles, bool date1904, DrawingImportFormatLimits limits, DrawingImportBuilder result)
    {
        var ns = SpreadsheetNamespace(document, "worksheet");
        if (document.Root!.Elements(ns + "mergeCells").Elements(ns + "mergeCell").Any()) throw new DrawingImportFault("merged_cells", "Merged cells in the selected sheet are unsupported; use an unmerged drawing table.");
        var previousRow = 0;
        foreach (var row in document.Root.Elements(ns + "sheetData").Elements(ns + "row"))
        {
            var inferred = row.Attribute("r") is null; var number = inferred ? previousRow + 1 : Integer((string?)row.Attribute("r"), "invalid_row", 1, 1_048_576);
            if (number <= previousRow || number > 1_048_576) throw new DrawingImportFault("invalid_row", "Row positions are duplicated, out of order or outside worksheet limits.", number);
            previousRow = number; if (inferred) result.Add("row_position_inferred", "Missing worksheet row coordinate follows source element order; it is not an external row identity.", false, number);
            var hidden = XmlBoolean((string?)row.Attribute("hidden"), false); var cells = new SortedDictionary<int, DrawingImportRawCell>(); var previousColumn = 0;
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = (string?)cell.Attribute("r"); var cellInferred = reference is null;
                var column = cellInferred ? previousColumn + 1 : Column(reference!, number);
                if (column <= previousColumn) throw new DrawingImportFault("invalid_cell_position", "Cell positions are duplicated or out of order.", number, column);
                previousColumn = column;
                var value = Cell(cell, ns, column, number, cellInferred, shared, styles, date1904, limits, result);
                if (column > limits.Columns)
                {
                    if (value.Kind == DrawingImportCellKind.Blank) { result.Add("trailing_blank_cell", "Blank formatted cells outside the table budget were ignored.", false, number, column); continue; }
                    throw new DrawingImportFault("column_limit", "Nonblank cell exceeds the column budget.", number, column);
                }
                cells.Add(column, value);
            }
            var meaningful = cells.Where(pair => pair.Value.Kind != DrawingImportCellKind.Blank).Select(pair => pair.Key).DefaultIfEmpty(0).Max();
            if (meaningful == 0) { if (hidden) result.Add("hidden_row", "Hidden wholly blank row was inspected.", false, number); continue; }
            // Explicit blank header cells inside the budget are real blank headers, including trailing ones.
            // Data-only trailing formatted blanks do not expand the table.
            var width = result.Headers.Count == 0 ? cells.Keys.DefaultIfEmpty(0).Max() : Math.Max(meaningful, result.Headers.Count);
            var values = Enumerable.Range(1, width).Select(column => cells.GetValueOrDefault(column) ?? Missing(column, number)).ToList();
            result.TableRow(number, inferred, hidden, values);
        }
    }
    static DrawingImportRawCell Cell(XElement cell, XNamespace ns, int column, int row, bool inferred, DrawingImportTextValue[] shared,
        DrawingImportNumberStyle[] styles, bool date1904, DrawingImportFormatLimits limits, DrawingImportBuilder result)
    {
        var reference = Coordinate(column, row); var raw = cell.Element(ns + "v")?.Value; if (raw is not null) result.Bound(raw, row, column);
        DrawingImportRawCell Value(DrawingImportCellKind kind, string? value = null, string? text = null, bool? boolean = null, DateTime? calendar = null, DateTimeOffset? zoned = null, TimeSpan? time = null, string? encodedText = null)
            => new(column, reference, kind, value, text, boolean, calendar, zoned, time, inferred, encodedText);
        DrawingImportRawCell Error(string code, string message, DrawingImportCellKind kind = DrawingImportCellKind.Error, string? value = null)
        { result.Add(code, message, row: row, column: column); return Value(kind, value ?? raw); }
        if (cell.Element(ns + "f") is { } formula) return Error("formula_not_supported", "Formula cells are refused; cached results are not imported.", DrawingImportCellKind.Formula, result.Bound(formula.Value, row, column));
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
        {
            var inline = cell.Element(ns + "is"); if (inline is null) return Error("invalid_inline_string", "Inline string content is missing.");
            var value = RichText(inline, ns, limits, row, column);
            return Value(value.Text.Length == 0 ? DrawingImportCellKind.Blank : DrawingImportCellKind.Text, value.Raw, value.Text, encodedText: value.Raw);
        }
        if (type == "s")
        {
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= shared.Length) return Error("invalid_shared_string", "Shared string index is invalid.");
            var value = shared[index];
            return Value(value.Text.Length == 0 ? DrawingImportCellKind.Blank : DrawingImportCellKind.Text, raw, value.Text, encodedText: value.Raw);
        }
        if (type == "str")
        {
            var text = raw is null ? null : DecodeXstring(raw);
            return Value(string.IsNullOrEmpty(text) ? DrawingImportCellKind.Blank : DrawingImportCellKind.Text, raw, text, encodedText: raw);
        }
        if (type == "e") return Error("source_cell_error", "Source cell contains a spreadsheet error.");
        if (type == "b")
        {
            return raw?.Trim() switch { "0" or "false" => Value(DrawingImportCellKind.Boolean, raw, boolean: false), "1" or "true" => Value(DrawingImportCellKind.Boolean, raw, boolean: true),
                _ => Error("invalid_boolean", "Boolean cell is neither true/false nor0/1.") };
        }
        if (type == "d")
        {
            if (raw is not null && DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return Value(DrawingImportCellKind.Date, raw, calendar: date.ToDateTime(TimeOnly.MinValue));
            if (raw is not null && DateTime.TryParseExact(raw, ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
                return Value(DrawingImportCellKind.DateTime, raw, calendar: DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
            if (raw is not null && DateTimeOffset.TryParseExact(raw, ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"],
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var zoned)) return Value(DrawingImportCellKind.DateTime, raw, zoned: zoned);
            return Error("invalid_iso_date", "Declared ISO date cell is invalid; no local date is guessed.");
        }
        if (type is not (null or "n")) return Error("unsupported_cell_type", "Unknown source cell type remains unreadable.", DrawingImportCellKind.Unknown);
        if (string.IsNullOrEmpty(raw)) return Value(DrawingImportCellKind.Blank, raw);
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) return Error("invalid_number", "Numeric source cell is not a finite number.");
        var style = cell.Attribute("s") is null ? 0 : Integer((string?)cell.Attribute("s"), "invalid_style", 0, limits.Styles);
        if (style >= styles.Length) return Error("invalid_style", "Cell style index is not present.");
        var numberStyle = styles[style];
        if (numberStyle is DrawingImportNumberStyle.Number or DrawingImportNumberStyle.Unknown)
        {
            if (numberStyle == DrawingImportNumberStyle.Unknown) result.Add("unknown_number_format", "Ambiguous/custom number format is preserved as a raw number, not guessed as a date.", false, row, column);
            return Value(DrawingImportCellKind.Number, raw);
        }
        try
        {
            if (number < 0) return Error("invalid_date_serial", "Negative date/time serial is unsupported.");
            if (numberStyle is DrawingImportNumberStyle.Time or DrawingImportNumberStyle.Duration)
            {
                var time = TimeSpan.FromDays(number);
                if (HasFractionalSerial(raw) && time.Ticks % TimeSpan.TicksPerDay == 0)
                    return Error("invalid_date_serial", "Fractional time cannot be represented without discarding it; correct the source precision.");
                return Value(numberStyle == DrawingImportNumberStyle.Duration ? DrawingImportCellKind.Duration : DrawingImportCellKind.Time, raw, time: time);
            }
            if (!date1904 && (number < 1 || number is >= 60 and < 61)) return Error("invalid_date_serial", "1900 date serial0 and the fictitious day60 are not real calendar dates.");
            var converted = (date1904 ? new DateTime(1904, 1, 1) : new DateTime(1899, 12, 31)).AddDays(date1904 ? number : number >= 61 ? number - 1 : number);
            var withTime = converted.TimeOfDay != TimeSpan.Zero;
            if (!withTime && HasFractionalSerial(raw)) return Error("invalid_date_serial", "Fractional time cannot be represented without discarding it; correct the source precision.");
            if (numberStyle == DrawingImportNumberStyle.Date && withTime) result.Add("date_has_time", "Date-formatted numeric cell also carries time; the time was retained.", false, row, column);
            return Value(numberStyle == DrawingImportNumberStyle.DateTime || withTime ? DrawingImportCellKind.DateTime : DrawingImportCellKind.Date, raw, calendar: converted);
        }
        catch (ArgumentOutOfRangeException) { return Error("invalid_date_serial", "Date/time serial is outside the representable range."); }
        catch (OverflowException) { return Error("invalid_date_serial", "Date/time serial is outside the representable range."); }
    }
    // Inspect the original finite numeric token so double/tick rounding cannot silently erase a date's time fraction.
    static bool HasFractionalSerial(string raw)
    {
        var token = raw.Trim().TrimStart('+', '-'); var exponentAt = token.IndexOfAny(['e', 'E']);
        var mantissa = exponentAt < 0 ? token : token[..exponentAt]; var point = mantissa.IndexOf('.');
        var fractionalDigits = point < 0 ? 0 : mantissa.Length - point - 1;
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal); var trailingZeroes = 0;
        for (var index = digits.Length - 1; index >= 0 && digits[index] == '0'; index--) trailingZeroes++;
        if (trailingZeroes == digits.Length) return false;
        long exponent = 0;
        if (exponentAt >= 0 && !long.TryParse(token[(exponentAt + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
            return token[exponentAt + 1] == '-';
        // Avoid subtraction overflow for deliberately enormous exponents.
        return exponent < (long)fractionalDigits - trailingZeroes;
    }
    static bool XmlBoolean(string? value, bool fallback) => value switch { null => fallback, "1" or "true" => true, "0" or "false" => false,
        _ => throw new DrawingImportFault("invalid_boolean_attribute", "A workbook Boolean attribute is invalid.") };
    static int Integer(string? value, string code, int minimum, int maximum) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= minimum && number <= maximum
        ? number : throw new DrawingImportFault(code, "Integer coordinate/style metadata is invalid.");
    static int Column(string reference, int row)
    {
        var index = 0; var column = 0;
        while (index < reference.Length && reference[index] is >= 'A' and <= 'Z') { if (index > 2) throw new DrawingImportFault("invalid_cell_position", "Cell coordinate column is too long.", row); column = column * 26 + reference[index++] - 'A' + 1; }
        if (index == 0 || column > 16_384 || !int.TryParse(reference[index..], NumberStyles.None, CultureInfo.InvariantCulture, out var actual) || actual != row)
            throw new DrawingImportFault("invalid_cell_position", "Cell reference does not belong to its source row.", row);
        return column;
    }
    static DrawingImportRawCell Missing(int column, int row) => new(column, Coordinate(column, row), DrawingImportCellKind.Missing, null);
    static string Coordinate(int column, int row)
    {
        var letters = ""; for (var number = column; number > 0; number = (number - 1) / 26) letters = (char)('A' + (number - 1) % 26) + letters;
        return letters + row.ToString(CultureInfo.InvariantCulture);
    }
}
