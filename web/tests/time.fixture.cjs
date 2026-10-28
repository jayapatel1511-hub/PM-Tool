// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One time entry typed against the contract, so a server rename of a field fails `npm run typecheck:tests`.
 * An entry with no note carries `note` as null, as the page receives it.
 * @param {{ id: string, projectId: string, taskId: string, userId: string } & Partial<Schemas['TimeRow']>} row
 * @returns {Schemas['TimeRow']}
 */
const timeRow = (row) => ({
  projectNumber: 'SYN-RCV', projectName: 'Synthetic project', taskKey: 'SYN-T1', taskName: 'Synthetic work', person: 'Synthetic PM',
  workDate: '2026-10-13', hours: 3, note: null, rowVersion: 1, canEdit: true, editNeedsReason: false,
  ...row,
})

/**
 * A range of entries with the totals the server derives from them: by day, by task (in task key order) and by project (in project number order).
 * @param {string} from
 * @param {string} to
 * @param {Schemas['TimeRow'][]} entries
 * @param {boolean} [canReview]
 * @returns {Schemas['TimeView']}
 */
const timeView = (from, to, entries, canReview = false) => {
  /** @template T @param {(row: Schemas['TimeRow']) => string} key @param {(row: Schemas['TimeRow'], hours: number) => T} make @returns {T[]} */
  const group = (key, make) => [...new Set(entries.map(key))].map((k) => { const rows = entries.filter((r) => key(r) === k); return make(rows[0], rows.reduce((s, r) => s + r.hours, 0)) })
  return {
    from, to, entries, total: entries.reduce((s, r) => s + r.hours, 0),
    byDay: group((r) => r.workDate, (r, hours) => ({ date: r.workDate, hours })).sort((a, b) => a.date.localeCompare(b.date)),
    byTask: group((r) => r.taskId, (r, hours) => ({ taskId: r.taskId, taskKey: r.taskKey, taskName: r.taskName, projectNumber: r.projectNumber, hours })).sort((a, b) => a.taskKey.localeCompare(b.taskKey)),
    byProject: group((r) => r.projectId, (r, hours) => ({ projectId: r.projectId, projectNumber: r.projectNumber, projectName: r.projectName, hours })).sort((a, b) => a.projectNumber.localeCompare(b.projectNumber)),
    canReview,
  }
}

/**
 * The answer to Add Time (201) and to a correction (200).
 * @param {string} id
 * @param {number} rowVersion
 * @returns {Schemas['EntrySaved']}
 */
const entrySaved = (id, rowVersion) => ({ id, rowVersion })

module.exports = { timeRow, timeView, entrySaved }
