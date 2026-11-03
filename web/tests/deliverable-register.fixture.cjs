// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
/** @param {Partial<Schemas['DeliverableRegisterInfo']>} [value] @returns {Schemas['DeliverableRegisterInfo']} */
const registerInfo = (value = {}) => ({ clientDrawingNumber: null, numberPattern: null, numberMatchesPattern: null, currentIssue: null, ...value })
/** @param {Partial<Schemas['DeliverableIssueRegisterInfo']>} [value] @returns {Schemas['DeliverableIssueRegisterInfo']} */
const issueRegister = (value = {}) => ({ rowVersion: 0, purposeOfIssueId: null, purpose: null, returnCodeId: null, returnCode: null, returnLabel: null, responseDate: null, ...value })
/** @param {Pick<Schemas['DeliverableIssueRead'], 'id' | 'issuedDate'> & Partial<Schemas['DeliverableIssueRead']>} value @returns {Schemas['DeliverableIssueRead']} */
const issuedRow = value => ({ revision: null, issuedTo: null, transmittalUrl: null, note: null, issuedBy: null, ...value })
module.exports = { registerInfo, issueRegister, issuedRow }
