// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One row of the Submissions list, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * @param {Pick<Schemas['SubmissionListRow'], 'id' | 'key' | 'title' | 'status' | 'coordinatorId' | 'targetDate' | 'blockerCount'> & Partial<Schemas['SubmissionListRow']>} r
 * @returns {Schemas['SubmissionListRow']}
 */
const submissionListRow = (r) => ({
  purpose: 'Permit review', coordinatorName: null, milestoneId: '00000000-0000-4000-8000-000000000000', milestoneName: null, manifestVersion: 1, rowVersion: 1, ...r,
})

/**
 * One page of the list. The page and size are the request's, as the server echoes them.
 * @param {Schemas['SubmissionListRow'][]} items
 * @param {{ total?: number, page?: number, pageSize?: number }} [paging]
 * @returns {Schemas['PageOfListRow']}
 */
const submissionPage = (items, paging = {}) => ({ items, page: paging.page ?? 1, pageSize: paging.pageSize ?? 50, totalCount: paging.total ?? items.length })
module.exports = { submissionListRow, submissionPage }
