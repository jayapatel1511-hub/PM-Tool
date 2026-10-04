import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Field, Loading, Section, Spinner, selectCls, tdCls, thCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useReference } from '@/hooks/data'
import { ApiError, del, get, post, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { ADMIN_EXTRA } from './Admin'

interface HolidayRow { id: string; officeId?: string | null; office?: string | null; date: string; name: string }

const ORG = '__org'

/** Holiday calendars (§10.4, packet 021): each office's statutory holidays, plus the organisation's that apply to every
 *  office. They count only when the organisation counts thresholds in working days (Settings). */
export function Holidays() {
  const qc = useQueryClient()
  const ref = useReference()
  const [year, setYear] = useState(new Date().getFullYear())
  const [office, setOffice] = useState<string>('')
  const [f, setF] = useState<{ office: string; date: string; name: string }>({ office: ORG, date: '', name: '' })
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const q = useQuery({ queryKey: ['admin', 'holidays', year, office], queryFn: () => get<HolidayRow[]>(`admin/holidays${qs({ year, officeId: office === ORG ? null : office || null })}`) })
  const rows = (q.data ?? []).filter((h) => office !== ORG || !h.officeId)
  const offices = (ref.data?.offices ?? []).filter((o) => o.isActive)
  const refresh = () => qc.invalidateQueries({ queryKey: ['admin', 'holidays'] })
  const add = async () => {
    setErr(null); setBusy(true)
    try {
      await post('admin/holidays', { officeId: f.office === ORG ? null : f.office, date: f.date, name: f.name })
      toast.success(t('common.saved')); setF({ ...f, date: '', name: '' }); refresh()
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  const remove = async (h: HolidayRow) => { try { await del(`admin/holidays/${h.id}`); refresh() } catch (e) { toast.error((e as Error).message) } }
  return (
    <div className="space-y-6">
      <p className="max-w-3xl text-base/6 text-muted-foreground">{t('holiday.intro')}</p>
      <Section title={t('holiday.add')}>
        <form className="flex flex-wrap items-end gap-3 p-5" onSubmit={(e) => { e.preventDefault(); add() }}>
          <Field label={t('holiday.calendar')} htmlFor="h-office" className="w-full sm:w-64">
            <select id="h-office" className={selectCls} value={f.office} onChange={(e) => setF({ ...f, office: e.target.value })}>
              <option value={ORG}>{t('holiday.org')}</option>{offices.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
            </select>
          </Field>
          <Field label={t('holiday.date')} htmlFor="h-date" error={err?.fieldErrors.date} className="w-full sm:w-44"><Input id="h-date" type="date" required value={f.date} onChange={(e) => setF({ ...f, date: e.target.value })} /></Field>
          <Field label={t('common.name')} htmlFor="h-name" error={err?.fieldErrors.name} className="w-full sm:w-64"><Input id="h-name" required placeholder={t('holiday.namePlaceholder')} value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
          <Button type="submit" disabled={busy || !f.date || !f.name.trim()}>{busy ? <Spinner /> : <Plus className="size-4" />}{busy ? t('common.saving') : t('common.add')}</Button>
        </form>
        {err && !Object.keys(err.fieldErrors).length && <div className="px-5 pb-5"><ErrorBanner error={err} /></div>}
      </Section>
      <Section title={t('holiday.list')}>
        <div className="flex flex-wrap items-end gap-3 border-b px-5 py-4">
          <Field label={t('holiday.calendar')} htmlFor="h-filter" className="w-full sm:w-64">
            <select id="h-filter" className={selectCls} value={office} onChange={(e) => setOffice(e.target.value)}>
              <option value="">{t('holiday.all')}</option><option value={ORG}>{t('holiday.org')}</option>{offices.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
            </select>
          </Field>
          <Field label={t('holiday.year')} htmlFor="h-year" className="w-28">
            <Input id="h-year" type="number" className="tabular-nums" value={year} min={2000} max={2100} onChange={(e) => setYear(Number(e.target.value) || year)} />
          </Field>
        </div>
        {q.error ? <div className="p-5"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div> : q.isPending ? <Loading rows={4} /> : rows.length === 0 ? <Empty>{t('holiday.empty', { year })}</Empty> : (
          <div className="scroll-region overflow-x-auto">
            <table className="w-full text-sm">
              <caption className="sr-only">{t('holiday.list')}</caption>
              <thead className="bg-muted">
                <tr><th scope="col" className={thCls}>{t('holiday.date')}</th><th scope="col" className={thCls}>{t('common.name')}</th><th scope="col" className={thCls}>{t('holiday.calendar')}</th><th scope="col" className={thCls}><span className="sr-only">{t('common.actions')}</span></th></tr>
              </thead>
              <tbody>{rows.map((h) => (
                <tr key={h.id} className="border-t hover:bg-muted">
                  <td className={cn(tdCls, 'whitespace-nowrap tabular-nums')}>{fmtDate(h.date)}</td>
                  <td className={cn(tdCls, 'font-medium')}>{h.name}</td>
                  <td className={cn(tdCls, 'text-muted-foreground')}>{h.office ?? t('holiday.org')}</td>
                  <td className={cn(tdCls, 'py-0.5 text-right')}>
                    <Button variant="ghost" size="icon-sm" className="text-bad hover:text-bad" aria-label={t('holiday.remove', { name: h.name })} onClick={() => remove(h)}><Trash2 className="size-4" /></Button>
                  </td>
                </tr>))}
              </tbody>
            </table>
          </div>
        )}
      </Section>
    </div>
  )
}

ADMIN_EXTRA.push({ to: '/admin/holidays', label: t('holiday.title') })
