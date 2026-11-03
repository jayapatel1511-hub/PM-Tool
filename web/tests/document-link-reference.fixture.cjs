// @ts-check
/** AC-SCR-77/84 synthetic UI contracts; actual comparison/authority/persistence is API-tested separately. */
/** @typedef {import('../src/gen/api').components['schemas']} Schemas */
const { surfacePerson } = require('./person-surface-completeness.fixture.cjs')
/** @param {number} n */
const id = n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`
const stamp = '2026-11-02T13:00:00Z', registrarId = id(17901), authorId = id(17801)
/** @param {string} state @returns {Schemas['DocumentLinkReferenceContext']} */
const context = state => {
  const declaration = state === 'NotRecorded' ? null : state === 'Matches' ? 'D' : 'C'
  const current = ['Matches', 'Different'].includes(state) ? 'D' : null
  return {
    reference: state === 'NotRecorded' ? null : { sourceSystem: 'Synthetic external register', stableSourceId: 'DOC-CIV-17', declaredRevision: declaration,
      registeredBy: registrarId, registeredByUser: { id: registrarId, displayName: 'Synthetic reference registrar', isActive: false }, registeredAt: stamp, registrationKind: 'Manual' },
    target: ['NotRecorded', 'Matches', 'Different', 'CurrentNotRecorded'].includes(state)
      ? { id: id(17950), key: 'D-REF', name: 'Registered civil drawing', status: 'In Progress', currentRevision: current, rowVersion: 93 } : null,
    comparison: { state, declaredRevision: declaration, currentRevision: current },
  }
}
/** @returns {Schemas['LinkListResponse']} */
const fresh = () => ({ canAdd: true, links: ['Different', 'Matches', 'NotRecorded', 'NoDeliverable', 'TargetChanged', 'TargetUnavailable', 'CurrentNotRecorded'].map((state, index) => ({
  id: id(17001 + index), title: `Synthetic ${state} reference`, url: `https://example.test/reference-${index}`, linkType: 'Other', rowVersion: 9,
  isNetworkPath: false, addedBy: authorId, addedByName: 'Original synthetic link author', addedAt: '2026-10-01T10:00:00Z', canChange: index === 0, referenceContext: context(state),
})), inherited: [{ id: id(17020), title: 'Inherited network drawing reference', url: '\\\\fileserver\\synthetic\\drawing', linkType: 'Network Folder', rowVersion: 9,
  isNetworkPath: true, addedBy: authorId, addedByName: 'Original synthetic link author', addedAt: '2026-10-01T10:00:00Z', canChange: true,
  referenceContext: { ...context('Different'), reference: { sourceSystem: 'Synthetic inherited source', stableSourceId: 'DOC-CIV-20', declaredRevision: 'C', registeredBy: id(17902),
    registeredByUser: { id: registrarId, displayName: 'Mismatched registrar descriptor', isActive: true }, registeredAt: stamp, registrationKind: 'Manual' } } }],
})
/** @param {Schemas['DocumentLinkReferenceInput'] | null} input @param {string} viewer @returns {Schemas['DocumentLinkReferenceContext']} */
const registered = (input, viewer) => {
  if (!input) return context('NotRecorded')
  const declared = input.declaredRevision?.trim() || null
  return { ...context(declared ? declared === 'D' ? 'Matches' : 'Different' : 'NotRecorded'), target: context('Different').target,
    reference: { sourceSystem: input.sourceSystem?.trim() ?? '', stableSourceId: input.stableSourceId?.trim() || null, declaredRevision: declared,
      registeredBy: viewer, registeredByUser: { id: viewer, displayName: 'Current synthetic registrar', isActive: true }, registeredAt: stamp, registrationKind: 'Manual' },
    comparison: { state: !declared ? 'NotRecorded' : declared === 'D' ? 'Matches' : 'Different', declaredRevision: declared, currentRevision: 'D' } }
}
/** @param {Schemas['LinkRow']} row @returns {Schemas['LinkResponse']} */
const reply = row => ({ id: row.id, title: row.title, url: row.url, linkType: row.linkType, rowVersion: row.rowVersion })
/** @param {Schemas['LinkListResponse']} data @param {Schemas['DocumentLinkAddBody']} body @param {string} viewer */
const create = (data, body, viewer) => {
  /** @type {Schemas['LinkRow']} */
  const row = { id: id(17100 + data.links.length), title: body.title ?? 'detected-drawing', url: body.url, linkType: body.linkType ?? (body.url.startsWith('\\\\') ? 'Network Folder' : body.url.includes('.sharepoint.com') ? 'SharePoint' : 'Other'),
    rowVersion: 1, isNetworkPath: body.url.startsWith('\\\\'), addedBy: viewer, addedByName: 'Original current creator', addedAt: stamp, canChange: true,
    referenceContext: registered(body.reference ?? null, viewer) }
  data.links.push(row); return reply(row)
}
/** @param {Schemas['LinkListResponse']} data @param {string} linkId @param {Schemas['DocumentLinkReferenceInput'] | null} input @param {string} viewer */
const replace = (data, linkId, input, viewer) => {
  const row = data.links.find(row => row.id === linkId)
  if (!row) throw new Error('Synthetic own link required')
  row.referenceContext = registered(input, viewer); row.rowVersion += 1; return reply(row)
}
module.exports = { id, stamp, registrarId, authorId, fresh, context, registered, create, replace, reply, person: surfacePerson }
