import { useQuery } from '@tanstack/react-query'
import { get } from '@/lib/api'
import { useFlag } from '@/lib/auth'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
export interface DateContextRead { dates: { earliestStart?: { date: string }; earliestDue?: { date: string }; latestDue?: { date: string }; inconsistencies: { text: string }[]; successors: { id: string; key: string; reason: { text: string } }[]; newInconsistencies: { text: string }[] }; availability?: { date: string; message: string; nextAvailableDate?: string }[]; tasks?: { id: string; key: string; reason: { text: string } }[] }
export function DateHint({ type, id, startDate, dueDate }: { type: string; id: string; startDate?: string | null; dueDate?: string | null }) {
  const enabled = useFlag('date_context')
  const params = new URLSearchParams(); if (startDate !== undefined) params.set('startDate', startDate ?? ''); if (dueDate !== undefined) params.set('dueDate', dueDate ?? '')
  const q = useQuery({ queryKey: ['date-context', type, id, startDate, dueDate], enabled, staleTime: 0, queryFn: () => get<DateContextRead>(`items/${type}/${id}/date-context?${params}`) })
  if (!enabled || !q.data) return null
  const d = q.data.dates
  return <span>{d.earliestStart && <span className="block">{t('interaction.earliestStart', { date: fmtDate(d.earliestStart.date) })}</span>}{d.earliestDue && <span className="block">{t('interaction.earliestDue', { date: fmtDate(d.earliestDue.date) })}</span>}{d.latestDue && <span className="block">{t('interaction.latestDue', { date: fmtDate(d.latestDue.date) })}</span>}{d.inconsistencies.map((r,i)=><span className="block" key={i}>{r.text}</span>)}{q.data.availability?.map(a=><span className="block" key={a.date}>{fmtDate(a.date)} · {t('interaction.unavailable')}{a.nextAvailableDate && ` · ${t('interaction.nextAvailable', { date: fmtDate(a.nextAvailableDate) })}`}</span>)}</span>
}
