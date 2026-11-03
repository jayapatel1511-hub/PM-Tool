/** Canonical workflow lane IDs are stable; presentation names stay in i18n. */
export function laneClass(lane: string): string { return `semantic-lane-${lane.toLowerCase().replace(/[^a-z0-9]+/g, '-')}` }

/** Presentation roles only. Consumers receive the canonical lane from the server;
 * this table does not infer a lane from task/deliverable statuses. */
export const LANE_FILLS = {
  'To Do': 'var(--lane-todo)',
  'In Progress': 'var(--lane-progress)',
  Review: 'var(--lane-review)',
  Done: 'var(--lane-done)',
  'On Hold': 'var(--time-away-hatch)',
} as const
export const TIME_AWAY_FILL = 'var(--time-away-hatch)'
export const NONWORKING_FILL = 'var(--nonworking-hatch)'
export function laneFill(lane: string): string | undefined { return Object.hasOwn(LANE_FILLS, lane) ? LANE_FILLS[lane as keyof typeof LANE_FILLS] : undefined }
