import { MessageSquare } from 'lucide-react'
import { useLocation, useNavigate } from 'react-router'
import { useFlag } from '@/lib/auth'
import { ago, fmtTime } from '@/lib/format'
import { t, type KeyIn } from '@/lib/i18n'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import type { TaskRow } from './Tasks'
import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'

type Row = Pick<TaskRow, 'id' | 'key' | 'lastActivityAt' | 'lastActivityActorName' | 'commentCount'> & {
  lastActivityEventAt?: string | null; lastActivityActorId?: string | null; lastActivityActorType?: string | null
}
const label = (key: KeyIn<'workActivity.'>, values?: Record<string, string | number>) => t(`workActivity.${key}`, values)

/** Existing page facts only: a missing actor is unknown, never an inferred System write. */
export function TaskLastActivity({ row, focusKey }: { row: Row; focusKey?: string }) {
  const people = useFlag('people_screens')
  const at = 'lastActivityEventAt' in row ? row.lastActivityEventAt : row.lastActivityAt
  const knownTime = typeof at === 'string' && Number.isFinite(Date.parse(at))
  const actor = typeof row.lastActivityActorName === 'string' && row.lastActivityActorName.trim() ? row.lastActivityActorName : label('actorUnavailable')
  const knownUser = people && row.lastActivityActorId && (row.lastActivityActorType === 'User' || row.lastActivityActorType === 'Admin') && !!row.lastActivityActorName?.trim()
  return <span role="group" data-work-activity={row.id} className="inline-flex min-w-0 flex-wrap gap-x-1 text-xs text-muted-foreground" aria-label={t('task.lastActivity')}
    title={knownTime ? fmtTime(at) : label('timeUnavailable')}>
    {knownTime ? <time dateTime={at}>{ago(at)}</time> : <span>{label('timeUnavailable')}</span>}
    <span aria-hidden>·</span>{knownUser ? <PersonPanelTrigger userId={row.lastActivityActorId!} name={actor} focusKey={focusKey ?? `task:${row.id}:last-activity:actor`} className="break-words" /> : <span className="break-words">{actor}</span>}
  </span>
}

/** The exact panel and section are set atomically; no closed row fetches its comments/history. */
export function TaskCommentCount({ row }: { row: Row }) {
  const enabled = useFlag('work_screens'), location = useLocation(), navigate = useNavigate()
  if (!enabled) return null
  if (!Number.isInteger(row.commentCount) || row.commentCount < 0)
    return <span className="text-xs text-muted-foreground">{label('commentsUnavailable')}</span>
  if (row.commentCount === 0) return null
  return <button type="button" data-work-comments={row.id} className="inline-flex min-h-8 shrink-0 items-center gap-1 rounded-sm px-1 text-xs text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
    aria-label={label('openComments', { key: row.key, n: row.commentCount })} onClick={event => {
      const next = new URLSearchParams(location.search)
      next.set('panel', `Task:${row.id}`); next.set('taskSection', 'comments')
      const prior = location.state && typeof location.state === 'object' && !Array.isArray(location.state) ? location.state : {}
      navigate(`${location.pathname}?${next}${location.hash}`, { state: { ...prior, recordContextReturn: captureRecordContextOrigin(location, event.currentTarget) } })
    }}><MessageSquare className="size-3.5" aria-hidden /><span className="tabular-nums">{row.commentCount}</span></button>
}

export function TaskActivityComments({ row, focusKey }: { row: Row; focusKey?: string }) {
  const enabled = useFlag('work_screens')
  return enabled ? <span className="inline-flex min-w-0 flex-wrap items-center gap-x-2"><TaskLastActivity row={row} focusKey={focusKey} /><TaskCommentCount row={row} /></span> : null
}
