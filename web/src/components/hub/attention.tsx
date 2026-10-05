import { useQuery } from '@tanstack/react-query'
import { BellOff } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Key, Pill } from '@/components/hub/pills'
import { Why, type Reason } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { get, post, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'

// Attention items (§12.12): ranked rule firings with the rule, threshold and values behind each (AC-ATT-06); never dismissible, only snoozed (ATT-03).

export interface AttentionItem {
  id: string; projectId: string; projectNumber?: string; ruleId: string; ruleName: string; itemType: string; itemId: string; itemKey?: string; itemName?: string
  severity: string; message: string; why?: Reason | null; ownerName?: string | null; daysOverdueOrBlocked: number; priority?: string; dueDate?: string
  firstDetectedAt: string; ageDays: number; snoozed: boolean; snoozedUntil?: string; snoozeNote?: string
}
export interface AttentionList { items: AttentionItem[]; snoozed: number; total: number }

const TONE: Record<string, 'bad' | 'warn' | 'idle'> = { Critical: 'bad', Warning: 'warn', Info: 'idle' }

/** One project's attention list with "Why?" and snooze for the PM and leads (FR-ATT-01..03). */
export function AttentionPanel({ projectId, canSnooze, limit, disciplineId, severity }: { projectId: string; canSnooze: boolean; limit?: number; disciplineId?: string; severity?: string }) {
  const [showSnoozed, setShowSnoozed] = useState(false)
  const q = useQuery({ queryKey: ['p', projectId, 'attention', { showSnoozed, disciplineId, severity }],
    queryFn: () => get<AttentionList>(`projects/${projectId}/attention${qs({ includeSnoozed: showSnoozed || undefined, disciplineId, severity })}`) })
  if (q.isPending) return <Loading rows={3} />
  if (q.error) return <div className="p-3"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  return (
    <AttentionRows items={q.data.items} canSnooze={canSnooze} limit={limit} onChanged={() => q.refetch()}
      footer={(q.data.snoozed > 0 || showSnoozed) && <button className="hover:text-foreground" onClick={() => setShowSnoozed(!showSnoozed)}>{showSnoozed ? t('attention.hideSnoozed') : t('attention.showSnoozed', { n: q.data.snoozed })}</button>} />
  )
}

/** Ranked attention rows: severity, item, rule with "Why?", message, owner and age (§12.12, AC-ATT-06). */
export function AttentionRows({ items, canSnooze, limit, onChanged, footer, showProject }: {
  items: AttentionItem[]; canSnooze: boolean; limit?: number; onChanged: () => void; footer?: React.ReactNode; showProject?: boolean
}) {
  const openPanel = useItemPanel()
  const [snoozing, setSnoozing] = useState<AttentionItem | null>(null)
  const [all, setAll] = useState(false)
  const shown = limit && !all ? items.slice(0, limit) : items
  return (
    <div>
      {items.length === 0 ? <Empty>{t('attention.none')}</Empty> : (
        <ul className="divide-y">
          {shown.map((a) => (
            <li key={a.id} className={cn('flex items-start gap-3 px-5 py-3 text-sm', a.snoozed && 'text-muted-foreground')}>
              <Pill tone={TONE[a.severity] ?? 'idle'} className="mt-0.5">{tv(a.severity)}</Pill>
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-baseline gap-x-2">
                  {showProject && a.projectNumber && <Key>{a.projectNumber}</Key>}
                  <button className="text-left font-medium hover:underline" onClick={() => openPanel(a.itemType, a.itemId)}>{a.itemKey && <Key>{a.itemKey}</Key>} {a.itemName}</button>
                  <Why reasons={a.why ? [a.why] : []} title={`${a.ruleId} ${a.ruleName}`}><span className="text-xs text-muted-foreground">{a.ruleId} {a.ruleName}</span></Why>
                </div>
                <div className="text-sm text-muted-foreground">{a.message}</div>
                <div className="text-xs/[18px] text-muted-foreground">
                  {a.ownerName && <span>{a.ownerName} · </span>}{t('attention.age', { n: a.ageDays })}
                  {a.snoozed && <span> · {t('attention.snoozedUntil', { date: fmtDate(a.snoozedUntil), note: a.snoozeNote ?? '' })}</span>}
                </div>
              </div>
              {canSnooze && !a.snoozed && <Button size="sm" variant="ghost" onClick={() => setSnoozing(a)} aria-label={t('attention.snoozeItem', { item: a.itemKey ?? a.itemName ?? '' })}><BellOff className="size-3.5" />{t('attention.snooze')}</Button>}
            </li>
          ))}
        </ul>
      )}
      {((limit && items.length > limit) || footer) && (
        <div className="flex flex-wrap items-center gap-3 border-t px-5 py-2.5 text-sm text-muted-foreground">
          {limit && items.length > limit && <button className="min-h-6 text-primary underline underline-offset-4" onClick={() => setAll(!all)}>{all ? t('attention.showFewer') : t('attention.showAll', { n: items.length })}</button>}
          {footer}
        </div>
      )}
      {snoozing && <SnoozeDialog a={snoozing} onClose={(ok) => { setSnoozing(null); if (ok) onChanged() }} />}
    </div>
  )
}

function SnoozeDialog({ a, onClose }: { a: AttentionItem; onClose: (ok: boolean) => void }) {
  const [days, setDays] = useState('7')
  const n = Number(days)
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} title={t('attention.snoozeTitle', { item: a.itemKey ?? a.itemName ?? '' })} body={t('attention.snoozeBody')}
      reason busy={!(n >= 1 && n <= 30)} confirmLabel={t('attention.snooze')}
      onConfirm={async (note) => { await post(`attention/${a.id}/snooze`, { days: n, note }); toast.success(t('attention.snoozed', { n })); onClose(true) }}>
      <Field label={t('attention.days')} htmlFor="snz-days" hint={t('attention.daysHint')}><Input id="snz-days" type="number" min={1} max={30} value={days} onChange={(e) => setDays(e.target.value)} /></Field>
    </ConfirmDialog>
  )
}
