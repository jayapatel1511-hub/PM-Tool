"""Synthetic local QA only: ordinary drawing tables, never client source files."""
from pathlib import Path
import csv
import io
import json
import random
import string
import zipfile
from xml.sax.saxutils import escape

HERE = Path(__file__).parent
MAIN = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
REL = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
PACKAGE = 'http://schemas.openxmlformats.org/package/2006/relationships'

def rows(kind, bad=False):
    return [['title', 'client drawing number']] + [
        ['' if bad and number == 23 else f'Synthetic QA24 {kind} drawing {number:02}',
         'C12' if number == 1 else f'C-{number:03}'] for number in range(1, 41)]

def workbook(data, large=False):
    sheet = f'<worksheet xmlns="{MAIN}"><sheetData>'
    for index, cells in enumerate(data, 1):
        sheet += f'<row r="{index}">' + ''.join(
            f'<c r="{chr(65+column)}{index}" t="inlineStr"><is><t xml:space="preserve">{escape(value)}</t></is></c>'
            for column, value in enumerate(cells)) + '</row>'
    sheet += '</sheetData></worksheet>'
    parts = {
        '[Content_Types].xml': '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' + ('<Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>' if large else '') + '</Types>',
        '_rels/.rels': f'<Relationships xmlns="{PACKAGE}"><Relationship Id="book" Type="{REL}/officeDocument" Target="xl/workbook.xml"/></Relationships>',
        'xl/workbook.xml': f'<workbook xmlns="{MAIN}" xmlns:r="{REL}"><sheets><sheet name="Drawings" sheetId="1" r:id="sheet"/></sheets></workbook>',
        'xl/_rels/workbook.xml.rels': f'<Relationships xmlns="{PACKAGE}"><Relationship Id="sheet" Type="{REL}/worksheet" Target="worksheets/sheet1.xml"/>' + (f'<Relationship Id="strings" Type="{REL}/sharedStrings" Target="sharedStrings.xml"/>' if large else '') + '</Relationships>',
        'xl/worksheets/sheet1.xml': sheet,
    }
    if large:
        rng = random.Random(24052)
        values = [''.join(rng.choices(string.ascii_letters + string.digits, k=1400)) for _ in range(900)]
        parts['xl/sharedStrings.xml'] = f'<sst xmlns="{MAIN}" count="0" uniqueCount="900">' + ''.join(f'<si><t>{value}</t></si>' for value in values) + '</sst>'
    out = io.BytesIO()
    with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as archive:
        for name, text in parts.items():
            archive.writestr(name, text)
    return out.getvalue()

for bad in [False, True]:
    buffer = io.StringIO(newline='')
    csv.writer(buffer).writerows(rows('CSV', bad))
    (HERE / ('real-drawings-bad.csv' if bad else 'real-drawings.csv')).write_bytes(b'\xef\xbb\xbf' + buffer.getvalue().encode())
    (HERE / ('real-drawings-bad.xlsx' if bad else 'real-drawings.xlsx')).write_bytes(workbook(rows('XLSX', bad)))
large = workbook(rows('LARGE'), True)
assert len(large) <= 2 * 1024 * 1024
assert len(large) * 4 // 3 > 1024 * 1024
(HERE / 'real-drawings-large.xlsx').write_bytes(large)
print(json.dumps({'status': 'SYNTHETIC_FIXTURES_READY', 'rowsPerFile': 40, 'largeXlsxBytes': len(large), 'largeBase64Bytes': 4 * ((len(large) + 2) // 3)}))
