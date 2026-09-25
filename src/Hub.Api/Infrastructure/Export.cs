using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Hub.Api.Infrastructure;

public sealed record ExportColumn(string Header, Func<object, object?> Value);

/// CSV (UTF-8 with BOM) and a minimal XLSX writer with a header row, frozen pane, date-typed cells and a
/// Parameters sheet (§19 export rules). No third-party library: SpreadsheetML is a zip of four XML parts.
public static class Export
{
    public const int MaxRows = 50_000;

    public static string Csv(string? v)
    {
        v ??= "";
        // CSV injection (OWASP): text a spreadsheet would run as a formula gets a leading apostrophe; numbers stay numbers.
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0]) && !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) v = "'" + v;
        return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    public static byte[] Bom(string s) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(s)];

    public static byte[] ToCsv(IEnumerable<object> rows, IReadOnlyList<ExportColumn> cols)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", cols.Select(c => Csv(c.Header))));
        foreach (var r in rows) sb.AppendLine(string.Join(",", cols.Select(c => Csv(Format(c.Value(r))))));
        return Bom(sb.ToString());
    }

    static string? Format(object? v) => v switch
    {
        null => null,
        DateOnly d => d.ToString("yyyy-MM-dd"),
        DateTimeOffset t => t.ToString("yyyy-MM-dd HH:mm"),
        bool b => b ? "Yes" : "No",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable<string> xs => string.Join("; ", xs),
        _ => v.ToString(),
    };

    public static byte[] ToXlsx(string sheet, IEnumerable<object> rows, IReadOnlyList<ExportColumn> cols, IEnumerable<(string, string)> parameters)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Part(string name, string xml)
            {
                using var w = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Fastest).Open(), new UTF8Encoding(false));
                w.Write(xml);
            }
            Part("[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Part("_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Part("xl/workbook.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{X(Truncate(sheet, 31))}" sheetId="1" r:id="rId1"/><sheet name="Parameters" sheetId="2" r:id="rId2"/></sheets></workbook>""");
            Part("xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            // Style 1 = bold header, style 2 = date (numFmt 14), style 3 = date-time.
            Part("xl/styles.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="1"><numFmt numFmtId="164" formatCode="yyyy-mm-dd hh:mm"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="14" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs></styleSheet>""");

            var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews><sheetData>""");
            sb.Append("<row r=\"1\">");
            for (var c = 0; c < cols.Count; c++) sb.Append(Cell(c, 1, cols[c].Header, header: true));
            sb.Append("</row>");
            var r = 2;
            foreach (var row in rows.Take(MaxRows))
            {
                sb.Append($"<row r=\"{r}\">");
                for (var c = 0; c < cols.Count; c++) sb.Append(Cell(c, r, cols[c].Value(row)));
                sb.Append("</row>");
                r++;
            }
            sb.Append("</sheetData></worksheet>");
            Part("xl/worksheets/sheet1.xml", sb.ToString());

            Part("xl/worksheets/sheet2.xml", Rows(parameters));
        }
        return ms.ToArray();
    }

    static string Rows(IEnumerable<(string Key, string Value)> parameters)
    {
        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        var r = 1;
        foreach (var (k, v) in parameters) { sb.Append($"<row r=\"{r}\">{Cell(0, r, k, header: true)}{Cell(1, r, v)}</row>"); r++; }
        return sb.Append("</sheetData></worksheet>").ToString();
    }

    static string Col(int i) { var s = ""; i++; while (i > 0) { var m = (i - 1) % 26; s = (char)('A' + m) + s; i = (i - 1) / 26; } return s; }

    static string Cell(int c, int r, object? v, bool header = false)
    {
        var refc = $"{Col(c)}{r}";
        return v switch
        {
            null => $"<c r=\"{refc}\"/>",
            DateOnly d => $"<c r=\"{refc}\" s=\"2\"><v>{(d.ToDateTime(TimeOnly.MinValue) - new DateTime(1899, 12, 30)).TotalDays.ToString(CultureInfo.InvariantCulture)}</v></c>",
            DateTimeOffset t => $"<c r=\"{refc}\" s=\"3\"><v>{(t.UtcDateTime - new DateTime(1899, 12, 30)).TotalDays.ToString(CultureInfo.InvariantCulture)}</v></c>",
            int or long or decimal or double or float => $"<c r=\"{refc}\"><v>{Convert.ToString(v, CultureInfo.InvariantCulture)}</v></c>",
            _ => $"<c r=\"{refc}\" t=\"inlineStr\"{(header ? " s=\"1\"" : "")}><is><t xml:space=\"preserve\">{X(Format(v) ?? "")}</t></is></c>",
        };
    }

    static string X(string s) => SecurityElement.Escape(new string(s.Where(ch => ch == '\t' || ch == '\n' || ch == '\r' || ch >= 0x20).ToArray())) ?? "";
    static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];
}
