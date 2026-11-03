import { PersonPanelTrigger } from './person-panel-trigger'
import { WorkNewBadge, WorkActivityComments, type WorkSeenFields } from './work-seen'
import { useQuery } from '@tanstack/react-query'
import { BellOff } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useLocation, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import type { components } from '@/gen/api'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { RecordContext } from '@/components/hub/record-context'
import { Key, Pill } from '@/components/hub/pills'
import { Why, type Reason } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { typedRequest } from '@/lib/api'
import { captureRecordContextOrigin } from '@/lib/record-context-navigation'
import { useFlag, useMe } from '@/lib/auth'
import { fmtDate } from '@/lib/format'
import { runtimeText, t, tv } from '@/lib/i18n'
import { itemTypeKeys } from '@/gen/registry.gen'
import { cn } from '@/lib/utils'

// Attention items (§12.12): ranked rule firings with the rule, threshold and values behind each (AC-ATT-06); never dismissible, only snoozed (ATT-03).

export type AttentionItem = components['schemas']['AttentionRow'] & WorkSeenFields
export type AttentionList = components['schemas']['AttentionList']

const TONE: Record<string, 'bad' | 'warn' | 'idle'> = { Critical: 'bad', Warning: 'warn', Info: 'idle' }
const KNOWN_KINDS = new Set<string>(itemTypeKeys)

/** One project's attention list with "Why?" and snooze for the PM and leads (FR-ATT-01..03). */
export function AttentionPanel({ projectId, canSnooze, refused, limit, disciplineId, severity, groupByKind = false }: { projectId: string; canSnooze: boolean; refused?: string; limit?: number; disciplineId?: string; severity?: string; groupByKind?: boolean }) {
  const withContext = useFlag('screen_patterns')
  const [legacySnoozed, setLegacySnoozed] = useState(false)
  const [sp, setSp] = useSearchParams()
  const showSnoozed = groupByKind ? sp.get('attentionSnoozed') === 'true' : legacySnoozed
  const q = useQuery({ queryKey: ['p', projectId, 'attention', { showSnoozed, disciplineId, severity }],
    queryFn: () => typedRequest('get', '/api/v1/projects/{id}/attention', { params: { id: projectId }, query: { includeSnoozed: showSnoozed || undefined, disciplineId, severity } }) })
  const groups = new Map<string, AttentionItem[]>()
  for (const item of q.data?.items ?? []) { const kind = KNOWN_KINDS.has(`itemType.${item.itemType}`) ? item.itemType : '__other'; const rows = groups.get(kind) ?? []; rows.push(item); groups.set(kind, rows) }
  const requested = sp.get('attentionKind'), selected = requested && groups.has(requested) ? requested : groups.keys().next().value as string | undefined
  useEffect(() => {
    if (!groupByKind || q.isFetching || q.error || !q.data || requested === selected || (!selected && !requested && !sp.has('attentionAll'))) return
    const next = new URLSearchParams(sp); if (selected) next.set('attentionKind', selected); else next.delete('attentionKind'); next.delete('attentionAll'); setSp(next, { replace: true })
  }, [groupByKind, q.isFetching, q.error, q.data, selected, requested, sp, setSp])
  if (q.isPending) return <Loading rows={3} />
  if (q.error) return <div className="p-3"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div>
  const footer = (q.data.snoozed > 0 || showSnoozed) && <button className="hover:text-foreground" onClick={() => {
    if (!groupByKind) { setLegacySnoozed(!showSnoozed); return }
    const next = new URLSearchParams(sp); if (showSnoozed) next.delete('attentionSnoozed'); else next.set('attentionSnoozed', 'true'); setSp(next, { replace: true })
  }}>{showSnoozed ? t('attention.hideSnoozed') : t('attention.showSnoozed', { n: q.data.snoozed })}</button>
  if (groupByKind) return <div data-attention-groups>
    {groups.size > 0 && <div className="flex flex-wrap gap-2 border-b px-5 py-3" role="group" aria-label={t('dashboardScreens.attentionKinds')}>
      {[...groups].map(([kind, items]) => <Button key={kind} type="button" size="sm" variant={kind === selected ? 'secondary' : 'ghost'} aria-pressed={kind === selected} onClick={() => {
        const next = new URLSearchParams(sp); next.set('attentionKind', kind); next.delete('attentionAll'); setSp(next, { replace: true })
      }}>{kind === '__other' ? t('dashboardScreens.unknownKind') : runtimeText(`itemType.${kind}`)} <span className="tabular-nums">{q.data.truncated ? t('dashboardScreens.atLeastCount', { n: items.length }) : items.length}</span></Button>)}
    </div>}
    {q.data.truncated && <p className="px-5 py-2 text-xs text-muted-foreground">{t('dashboardScreens.attentionTruncated')}</p>}
    <AttentionRows key={`${projectId}:${selected ?? ''}`} items={selected ? groups.get(selected) ?? [] : []} canSnooze={canSnooze} refused={refused} limit={5}
      withContext={withContext} retainOrigin expanded={requested === selected && sp.get('attentionAll') === 'true'}
      onExpandedChange={all => { const next = new URLSearchParams(sp); if (selected) next.set('attentionKind', selected); if (all) next.set('attentionAll', 'true'); else next.delete('attentionAll'); setSp(next, { replace: true }) }}
      showAllLabel={q.data.truncated ? t('dashboardScreens.showLoaded', { n: selected ? groups.get(selected)?.length ?? 0 : 0 }) : undefined} onChanged={() => q.refetch()} footer={footer} />
  </div>
  return (
    <AttentionRows items={q.data.items} canSnooze={canSnooze} refused={refused} limit={limit} withContext={withContext} onChanged={() => q.refetch()}
      footer={footer} />
  )
}

/** Ranked attention rows: severity, item, rule with "Why?", message, owner and age (§12.12, AC-ATT-06). */
export function AttentionRows({ items, canSnooze, refused, limit, onChanged, footer, showProject, withContext = false, retainOrigin = false, expanded, onExpandedChange, showAllLabel }: {
  items: AttentionItem[]; canSnooze: boolean; refused?: string; limit?: number; onChanged: () => void; footer?: React.ReactNode; showProject?: boolean; withContext?: boolean
  retainOrigin?: boolean; expanded?: boolean; onExpandedChange?: (all: boolean) => void; showAllLabel?: string
}) {
  const openPanel = useItemPanel()
  const location = useLocation(), me = useMe()
  const [snoozing, setSnoozing] = useState<AttentionItem | null>(null)
  const [all, setAll] = useState(false)
  const isAll = expanded ?? all
  const shown = limit && !isAll ? items.slice(0, limit) : items
  return (
    <div>
      {items.length === 0 ? <Empty>{t('attention.none')}</Empty> : (
        <ul className="divide-y">
          {shown.map((a) => (
            <li key={a.id} className={cn('flex flex-wrap items-start gap-3 px-5 py-3 text-sm', a.snoozed && 'text-muted-foreground')}>
              {withContext && <div className="w-full">{a.context ? <RecordContext context={a.context} projectNumber={a.projectNumber ?? a.projectId} meId={me.id} openPanel={openPanel} /> : <p className="w-full text-xs text-muted-foreground">{t('recordContext.unavailable')}</p>}</div>}
              <Pill tone={TONE[a.severity] ?? 'idle'} className="mt-0.5">{tv(a.severity)}</Pill>
              <div className="min-w-0 flex-1 basis-48">
                <div className="flex flex-wrap items-baseline gap-x-2">
                  {showProject && a.projectNumber && !a.itemKey?.startsWith(a.projectNumber) && <Key>{a.projectNumber}</Key>}
                  <button className="text-left font-medium hover:underline" onClick={event => withContext || retainOrigin ? openPanel(a.itemType, a.itemId, captureRecordContextOrigin(location, event.currentTarget)) : openPanel(a.itemType, a.itemId)}>{a.itemKey && <Key>{a.itemKey}</Key>} {a.itemName}</button>
                  <WorkNewBadge seen={a.seen} />
                  <Why reasons={a.why ? [a.why as Reason] : []} title={`${a.ruleId} ${a.ruleName}`}><span className="text-xs text-muted-foreground">{a.ruleId} {a.ruleName}</span></Why>
                </div>
                <div className="text-sm text-muted-foreground">{a.message}</div>
                <div className="text-xs/[18px] text-muted-foreground">
                  {a.ownerName && <span>{a.ownerUserId ? <PersonPanelTrigger userId={a.ownerUserId} name={a.ownerName} focusKey={`attention:${a.id}:owner`}>{withContext ? t('attention.alertOwner', { name: a.ownerName }) : a.ownerName}</PersonPanelTrigger> : withContext ? t('attention.alertOwner', { name: a.ownerName }) : a.ownerName} · </span>}{t('attention.age', { n: a.ageDays })}
                  {a.snoozed && <span> · {t('attention.snoozedUntil', { date: fmtDate(a.snoozedUntil), note: a.snoozeNote ?? '' })}</span>}
                </div>
                {a.workActivity && <WorkActivityComments itemType={a.itemType} itemId={a.itemId} itemKey={a.itemKey ?? a.itemName ?? ''} projectNumber={a.projectNumber ?? undefined} fact={a.workActivity} focusKey={`attention:${a.id}:activity`} />}
              </div>
              {canSnooze && !a.snoozed && <Button size="sm" variant="ghost" disabled={!!refused} title={refused} onClick={() => setSnoozing(a)} aria-label={t('attention.snoozeItem', { item: a.itemKey ?? a.itemName ?? '' })}><BellOff className="size-3.5" />{t('attention.snooze')}</Button>}
            </li>
          ))}
        </ul>
      )}
      {((limit && items.length > limit) || footer) && (
        <div className="flex flex-wrap items-center gap-3 border-t px-5 py-2.5 text-sm text-muted-foreground">
          {limit && items.length > limit && <button className="min-h-6 text-primary underline underline-offset-4" onClick={() => onExpandedChange ? onExpandedChange(!isAll) : setAll(!isAll)}>{isAll ? t('attention.showFewer') : showAllLabel ?? t('attention.showAll', { n: items.length })}</button>}
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
      onConfirm={async (note) => { await typedRequest('post', '/api/v1/attention/{id}/snooze', { params: { id: a.id }, body: { days: n, note } }); toast.success(t('attention.snoozed', { n })); onClose(true) }}>
      <Field label={t('attention.days')} htmlFor="snz-days" hint={t('attention.daysHint')}><Input id="snz-days" type="number" min={1} max={30} value={days} onChange={(e) => setDays(e.target.value)} /></Field>
    </ConfirmDialog>
  )
}
