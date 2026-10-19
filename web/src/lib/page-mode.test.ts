import { test } from 'node:test'
import assert from 'node:assert/strict'
import { migratePageMode, pageMode } from './page-mode.ts'

test('legacy links migrate without confusing a saved-view id with a mode (AC-PLAT-08)', () => {
  for (const [key, mode] of [['tab', 'roles'], ['tab', 'log'], ['layout', 'list'], ['layout', 'projects']] as const) {
    const params = new URLSearchParams({ view: mode, q: 'kept' })
    assert.equal(pageMode(params, key, [mode], 'default'), mode)
    assert.equal(migratePageMode(params, key, [mode])?.toString(), `q=kept&${key}=${mode}`)
    assert.equal(params.get('view'), mode)
  }
  const saved = new URLSearchParams('view=saved-view-id')
  assert.equal(migratePageMode(saved, 'tab', ['roles']), null)
  assert.equal(pageMode(saved, 'tab', ['roles'], 'allocations'), 'allocations')
  assert.equal(migratePageMode(new URLSearchParams('tab=allocations&view=roles'), 'tab', ['roles'])?.get('tab'), 'allocations')
})
