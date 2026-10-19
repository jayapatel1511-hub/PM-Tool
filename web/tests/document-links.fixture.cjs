// @ts-check
/** @typedef {import('../src/gen/api').components['schemas']['LinkListResponse']} LinkListResponse */
/** @type {LinkListResponse} */
const documentLinks = {
  canAdd: true,
  links: [{
    id: '00000000-0000-0000-0000-000000000101', title: 'Current drawings',
    url: 'https://contoso.sharepoint.com/sites/synthetic/drawings', linkType: 'SharePoint',
    rowVersion: 1, isNetworkPath: false, addedBy: '00000000-0000-0000-0000-000000000201',
    addedByName: 'Priya Synthetic', addedAt: '2026-10-18T12:00:00Z', canChange: true,
  }],
  inherited: [{
    id: '00000000-0000-0000-0000-000000000102', title: 'Deliverable reference',
    url: '\\\\fileserver\\projects\\synthetic\\deliverable', linkType: 'Network Folder',
    rowVersion: 1, isNetworkPath: true, addedBy: '00000000-0000-0000-0000-000000000202',
    addedByName: 'Alex Synthetic', addedAt: '2026-10-18T12:05:00Z', canChange: false,
  }],
}
module.exports = { documentLinks }
