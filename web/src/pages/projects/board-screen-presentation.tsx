import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import type { TaskRow } from './Tasks'
import { hours } from '@/lib/format'
import { t, type KeyIn } from '@/lib/i18n'
import { cn } from '@/lib/utils'

interface LeadFact { state?: string | null; id?: string | null; name?: string | null }
interface BoardRow {
  board?: { reviewRequestedAt?: string | null; reviewWaitingDays?: number | null; reviewStaleDays?: number | null; disciplineLead?: LeadFact | null; projectManager?: LeadFact | null } | null
}
const label = (key: KeyIn<'boardFacts.'>, values?: Record<string, string | number>) => t(`boardFacts.${key}`, values)

/** These figures describe the actual visible cards, not an unrequested full-filtered-cohort aggregate. */
export function BoardCardTotals({ rows }: { rows: TaskRow[] }) {
  let tenths = 0, unestimated = 0, overdue = 0, unknown = 0
  for (const row of rows) {
    if (typeof row.estimatedHours === 'number' && Number.isFinite(row.estimatedHours) && row.estimatedHours >= 0) tenths += Math.round(row.estimatedHours * 10)
    else unestimated++
    if (!row.state || typeof row.state.isOverdue !== 'boolean') unknown++
    else if (row.state.isOverdue) overdue++
  }
  return <span data-board-totals className="flex flex-wrap gap-x-3 gap-y-1 text-xs/[18px] font-normal text-muted-foreground">
    <span>{label('hours', { hours: hours(tenths / 10), n: unestimated })}</span>
    <span>{label(unknown ? 'overdueUnknown' : 'overdue', { n: overdue, unknown })}</span>
  </span>
}

function leadPerson(rows: TaskRow[], kind: 'discipline' | 'project'): { id?: string; name: string } {
  const facts = rows.map(row => { const fact = (row as TaskRow & BoardRow).board; return kind === 'discipline' ? fact?.disciplineLead : fact?.projectManager })
  if (!facts.length || facts.some(fact => !fact)) return { name: t('common.unavailable') }
  const fingerprints = new Set(facts.map(fact => JSON.stringify([fact?.state, fact?.id, fact?.name])))
  if (fingerprints.size !== 1) return { name: t('common.unavailable') } // A later page can carry a separately-read changed lead.
  const fact = facts[0]!
  if (fact.state === 'Unassigned' && !fact.id) return { name: t('ind.unassigned') }
  return fact.state === 'Known' && fact.id && fact.name?.trim() ? { id: fact.id, name: fact.name } : { name: t('common.unavailable') }
}

export function BoardSwimlaneFacts({ rows, kind, focusKey }: { rows: TaskRow[]; kind: string; focusKey: string }) {
  const lead = kind === 'discipline' || kind === 'project' ? leadPerson(rows, kind) : null
  return <div data-board-swimlane-facts className="space-y-1">
    {(kind === 'discipline' || kind === 'project') && <p className="text-xs/[18px] text-muted-foreground">{label(kind === 'discipline' ? 'disciplineLead' : 'projectManager', { name: '' }).trim()} {lead?.id ? <PersonPanelTrigger userId={lead.id} name={lead.name} focusKey={focusKey} /> : lead?.name}</p>}
    <BoardCardTotals rows={rows} />
  </div>
}

export const boardCardCount = (n: number) => label(n === 1 ? 'cardOne' : 'cardMany', { n })

/** A missing recorded reviewer differs from a recorded reference whose person cannot be read. */
export function BoardReviewer({ row, personLinks = true, focusKey }: { row: TaskRow; personLinks?: boolean; focusKey: string }) {
  return <span data-board-reviewer={row.id}>{!row.reviewerId ? t('ind.unassigned') : personLinks && row.reviewerName?.trim() ? <PersonPanelTrigger userId={row.reviewerId} name={row.reviewerName} focusKey={focusKey} /> : row.reviewerName?.trim() || t('common.unavailable')}</span>
}

/** Server-derived age is tied to the canonical Ready-for-Review entry, never Last activity or In-Review entry. */
export function BoardReviewWaiting({ row }: { row: TaskRow }) {
  const fact = (row as TaskRow & BoardRow).board
  const known = !!fact?.reviewRequestedAt && Number.isFinite(Date.parse(fact.reviewRequestedAt))
    && typeof fact.reviewWaitingDays === 'number' && Number.isInteger(fact.reviewWaitingDays) && fact.reviewWaitingDays >= 0
    && typeof fact.reviewStaleDays === 'number' && Number.isInteger(fact.reviewStaleDays) && fact.reviewStaleDays >= 0
  const stale = known && fact!.reviewWaitingDays! >= fact!.reviewStaleDays!
  return <p data-board-review-waiting={row.id} data-board-review-stale={stale || undefined} className={cn('text-xs/[18px]', stale ? 'font-medium text-warn' : 'text-muted-foreground')}>
    {stale && <span aria-hidden>▲ </span>}{known ? label(fact!.reviewWaitingDays === 1 ? 'waitingOne' : 'waitingMany', { n: fact!.reviewWaitingDays! }) : label('waitingUnavailable')}
  </p>
}

export function BoardFiguresScope() {
  return <p data-board-figures-scope className="text-xs/[18px] text-muted-foreground">{label('shownOnly')}</p>
}
