import { Chip } from '@/components/hub/pills'
import { t } from '@/lib/i18n'

// Derived indicators (§10.3) as chips with icon and short text; computed by the rules engine (packet 005).

export interface DeliverableStateView {
  progressPct?: number | null; taskTotal: number; taskComplete: number; taskCancelled: number; taskOpen: number; taskOverdue: number; taskBlocked: number
  isOverdue: boolean; daysOverdue: number; isDueSoon: boolean; isAtRisk: boolean; atRiskReasons?: { text: string }[] | null; isUnassigned: boolean; isStale: boolean
  isDateInconsistent: boolean; inconsistencyDetail?: { text: string }[] | null; slipDays: number; isInactiveOwner: boolean; issuedWithOpenWork: boolean
  milestoneCancelled: boolean; isWaiting?: boolean; isBlocked?: boolean; blockingCount?: number
  blockedBy?: { key?: string; name?: string; reason?: string; blocking: boolean }[] | null
}

export function DeliverableIndicators({ s }: { s?: DeliverableStateView | null }) {
  if (!s) return null
  return (
    <span className="flex flex-wrap gap-1">
      {s.isOverdue && <Chip tone="bad">{t('ind.overdueD', { n: s.daysOverdue })}</Chip>}
      {s.isAtRisk && <Chip tone="warn" title={s.atRiskReasons?.map((r) => r.text).join('\n')}>{t('ind.atRisk')}</Chip>}
      {s.isDueSoon && !s.isOverdue && <Chip tone="warn">{t('ind.dueSoon')}</Chip>}
      {s.isBlocked && <Chip tone="bad">{t('ind.blocked')}</Chip>}
      {s.isWaiting && !s.isBlocked && <Chip tone="warn">{t('ind.waiting')}</Chip>}
      {(s.blockingCount ?? 0) > 0 && <Chip tone="bad">{t('ind.blocking', { n: s.blockingCount })}</Chip>}
      {s.isUnassigned && <Chip tone="warn">{t('ind.unassigned')}</Chip>}
      {s.isInactiveOwner && <Chip tone="bad">{t('ind.inactiveOwner')}</Chip>}
      {s.isDateInconsistent && <Chip tone="warn" title={s.inconsistencyDetail?.map((r) => r.text).join('\n')}>{t('ind.dateInconsistent')}</Chip>}
      {s.slipDays > 0 && <Chip tone="warn">{t('ind.slipped', { n: s.slipDays })}</Chip>}
      {s.isStale && <Chip tone="idle">{t('ind.stale')}</Chip>}
      {s.issuedWithOpenWork && <Chip tone="warn">{t('ind.issuedOpenWork')}</Chip>}
      {s.milestoneCancelled && <Chip tone="warn">{t('ind.milestoneCancelled')}</Chip>}
    </span>
  )
}

export function progressLabel(s?: DeliverableStateView | null) {
  if (!s || s.taskTotal - s.taskCancelled === 0) return undefined
  return t('deliverable.tasksOf', { n: s.taskComplete, total: s.taskTotal - s.taskCancelled })
}
