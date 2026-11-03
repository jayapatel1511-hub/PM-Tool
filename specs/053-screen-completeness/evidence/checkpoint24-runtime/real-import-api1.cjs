// Actual Kestrel + owned QA24 PostgreSQL. Synthetic sources only; progress receipts retain keys, never file bytes.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const { execFileSync } = require('node:child_process')
const m = JSON.parse(fs.readFileSync(path.join(__dirname, 'real-qa24-setup1.json'), 'utf8'))
assert.equal(m.database, 'hub_qa_checkpoint24'); assert.equal(m.status, 'PASS_NORMAL_COMMAND_SETUP')
const progressFile = path.join(__dirname, 'real-import-api1-progress.json')
assert.ok(!fs.existsSync(progressFile), 'Existing QA24 import progress must be recovered with its saved exact key/body; never restart this producer with new keys')
const result = { database: m.database, calls: [], checks: [], batches: [] }
const root = 'projects/' + m.projectId + '/deliverables'
const sql = query => execFileSync('docker', ['exec', 'codex-pm-052-053-tests', 'psql', '-U', 'hub', '-d', m.database, '-At', '-v', 'ON_ERROR_STOP=1', '-c', query], { encoding: 'utf8' }).trim()
const save = () => fs.writeFileSync(progressFile, JSON.stringify(result, null, 2) + '\n')
async function api(method, route, body, expected = [200], key) {
  const wire = body ? JSON.stringify(body) : undefined
  const response = await fetch(m.apiBase + '/api/v1/' + route, { method, headers: { 'X-Dev-User': 'priya@hub.test',
    ...(body ? { 'Content-Type': 'application/json' } : {}), ...(key ? { 'Idempotency-Key': key } : {}) }, ...(wire ? { body: wire } : {}) })
  const text = await response.text(); let data = null; try { data = text ? JSON.parse(text) : null } catch { data = text }
  result.calls.push({ method, route, status: response.status, bodyBytes: wire ? Buffer.byteLength(wire) : 0, key: key ?? null }); save()
  assert.ok(expected.includes(response.status), method + ' ' + route + ': ' + response.status + ' ' + text)
  return data
}
function snapshot() {
  // All hub table contents, including counters, audit, membership, notifications and receipts. No temp table or write.
  return JSON.parse(sql(`SELECT jsonb_object_agg(tablename, md5(query_to_xml(format('SELECT * FROM hub.%I AS t ORDER BY to_jsonb(t)::text',tablename),true,false,'')::text) ORDER BY tablename)::text FROM pg_tables WHERE schemaname='hub'`))
}
function body(file, format) {
  return { format, contentBase64: fs.readFileSync(path.join(__dirname, file)).toString('base64'),
    defaults: { projectDisciplineId: m.disciplineId, deliverableTypeId: m.typeId }, sourceReference: 'Synthetic QA24 normal intake' }
}
;(async () => {
  const sentinel = sql(`SELECT to_jsonb(d)::text FROM hub.deliverable d WHERE id='${m.sentinel.id}'`)
  for (const format of ['csv', 'xlsx']) {
    const before = snapshot(), bad = body('real-drawings-bad.' + format, format)
    const failedPreview = await api('POST', root + '/import-preview', bad, [200], 'qa24-preview-' + crypto.randomUUID())
    assert.equal(failedPreview.creates, 39); assert.equal(failedPreview.errors, 1); assert.equal(failedPreview.canCommit, false)
    assert.ok(failedPreview.rows.some(row => row.sourceRow === 24 && row.errors.length > 0))
    assert.deepEqual(snapshot(), before, 'Preview with an Idempotency-Key changes no hub table')
    await api('POST', root + '/import', { ...bad, projectRowVersion: failedPreview.projectRowVersion, previewFingerprint: 'intentionally-invalid-preview' }, [400], crypto.randomUUID())
    assert.deepEqual(snapshot(), before, 'One bad title refuses the entire direct commit with all hub tables unchanged')
    const input = body('real-drawings.' + format, format)
    const preview = await api('POST', root + '/import-preview', input)
    assert.equal(preview.creates, 40); assert.equal(preview.errors, 0); assert.equal(preview.canCommit, true)
    assert.ok(preview.rows.some(row => row.warnings.some(warning => warning.code === 'numberPattern')), 'C12 remains a nonfatal numbering warning')
    assert.deepEqual(snapshot(), before)
    const key = crypto.randomUUID(), request = { ...input, projectRowVersion: preview.projectRowVersion, previewFingerprint: preview.previewFingerprint }
    const batch = { format, key, file: 'real-drawings.' + format, projectRowVersion: preview.projectRowVersion, previewFingerprint: preview.previewFingerprint }; result.batches.push(batch); save()
    const made = await api('POST', root + '/import', request, [200], key)
    batch.created = made; save(); assert.equal(made.total, 40); assert.equal(new Set(made.rows.map(row => row.id)).size, 40)
    const ids = made.rows.map(row => "'" + row.id + "'").join(',')
    const facts = JSON.parse(sql(`SELECT jsonb_build_object('records',(SELECT count(*) FROM hub.deliverable WHERE id IN (${ids}) AND status='Not Started'),'issues',(SELECT count(*) FROM hub.deliverable_issue WHERE deliverable_id IN (${ids})),'createdLogs',(SELECT count(*) FROM hub.activity_log WHERE item_type='Deliverable' AND item_id IN (${ids}) AND action='Created' AND source='Import'),'batches',(SELECT count(DISTINCT snapshot->>'batchId') FROM hub.activity_log WHERE item_type='Deliverable' AND item_id IN (${ids}) AND action='Created' AND source='Import'),'sourceRows',(SELECT count(DISTINCT snapshot->>'sourceRow') FROM hub.activity_log WHERE item_type='Deliverable' AND item_id IN (${ids}) AND action='Created' AND source='Import'))::text`))
    assert.deepEqual(facts, { records: 40, issues: 0, createdLogs: 40, batches: 1, sourceRows: 40 }); batch.facts = facts
    const after = snapshot(); assert.deepEqual(await api('POST', root + '/import', request, [200], key), made)
    assert.deepEqual(snapshot(), after, 'Exact successful retry returns the same40 IDs with zero further table changes')
    assert.equal(sql(`SELECT to_jsonb(d)::text FROM hub.deliverable d WHERE id='${m.sentinel.id}'`), sentinel)
    result.checks.push(format + ': one bad title has zero table changes; correction creates40 and40normal Import logs, no issue evidence, exact retry inert, sentinel unchanged'); save()
  }
  const beforeLarge = snapshot(), large = body('real-drawings-large.xlsx', 'xlsx')
  const bytes = Buffer.byteLength(JSON.stringify(large)); assert.ok(bytes > 1024 * 1024 && bytes < 3 * 1024 * 1024)
  const largePreview = await api('POST', root + '/import-preview', large, [200], 'qa24-large-preview-' + crypto.randomUUID())
  assert.equal(largePreview.canCommit, true); assert.equal(largePreview.creates, 40); assert.deepEqual(snapshot(), beforeLarge)
  await api('POST', root, { name: 'x'.repeat(1100000), projectDisciplineId: m.disciplineId, deliverableTypeId: m.typeId, ownerId: m.people.marc.id, requiresReview: false }, [413])
  assert.deepEqual(snapshot(), beforeLarge, 'Ordinary create retains the global1MiB refusal without effects')
  result.largeRequestBytes = bytes; result.checks.push('Actual Kestrel accepts a supported >1MiB drawing preview with zero writes; ordinary create still413 at global1MiB')
  result.status = 'PASS_REAL_KESTREL_DATABASE'; result.boundary = 'Only owned synthetic QA24; API/database proof separate from Chrome UI and human/pilot acceptance'; save()
  fs.writeFileSync(path.join(__dirname, 'real-import-api1.json'), JSON.stringify(result, null, 2) + '\n')
  console.log(JSON.stringify({ status: result.status, checks: result.checks, created: result.batches.reduce((n, batch) => n + batch.created.total, 0), largeRequestBytes: bytes }))
})().catch(error => { result.error = error.message; save(); console.error(error); process.exitCode = 1 })
