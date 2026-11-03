// Pure in-memory CSV/XLSX format-reader tests; no project validation or atomic-import acceptance.
// Pure in-memory parser coverage. No HTTP, database, domain IDs, default resolution or import acceptance claim.
using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Hub.Api.Features;
using Xunit;

namespace Hub.Tests.Api;

public sealed class DrawingImportFormatTests
{
    const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    const string PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    [Theory]
    [InlineData(DrawingImportFormatKind.Csv)]
    [InlineData(DrawingImportFormatKind.Xlsx)]
    public void Ordinary_forty_rows_need_no_provenance_column(DrawingImportFormatKind format)
    {
        var bytes = format == DrawingImportFormatKind.Csv ? Csv(FortyCsv()) : Book(FortyRows());
        var parsed = DrawingImportFormat.Parse(new(format, bytes));
        Assert.True(parsed.Success, Explain(parsed));
        Assert.True(parsed.Complete);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), parsed.ContentSha256);
        Assert.Equal(new[] { "title", "client drawing number" }, parsed.Headers.Select(value => value.Normalized));
        Assert.Equal(40, parsed.Records.Count);
        Assert.Equal(Enumerable.Range(2, 40), parsed.Records.Select(value => value.SourceRow));
        Assert.All(parsed.Records, row => {
            Assert.False(row.SourceRowInferred);
            Assert.False(row.Hidden);
            Assert.All(row.Cells, cell => Assert.Equal(DrawingImportCellKind.Text, cell.Kind));
        });
        Assert.Equal("Drawing01", parsed.Records[0].Cells[0].Text);
        Assert.Equal("C-040", parsed.Records[39].Cells[1].Text);
        Assert.DoesNotContain(parsed.Headers, value => value.Normalized.Contains("source", StringComparison.Ordinal));
        Assert.Equal(format == DrawingImportFormatKind.Xlsx ? "Drawings" : null, parsed.SelectedSheet);
    }

    [Theory]
    [InlineData(DrawingImportFormatKind.Csv)]
    [InlineData(DrawingImportFormatKind.Xlsx)]
    public void Blank_title_remains_raw_for_the_later_domain_validator(DrawingImportFormatKind format)
    {
        // Success here means format-readable, never permission to create. Preview/create validation must refuse this batch.
        var bytes = format == DrawingImportFormatKind.Csv ? Csv(FortyCsv(blankTitle: 10)) : Book(FortyRows(blankTitle: 10));
        var parsed = DrawingImportFormat.Parse(new(format, bytes));
        Assert.True(parsed.Success, Explain(parsed));
        Assert.Equal(40, parsed.Records.Count);
        var cell = parsed.Records[9].Cells[0];
        Assert.Equal(DrawingImportCellKind.Blank, cell.Kind);
        Assert.True(string.IsNullOrEmpty(cell.Text));
        Assert.Equal(11, parsed.Records[9].SourceRow);
    }

    [Fact]
    public void Csv_preserves_bom_unicode_quoting_physical_rows_and_literal_types()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Csv("title,client drawing number\r\n\"Étage, 北\",0012\r\n\"line one\r\nline \"\"two\"\"\",2026-11-02\r\n=1+1,true\r\n")).ToArray();
        var parsed = DrawingImportFormat.Parse(new(DrawingImportFormatKind.Csv, bytes));
        Assert.True(parsed.Success, Explain(parsed));
        Assert.Equal(new[] { 2, 3, 5 }, parsed.Records.Select(row => row.SourceRow));
        Assert.Equal("Étage, 北", parsed.Records[0].Cells[0].Text);
        Assert.Equal("0012", parsed.Records[0].Cells[1].Text);
        Assert.Equal("line one\r\nline \"two\"", parsed.Records[1].Cells[0].Text);
        Assert.Equal("2026-11-02", parsed.Records[1].Cells[1].Text);
        Assert.Equal("=1+1", parsed.Records[2].Cells[0].Text);
        Assert.Equal("true", parsed.Records[2].Cells[1].Text);
        Assert.All(parsed.Records.SelectMany(row => row.Cells), cell => {
            Assert.Equal(DrawingImportCellKind.Text, cell.Kind);
            Assert.Null(cell.CalendarValue);
            Assert.Null(cell.Boolean);
        });
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), parsed.ContentSha256);
    }

    [Theory]
    [InlineData("title,title\na,b", "duplicate_header")]
    [InlineData(" Title ,TITLE\na,b", "duplicate_header")]
    [InlineData("title,\na,b", "blank_header")]
    [InlineData("title,number\na", "short_csv_record")]
    [InlineData("title\na,b", "row_wider_than_header")]
    [InlineData("title\n\"not closed", "invalid_csv")]
    [InlineData("title\na\"b", "invalid_csv")]
    [InlineData("title\n\"a\"junk", "invalid_csv")]
    public void Csv_bad_headers_shapes_or_quoting_are_diagnosed(string input, string code)
        => Failure(DrawingImportFormat.Parse(new(DrawingImportFormatKind.Csv, Csv(input))), code);

    [Fact]
    public void Invalid_utf8_empty_content_and_unsupported_format_do_not_throw()
    {
        Failure(DrawingImportFormat.Parse(new(DrawingImportFormatKind.Csv, [0xC3, 0x28])), "invalid_utf8");
        Failure(DrawingImportFormat.Parse(new(DrawingImportFormatKind.Csv, [])), "empty_file");
        Failure(DrawingImportFormat.Parse(new((DrawingImportFormatKind)999, Csv("x"))), "unsupported_format");
        Failure(DrawingImportFormat.Parse(new(DrawingImportFormatKind.Csv, Csv("title\na"), "Drawings")), "worksheet_not_csv");
    }

    [Fact]
    public void Csv_source_and_table_budgets_refuse_excess_without_claiming_all_rows_were_checked()
    {
        var thousand = "title\n" + string.Join('\n', Enumerable.Range(1, 1000).Select(index => "D" + index));
        Assert.True(DrawingImportFormat.Parse(new(DrawingImportFormatKind.Csv, Csv(thousand))).Success);
        var tooMany = DrawingImportFormat.Parse(new(DrawingImportFormatKind.Csv, Csv(thousand + "\nD1001")));
        Failure(tooMany, "record_limit");
        Assert.False(tooMany.Complete);
        Assert.Equal(1000, tooMany.Records.Count);
        var wide = string.Join(',', Enumerable.Range(1, 101).Select(index => "h" + index)) + "\nvalue";
        Failure(DrawingImportFormat.Parse(new(DrawingImportFormatKind.Csv, Csv(wide))), "column_limit");
        var file = DrawingImportFormat.Parse(new(DrawingImportFormatKind.Csv, new byte[256 * 1024 + 1]));
        Failure(file, "file_limit");
        Assert.Null(file.ContentSha256);
        Failure(DrawingImportFormat.Parse(new(DrawingImportFormatKind.Csv, Csv("title\n" + new string('x', 16_001)))), "cell_limit");
    }

    [Fact]
    public void Xlsx_shared_rich_text_keeps_leading_zeroes_and_sparse_cells_keep_missing_distinct_from_blank()
    {
        var rows = Row(1, Inline("A1", "title"), Inline("B1", "client drawing number"), Inline("C1", "description"))
            + Row(2, "<c r=\"A2\" t=\"s\"><v>0</v></c>", Inline("B2", "0012"), "<c r=\"C2\"/>")
            + Row(3, Inline("A3", "other"), Inline("C3", "tail"));
        var shared = $"<sst xmlns=\"{Main}\"><si><r><t>North </t></r><r><t>Bridge</t></r><rPh sb=\"0\" eb=\"1\"><t>phonetic</t></rPh></si></sst>";
        var parsed = Xlsx(Book(rows, shared: shared));
        Assert.True(parsed.Success, Explain(parsed));
        Assert.Equal("North Bridge", parsed.Records[0].Cells[0].Text);
        Assert.Equal("0", parsed.Records[0].Cells[0].Raw); // Shared-string index is source representation, not identity.
        Assert.Equal("0012", parsed.Records[0].Cells[1].Text);
        Assert.Equal(DrawingImportCellKind.Blank, parsed.Records[0].Cells[2].Kind);
        Assert.Equal(DrawingImportCellKind.Missing, parsed.Records[1].Cells[1].Kind);
        Assert.Null(parsed.Records[1].Cells[1].Raw);
        Assert.Equal("B3", parsed.Records[1].Cells[1].Reference);
    }

    [Theory]
    [InlineData("inline")]
    [InlineData("shared")]
    [InlineData("str")]
    public void Spreadsheet_text_decodes_xstring_once_and_preserves_source_representation(string sourceKind)
    {
        var cases = new[] {
            (Raw: "Top_x000D_Bottom", Text: "Top\rBottom"),
            (Raw: "_x005F_x0041_", Text: "_x0041_"),
            (Raw: "_x005f_x000D_", Text: "_x000D_"),
            (Raw: "_x0041__x000d__x0042_", Text: "A\rB"),
            (Raw: "_xD83D__xDE80_", Text: "\uD83D\uDE80"),
            (Raw: "_x004G_ _x123_ _X0041_", Text: "_x004G_ _x123_ _X0041_")
        };
        foreach (var sample in cases)
        {
            var parsed = Xlsx(TextBook(sourceKind, sample.Raw));
            Assert.True(parsed.Success, Explain(parsed));
            var cell = Assert.Single(parsed.Records).Cells[0];
            Assert.Equal(DrawingImportCellKind.Text, cell.Kind);
            Assert.Equal(sample.Text, cell.Text);
            Assert.Equal(sample.Raw, cell.EncodedText);
            Assert.Equal(sourceKind == "shared" ? "0" : sample.Raw, cell.Raw);
        }
    }

    [Theory]
    [InlineData("inline")]
    [InlineData("shared")]
    public void Rich_text_decodes_each_source_run_without_rescanning_joined_text(string sourceKind)
    {
        var runs = "<r><t>_x00</t></r><r><t>41_</t></r><rPh sb=\"0\" eb=\"1\"><t>_x000D_</t></rPh>";
        var cell = sourceKind == "inline" ? "<c r=\"A2\" t=\"inlineStr\"><is>" + runs + "</is></c>" : "<c r=\"A2\" t=\"s\"><v>0</v></c>";
        var shared = sourceKind == "shared" ? $"<sst xmlns=\"{Main}\"><si>{runs}</si></sst>" : null;
        var parsed = Xlsx(Book(ValueRows(cell), shared: shared));
        Assert.True(parsed.Success, Explain(parsed));
        Assert.Equal("_x0041_", Assert.Single(parsed.Records).Cells[0].Text);
        Assert.Equal("_x0041_", parsed.Records[0].Cells[0].EncodedText);

        runs = "<r><t>Top_x000D_</t></r><r><t>Bottom</t></r>";
        cell = sourceKind == "inline" ? "<c r=\"A2\" t=\"inlineStr\"><is>" + runs + "</is></c>" : "<c r=\"A2\" t=\"s\"><v>0</v></c>";
        shared = sourceKind == "shared" ? $"<sst xmlns=\"{Main}\"><si>{runs}</si></sst>" : null;
        parsed = Xlsx(Book(ValueRows(cell), shared: shared));
        Assert.True(parsed.Success, Explain(parsed));
        Assert.Equal("Top\rBottom", Assert.Single(parsed.Records).Cells[0].Text);
        Assert.Equal("Top_x000D_Bottom", parsed.Records[0].Cells[0].EncodedText);
    }

    [Fact]
    public void Xstring_headers_normalize_decoded_text_but_csv_escape_looking_text_stays_literal()
    {
        var parsed = Xlsx(Book(Row(1, Inline("A1", "ti_x0074_le"), Inline("B1", "_x005F_x0041_"))
            + Row(2, Inline("A2", "drawing"), Inline("B2", "literal"))));
        Assert.True(parsed.Success, Explain(parsed));
        Assert.Equal("title", parsed.Headers[0].Normalized);
        Assert.Equal("ti_x0074_le", parsed.Headers[0].Raw);
        Assert.Equal("title", parsed.Headers[0].Cell.Text);
        Assert.Equal("_x0041_", parsed.Headers[1].Normalized);
        Assert.Equal("_x005F_x0041_", parsed.Headers[1].Raw);
        var csv = DrawingImportFormat.Parse(new(DrawingImportFormatKind.Csv, Csv("title\n_x000D_\n_x005F_x0041_")));
        Assert.True(csv.Success, Explain(csv));
        Assert.Equal(new[] { "_x000D_", "_x005F_x0041_" }, csv.Records.Select(row => row.Cells[0].Text));
        Assert.All(csv.Records, row => Assert.Null(row.Cells[0].EncodedText));
    }

    [Theory]
    [InlineData("inline")]
    [InlineData("shared")]
    [InlineData("str")]
    public void Encoded_input_is_bounded_before_decoding_can_shrink_it(string sourceKind)
    {
        var encoded = string.Concat(Enumerable.Repeat("_x0041_", 2286)); // 16,002 raw characters; only 2,286 decoded.
        var parsed = Xlsx(TextBook(sourceKind, encoded));
        Failure(parsed, "cell_limit");
        if (sourceKind != "shared")
        {
            var error = Assert.Single(parsed.Diagnostics, value => value.Code == "cell_limit");
            Assert.Equal(2, error.SourceRow);
            Assert.Equal(1, error.Column);
        }
        if (sourceKind == "shared")
            Failure(Xlsx(TextBook(sourceKind, "_x0041_"), limits: new() { SharedStringCharacters = 6 }), "shared_string_limit");
    }

    [Fact]
    public void Xlsx_numeric_boolean_error_and_unknown_types_do_not_gain_invented_text()
    {
        var parsed = Xlsx(Book(Row(1, Inline("A1", "value")) + Row(2, Number("A2", "45000"))
            + Row(3, "<c r=\"A3\" t=\"b\"><v>1</v></c>")
            + Row(4, "<c r=\"A4\" t=\"e\"><v>#REF!</v></c>")
            + Row(5, "<c r=\"A5\" t=\"future\"><v>unknown</v></c>")));
        Assert.True(parsed.Complete);
        Assert.Equal(4, parsed.Records.Count);
        Assert.Equal(DrawingImportCellKind.Number, parsed.Records[0].Cells[0].Kind);
        Assert.Equal("45000", parsed.Records[0].Cells[0].Raw);
        Assert.Null(parsed.Records[0].Cells[0].CalendarValue);
        Assert.Null(parsed.Records[0].Cells[0].Text);
        Assert.True(parsed.Records[1].Cells[0].Boolean == true);
        Failure(parsed, "source_cell_error");
        Failure(parsed, "unsupported_cell_type");
        Assert.Equal(DrawingImportCellKind.Unknown, parsed.Records[3].Cells[0].Kind);
    }

    [Fact]
    public void Formula_with_cached_result_is_an_error_and_all_forty_readable_rows_are_returned()
    {
        var rows = FortyRows().Replace(Inline("A11", "Drawing10"), "<c r=\"A11\" t=\"str\"><f>CONCAT(\"unsafe\",\" result\")</f><v>cached title</v></c>", StringComparison.Ordinal);
        var parsed = Xlsx(Book(rows));
        Failure(parsed, "formula_not_supported");
        Assert.True(parsed.Complete);
        Assert.Equal(40, parsed.Records.Count);
        var cell = parsed.Records[9].Cells[0];
        Assert.Equal(DrawingImportCellKind.Formula, cell.Kind);
        Assert.Contains("CONCAT", cell.Raw);
        Assert.Null(cell.Text);
        Assert.Null(cell.CalendarValue);
        Assert.DoesNotContain(parsed.Records.SelectMany(row => row.Cells), value => value.Raw == "cached title" || value.Text == "cached title");
        Assert.Equal(11, Assert.Single(parsed.Diagnostics, value => value.Code == "formula_not_supported").SourceRow);
    }

    [Fact]
    public void Hidden_rows_are_included_and_multiple_sheets_require_exact_selection()
    {
        var rows = FortyRows().Replace("<row r=\"11\">", "<row r=\"11\" hidden=\"1\">", StringComparison.Ordinal);
        var bytes = Book(rows, secondRows: Row(1, Inline("A1", "title")) + Row(2, Inline("A2", "second")));
        var noSelection = Xlsx(bytes);
        Failure(noSelection, "worksheet_required");
        Assert.Equal(new[] { "Drawings", "Other" }, noSelection.Sheets.Select(sheet => sheet.Name));
        Assert.Empty(noSelection.Records);
        var selected = Xlsx(bytes, "Drawings");
        Assert.True(selected.Success, Explain(selected));
        Assert.Equal(40, selected.Records.Count);
        Assert.True(selected.Records[9].Hidden);
        Assert.Contains(selected.Diagnostics, value => value.Code == "hidden_row" && value.SourceRow == 11 && value.Severity == DrawingImportDiagnosticSeverity.Warning);
        Assert.Equal("second", Assert.Single(Xlsx(bytes, "Other").Records).Cells[0].Text);
        Failure(Xlsx(bytes, "drawings"), "worksheet_missing");
        Failure(Xlsx(Book(FortyRows(), firstState: "hidden")), "worksheet_required");
        var hidden = Xlsx(Book(FortyRows(), firstState: "hidden"), "Drawings");
        Assert.True(hidden.Success, Explain(hidden));
        Assert.Contains(hidden.Diagnostics, value => value.Code == "hidden_sheet");
    }

    [Fact]
    public void Duplicate_blank_and_nontext_xlsx_headers_are_errors()
    {
        Failure(Xlsx(Book(Row(1, Inline("A1", " Title "), Inline("B1", "TITLE")) + Row(2, Inline("A2", "a")))), "duplicate_header");
        Failure(Xlsx(Book(Row(1, Inline("A1", "title"), "<c r=\"B1\"/>") + Row(2, Inline("A2", "a")))), "blank_header");
        Failure(Xlsx(Book(Row(1, Number("A1", "123")) + Row(2, Inline("A2", "a")))), "header_not_text");
        Failure(Xlsx(Book(Row(1, Inline("A1", "title")) + Row(2, Inline("A2", "a"), Inline("B2", "extra")))), "row_wider_than_header");
        Failure(Xlsx(Book(Row(1, Inline("A1", "title")) + Row(2, Inline("CW2", "column101")))), "column_limit");
    }

    [Theory]
    [InlineData(false, "1", "1900-01-01")]
    [InlineData(false, "59", "1900-02-28")]
    [InlineData(false, "61", "1900-03-01")]
    [InlineData(true, "0", "1904-01-01")]
    [InlineData(true, "60", "1904-03-01")]
    public void Declared_excel_date_styles_use_the_correct_calendar(bool date1904, string serial, string expected)
    {
        var parsed = Xlsx(Book(ValueRows(Number("A2", serial, 1)), styles: Style(0, 14), date1904: date1904));
        Assert.True(parsed.Success, Explain(parsed));
        var cell = Assert.Single(parsed.Records).Cells[0];
        Assert.Equal(DrawingImportCellKind.Date, cell.Kind);
        Assert.Equal(expected, cell.CalendarValue!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Assert.Equal(DateTimeKind.Unspecified, cell.CalendarValue.Value.Kind);
        Assert.Equal(serial, cell.Raw);
        Assert.Equal(date1904, parsed.Date1904);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("60")]
    [InlineData("60.5")]
    [InlineData("61.00000000000000000001")]
    [InlineData("-1")]
    [InlineData("9999999999999999999")]
    public void Invalid_1900_calendar_dates_are_refused(string serial)
        => Failure(Xlsx(Book(ValueRows(Number("A2", serial, 1)), styles: Style(0, 14))), "invalid_date_serial");

    [Theory]
    [InlineData("<c r=\"A2\" t=\"d\"><v>11/02/2026</v></c>", "invalid_iso_date")]
    [InlineData("<c r=\"A2\" t=\"d\"><v>1900-02-29</v></c>", "invalid_iso_date")]
    [InlineData("<c r=\"A2\"><v>NaN</v></c>", "invalid_number")]
    [InlineData("<c r=\"A2\"><v>1e999</v></c>", "invalid_number")]
    [InlineData("<c r=\"A2\" t=\"b\"><v>yes</v></c>", "invalid_boolean")]
    [InlineData("<c r=\"A2\" t=\"s\"><v>9</v></c>", "invalid_shared_string")]
    [InlineData("<c r=\"A2\" s=\"1\"><v>61</v></c>", "invalid_style")]
    public void Invalid_declared_values_remain_diagnostics_instead_of_coercions(string cell, string code)
        => Failure(Xlsx(Book(ValueRows(cell))), code);

    [Fact]
    public void Date_fractions_time_duration_and_iso_offsets_are_retained_without_timezone_guessing()
    {
        var rows = Row(1, Inline("A1", "value")) + Row(2, Number("A2", "61.5", 1))
            + Row(3, Number("A3", "0.5", 2)) + Row(4, Number("A4", "1.5", 3))
            + Row(5, "<c r=\"A5\" t=\"d\"><v>2026-11-02</v></c>")
            + Row(6, "<c r=\"A6\" t=\"d\"><v>2026-11-02T12:34:56</v></c>")
            + Row(7, "<c r=\"A7\" t=\"d\"><v>2026-11-02T12:34:56-04:00</v></c>")
            + Row(8, "<c r=\"A8\" t=\"d\"><v>2026-11-02T12:34:56Z</v></c>");
        var parsed = Xlsx(Book(rows, styles: Style(0, 14, 20, 46)));
        Assert.True(parsed.Success, Explain(parsed));
        Assert.Equal(new DateTime(1900, 3, 1, 12, 0, 0), parsed.Records[0].Cells[0].CalendarValue);
        Assert.Contains(parsed.Diagnostics, value => value.Code == "date_has_time");
        Assert.Equal(DrawingImportCellKind.Time, parsed.Records[1].Cells[0].Kind);
        Assert.Equal(TimeSpan.FromHours(12), parsed.Records[1].Cells[0].TimeValue);
        Assert.Equal(DrawingImportCellKind.Duration, parsed.Records[2].Cells[0].Kind);
        Assert.Equal(TimeSpan.FromHours(36), parsed.Records[2].Cells[0].TimeValue);
        Assert.Equal(DrawingImportCellKind.Date, parsed.Records[3].Cells[0].Kind);
        Assert.Equal(DateTimeKind.Unspecified, parsed.Records[4].Cells[0].CalendarValue!.Value.Kind);
        Assert.Null(parsed.Records[4].Cells[0].ZonedValue);
        Assert.Equal(TimeSpan.FromHours(-4), parsed.Records[5].Cells[0].ZonedValue!.Value.Offset);
        Assert.Null(parsed.Records[5].Cells[0].CalendarValue);
        Assert.Equal(TimeSpan.Zero, parsed.Records[6].Cells[0].ZonedValue!.Value.Offset);
    }

    [Theory]
    [InlineData("yyyy-mm-dd", DrawingImportCellKind.Date, false)]
    [InlineData("yyyy-mm-dd hh:mm", DrawingImportCellKind.DateTime, false)]
    [InlineData("00000", DrawingImportCellKind.Number, false)]
    [InlineData("mm", DrawingImportCellKind.Number, true)]
    [InlineData("[>=1]yyyy-mm-dd;0", DrawingImportCellKind.Number, true)]
    public void Custom_formats_only_convert_unambiguous_declared_dates(string code, DrawingImportCellKind expected, bool warning)
    {
        var styles = $"<styleSheet xmlns=\"{Main}\"><numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"{Escape(code)}\"/></numFmts><cellXfs count=\"2\"><xf numFmtId=\"0\"/><xf numFmtId=\"164\"/></cellXfs></styleSheet>";
        var parsed = Xlsx(Book(ValueRows(Number("A2", "61", 1)), styles: styles));
        Assert.True(parsed.Success, Explain(parsed));
        var cell = Assert.Single(parsed.Records).Cells[0];
        Assert.Equal(expected, cell.Kind);
        Assert.Equal("61", cell.Raw);
        Assert.Null(cell.Text); // A zero-padding display format does not restore an unknown drawing number.
        Assert.Equal(warning, parsed.Diagnostics.Any(value => value.Code == "unknown_number_format"));
    }

    [Theory]
    [InlineData("<row r=\"2\"><c r=\"A3\" t=\"inlineStr\"><is><t>x</t></is></c></row>", "invalid_cell_position")]
    [InlineData("<row r=\"2\"><c r=\"A2\"><v>1</v></c><c r=\"A2\"><v>2</v></c></row>", "invalid_cell_position")]
    [InlineData("<row r=\"2\"><c r=\"B2\"><v>1</v></c><c r=\"A2\"><v>2</v></c></row>", "invalid_cell_position")]
    [InlineData("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>", "invalid_row")]
    public void Inconsistent_source_positions_are_refused(string dataRow, string code)
        => Failure(Xlsx(Book(Row(1, Inline("A1", "title")) + dataRow)), code);

    [Fact]
    public void Missing_coordinates_are_marked_as_inferred_source_order_only()
    {
        var parsed = Xlsx(Book("<row><c t=\"inlineStr\"><is><t>title</t></is></c></row><row><c t=\"inlineStr\"><is><t>drawing</t></is></c></row>"));
        Assert.True(parsed.Success, Explain(parsed));
        var row = Assert.Single(parsed.Records);
        Assert.Equal(2, row.SourceRow);
        Assert.True(row.SourceRowInferred);
        Assert.True(row.Cells[0].CoordinateInferred);
        Assert.Contains(parsed.Diagnostics, value => value.Code == "row_position_inferred");
    }

    [Theory]
    [InlineData("../outside.xml")]
    [InlineData("/absolute.xml")]
    [InlineData("xl/../../outside.xml")]
    [InlineData("xl\\outside.xml")]
    [InlineData("xl/%2e%2e/outside.xml")]
    public void Unsafe_zip_names_are_never_extracted_or_accepted(string name)
        => Failure(Xlsx(Book(FortyRows(), mutate: parts => parts.Add(name, "<x/>"))), "unsafe_part");

    [Fact]
    public void Case_collisions_unsafe_relationships_external_links_macros_and_arbitrary_zips_are_refused()
    {
        Failure(Xlsx(Book(FortyRows(), mutate: parts => parts.Add("XL/WORKBOOK.XML", parts["xl/workbook.xml"]))), "duplicate_part");
        Failure(Xlsx(Book(FortyRows(), mutate: parts => parts["xl/_rels/workbook.xml.rels"] = Relationships(Relationship("sheet1", Rel + "/worksheet", "../../outside.xml")))), "unsafe_relationship");
        Failure(Xlsx(Book(FortyRows(), mutate: parts => parts["xl/_rels/workbook.xml.rels"] = Relationships(Relationship("sheet1", Rel + "/worksheet", "https://example.invalid/secret", "External")))), "external_relationship");
        Failure(Xlsx(Book(FortyRows(), mutate: parts => parts.Add("xl/vbaProject.bin", "macro"))), "macro_or_binary_workbook");
        Failure(Xlsx(Archive(new Dictionary<string, string> { ["ordinary.txt"] = "not a workbook" })), "missing_part");
        Failure(Xlsx(Csv("not a zip")), "invalid_xlsx");
    }

    [Fact]
    public void Ambiguous_sheet_relationships_and_unsupported_package_types_are_diagnosed()
    {
        Failure(Xlsx(Book(FortyRows(), mutate: parts => parts["xl/workbook.xml"] = parts["xl/workbook.xml"]
            .Replace("xmlns:r=", "xmlns:s=\"http://purl.oclc.org/ooxml/officeDocument/relationships\" xmlns:r=", StringComparison.Ordinal)
            .Replace("r:id=\"sheet1\"", "r:id=\"sheet1\" s:id=\"sheet1\"", StringComparison.Ordinal))), "invalid_sheet");
        Failure(Xlsx(Book(FortyRows(), mutate: parts => parts["[Content_Types].xml"] = parts["[Content_Types].xml"]
            .Replace("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", "application/octet-stream", StringComparison.Ordinal))), "unsupported_workbook_type");
    }

    [Fact]
    public void Dtd_malformed_xml_and_interpreted_parts_with_unscanned_extensions_are_refused()
    {
        Failure(Xlsx(Book(FortyRows(), mutate: parts => parts["xl/worksheets/sheet1.xml"] = "<!DOCTYPE worksheet [<!ENTITY x SYSTEM 'file:///never-read'>]><worksheet xmlns='" + Main + "'><sheetData>&x;</sheetData></worksheet>")), "invalid_xml");
        Failure(Xlsx(Book(FortyRows(), mutate: parts => parts["xl/worksheets/sheet1.xml"] = "<broken>")), "invalid_xml");
        Failure(Xlsx(Book(FortyRows(), mutate: parts => {
            parts["xl/worksheets/sheet1.data"] = parts["xl/worksheets/sheet1.xml"];
            parts.Remove("xl/worksheets/sheet1.xml");
            parts["xl/_rels/workbook.xml.rels"] = parts["xl/_rels/workbook.xml.rels"].Replace("sheet1.xml", "sheet1.data", StringComparison.Ordinal);
        })), "unsupported_xml_part_name");
    }

    [Fact]
    public void Compressed_expanded_entry_and_xml_budgets_are_independent()
    {
        Failure(Xlsx(new byte[2 * 1024 * 1024 + 1]), "file_limit");
        var expanded = Book(FortyRows(), mutate: parts => parts.Add("large.txt", new string('x', 16 * 1024 * 1024 + 1)));
        Assert.True(expanded.Length < 2 * 1024 * 1024); // Highly compressible archive still exceeds expanded budget.
        Failure(Xlsx(expanded), "expanded_limit");
        var boundary = Book(FortyRows(), mutate: parts => { while (parts.Count < 128) parts.Add("extra" + parts.Count + ".txt", ""); });
        Assert.True(Xlsx(boundary).Success);
        var over = Book(FortyRows(), mutate: parts => { while (parts.Count < 129) parts.Add("extra" + parts.Count + ".txt", ""); });
        Failure(Xlsx(over), "entry_limit");
        Failure(Xlsx(Book(FortyRows()), limits: new() { XmlNodes = 10 }), "xml_node_limit");
    }

    [Fact]
    public void Shared_strings_styles_cell_text_and_diagnostics_are_bounded()
    {
        var shared = $"<sst xmlns=\"{Main}\"><si><t>one</t></si><si><t>two</t></si></sst>";
        Failure(Xlsx(Book(FortyRows(), shared: shared), limits: new() { SharedStrings = 1 }), "shared_string_limit");
        Failure(Xlsx(Book(FortyRows(), shared: shared), limits: new() { SharedStringCharacters = 5 }), "shared_string_limit");
        Failure(Xlsx(Book(FortyRows(), styles: Style(0, 14)), limits: new() { Styles = 1 }), "style_limit");
        Failure(Xlsx(Book(ValueRows(Inline("A2", new string('x', 16_001))))), "cell_limit");
        var formulas = Row(1, Inline("A1", "title")) + string.Concat(Enumerable.Range(2, 3).Select(index => Row(index, $"<c r=\"A{index}\"><f>1+1</f><v>2</v></c>")));
        var limited = Xlsx(Book(formulas), limits: new() { Diagnostics = 2 });
        Failure(limited, "diagnostic_limit");
        Assert.False(limited.Complete);
        Assert.InRange(limited.Diagnostics.Count, 1, 3); // Budget plus one fatal summary, never unbounded error allocation.
    }

    [Fact]
    public void Selected_sheet_merges_are_explicitly_unsupported()
    {
        var bytes = Book(FortyRows(), mutate: parts => parts["xl/worksheets/sheet1.xml"] = parts["xl/worksheets/sheet1.xml"].Replace("</worksheet>", "<mergeCells count=\"1\"><mergeCell ref=\"D1:E1\"/></mergeCells></worksheet>", StringComparison.Ordinal));
        Failure(Xlsx(bytes), "merged_cells"); // Includes merges outside the drawing table: intentional format-reader limitation.
    }

    static DrawingImportFormatResult Xlsx(byte[] bytes, string? worksheet = null, DrawingImportFormatLimits? limits = null)
        => DrawingImportFormat.Parse(new(DrawingImportFormatKind.Xlsx, bytes, worksheet), limits);
    static void Failure(DrawingImportFormatResult parsed, string code)
    {
        Assert.False(parsed.Success);
        Assert.Contains(parsed.Diagnostics, value => value.Code == code && value.Severity == DrawingImportDiagnosticSeverity.Error);
    }
    static string Explain(DrawingImportFormatResult parsed) => string.Join("; ", parsed.Diagnostics.Select(value => value.Code + ": " + value.Message));
    static byte[] Csv(string value) => new UTF8Encoding(false).GetBytes(value);
    static string Escape(string value) => SecurityElement.Escape(value)!;
    static string Inline(string coordinate, string value) => $"<c r=\"{coordinate}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(value)}</t></is></c>";
    static string Number(string coordinate, string value, int style = 0) => $"<c r=\"{coordinate}\" s=\"{style}\"><v>{Escape(value)}</v></c>";
    static string Row(int number, params string[] cells) => $"<row r=\"{number}\">{string.Concat(cells)}</row>";
    static string ValueRows(string cell) => Row(1, Inline("A1", "value")) + Row(2, cell);
    static string FortyCsv(int? blankTitle = null) => "title,client drawing number\n" + string.Join('\n', Enumerable.Range(1, 40).Select(index => $"{(index == blankTitle ? "" : $"Drawing{index:00}")},C-{index:000}"));
    static string FortyRows(int? blankTitle = null) => Row(1, Inline("A1", "title"), Inline("B1", "client drawing number"))
        + string.Concat(Enumerable.Range(1, 40).Select(index => Row(index + 1, Inline("A" + (index + 1), index == blankTitle ? "" : $"Drawing{index:00}"), Inline("B" + (index + 1), $"C-{index:000}"))));
    static string Style(params int[] ids) => $"<styleSheet xmlns=\"{Main}\"><cellXfs count=\"{ids.Length}\">{string.Concat(ids.Select(id => $"<xf numFmtId=\"{id}\"/>"))}</cellXfs></styleSheet>";
    static string Relationship(string id, string type, string target, string? mode = null)
        => $"<Relationship Id=\"{id}\" Type=\"{type}\" Target=\"{Escape(target)}\"{(mode is null ? "" : $" TargetMode=\"{mode}\"")}/>";
    static string Relationships(params string[] values) => $"<Relationships xmlns=\"{PackageRel}\">{string.Concat(values)}</Relationships>";

    static byte[] TextBook(string sourceKind, string encoded)
    {
        var cell = sourceKind switch {
            "inline" => Inline("A2", encoded),
            "shared" => "<c r=\"A2\" t=\"s\"><v>0</v></c>",
            "str" => "<c r=\"A2\" t=\"str\"><v>" + Escape(encoded) + "</v></c>",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceKind))
        };
        var shared = sourceKind == "shared" ? $"<sst xmlns=\"{Main}\"><si><t>{Escape(encoded)}</t></si></sst>" : null;
        return Book(ValueRows(cell), shared: shared);
    }

    static byte[] Book(string rows, string? styles = null, string? shared = null, bool date1904 = false,
        string? secondRows = null, string firstState = "visible", Action<Dictionary<string, string>>? mutate = null)
    {
        var extraTypes = (styles is null ? "" : "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>")
            + (shared is null ? "" : "<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>")
            + (secondRows is null ? "" : "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        var parts = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["[Content_Types].xml"] = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" + extraTypes + "</Types>",
            ["_rels/.rels"] = Relationships(Relationship("office", Rel + "/officeDocument", "xl/workbook.xml")),
            ["xl/workbook.xml"] = $"<workbook xmlns=\"{Main}\" xmlns:r=\"{Rel}\"><workbookPr date1904=\"{(date1904 ? "1" : "0")}\"/><sheets><sheet name=\"Drawings\" sheetId=\"1\" r:id=\"sheet1\" state=\"{firstState}\"/>{(secondRows is null ? "" : "<sheet name=\"Other\" sheetId=\"2\" r:id=\"sheet2\"/>")}</sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] = Relationships(Relationship("sheet1", Rel + "/worksheet", "worksheets/sheet1.xml")
                + (secondRows is null ? "" : Relationship("sheet2", Rel + "/worksheet", "worksheets/sheet2.xml"))
                + (styles is null ? "" : Relationship("styles", Rel + "/styles", "styles.xml"))
                + (shared is null ? "" : Relationship("strings", Rel + "/sharedStrings", "sharedStrings.xml"))),
            ["xl/worksheets/sheet1.xml"] = $"<worksheet xmlns=\"{Main}\"><sheetData>{rows}</sheetData></worksheet>"
        };
        if (styles is not null) parts.Add("xl/styles.xml", styles);
        if (shared is not null) parts.Add("xl/sharedStrings.xml", shared);
        if (secondRows is not null) parts.Add("xl/worksheets/sheet2.xml", $"<worksheet xmlns=\"{Main}\"><sheetData>{secondRows}</sheetData></worksheet>");
        mutate?.Invoke(parts);
        return Archive(parts);
    }

    static byte[] Archive(Dictionary<string, string> parts)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        foreach (var (name, body) in parts)
        {
            using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(body);
        }
        return buffer.ToArray();
    }
}
