import { useContext } from 'react'
import { Ctx } from '@/lib/auth-context'
import { t } from '@/lib/i18n'
import './title-block.css'

export type TitleBlockReference = { label: string; value: string }

/** These are instants from the response, formatted in the organisation's zone. */
export function formatReferenceTime(value: string, zone?: string | null): string | null {
  if (!zone || !Number.isFinite(Date.parse(value))) return null
  try {
    const parts = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(value))
    const get = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)?.value ?? ''
    return `${get('year')}-${get('month')}-${get('day')} ${get('hour')}:${get('minute')}`
  } catch { return null }
}

export function TitleBlock({ asOf, timeZone, references = [] }: { asOf?: string | null; timeZone?: string | null; references?: TitleBlockReference[] }) {
  const auth = useContext(Ctx)
  if (auth?.me.flags?.visual_system !== true || !asOf) return null
  const zone = timeZone ?? auth.me.settings.orgTimeZone
  const rows = [{ label: t('visual.currentValues'), value: asOf }, ...references]
  return <dl className="visual-title-block" aria-label={t('visual.currentValues')}>{rows.map((r) => {
    const formatted = formatReferenceTime(r.value, zone)
    return <div key={`${r.label}:${r.value}`}><dt>{r.label}</dt><dd className="font-mono tabular-nums">{formatted ? <time dateTime={r.value}>{formatted}</time> : t('common.unavailable')}</dd></div>
  })}</dl>
}
