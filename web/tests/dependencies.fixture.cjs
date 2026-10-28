// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */

/**
 * The task at the other end of a link, typed against the contract so a server rename of a field fails `npm run typecheck:tests`.
 * A task with no dates and no assignee has them sent as null, as the server does.
 * @param {Pick<Schemas['DependencyTaskRef'], 'id' | 'key' | 'name'> & Partial<Schemas['DependencyTaskRef']>} r
 * @returns {Schemas['DependencyTaskRef']}
 */
const dependencyTask = (r) => ({ status: 'Not Started', dueDate: null, startDate: null, assigneeId: null, assigneeName: null, overdue: false, blocked: false, waiting: false, ...r })

/**
 * One link as the task panel lists it: Finish-to-Start with no lag and no note unless given.
 * @param {Pick<Schemas['TaskDependencyEdge'], 'dependencyId' | 'task'> & Partial<Schemas['TaskDependencyEdge']>} r
 * @returns {Schemas['TaskDependencyEdge']}
 */
const dependencyEdge = (r) => ({ note: null, lagDays: 0, type: 'FinishToStart', satisfied: false, ...r })

/**
 * A task's links on both sides.
 * @param {Schemas['TaskDependencyEdge'][]} [dependsOn]
 * @param {Schemas['TaskDependencyEdge'][]} [blocks]
 * @returns {Schemas['TaskDependencyList']}
 */
const taskDependencies = (dependsOn = [], blocks = []) => ({ dependsOn, blocks })

/**
 * The answer to a date read: no earliest, latest or inconsistencies unless given. `availability` and `tasks` are left out unless given,
 * as the server leaves them out when they do not apply.
 * @param {Partial<Schemas['DateContext']>} [dates]
 * @param {Pick<Schemas['DateContextRead'], 'availability' | 'tasks'>} [rest]
 * @returns {Schemas['DateContextRead']}
 */
const dateContextRead = (dates = {}, rest = {}) => ({
  dates: { earliestStart: null, earliestDue: null, latestDue: null, inconsistencies: [], successors: [], newInconsistencies: [], ...dates }, ...rest,
})

module.exports = { dependencyTask, dependencyEdge, taskDependencies, dateContextRead }
