"""Run from the repository root; measure the spike against the reviewed foundations."""
import json
import subprocess
from pathlib import Path

BASE = 'e80fbe1e0358444e77d186ad1aba5f85045571ff'
def nonblank(source):
    return sum(bool(line.strip()) for line in source.splitlines())
def section(source, start, end):
    return source[source.index(start):source.index(end, source.index(start))]
def diff_lines(path):
    diff = subprocess.check_output(['git', 'diff', BASE, '--', path], text=True)
    return [line for line in diff.splitlines() if line[:1] in ['+', '-'] and not line.startswith(('+++', '---')) and line[1:].strip()]
old = subprocess.check_output(['git', 'show', BASE + ':web/src/pages/projects/Registers.tsx'], text=True)
new = Path('web/src/pages/projects/Registers.tsx').read_text()
risk_deleted = nonblank(section(old, 'export function RisksTab', 'export function IssuesTab')) + nonblank(section(old, 'export function RiskPanel', 'export function IssuePanel'))
risk_new = nonblank(section(new, 'export function RisksRecordList', 'export function IssuesTab')) + nonblank(section(new, 'const riskPanelSlots', 'export function IssuePanel'))
risk_new += sum(line.startswith('+') for line in diff_lines('src/Hub.Api/Records/Risk.cs'))
paths = ['src/Hub.Api/Features/RecordEndpoints.cs', 'src/Hub.Api/Records/OpenRole.cs', 'src/Hub.Api/Text.cs', 'src/Hub.Domain/Flags.cs', 'web/src/pages/projects/Allocations.tsx', 'web/src/components/hub/export.tsx', 'web/src/components/hub/records.tsx', 'web/src/components/hub/record-panel.tsx', 'web/src/app/registrations.tsx', 'web/src/components/hub/panel-host.tsx']
roles = []
for path in paths:
    row = {'path': path}
    if path in ['src/Hub.Api/Features/RecordEndpoints.cs', 'web/src/components/hub/records.tsx']:
        row.update(touchedNonblank=nonblank(Path(path).read_text()), method='whole shared file conservatively charged')
    elif path.endswith('/record-panel.tsx'):
        selected = [(i + 1, line) for i, line in enumerate(Path(path).read_text().splitlines()) if any(key in line for key in ['itemKeys,', 'const root =', 'const query =', 'const data =', 'const row =', "'risk' in data", 'const activeTab =', 'OpenRoleRecordPanel'])]
        row.update(touchedNonblank=len(selected), method='Risk panel engine excluded; cache/read-only/wrapper lines charged', lines=[line for line, _ in selected])
    else:
        row.update(touchedNonblank=len(diff_lines(path)), method='all nonblank added and deleted lines')
    roles.append(row)
result = {'base': BASE, 'risk': {'deletedNonblank': risk_deleted, 'newDefinitionsSlotsAndGlueNonblank': risk_new, 'panelSlots': ['Realise', 'Close', 'Links'], 'specificStylesheets': []}, 'openRoles': {'files': len(roles), 'touchedNonblank': sum(row['touchedNonblank'] for row in roles), 'detail': roles}, 'boundary': 'S05/S06 source attribution; shared S02 definitions/contracts and S04 Risk-panel engine excluded, tests and generated outputs excluded. Entire RecordList and RecordEndpoints files are conservatively charged.'}
assert risk_new < risk_deleted
assert len(roles) <= 10 and result['openRoles']['touchedNonblank'] <= 300
output = Path('specs/043-record-engine/evidence/spike-measurement.json')
output.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({'risk': result['risk'], 'openRoleFiles': len(roles), 'openRoleTouchedNonblank': result['openRoles']['touchedNonblank']}, indent=2))
