// Deterministic contract for the actual autosave hook, save controls and versioned transport.
// Complements data-entry.cjs; lifecycle, storage and network are controlled here.
const assert = require('node:assert/strict')
const fs = require('node:fs')
const cp = require('node:child_process')
const path = require('node:path')
const vm = require('node:vm')
const root = cp.execFileSync('git', ['rev-parse', '--show-toplevel'], { encoding: 'utf8' }).trim()
const req = require('node:module').createRequire(path.join(root, 'web/package.json'))
const ts = req('typescript'), React = req('react')
const { renderToStaticMarkup } = req('react-dom/server')
function source(file) {
  return process.env.REVIEW_REF
    ? cp.execFileSync('git', ['show', `${process.env.REVIEW_REF}:${file}`], { cwd: root, encoding: 'utf8' })
    : fs.readFileSync(path.join(root, file), 'utf8')
}
function declarations(file, names) {
  const text = source(file)
  const ast = ts.createSourceFile(file, text, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX)
  const seen = new Set()
  const code = ast.statements.filter(n => {
    const found = n.name ? [n.name.text] : ts.isVariableStatement(n) ? n.declarationList.declarations.map(d => d.name.getText(ast)) : []
    const selected = found.filter(x => names.includes(x))
    selected.forEach(x => seen.add(x))
    return selected.length
  }).map(n => n.getText(ast)).join('\n')
  assert.deepEqual([...seen].sort(), [...names].sort(), `declarations in ${file}`)
  return code
}
const program = declarations('web/src/lib/api.ts', ['ApiError', 'authHeaders', 'onUnauthorized', 'configureApi', 'requestHeaders', 'watching', 'watchWrite', 'api'])
  + '\n' + declarations('web/src/components/hub/fields.tsx', ['blank', 'why', 'RETRY_MS', 'since', 'useAutosaveField', 'asText', 'NoteAction', 'SaveNote'])
  + '\nthis.exposed = { hook: useAutosaveField, note: SaveNote, api, configureApi };'
let slots = [], cursor = 0, effects = [], dirty = false
const forms = new Set(), timers = new Map()
let timerId = 0, pending = null, writes = [], expired = false
let server = { value: 'Original', version: 1 }, display = { ...server }
const context = {
  exports: {}, React, DOMException,
  useQueryClient: () => ({}),
  useMe: () => ({ id: 'alex', settings: { draftKeepDays: 7 } }),
  useFlag: () => true,
  useFormNavigationPreserver: () => () => false, // outside a FormNavigationScope nothing preserves navigation
  useState: init => {
    const i = cursor++
    if (!(i in slots)) slots[i] = { value: typeof init === 'function' ? init() : init }
    return [slots[i].value, v => { slots[i].value = typeof v === 'function' ? v(slots[i].value) : v; dirty = true }]
  },
  useRef: init => {
    const i = cursor++
    if (!(i in slots)) slots[i] = { current: init }
    return slots[i]
  },
  useEffect: (f, deps) => {
    const i = cursor++, old = slots[i]
    if (!deps || !old || deps.some((d, k) => !Object.is(d, old.deps[k]))) effects.push(() => {
      old?.cleanup?.()
      slots[i] = { deps, cleanup: f() }
    })
  },
  pendingRoute: () => '/my-work?Task:a',
  peekPending: () => pending,
  dropPending: () => { pending = null },
  keepPending: (user, route, field, value, version) => { pending = { value, version } },
  registerForm: f => { forms.add(f); return () => forms.delete(f) },
  keepUnsent: () => { for (const f of forms) f.keep?.() },
  invalidateChange: () => { display = { ...server }; return Promise.resolve() },
  setTimeout: (f, ms) => { const id = ++timerId; timers.set(id, { f, ms }); return id },
  clearTimeout: id => timers.delete(id),
  observeBuild: () => {}, toast: { warning: () => {} },
  t: (key, args) => key + (args ? ' ' + JSON.stringify(args) : ''),
  ago: () => '', cn: () => '', Spinner: () => null, Check: () => null,
  fetch: async (url, o) => {
    if (o.method === 'GET') return { status: expired ? 401 : 200, ok: !expired, json: async () => ({}) }
    const value = JSON.parse(o.body).name
    const version = Number(o.headers['If-Match']?.replaceAll('"', ''))
    writes.push({ value, version })
    if (version !== server.version) return {
      status: 409, ok: false,
      json: async () => ({ code: 'concurrency_conflict', currentRowVersion: server.version, changedBy: 'Pat' }),
    }
    server = { value, version: server.version + 1 }
    return { status: 200, ok: true, json: async () => ({ rowVersion: server.version }) }
  },
}
vm.createContext(context)
vm.runInContext(ts.transpileModule(program, { compilerOptions: {
  target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.React,
} }).outputText, context)
const { hook, note, api, configureApi } = context.exposed
configureApi(async () => ({}), () => {})
let h
function render() {
  let n = 0
  do {
    assert.ok(n++ < 10, 'render settles')
    dirty = false; cursor = 0
    const seen = { ...display }
    h = hook(seen.value, value => api('tasks/a', {
      method: 'PATCH', body: { name: value }, ifMatch: seen.version,
    }), { delayMs: 1000, field: 'Name' })
    const todo = effects; effects = []; todo.forEach(f => f())
  } while (dirty)
  return h
}
function remount() {
  for (const s of slots) s?.cleanup?.()
  slots = []; effects = []; timers.clear(); forms.clear()
  display = { ...server }
  return render()
}
async function pulse401() {
  expired = true
  await assert.rejects(() => api('me/pulse'), e => e.status === 401)
  expired = false
}
async function tick() {
  for (const [id, t] of [...timers]) { timers.delete(id); t.f() }
  await Promise.resolve(); await Promise.resolve(); render()
}
const results = []
function check(name, fn) {
  try { fn(); results.push({ name, pass: true }) }
  catch (e) { results.push({ name, pass: false, error: e.message }) }
}
;(async () => {
  render(); h.schedule('Alex recovered text'); await pulse401()
  check('predebounce401 preserves text without a base or write', () => {
    assert.equal(writes.length, 0); assert.equal(pending.value, 'Alex recovered text'); assert.equal(pending.version, undefined)
  })
  server = { value: 'Pat newer text', version: 7 }; remount(); await tick()
  check('resume holds both values and never replays', () => {
    assert.equal(writes.length, 0); assert.equal(h.state.status, 'conflict')
    assert.equal(h.current, 'Pat newer text'); assert.equal(h.kept.value, 'Alex recovered text')
    const html = renderToStaticMarkup(note({ save: h }))
    assert.ok(html.includes('Pat newer text') && html.includes('Alex recovered text'))
  })
  check('explicit Keep mine and Take theirs are visible', () => {
    const html = renderToStaticMarkup(note({ save: h }))
    assert.ok(html.includes('inline.keepMine'), 'Keep mine missing from rendered SaveNote')
    assert.ok(html.includes('inline.takeTheirs'))
  })
  await h.commit('Alex recovered text'); h.schedule('Alex recovered text'); await tick()
  check('ordinary blur and debounce cannot resolve unknown-base conflict', () => assert.equal(writes.length, 0))
  await pulse401()
  check('second401 preserves recovered text', () => assert.equal(pending?.value, 'Alex recovered text'))
  remount()
  h.schedule('Alex revised text'); render(); await h.commit('Alex revised text'); render()
  check('editing an unresolved recovery updates the retained conflict without writing', () => {
    assert.equal(writes.length, 0)
    assert.equal(h.state.value, 'Alex revised text')
    assert.equal(h.kept.value, 'Alex revised text')
    assert.ok(renderToStaticMarkup(note({ save: h })).includes('Alex revised text'))
  })
  await h.keepMine(); render()
  check('explicit acceptance sends revised text with displayed fresh If-Match', () => {
    assert.deepEqual(writes, [{ value: 'Alex revised text', version: 7 }])
    assert.equal(server.value, 'Alex revised text')
  })
  pending = { value: 'Alex second text' }; server = { value: 'Pat visible text', version: 10 }; remount()
  server = { value: 'Pat raced text', version: 11 }; await h.keepMine(); render()
  check('concurrent edit refuses explicit stale acceptance', () => {
    assert.equal(writes.at(-1).version, 10); assert.equal(server.value, 'Pat raced text')
    assert.equal(h.state.status, 'conflict'); assert.equal(h.state.version, 11); assert.equal(h.current, 'Pat raced text')
  })
  await h.keepMine(); render()
  check('versioned Keep mine uses refusal version', () => {
    assert.equal(writes.at(-1).version, 11); assert.equal(server.value, 'Alex second text')
  })
  pending = { value: 'Alex discard text' }; server = { value: 'Pat retained text', version: 20 }; remount()
  const count = writes.length; h.discard(); render(); await tick()
  check('Take theirs clears recovery without a write', () => {
    assert.equal(writes.length, count); assert.equal(h.kept, undefined); assert.equal(h.state, null)
    assert.equal(server.value, 'Pat retained text'); assert.equal([...forms].some(f => f.unsaved()), false)
  })
  console.log(JSON.stringify({ revision: process.env.REVIEW_REF ?? 'working tree', results }, null, 2))
  process.exitCode = results.some(x => !x.pass) ? 1 : 0
})().catch(e => { console.error(e); process.exitCode = 1 })
