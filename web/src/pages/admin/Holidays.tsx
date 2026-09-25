import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Field, Loading, Section, Spinner, selectCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useReference } from '@/hooks/data'
import { ApiError, del, get, post, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
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
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">{t('holiday.intro')}</p>
      <Section title={t('holiday.add')}>
        <form className="flex flex-wrap items-end gap-3 p-4" onSubmit={(e) => { e.preventDefault(); add() }}>
          <Field label={t('holiday.calendar')} htmlFor="h-office">
            <select id="h-office" className={selectCls} value={f.office} onChange={(e) => setF({ ...f, office: e.target.value })}>
              <option value={ORG}>{t('holiday.org')}</option>{offices.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
            </select>
          </Field>
          <Field label={t('holiday.date')} htmlFor="h-date" error={err?.fieldErrors.date}><Input id="h-date" type="date" required value={f.date} onChange={(e) => setF({ ...f, date: e.target.value })} /></Field>
          <Field label={t('common.name')} htmlFor="h-name" error={err?.fieldErrors.name}><Input id="h-name" required placeholder={t('holiday.namePlaceholder')} value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
          <Button type="submit" disabled={busy || !f.date || !f.name.trim()}>{busy ? <Spinner /> : <Plus className="size-4" />}{t('common.add')}</Button>
        </form>
        {err && !Object.keys(err.fieldErrors).length && <div className="px-4 pb-4"><ErrorBanner error={err} /></div>}
      </Section>
      <Section title={t('holiday.list')} actions={
        <div className="flex gap-2">
          <select className="h-8 rounded-md border bg-card px-2 text-sm" value={office} onChange={(e) => setOffice(e.target.value)} aria-label={t('holiday.calendar')}>
            <option value="">{t('holiday.all')}</option><option value={ORG}>{t('holiday.org')}</option>{offices.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
          </select>
          <Input type="number" className="h-8 w-24" value={year} min={2000} max={2100} onChange={(e) => setYear(Number(e.target.value) || year)} aria-label={t('holiday.year')} />
        </div>
      }>
        {q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : q.isPending ? <Loading rows={4} /> : rows.length === 0 ? <Empty>{t('holiday.empty', { year })}</Empty> : (
          <table className="w-full text-sm">
            <thead className="bg-muted/60 text-left text-xs text-muted-foreground">
              <tr><th className="px-4 py-2 font-medium">{t('holiday.date')}</th><th className="px-3 py-2 font-medium">{t('common.name')}</th><th className="px-3 py-2 font-medium">{t('holiday.calendar')}</th><th className="px-3 py-2"><span className="sr-only">{t('common.actions')}</span></th></tr>
            </thead>
            <tbody>{rows.map((h) => (
              <tr key={h.id} className="border-t">
                <td className="whitespace-nowrap px-4 py-2 tabular-nums">{fmtDate(h.date)}</td><td className="px-3 py-2">{h.name}</td>
                <td className="px-3 py-2 text-muted-foreground">{h.office ?? t('holiday.org')}</td>
                <td className="px-3 py-2 text-right"><Button variant="ghost" size="icon" className="size-7" aria-label={t('holiday.remove', { name: h.name })} onClick={() => remove(h)}><Trash2 className="size-3.5" /></Button></td>
              </tr>))}
            </tbody>
          </table>
        )}
      </Section>
    </div>
  )
}

ADMIN_EXTRA.push({ to: '/admin/holidays', label: t('holiday.title') })
