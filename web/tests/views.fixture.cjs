// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * One saved view typed against the contract, so a server rename of a field fails `npm run typecheck:tests`.
 * A view nobody shared has no project and the person who owns it as `owner` null, and nothing it names is dropped.
 * @param {Pick<Schemas['SavedViewRow'], 'id' | 'name'> & Partial<Schemas['SavedViewRow']>} r
 * @returns {Schemas['SavedViewRow']}
 */
const savedView = (r) => ({ scope: 'Personal', projectId: null, isDefault: false, rowVersion: 1, params: {}, dropped: [], owner: null, canEdit: true, ...r })

/**
 * The answer to GET /views: the views a list's menu shows and whether the person may save one for the whole project.
 * @param {Schemas['SavedViewRow'][]} [views]
 * @param {boolean} [canShare]
 * @returns {Schemas['SavedViewList']}
 */
const viewList = (views = [], canShare = false) => ({ views, canShare })

/**
 * The answer to saving a view (201) and to changing one (200).
 * @param {string} id
 * @param {number} [rowVersion]
 * @returns {Schemas['ViewSaved']}
 */
const viewSaved = (id, rowVersion = 1) => ({ id, rowVersion })

module.exports = { savedView, viewList, viewSaved }
