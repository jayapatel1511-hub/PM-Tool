import { useQueryClient } from '@tanstack/react-query'
import { ChevronDown } from 'lucide-react'
import { useState } from 'react'
import { ErrorBanner } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { t } from '@/lib/i18n'
import { fmtTime } from '@/lib/format'
import { invalidateNotifications, invalidatePreferences, refreshProject } from '@/lib/keys'

const levels = ['AllActivity', 'MyItemsOnly', 'Muted'] as const
type Entry = components['schemas']['NotificationScreenRow']
const deliveryStates = ['Pending', 'PendingAvailability', 'HeldAway', 'HeldNoDigest', 'Queued', 'ReservedForDigest', 'Accepted', 'Simulated',
  'Unknown', 'Uncertain', 'Exhausted', 'ChannelOff', 'NoAddress', 'Muted', 'AccessRemoved', 'RecipientInactive', 'Duplicate', 'Mixed', 'Emailed', 'Partial'] as const
const oneOf = <T extends string>(values: readonly T[], value: string): value is T => values.some(known => known === value)
const occurrenceCount = (value: unknown): value is number => typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= 2147483647
const acceptanceTime = (value: string | null | undefined): value is string => {
  if (typeof value !== 'string') return false
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$/.exec(value)
  if (!match) return false
  const [, y, m, d, h, min, s, offset] = match, year = Number(y), month = Number(m), day = Number(d)
  const offsetHours = Number(offset.slice(1, 3)), offsetMinutes = Number(offset.slice(4))
  return year > 0 && month >= 1 && month <= 12 && day >= 1 && day <= new Date(Date.UTC(year, month, 0)).getUTCDate()
    && Number(h) <= 23 && Number(min) <= 59 && Number(s) <= 59
    && (offset === 'Z' || offsetHours <= 14 && offsetMinutes <= 59 && (offsetHours < 14 || offsetMinutes === 0)) && Number.isFinite(Date.parse(value))
}

/** Public delivery facts only; neither notification presence nor a queued/simulated attempt proves email acceptance. */
function NotificationDelivery({ entry }: { entry: Entry }) {
  const delivery = entry.screen.delivery
  const unknown = <span data-notification-delivery="Unknown">{t('notifScreen.deliveryUnknown')}</span>
  if (!delivery || !oneOf(deliveryStates, delivery.state) || delivery.state === 'Unknown'
    || !oneOf(['Known', 'Partial'] as const, delivery.historyCoverage)) return unknown
  const { knownOccurrences: known, totalOccurrences: total, realEmailedOccurrences: real, uncertainOccurrences: uncertain, acceptedAt } = delivery
  if (!occurrenceCount(known) || !occurrenceCount(total) || !occurrenceCount(real) || !occurrenceCount(uncertain) || total <= 0
    || total !== entry.notification.count || known <= 0 || known > total || real + uncertain > known
    || (delivery.historyCoverage === 'Known' ? known !== total : known >= total)) return unknown
  const emailed = delivery.state === 'Emailed' && delivery.historyCoverage === 'Known' && known === real && real === total && uncertain === 0 && acceptanceTime(acceptedAt)
  if (delivery.state === 'Emailed' && !emailed || delivery.state !== 'Emailed' && acceptedAt != null
    || delivery.state !== 'Emailed' && real === total
    || real > 0 && delivery.state !== 'Emailed' && delivery.state !== 'Partial'
    || uncertain > 0 && delivery.state !== 'Uncertain' && delivery.state !== 'Partial'
    || delivery.historyCoverage === 'Partial' && !['Mixed', 'Partial', 'Uncertain'].includes(delivery.state)
    || delivery.state === 'Simulated' && (real !== 0 || uncertain !== 0)) return unknown
  return <span className="inline-flex min-w-0 flex-col gap-1" data-notification-delivery={delivery.state} data-notification-delivery-coverage={delivery.historyCoverage}>
    <span>{t(`notifScreen.deliveryState.${delivery.state}`)}</span>
    <span>{t(`notifScreen.deliveryCoverage.${delivery.historyCoverage}`)} · {t(total === 1 ? 'notifScreen.deliveryCountsOne' : 'notifScreen.deliveryCounts', { known, total })}</span>
    {real > 0 && <span>{t(real === 1 ? 'notifScreen.deliveryRealCountOne' : 'notifScreen.deliveryRealCount', { n: real })}</span>}
    {uncertain > 0 && <span>{t(uncertain === 1 ? 'notifScreen.deliveryUncertainCountOne' : 'notifScreen.deliveryUncertainCount', { n: uncertain })}</span>}
    {emailed && <time dateTime={acceptedAt!}>{t('notifScreen.deliveryAcceptedAt', { time: fmtTime(acceptedAt) })}</time>}
  </span>
}

/** S45/S47: sibling to the open/read button; delivery wording follows the recorded public receipt projection. */
export function NotificationRowActions({ entry }: { entry: Entry }) {
  const qc = useQueryClient(), n = entry.notification, follow = entry.screen.follow
  const [busy, setBusy] = useState(false), [error, setError] = useState<unknown>(null), [unavailable, setUnavailable] = useState(false)
  const valid = !unavailable && follow && follow.projectId === n.projectId && follow.projectNumber === n.projectNumber
    && (follow.level == null || levels.some(level => level === follow.level))
  const change = async (level: typeof levels[number]) => {
    if (!valid || !follow || busy) return
    setBusy(true); setError(null)
    try {
      const reply = await typedRequest('put', '/api/v1/projects/{id}/follow', { params: { id: follow.projectId }, body: { level } })
      if (reply.level !== level || reply.source !== 'Manual') { setUnavailable(true); throw new Error(t('notifScreen.followUnavailable')) }
      await Promise.all([invalidateNotifications(qc), invalidatePreferences(qc)])
      refreshProject(qc, follow.projectId)
    } catch (cause) {
      if (cause instanceof ApiError && (cause.status === 403 || cause.status === 404)) setUnavailable(true)
      setError(cause)
    } finally { setBusy(false) }
  }
  return <div className="space-y-2">
    <div className="flex flex-wrap items-center gap-x-3 gap-y-2 text-xs text-muted-foreground">
      {valid && follow ? <DropdownMenu><DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm" aria-busy={busy} aria-label={t('notifScreen.followFor', { project: follow.projectNumber })}
          title={busy ? t('notifScreen.followSaving') : undefined}>
          <span className="key">{follow.projectNumber}</span><span>{t(`follow.${levels.find(level => level === follow.level) ?? 'none'}`)}</span><ChevronDown className="size-3.5" />
        </Button>
      </DropdownMenuTrigger><DropdownMenuContent align="start"><DropdownMenuRadioGroup value={follow.level ?? ''}>
        {levels.map(level => <DropdownMenuRadioItem key={level} value={level} disabled={busy} onSelect={() => void change(level)}>{t(`follow.${level}`)}</DropdownMenuRadioItem>)}
      </DropdownMenuRadioGroup></DropdownMenuContent></DropdownMenu> : n.projectNumber ? <span><span className="key">{n.projectNumber}</span> · {t('notifScreen.followUnavailable')}</span> : null}
      {entry.screen.needsYourAction === true && <span className="font-medium text-foreground">{t('notifScreen.needsAction')}</span>}
      <NotificationDelivery entry={entry} />
    </div>
    {error != null && <ErrorBanner error={error} />}
  </div>
}
