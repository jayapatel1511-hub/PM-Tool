import { test } from 'node:test'
import assert from 'node:assert/strict'

// The browser pieces the module touches, kept in memory.
const store = new Map<string, string>()
let full = false
Object.assign(globalThis, {
  localStorage: {
    get length() { return store.size }, key: (i: number) => [...store.keys()][i] ?? null, getItem: (k: string) => store.get(k) ?? null,
    removeItem: (k: string) => { store.delete(k) }, setItem: (k: string, v: string) => { if (full) throw new Error('full'); store.set(k, v) },
  },
  window: { addEventListener: () => {} },
})
const { clearDraft, discardUnsaved, draftStorageKey, hasUnsaved, leave, leaveQuestion, pruneDrafts, readDraft, registerForm, writeDraft } = await import('./drafts.ts')

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
