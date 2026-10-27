// @ts-check
/**
 * The start-readiness answer for a task that was never assessed and whose viewer cannot authorise a start, typed against
 * the contract so a server rename fails `npm run typecheck:tests`.
 * @param {{ taskId: string, key: string }} task
 * @returns {import('../src/gen/api').components['schemas']['TaskStartStatus']}
 */
const startReadiness = (task) => ({
  taskId: task.taskId, key: task.key, status: 'Not Started', readinessState: 'Needs Assessment', assessed: false, unknown: [], blocked: [], note: null,
  needsAuthorisation: true, canAuthorise: false, authorisation: null, unusableAuthorisation: null,
})
module.exports = { startReadiness }
