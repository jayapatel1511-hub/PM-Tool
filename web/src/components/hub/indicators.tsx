import type { ReactNode } from 'react'
import { Field, selectCls } from '@/components/hub/common'
import { Chip, type Tone } from '@/components/hub/pills'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { t } from '@/lib/i18n'

// Derived indicators (§10.3) as chips with icon and short text; computed by the rules engine (packet 005).

export interface DeliverableStateView {
  progressPct?: number | null; taskTotal: number; taskComplete: number; taskCancelled: number; taskOpen: number; taskOverdue: number; taskBlocked: number
  isOverdue: boolean; daysOverdue: number; isDueSoon: boolean; isAtRisk: boolean; atRiskReasons?: { text: string }[] | null; isUnassigned: boolean; isStale: boolean
  isDateInconsistent: boolean; inconsistencyDetail?: { text: string }[] | null; slipDays: number; isInactiveOwner: boolean; issuedWithOpenWork: boolean
  milestoneCancelled: boolean; isWaiting?: boolean; isBlocked?: boolean; blockingCount?: number
  blockedBy?: { key?: string; name?: string; reason?: string; blocking: boolean }[] | null; notes?: string[] | null
}

/** Evaluator note codes (§10.3) with their human labels; unknown codes are not shown. */
export const NOTE_LABEL: Record<string, string> = {
  predecessor_cancelled: 'ind.predecessorCancelled', decision_cancelled: 'ind.decisionCancelled', deliverable_issued: 'ind.deliverableIssued', assignee_not_on_project: 'ind.notOnProject',
  finished_before_predecessor: 'ind.finishedBeforePredecessor',
}

/** One indicator: its tone, its text, and optionally the node that renders it (for a chip that opens its reasons). */
export interface Signal { key: string; tone: Tone; label: string; title?: string; node?: ReactNode }
const RANK: Record<Tone, number> = { bad: 0, warn: 1, work: 2, idle: 3, ok: 4, done: 5 }

/** One signal per row (§41 R7): the most severe indicator, then "+n" whose tooltip lists the rest. `all` shows every
 *  indicator, for detail panels. Severity is the tone (red, then amber, then the rest); ties keep the caller's order. */
export function OneSignal({ signals, all }: { signals: Signal[]; all?: boolean }) {
  if (!signals.length) return null
  const list = [...signals].sort((a, b) => RANK[a.tone] - RANK[b.tone])
  const chip = (s: Signal) => s.node ?? <Chip key={s.key} tone={s.tone} title={s.title}>{s.label}</Chip>
  if (all) return <span className="flex flex-wrap gap-1">{list.map((s) => <span key={s.key} className="contents">{chip(s)}</span>)}</span>
  const [first, ...rest] = list
  return (
    <span className="inline-flex items-center gap-1 whitespace-nowrap" data-signals={list.length}>
      {chip(first)}
      {rest.length > 0 && (
        <Tooltip>
          <TooltipTrigger asChild>
            <button type="button" className="inline-flex min-h-5 items-center rounded-md border px-1.5 text-xs/[18px] font-medium text-muted-foreground tabular-nums hover:text-foreground"
              aria-label={t('signal.more', { n: rest.length, list: rest.map((s) => s.label).join(', ') })}>+{rest.length}</button>
          </TooltipTrigger>
          <TooltipContent className="max-w-72 text-left"><ul className="space-y-0.5">{rest.map((s) => <li key={s.key}>{s.label}</li>)}</ul></TooltipContent>
        </Tooltip>
      )}
    </span>
  )
}

function deliverableSignals(s: DeliverableStateView): Signal[] {
  const out: (Signal | false)[] = [
    s.isOverdue && { key: 'overdue', tone: 'bad', label: t('ind.overdueD', { n: s.daysOverdue }) },
    s.isAtRisk && { key: 'atRisk', tone: 'warn', label: t('ind.atRisk'), title: s.atRiskReasons?.map((r) => r.text).join('\n') },
    s.isDueSoon && !s.isOverdue && { key: 'dueSoon', tone: 'warn', label: t('ind.dueSoon') },
    !!s.isBlocked && { key: 'blocked', tone: 'bad', label: t('ind.blocked') },
    !!s.isWaiting && !s.isBlocked && { key: 'waiting', tone: 'warn', label: t('ind.waiting') },
    (s.blockingCount ?? 0) > 0 && { key: 'blocking', tone: 'bad', label: t('ind.blocking', { n: s.blockingCount }) },
    s.isUnassigned && { key: 'unassigned', tone: 'warn', label: t('ind.unassigned') },
    s.isInactiveOwner && { key: 'inactiveOwner', tone: 'bad', label: t('ind.inactiveOwner') },
    s.isDateInconsistent && { key: 'dateInconsistent', tone: 'warn', label: t('ind.dateInconsistent'), title: s.inconsistencyDetail?.map((r) => r.text).join('\n') },
    s.slipDays > 0 && { key: 'slipped', tone: 'warn', label: t('ind.slipped', { n: s.slipDays }) },
    s.isStale && { key: 'stale', tone: 'idle', label: t('ind.stale') },
    s.issuedWithOpenWork && { key: 'issuedOpenWork', tone: 'warn', label: t('ind.issuedOpenWork') },
    s.milestoneCancelled && { key: 'milestoneCancelled', tone: 'warn', label: t('ind.milestoneCancelled') },
    ...(s.notes ?? []).filter((n) => NOTE_LABEL[n]).map((n) => ({ key: n, tone: 'warn' as const, label: t(NOTE_LABEL[n]) })),
  ]
  return out.filter(Boolean) as Signal[]
}

/** A deliverable's indicators: one per row in lists, every one with `all` in its panel. */
export function DeliverableIndicators({ s, all }: { s?: DeliverableStateView | null; all?: boolean }) {
  if (!s) return null
  return <OneSignal signals={deliverableSignals(s)} all={all} />
}

export function progressLabel(s?: DeliverableStateView | null) {
  if (!s || s.taskTotal - s.taskCancelled === 0) return undefined
  return t('deliverable.tasksOf', { n: s.taskComplete, total: s.taskTotal - s.taskCancelled })
}

// ---------- Dependency link types (§40, FR-DEPT-01, AC-DEPT-01) ----------

const DEP_TYPES = ['FinishToStart', 'StartToStart', 'FinishToFinish'] as const
const DEP_CODE: Record<string, string> = { FinishToStart: 'FS', StartToStart: 'SS', FinishToFinish: 'FF' }

/** The link's type as a two-letter chip with its lag ("SS +2d"); the full name and lag are in the title and for screen readers. */
export function DepTypeChip({ type, lag }: { type?: string | null; lag: number }) {
  const kind = type ?? 'FinishToStart'
  const full = lag > 0 ? t('dep.typeWithLag', { type: t(`dep.type.${kind}`), n: lag }) : t(`dep.type.${kind}`)
  return (
    <span title={full} className="inline-flex shrink-0 items-center rounded-md border px-1.5 text-xs/[18px] font-medium text-muted-foreground tabular-nums">
      <span aria-hidden>{DEP_CODE[kind] ?? kind}{lag > 0 && ` +${lag}d`}</span><span className="sr-only">{full}</span>
    </span>
  )
}

/** The link type select with the hint for the chosen type, used by the task and deliverable link dialogs. */
export function DepTypeField({ id, value, onChange }: { id: string; value: string; onChange: (v: string) => void }) {
  return (
    <Field label={t('dep.type')} htmlFor={id} hint={t(`dep.typeHint.${value}`)}>
      <select id={id} className={selectCls} value={value} onChange={(e) => onChange(e.target.value)}>
        {DEP_TYPES.map((x) => <option key={x} value={x}>{t(`dep.type.${x}`)}</option>)}
      </select>
    </Field>
  )
}
