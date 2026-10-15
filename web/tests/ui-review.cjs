// Adversarial regressions for the real renderers and layout used by the screenclip fixes; no browser or database writes.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const React = require('react')
const { renderToStaticMarkup } = require('react-dom/server')

;(async () => {
  const { createServer } = await import('vite')
  const root = path.resolve(__dirname, '..')
  // Keep this test's dependency cache separate from a running dev server.
  const cacheDir = fs.mkdtempSync(path.join(require('node:os').tmpdir(), 'pm-ui-review-'))
  const server = await createServer({ root, cacheDir, configFile: false, resolve: { alias: { '@': path.join(root, 'src') } }, server: { middlewareMode: true, hmr: false, ws: false }, appType: 'custom' })
  try {
    const { ChangeList, ActivityTable } = await server.ssrLoadModule('/src/components/hub/activity.tsx')
    const { showPhoneTaskField } = await server.ssrLoadModule('/src/pages/projects/phone-task-fields.ts')
    const { milestoneLabelLayout } = await server.ssrLoadModule('/src/pages/gantt-labels.ts')
    const { plural, tv } = await server.ssrLoadModule('/src/lib/i18n.ts')
    const guid = '00000000-0000-4000-8000-000000000001'
    const stamp = '2026-10-12T10:43:59.1234567+00:00'
    const draw = row => renderToStaticMarkup(React.createElement(ChangeList, { row }))

    const changes = draw({ action: 'Updated', changes: [
      { field: 'SourceRevisionId', old: guid, new: 'source', oldLabel: guid, newLabel: 'SYN-SOURCE-01 · R2' },
      ...Array.from({ length: 9 }, (_, i) => ({ field: 'Note' + i, new: 'Kept ' + i })),
      { field: 'StartedAt', newLabel: stamp }, { field: 'Flag', new: false }, { field: 'Hours', new: 0 },
      { field: 'Metadata', new: { note: 'Preserved object' } },
    ] })
    assert.ok(changes.includes('SYN-SOURCE-01 · R2') && changes.includes(guid), 'unresolved old reference must not hide its readable successor')
    assert.ok(changes.includes('Unresolved reference') && changes.includes('<details'), 'raw fallback must remain inspectable')
    assert.ok(changes.includes('Kept 8'), 'changes after the eighth must survive')
    assert.ok(changes.includes(stamp) && changes.includes('<time'), 'exact timestamp must survive formatting')
    assert.match(changes, /Flag:[\s\S]*?false/, 'false must not be removed')
    assert.match(changes, /Hours:[\s\S]*?→\s*0/, 'zero must not be removed')
    assert.ok(changes.includes('Preserved object') && !changes.includes('[object Object]'), 'object fallback preserves its contents')
    assert.ok(!changes.includes('field.SourceRevisionId'), 'unknown field label has a readable fallback')
    const roles = draw({ action: 'Updated', changes: [{ field: 'Role', old: 'ReadOnly', new: 'ProjectManager' }] })
    assert.ok(roles.includes('Read Only') && roles.includes('Project Manager'), 'audit roles use readable enum labels')
    const roleRow = { id: 'role', occurredAt: stamp, itemType: 'User', itemId: guid, itemName: 'ProjectManager', action: 'Created', changes: [{ field: 'Role', new: 'ProjectManager' }] }
    for (const onOpen of [undefined, () => {}]) {
      const table = renderToStaticMarkup(React.createElement(ActivityTable, { rows: [roleRow], total: 1, page: 1, pageSize: 50, onPage: () => {}, onOpen }))
      assert.ok(table.includes('Project Manager') && !table.includes('ProjectManager'), 'role item headings use the same readable label')
    }
    const namedPerson = renderToStaticMarkup(React.createElement(ActivityTable, { rows: [{ ...roleRow, changes: [{ field: 'Name', new: 'ProjectManager' }] }], total: 1, page: 1, pageSize: 50, onPage: () => {} }))
    assert.ok(namedPerson.includes('ProjectManager'), 'ordinary record names remain verbatim')

    const deleted = draw({ action: 'Deleted', snapshot: { PredecessorTaskId: guid, UnknownId: guid, DependencyType: 'StartToStart', Count: 0, Flag: false },
      snapshotLabels: { PredecessorTaskId: 'SYN-101-T001 Survey' }, changes: [] })
    assert.ok(deleted.includes('SYN-101-T001 Survey') && deleted.includes(guid), 'resolved and unresolved snapshot references survive')
    assert.ok(deleted.includes('Start-to-Start') && deleted.includes('false') && deleted.includes('Count:'), 'snapshot values keep their meaning')

    const task = { requiresReview: false, state: null, reviewRound: 0, dueDateChangeCount: 0, estimatedHours: null }
    assert.equal(showPhoneTaskField('estimate', task), true, 'unknown estimate remains visible')
    assert.equal(showPhoneTaskField('due', task), true, 'missing due date remains visible')
    assert.equal(showPhoneTaskField('assignee', task), true, 'unassigned work remains visible')
    assert.equal(showPhoneTaskField('reviewer', task), false)
    assert.equal(showPhoneTaskField('reviewer', { ...task, requiresReview: true }), true, 'missing required reviewer remains visible')

    for (const anchors of [[0, 30, 60], [0, 30, 60, 90, 120], [20, 20, 20, 22], [185, 190, 195]]) {
      const layout = milestoneLabelLayout(anchors, 200).filter(label => label.maxWidth > 0)
      for (const label of layout) assert.ok(label.left >= 0 && label.left + label.maxWidth <= 200 && label.top >= 30, 'labels stay inside the date region and below the marker buttons')
      for (const top of [31, 53]) {
        const lane = layout.filter(label => label.top === top)
        for (let i = 1; i < lane.length; i++) assert.ok(lane[i - 1].left + lane[i - 1].maxWidth <= lane[i].left, 'same-row milestone labels cannot overlap')
      }
    }
    assert.equal(plural(1, 'coord.countOne', 'coord.count'), '1 record in this view')
    assert.equal(plural(2, 'coord.countOne', 'coord.count'), '2 records in this view')
    assert.equal(tv('StartToStart'), 'Start-to-Start')
    assert.equal(plural(1, 'timeline.overdueByOne', 'timeline.overdueBy', { n: 1 }), 'Overdue 1 day')
    assert.equal(plural(2, 'timeline.overdueByOne', 'timeline.overdueBy', { n: 2 }), 'Overdue 2 days')
    process.stdout.write('PASS: audit preservation, phone unknowns/optional fields, dense milestone labels, count and dependency labels\n')
  } finally { await server.close(); fs.rmSync(cacheDir, { recursive: true, force: true }) }
})().catch(error => { console.error(error); process.exitCode = 1 })
