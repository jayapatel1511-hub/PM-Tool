// S46: AC-SCR-79 browser-only last-ten history/Clear; AC-SCR-84 no storage access while off.
import { test } from 'node:test'
import assert from 'node:assert/strict'
import { clearRecentSearches, isRecentSearchNavigation, normalizeRecentSearch, readRecentSearches, recentSearchStorageKey, rememberRecentSearch,
  type RecentSearchStorage, type RecentSearchVisit } from './recent-searches.ts'

const viewer = '00000000-0000-0000-0000-000000000101', other = '00000000-0000-0000-0000-000000000102'
function memory() {
  const values = new Map<string, string>()
  const calls = { resolve: 0, get: 0, set: 0, remove: 0 }
  const denied = { resolve: false, get: false, set: false, remove: false }
  const storage: RecentSearchStorage = {
    getItem(key) { calls.get++; if (denied.get) throw new Error('read denied'); return values.get(key) ?? null },
    setItem(key, value) { calls.set++; if (denied.set) throw new Error('quota'); values.set(key, value) },
    removeItem(key) { calls.remove++; if (denied.remove) throw new Error('clear denied'); values.delete(key) },
  }
  const source = () => { calls.resolve++; if (denied.resolve) throw new Error('storage getter denied'); return storage }
  return { values, calls, denied, source }
}

test('last ten searches are trimmed, case-deduplicated and most recent first without changing internal search meaning', () => {
  const m = memory()
  for (let n = 0; n < 12; n++) rememberRecentSearch(viewer, '  Drawing ' + n + '  ', true, m.source)
  assert.deepEqual(readRecentSearches(viewer, true, m.source), Array.from({ length: 10 }, (_, n) => 'Drawing ' + (11 - n)))
  assert.equal(m.calls.set, 12)
  rememberRecentSearch(viewer, 'Drawing 11', true, m.source)
  assert.equal(m.calls.set, 12, 'Most-recent exact repeat, including repeated mount effects, performs no second write')
  assert.deepEqual(rememberRecentSearch(viewer, '  DRAWING 7 ', true, m.source).slice(0, 3), ['DRAWING 7', 'Drawing 11', 'Drawing 10'])
  assert.equal(readRecentSearches(viewer, true, m.source).filter(term => term.toLowerCase() === 'drawing 7').length, 1)
  rememberRecentSearch(viewer, 'Civil  review', true, m.source); rememberRecentSearch(viewer, 'Civil review', true, m.source)
  assert.deepEqual(readRecentSearches(viewer, true, m.source).slice(0, 2), ['Civil review', 'Civil  review'], 'Internal spaces remain part of the actual term')
  const returned = readRecentSearches(viewer, true, m.source); returned.push('not persisted')
  assert.equal(readRecentSearches(viewer, true, m.source).includes('not persisted'), false, 'Returned arrays do not alias saved history')
})

test('actual viewer IDs partition read, record and Clear, leaving another account and unrelated browser data intact', () => {
  const m = memory(), firstKey = recentSearchStorageKey(viewer)!, secondKey = recentSearchStorageKey(other)!
  assert.notEqual(firstKey, secondKey)
  rememberRecentSearch(viewer, 'First account term', true, m.source)
  rememberRecentSearch(other, 'Second account term', true, m.source)
  m.values.set('unrelated.preferences', 'preserved')
  assert.deepEqual(readRecentSearches(viewer, true, m.source), ['First account term'])
  assert.deepEqual(readRecentSearches(other, true, m.source), ['Second account term'])
  assert.equal(clearRecentSearches(viewer, true, m.source), true)
  assert.equal(m.values.has(firstKey), false)
  assert.deepEqual(readRecentSearches(other, true, m.source), ['Second account term'])
  assert.equal(m.values.get('unrelated.preferences'), 'preserved')
  assert.notEqual(recentSearchStorageKey('viewer/one'), recentSearchStorageKey('viewer%2Fone'), 'Encoded partition keys cannot collide')
  assert.deepEqual(rememberRecentSearch(viewer, 'First account term', true, m.source), ['First account term'], 'An explicit same-term submission after Clear can be recorded again')
})

test('history accepts bounded well-formed single-line Unicode strings and never truncates or coerces invalid input', () => {
  assert.equal(normalizeRecentSearch('  café 👷 東京  '), 'café 👷 東京')
  assert.equal(normalizeRecentSearch('x'.repeat(200)), 'x'.repeat(200))
  assert.equal(normalizeRecentSearch('x'.repeat(201)), null)
  for (const term of [null, undefined, 42, {}, ['a'], '', '   ', 'two\nlines', 'two\u2028lines', 'two\u2029lines', '\tEIT', 'bad\u0000name', 'bad\u0085name', '\uD800', '\uDC00']) assert.equal(normalizeRecentSearch(term), null)
  const m = memory(); rememberRecentSearch(viewer, 'kept', true, m.source)
  for (const term of [' ', 'x'.repeat(201), '\uD800', { toString: () => { throw new Error('must not coerce') } }]) assert.deepEqual(rememberRecentSearch(viewer, term, true, m.source), ['kept'])
  assert.equal(m.calls.set, 1, 'Invalid terms do not replace legal prior history')
})

test('corrupt, mixed, oversized or old unbounded storage is read safely without auto-writing a repair', () => {
  const m = memory(), key = recentSearchStorageKey(viewer)!
  for (const raw of ['{broken', 'null', '{}', JSON.stringify({ items: ['wrong schema'] }), 'x'.repeat(20_000)]) {
    m.values.set(key, raw); assert.deepEqual(readRecentSearches(viewer, true, m.source), [])
  }
  m.values.set(key, JSON.stringify([' first ', null, 42, 'FIRST', 'valid 👷', 'bad\nline', '\uD800', 'x'.repeat(201)]))
  assert.deepEqual(readRecentSearches(viewer, true, m.source), ['first', 'valid 👷'])
  m.values.set(key, JSON.stringify(Array.from({ length: 30 }, (_, n) => 'term ' + n)))
  assert.deepEqual(readRecentSearches(viewer, true, m.source), Array.from({ length: 10 }, (_, n) => 'term ' + n))
  assert.equal(m.calls.set, 0); assert.equal(m.calls.remove, 0, 'Reads preserve storage until an explicit record or Clear')
  m.values.set(key, '{broken')
  assert.deepEqual(rememberRecentSearch(viewer, 'new legal term', true, m.source), ['new legal term'], 'A deliberate search may replace corrupt history in its own partition')
})

test('module off and invalid viewer IDs never resolve or touch browser storage', () => {
  const m = memory(); m.denied.resolve = true
  assert.deepEqual(readRecentSearches(viewer, false, m.source), [])
  assert.deepEqual(rememberRecentSearch(viewer, 'hidden term', false, m.source), [])
  assert.equal(clearRecentSearches(viewer, false, m.source), false)
  for (const invalid of ['', ' ', ' leading', 'trailing ', 'x'.repeat(129), 'bad\nviewer', '\uD800']) {
    assert.equal(recentSearchStorageKey(invalid), null)
    assert.deepEqual(readRecentSearches(invalid, true, m.source), [])
    assert.deepEqual(rememberRecentSearch(invalid, 'term', true, m.source), [])
    assert.equal(clearRecentSearches(invalid, true, m.source), false)
  }
  assert.deepEqual(m.calls, { resolve: 0, get: 0, set: 0, remove: 0 })
})

test('denied getters/reads, full storage and refused Clear are nonthrowing and never claim deletion or overwrite unknown history', () => {
  const m = memory(), key = recentSearchStorageKey(viewer)!
  rememberRecentSearch(viewer, 'old term', true, m.source)
  m.denied.set = true
  assert.deepEqual(rememberRecentSearch(viewer, 'new term', true, m.source), ['old term'])
  assert.deepEqual(JSON.parse(m.values.get(key)!), ['old term'])
  m.denied.remove = true; assert.equal(clearRecentSearches(viewer, true, m.source), false)
  assert.equal(m.values.has(key), true)
  m.denied.get = true
  const writes = m.calls.set
  assert.deepEqual(readRecentSearches(viewer, true, m.source), [])
  assert.deepEqual(rememberRecentSearch(viewer, 'must not overwrite unreadable data', true, m.source), [])
  assert.equal(m.calls.set, writes)
  m.denied.resolve = true
  assert.deepEqual(readRecentSearches(viewer, true, m.source), [])
  assert.deepEqual(rememberRecentSearch(viewer, 'term', true, m.source), [])
  assert.equal(clearRecentSearches(viewer, true, m.source), false)
  assert.deepEqual(readRecentSearches(viewer, true, () => null), [])
  assert.deepEqual(rememberRecentSearch(viewer, 'term', true, () => null), [])
})

test('committed term navigation records once while Person returns, type/filter/paging changes and module re-renders do not', () => {
  const context = { enabled: true, personPanel: false, restoring: false }, first: RecentSearchVisit = { viewerId: viewer, term: 'EIT' }
  assert.equal(isRecentSearchNavigation(null, first, context), true, 'A committed initial results URL is a search visit')
  assert.equal(isRecentSearchNavigation(first, first, context), false, 'Same term on a type, filter, paging or repeated render is not a new search')
  assert.equal(isRecentSearchNavigation(first, { ...first, term: ' EIT ' }, context), false)
  assert.equal(isRecentSearchNavigation(null, first, { ...context, personPanel: true }), false, 'A direct Person overlay URL is not a search submission')
  assert.equal(isRecentSearchNavigation(null, first, { ...context, restoring: true }), false, 'A remounted source return must not record again')
  assert.equal(isRecentSearchNavigation(first, first, { ...context, restoring: true }), false)
  assert.equal(isRecentSearchNavigation(first, { ...first, term: 'Civil' }, context), true)
  assert.equal(isRecentSearchNavigation(first, { ...first, term: 'eit' }, context), true, 'Deliberate changed spelling may update the latest term spelling')
  assert.equal(isRecentSearchNavigation(first, { ...first, viewerId: other }, context), true)
  assert.equal(isRecentSearchNavigation(null, first, { ...context, enabled: false }), false)
  assert.equal(isRecentSearchNavigation(null, { ...first, term: ' ' }, context), false)
})
