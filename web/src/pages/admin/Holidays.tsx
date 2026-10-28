import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Field, Section, Spinner, selectCls } from '@/components/hub/common'
import { DataTable } from '@/components/hub/registers'
import { useTable, type Column } from '@/components/hub/table'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useReference } from '@/hooks/data'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { invalidateChange } from '@/lib/keys'

type HolidayRow = components['schemas']['HolidayRow']

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
  const q = useQuery({ queryKey: ['admin', 'holidays', year, office], queryFn: () => typedRequest('get', '/api/v1/admin/holidays', { query: { year, officeId: office === ORG ? undefined : office || undefined } }) })
  const rows = (q.data ?? []).filter((h) => office !== ORG || !h.officeId)
  const offices = (ref.data?.offices ?? []).filter((o) => o.isActive)
  const refresh = () => invalidateChange(qc, 'Holiday')
  const add = async () => {
    setErr(null); setBusy(true)
    try {
      await typedRequest('post', '/api/v1/admin/holidays', { body: { officeId: f.office === ORG ? null : f.office, date: f.date, name: f.name } })
      toast.success(t('common.saved')); setF({ ...f, date: '', name: '' }); refresh()
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  const remove = async (h: HolidayRow) => { try { await typedRequest('delete', '/api/v1/admin/holidays/{id}', { params: { id: h.id } }); refresh() } catch (e) { toast.error((e as Error).message) } }
  // Every column is fixed: the page has no Columns menu and no sort, so a `?cols=` link cannot hide one (§13.0).
  const columns: Column<HolidayRow>[] = [
    { id: 'date', label: t('holiday.date'), fixed: true, className: 'whitespace-nowrap tabular-nums', cell: (h) => fmtDate(h.date) },
    { id: 'name', label: t('common.name'), fixed: true, className: 'font-medium', cell: (h) => h.name },
    { id: 'calendar', label: t('holiday.calendar'), fixed: true, className: 'text-muted-foreground', cell: (h) => h.office ?? t('holiday.org') },
    { id: 'actions', label: t('common.actions'), fixed: true, srOnly: true, className: 'py-0.5 text-right', cell: (h) => (
      <Button variant="ghost" size="icon-sm" className="text-bad hover:text-bad" aria-label={t('holiday.remove', { name: h.name })} onClick={() => remove(h)}><Trash2 className="size-4" /></Button>
    ) },
  ]
  const table = useTable('hub.holidays.columns', columns, rows, (h) => [h.date, h.name])
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
        {q.error ? <div className="p-5"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div> : (
          <DataTable flush table={table} rows={table.sorted} loading={q.isPending} empty={<Empty>{t('holiday.empty', { year })}</Empty>} current={() => false} caption={t('holiday.list')} />
        )}
      </Section>
    </div>
  )
}
