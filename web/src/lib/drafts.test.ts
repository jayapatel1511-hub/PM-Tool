import { test } from 'node:test'
import assert from 'node:assert/strict'

// The browser pieces the module touches, kept in memory.
const store = new Map<string, string>()
const tab = new Map<string, string>()
let full = false
Object.assign(globalThis, {
  sessionStorage: { getItem: (k: string) => tab.get(k) ?? null, removeItem: (k: string) => { tab.delete(k) }, setItem: (k: string, v: string) => { if (full) throw new Error('full'); tab.set(k, v) } },
  location: { pathname: '/my-work', search: '?panel=Task:t1&view=all' },
  localStorage: {
    get length() { return store.size }, key: (i: number) => [...store.keys()][i] ?? null, getItem: (k: string) => store.get(k) ?? null,
    removeItem: (k: string) => { store.delete(k) }, setItem: (k: string, v: string) => { if (full) throw new Error('full'); store.set(k, v) },
  },
  window: { addEventListener: () => {} },
})
const { blocksUnsavedNavigation, clearDraft, discardUnsaved, draftStorageKey, dropPending, forgetDrafts, hasUnsaved, keepPending, keepUnsent, leave, leaveQuestion, peekPending, pendingRoute, pruneDrafts, readDraft, registerForm, writeDraft } = await import('./drafts.ts')
const { preservesTaskDetailNavigation, taskDetailReturnOrigin } = await import('./drafts.ts')

test('Task preservation follows only its own presentation and captured Person chain, including nested return and paging', () => {
  const actor = '00000000-0000-0000-0000-000000000123', taskId = 'synthetic-task'
  const source = { pathname: '/projects/SYN/tasks', search: `?projects=all&page=2&panel=Task:${taskId}&taskSection=history&taskHistoryActor=${taskId}:${actor}`, hash: '' }
  const sourceUrl = source.pathname + source.search
  const origin = { url: sourceUrl, scrollTop: 400, surface: { slot: 'sheet-content', scrollTop: 600, scrollLeft: 0 } }
  const person = { ...source, search: '?projects=all&page=2&panel=Person:person-a', state: { recordContextReturn: origin } }
  const preserve = preservesTaskDetailNavigation(taskId)
  assert.equal(preserve(source, { ...source, search: source.search + `&taskHistoryPage=${taskId}:2&groupChanges=true` }), true)
  assert.equal(preserve(source, person), true); assert.equal(preserve(person, source), true)
  const paged = { ...person, search: person.search + '&personProjectsPage=2' }
  assert.equal(preserve(person, paged), true)
  const nested = { ...person, search: '?projects=all&page=2&panel=Person:person-b', state: { recordContextReturn: { url: person.pathname + person.search, parent: origin, surface: { slot: 'sheet-content', scrollTop: 20, scrollLeft: 0 } } } }
  assert.equal(preserve(person, nested), true); assert.equal(preserve(nested, person), true)
  assert.equal(taskDetailReturnOrigin(nested.state, taskId)?.url, sourceUrl)
  for (const refused of [
    { ...source, search: source.search.replace('projects=all', 'projects=other') },
    { ...source, search: source.search.replace(`panel=Task:${taskId}`, 'panel=Task:another-task') },
    { ...source, search: source.search.replace(`taskHistoryActor=${taskId}:`, 'taskHistoryActor=another-task:') },
    { ...source, search: source.search + `&taskHistoryActor=${taskId}:${actor}` },
    { ...source, search: source.search.replace('taskSection=history', 'taskSection=unknown') },
    { ...source, pathname: '/projects' },
  ]) assert.equal(preserve(source, refused), false)
  const modal = { ...person, state: { recordContextReturn: { ...origin, surface: { slot: 'dialog-content', scrollTop: 0, scrollLeft: 0 } } } }
  assert.equal(preserve(source, modal), false); assert.equal(preserve(modal, source), false)
  assert.equal(taskDetailReturnOrigin(modal.state, taskId), null)
  assert.equal(preserve(source, { ...person, state: null }), false)
  const alias = { pathname: '/tasks/SYN-T001', search: '?taskSection=details', hash: '' }
  assert.equal(preservesTaskDetailNavigation(taskId, 'SYN-T001')(alias, { ...alias, search: '?taskSection=history' }), true)
})

test('Task presentation proof preserves only its mounted form; leaving, unload and another dirty form remain guarded', () => {
  const source = { pathname: '/tasks/synthetic-task', search: '?taskSection=details', hash: '' }
  const history = { ...source, search: '?taskSection=history' }
  const off = registerForm({ unsaved: () => true, discard: () => {}, preservesNavigation: preservesTaskDetailNavigation('synthetic-task') })
  try {
    assert.equal(hasUnsaved(), true)
    assert.equal(blocksUnsavedNavigation({ currentLocation: source, nextLocation: history }), false)
    assert.equal(blocksUnsavedNavigation({ currentLocation: source, nextLocation: { pathname: '/tasks', search: '', hash: '' } }), true)
    const other = registerForm({ unsaved: () => true, discard: () => {} })
    try { assert.equal(blocksUnsavedNavigation({ currentLocation: source, nextLocation: history }), true) } finally { other() }
    assert.equal(hasUnsaved(), true)
  } finally { off() }
})

test('a preserved overlay bypasses only its own form, while unload, close, other navigation and other dirty forms stay guarded', () => {
  const base = { pathname: '/projects/P/deliverables', search: '?group=none', hash: '' }
  const overlay = { ...base, search: '?group=none&panel=Person:u1' }
  const other = { pathname: '/projects', search: '', hash: '' }
  let dirty = true
  const off = registerForm({ unsaved: () => dirty, discard: () => { dirty = false },
    preservesNavigation: (from, to) => from.pathname === base.pathname && to.pathname === base.pathname && (from.search === overlay.search || to.search === overlay.search) })
  try {
    assert.equal(blocksUnsavedNavigation({ currentLocation: base, nextLocation: overlay }), false)
    assert.equal(blocksUnsavedNavigation({ currentLocation: overlay, nextLocation: base }), false)
    let returned = 0
    leave(() => { returned++ }, { currentLocation: overlay, nextLocation: base })
    assert.equal(returned, 1, 'explicit Person close uses the same destination-aware form verdict')
    assert.equal(leaveQuestion.get(), null)
    leave(() => { returned++ })
    assert.equal(returned, 1, 'closing the actual source form is still guarded')
    leaveQuestion.answer(false)
    assert.equal(blocksUnsavedNavigation({ currentLocation: overlay, nextLocation: other }), true)
    assert.equal(hasUnsaved(), true, 'browser unload and explicit close still see the memory-only file')
    const removeOther = registerForm({ unsaved: () => true, discard: () => {} })
    try {
      assert.equal(blocksUnsavedNavigation({ currentLocation: base, nextLocation: overlay }), true)
      leave(() => { returned++ }, { currentLocation: overlay, nextLocation: base })
      assert.equal(returned, 1, 'another dirty form still blocks explicit Person close')
      leaveQuestion.answer(false)
    } finally { removeOther() }
    discardUnsaved(); assert.equal(blocksUnsavedNavigation({ currentLocation: base, nextLocation: other }), false)
  } finally { off() }
})

const DAY = 86_400_000
const ago = (days: number) => new Date(Date.now() - days * DAY).toISOString()

test('a draft is read back until draft_keep_days, then dropped when read or pruned (FR-SAV-03)', () => {
  const key = draftStorageKey('u1', 'task.new.p1')
  assert.equal(key, 'draft.u1.task.new.p1')
  assert.equal(writeDraft(key, { name: 'x' }, ago(6)), true)
  assert.deepEqual(readDraft(key, 7)?.value, { name: 'x' })
  assert.equal(readDraft(key, 5), null, 'older than the setting')
  assert.equal(store.has(key), false, 'and removed')
  writeDraft(key, { name: 'old' }, ago(8)); writeDraft('draft.u2.comment.Task.t1', 'fresh'); store.set('hub.rail', 'open'); store.set('draft.u1.broken', '{not json')
  pruneDrafts(7)
  assert.deepEqual([...store.keys()].sort(), ['draft.u2.comment.Task.t1', 'hub.rail'], 'prune keeps fresh drafts and anything that is not a draft')
  clearDraft('draft.u2.comment.Task.t1'); store.clear()
})

test('a browser that refuses the write gives no draft', () => {
  full = true
  assert.equal(writeDraft('draft.u1.x', 1), false)
  full = false
})

test('leaving asks only while something typed is neither saved nor drafted (FR-SAV-04, FR-SAV-06)', () => {
  let typed = false, discarded = 0
  const off = registerForm({ unsaved: () => typed, discard: () => { discarded++ } })
  let left = 0
  leave(() => { left++ })
  assert.equal(left, 1, 'nothing typed: goes at once')
  typed = true
  assert.equal(hasUnsaved(), true)
  leave(() => { left++ })
  assert.equal(left, 1, 'held back for the question')
  assert.ok(leaveQuestion.get())
  leaveQuestion.answer(false)
  assert.equal(left, 1, 'Keep editing: stays')
  assert.equal(leaveQuestion.get(), null)
  leave(() => { left++ })
  leaveQuestion.answer(true)
  assert.equal(left, 2, 'Discard: goes')
  discardUnsaved()
  assert.equal(discarded, 1)
  off()
  assert.equal(hasUnsaved(), false)
})

test('a save cut off by an expired sign-in waits for the same user, record and field, and is spent once (FR-SAV-05)', () => {
  const route = pendingRoute()
  assert.equal(route, '/my-work?Task:t1')
  keepPending('u1', route, 'Description', 'typed', 3)
  keepPending('u1', route, 'Description', 'typed again', 4) // the same field again replaces it
  keepPending('u1', route, 'Name', null) // a cleared field is kept too
  assert.deepEqual(peekPending('u1', 'Description', 7), { value: 'typed again', version: 4 }, 'with the row version it was typed against')
  assert.deepEqual(peekPending('u1', 'Name', 7), { value: null, version: undefined })
  assert.equal(peekPending('u2', 'Description', 7), null, 'another person who signs in does not get it')
  assert.equal(peekPending('u1', 'Owner', 7), null, 'nor does another field')
  Object.assign(globalThis.location, { search: '?view=all&panel=Task:t1' })
  assert.deepEqual(peekPending('u1', 'Description', 7), { value: 'typed again', version: 4 }, 'other query parameters changing do not matter')
  Object.assign(globalThis.location, { search: '?panel=Task:t2' })
  assert.equal(peekPending('u1', 'Description', 7), null, 'another record in the panel does not get it')
  keepPending('u1', route, 'Name', 'typed for t1') // the refusal came back after the panel moved on to t2
  assert.equal(peekPending('u1', 'Name', 7), null, 'kept for the record the field was opened on, not the one on screen')
  Object.assign(globalThis.location, { search: '?panel=Task:t1' })
  assert.deepEqual(peekPending('u1', 'Name', 7), { value: 'typed for t1', version: undefined })
  dropPending('u1', 'Description')
  assert.equal(peekPending('u1', 'Description', 7), null, 'one retry')
  dropPending('u1', 'Name')
  assert.equal(tab.has('hub.pendingFields'), false, 'nothing is left behind')
  keepPending('u1', route, 'Description', 'old')
  const [kept] = JSON.parse(tab.get('hub.pendingFields')!)
  tab.set('hub.pendingFields', JSON.stringify([{ ...kept, at: ago(8) }]))
  assert.equal(peekPending('u1', 'Description', 7), null, 'older than draft_keep_days')
  tab.clear()
  full = true
  assert.doesNotThrow(() => keepPending('u1', route, 'Description', 'x'), 'a browser that refuses the write loses nothing more')
  full = false
})

test('a 401 from any request asks every form to keep its typed input (FR-SAV-05)', () => {
  let kept = 0
  const off = registerForm({ unsaved: () => false, discard: () => {}, keep: () => { kept++ } })
  const plain = registerForm({ unsaved: () => false, discard: () => {} }) // a form with a draft has nothing more to keep
  keepUnsent()
  assert.equal(kept, 1)
  off(); plain()
  keepUnsent()
  assert.equal(kept, 1, 'a form that is gone keeps nothing')
})

test('an explicit sign-out drops the person\'s drafts and waiting fields, and only theirs (051-D2)', () => {
  writeDraft(draftStorageKey('u1', 'task.new.p1'), { name: 'x' }); writeDraft(draftStorageKey('u2', 'task.new.p1'), { name: 'y' }); store.set('hub.rail', 'open')
  keepPending('u1', pendingRoute(), 'Name', 'mine'); keepPending('u2', pendingRoute(), 'Name', 'theirs')
  forgetDrafts('u1')
  assert.deepEqual([...store.keys()].sort(), ['draft.u2.task.new.p1', 'hub.rail'])
  assert.equal(peekPending('u1', 'Name', 7), null)
  assert.deepEqual(peekPending('u2', 'Name', 7), { value: 'theirs', version: undefined })
  forgetDrafts('u2')
  assert.equal(tab.has('hub.pendingFields'), false)
  store.clear()
})
