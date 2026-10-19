// @ts-check
/** @type {import('../src/gen/api').components['schemas']['CommentListResponse']} */
const comments = {
  canComment: true, commentReason: null,
  items: [{
    id: '00000000-0000-0000-0000-000000000401',
    authorId: '00000000-0000-0000-0000-000000000002', authorName: 'Alex Chen',
    commentKind: 'General', reviewRound: null, editedAt: null, createdAt: '2026-10-18T12:00:00Z',
    deleted: false, deletedByPm: false, body: 'Synthetic coordination note',
    mentions: [{ id: '00000000-0000-0000-0000-000000000403', name: 'Priya Synthetic' }],
    canEdit: true, canDelete: true,
  }, {
    id: '00000000-0000-0000-0000-000000000402',
    authorId: '00000000-0000-0000-0000-000000000002', authorName: 'Alex Chen',
    commentKind: 'General', reviewRound: null, editedAt: null, createdAt: '2026-10-18T12:01:00Z',
    deleted: true, deletedByPm: true, body: null, mentions: [], canEdit: false, canDelete: false,
  }],
}
module.exports = { comments }
