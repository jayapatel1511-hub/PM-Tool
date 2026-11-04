import { test } from 'node:test'
import assert from 'node:assert/strict'
import { sequenceCue } from './milestone-sequence.ts'

const ms = (id: string, milestoneType: string, date: string | null, isCancelled = false) => ({ id, name: id, milestoneType, date, isCancelled })

test('AC-SCR-66: IFC dated before Design Submission is out of sequence; an undated milestone says No date', () => {
  const design = ms('Design Submission', 'Design Submission', '2026-11-10'), ifc = ms('IFC', 'IFC', '2026-11-01'), none = ms('Kickoff', 'Kickoff', null)
  const all = [design, ifc, none]
  assert.deepEqual(sequenceCue(ifc, all), { kind: 'before', name: 'Design Submission' })
  assert.equal(sequenceCue(design, all), null)
  assert.deepEqual(sequenceCue(none, all), { kind: 'noDate' })
})

test('Other, cancelled milestones and same-day dates raise no cue', () => {
  const design = ms('D', 'Design Submission', '2026-11-10'), other = ms('O', 'Other', '2026-11-01'), cancelled = ms('C', 'Design Submission', '2026-12-01', true)
  const ifc = ms('I', 'IFC', '2026-11-10')
  assert.equal(sequenceCue(other, [design, other]), null)
  assert.equal(sequenceCue(ifc, [design, ifc, cancelled]), null)
  assert.equal(sequenceCue(cancelled, [cancelled]), null)
})

test('the earliest later-dated earlier-type milestone is named', () => {
  const kickoff = ms('K', 'Kickoff', '2026-12-01'), design = ms('D', 'Design Submission', '2026-11-20'), ifc = ms('I', 'IFC', '2026-11-01')
  assert.deepEqual(sequenceCue(ifc, [kickoff, design, ifc]), { kind: 'before', name: 'D' })
})
