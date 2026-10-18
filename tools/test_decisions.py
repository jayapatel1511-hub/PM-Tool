"""Checks specs/DECISIONS.md against FR-DLV-26 and AC-DLV-21.

Run: python3 tools/test_decisions.py
"""
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
LOG = ROOT / "specs/DECISIONS.md"
COLUMNS = ["ID", "Packet", "Question", "Default", "Flag", "Status", "Last change", "Source"]
ID_FORMS = re.compile(r"^(RPT-\d{2}|\d{3}-D\d+|\d{3}-DEF\d+)$")
STATUSES = {"default-active", "confirmed", "overruled", "deferred"}
DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
# The source lists FR-DLV-26 names: label -> (file, how its row IDs are found).
SOURCES = {
    "036 §14": ("specs/036-staffing-and-signal-trust/verification.md", "## 14."),
    "037 §8": ("specs/037-calm-workspace/verification.md", "## 8."),
    "RC handoff": ("docs/handoffs/2026-10-12-packets-036-037-release-candidate.md", "Earlier rows that change behaviour"),
}
SHARED = "036 §14 and 037 §8"
FIRST_DLV21_PACKET = 39  # packets 039 onwards are written under FR-DLV-21


def parse_log(text):
    """Return the table rows as dicts; raise ValueError on a malformed table."""
    lines = [l for l in text.splitlines() if l.startswith("|")]
    header = [c.strip() for c in lines[0].strip("|").split("|")]
    if header != COLUMNS:
        raise ValueError(f"columns {header} != {COLUMNS}")
    rows = []
    for line in lines[2:]:
        cells = [c.strip() for c in line.strip().strip("|").split(" | ")]
        if len(cells) != len(COLUMNS):
            raise ValueError(f"{len(cells)} cells: {line[:60]}")
        rows.append(dict(zip(COLUMNS, cells)))
    return rows


def expand(ids):
    """'A1–A3, B2' -> ['A1', 'A2', 'A3', 'B2']."""
    out = []
    for part in ids.split(","):
        part = part.strip()
        m = re.fullmatch(r"([A-Z]+)(\d+)[–-]\1?(\d+)", part)
        out += [f"{m[1]}{n}" for n in range(int(m[2]), int(m[3]) + 1)] if m else [part]
    return out


def source_refs(rows):
    """(label, source row ID) -> list of log IDs that name it."""
    refs = {}
    for row in rows:
        for segment in row["Source"].split("; "):
            label, _, ids = segment.partition(": ")
            labels = list(SOURCES)[:2] if label == SHARED else [label]
            if not ids or not all(l in SOURCES for l in labels):
                continue
            for sid in expand(ids):
                for l in labels:
                    refs.setdefault((l, sid), []).append(row["ID"])
    return refs


def source_rows(label, root=ROOT):
    path, marker = SOURCES[label]
    text = (root / path).read_text()
    if label == "RC handoff":
        block = text.split(marker, 1)[1].split("\n", 1)[1].split("\n\n", 1)[0]
        return [re.match(r"- ([A-Z0-9-]+?)(?:,|'s)", l)[1] for l in block.splitlines()]
    section = text.split("\n" + marker, 1)[1].split("\n## ", 1)[0]
    return re.findall(r"^\| ([A-Z]+\d+) \|", section, re.M)


def packet_decisions(spec_text, packet):
    """Decision IDs a packet spec.md lists: '| D3 |' rows, '- D3:' bullets, '048-D1 to 048-D10'."""
    section = re.split(r"\n## [^\n]*[Dd]ecision[^\n]*\n", spec_text, 1)[1].split("\n## ", 1)[0]
    ids = {f"{packet}-D{n}" for n in re.findall(r"^(?:\| D(\d+) \||- D(\d+):)", section, re.M) for n in n if n}
    for a, b in re.findall(rf"{packet}-D(\d+) to {packet}-D(\d+)", section):
        ids |= {f"{packet}-D{n}" for n in range(int(a), int(b) + 1)}
    return ids


class DecisionLogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rows = parse_log(LOG.read_text())

    def test_ids_unique_and_in_allowed_forms(self):
        ids = [r["ID"] for r in self.rows]
        self.assertEqual(sorted(i for i in set(ids) if ids.count(i) > 1), [])
        self.assertEqual([i for i in ids if not ID_FORMS.match(i)], [])

    def test_status_and_last_change(self):
        self.assertEqual([r["ID"] for r in self.rows if r["Status"] not in STATUSES], [])
        self.assertEqual([r["ID"] for r in self.rows if not DATE.match(r["Last change"])], [])
        self.assertEqual([r["ID"] for r in self.rows if not r["Flag"] or not r["Source"]], [])

    def test_every_source_row_appears_once(self):
        refs = source_refs(self.rows)
        for label in SOURCES:
            expected = source_rows(label)
            self.assertTrue(expected, label)
            for sid in expected:
                with self.subTest(source=label, row=sid):
                    self.assertEqual(len(refs.get((label, sid), [])), 1, refs.get((label, sid)))
        stray = [k for k in refs if k[1] not in source_rows(k[0])]
        self.assertEqual(stray, [], "log names source rows that do not exist")

    def test_every_packet_decision_has_a_row(self):
        ids = {r["ID"] for r in self.rows}
        for spec in sorted(ROOT.glob("specs/[0-9][0-9][0-9]-*/spec.md")):
            packet = spec.parent.name[:3]
            if int(packet) < FIRST_DLV21_PACKET:
                continue
            found = packet_decisions(spec.read_text(), packet)
            with self.subTest(packet=packet):
                self.assertTrue(found, "no decisions found")
                self.assertEqual(sorted(found - ids), [])


class ValidatorTests(unittest.TestCase):
    HEAD = "| " + " | ".join(COLUMNS) + " |\n|" + "---|" * len(COLUMNS) + "\n"

    def test_parse_refuses_wrong_columns_and_cell_count(self):
        with self.assertRaises(ValueError):
            parse_log("| ID | Packet | Question | Default | Status |\n|---|---|---|---|---|\n")
        with self.assertRaises(ValueError):
            parse_log(self.HEAD + "| 039-D1 | 039 | Q | D | none | confirmed |\n")

    def test_id_forms(self):
        good = ["RPT-01", "039-D1", "039-D16", "040-DEF1"]
        bad = ["RPT-1", "39-D1", "039-d1", "039-DEF", "D1", "039-X1"]
        self.assertEqual([i for i in good if not ID_FORMS.match(i)], [])
        self.assertEqual([i for i in bad if ID_FORMS.match(i)], [])

    def test_source_refs_expand_ranges_and_shared_label(self):
        rows = parse_log(self.HEAD + "| 036-D27 | 036 | Q | D | none | default-active | 2026-10-18 | 036 §14 and 037 §8: A1–A3, B2; RC handoff: 036-D2; other: x |\n")
        refs = source_refs(rows)
        self.assertEqual(sorted(refs), sorted([(l, s) for l in ("036 §14", "037 §8") for s in ("A1", "A2", "A3", "B2")] + [("RC handoff", "036-D2")]))

    def test_packet_decisions_reads_table_bullets_and_ranges(self):
        spec = "## Scope\n| D9 | no |\n## Decisions\n| D1 | a |\n| D2 | b |\n- D3: c\n048-D4 to 048-D6 in the log.\n## Next\n| D7 | x |\n"
        self.assertEqual(packet_decisions(spec, "048"), {f"048-D{n}" for n in range(1, 7)})


if __name__ == "__main__":
    unittest.main()
