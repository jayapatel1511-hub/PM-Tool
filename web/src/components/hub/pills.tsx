import { cn } from '@/lib/utils'
import { t, tv } from '@/lib/i18n'

// Status colour language (§13.0): every colour carries text and an icon; colour is never the only signal.
type Tone = 'ok' | 'warn' | 'bad' | 'idle' | 'done' | 'work'
const TONE: Record<Tone, { cls: string; icon: string }> = {
  ok: { cls: 'bg-ok-bg text-ok border-ok/25', icon: '●' },
  warn: { cls: 'bg-warn-bg text-warn border-warn/25', icon: '▲' },
  bad: { cls: 'bg-bad-bg text-bad border-bad/25', icon: '■' },
  idle: { cls: 'bg-idle-bg text-idle border-idle/20', icon: '○' },
  done: { cls: 'bg-done-bg text-done border-done/25', icon: '✓' },
  work: { cls: 'bg-work-bg text-work border-work/25', icon: '◐' },
}

const STATUS_TONE: Record<string, Tone> = {
  'On Track': 'ok', Green: 'ok',
  'At Risk': 'warn', Yellow: 'warn', Deferred: 'warn', Monitoring: 'warn', 'Revision Required': 'warn',
  Overdue: 'bad', Red: 'bad', Blocked: 'bad', Critical: 'bad', Realised: 'bad',
  'Not Started': 'idle', 'On Hold': 'idle', Grey: 'idle', Cancelled: 'idle', Setup: 'idle', Archived: 'idle', Pending: 'idle', Closed: 'idle',
  Complete: 'done', Issued: 'done', Accepted: 'done', Decided: 'done', Resolved: 'done', 'Ready to Issue': 'done',
  'In Progress': 'work', 'In Review': 'work', 'Ready for Review': 'work', Active: 'work', 'Under Review': 'work', Open: 'work',
}

export function toneOf(status?: string | null): Tone { return (status && STATUS_TONE[status]) || 'idle' }

export function Pill({ tone, children, className, title, icon = true }: { tone: Tone; children: React.ReactNode; className?: string; title?: string; icon?: boolean }) {
  const s = TONE[tone]
  return (
    <span title={title} className={cn('inline-flex items-center gap-1 whitespace-nowrap rounded-full border px-2 py-0.5 text-xs font-medium leading-4', s.cls, className)}>
      {icon && <span aria-hidden className="text-[9px]">{s.icon}</span>}{children}
    </span>
  )
}

export function StatusPill({ status, className }: { status?: string | null; className?: string }) {
  if (!status) return <span className="text-muted-foreground">{t('common.dash')}</span>
  return <Pill tone={toneOf(status)} className={className}>{tv(status)}</Pill>
}

export function HealthPill({ health, label, className }: { health?: string | null; label?: string; className?: string }) {
  const h = health ?? 'Grey'
  return <Pill tone={toneOf(h)} className={className}>{label ? `${label}: ` : ''}{t(`health.${h}`)}</Pill>
}

const PRIORITY: Record<string, string> = {
  Critical: 'bg-bad text-white border-bad', High: 'bg-bad-bg text-bad border-bad/30', Medium: 'bg-warn-bg text-warn border-warn/30', Low: 'bg-muted text-muted-foreground border-border',
}
export function PriorityBadge({ priority }: { priority?: string | null }) {
  if (!priority) return null
  return <span className={cn('inline-flex rounded-md border px-1.5 py-0.5 text-xs font-medium', PRIORITY[priority])}>{tv(priority)}</span>
}

/** Small indicator chip with icon and short text ("Overdue 3d", "Blocked", "Waiting", "Review r2"). */
export function Chip({ tone, children, title, onClick }: { tone: Tone; children: React.ReactNode; title?: string; onClick?: () => void }) {
  const s = TONE[tone]
  const cls = cn('inline-flex items-center gap-1 whitespace-nowrap rounded border px-1.5 py-px text-[11px] font-medium', s.cls, onClick && 'cursor-pointer hover:brightness-95')
  return onClick
    ? <button type="button" title={title} onClick={onClick} className={cls}><span aria-hidden className="text-[8px]">{s.icon}</span>{children}</button>
    : <span title={title} className={cls}><span aria-hidden className="text-[8px]">{s.icon}</span>{children}</span>
}

export function Key({ children }: { children: React.ReactNode }) {
  return <span className="key text-muted-foreground">{children}</span>
}

export function ProgressBar({ pct, label }: { pct?: number | null; label?: string }) {
  if (pct == null) return <span className="text-muted-foreground">{t('common.dash')}</span>
  return (
    <span className="inline-flex items-center gap-2" title={label}>
      <span className="h-1.5 w-16 overflow-hidden rounded-full bg-muted" role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} aria-label={label ?? `${pct}%`}>
        <span className="block h-full rounded-full bg-primary" style={{ width: `${pct}%` }} />
      </span>
      <span className="text-xs tabular-nums">{pct}%</span>
    </span>
  )
}
