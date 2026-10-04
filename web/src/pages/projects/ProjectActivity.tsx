import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router'
import { ActivityTable, type ActivityRow } from '@/components/hub/activity'
import { ActiveFilters, Empty, ErrorBanner, Field, FilterBar, Loading, Page, selectCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { get, qs } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import { useItemPanel } from '@/components/hub/panel-host'
import { useCurrentProject } from './ProjectLayout'
import { ExportMenu } from '@/components/hub/export'
import { PeoplePicker } from '@/components/hub/people'

const CATEGORIES = ['status', 'assignment', 'date', 'deletion', 'creation', 'comment', 'decision', 'health', 'dependency', 'structure']
const TYPES = ['Project', 'Milestone', 'Deliverable', 'Task', 'Decision', 'Dependency', 'Comment', 'DocumentLink', 'Member', 'Discipline', 'TimeEntry', 'Risk', 'Issue', 'Action']
const FILTERS = ['from', 'to', 'itemType', 'category', 'disciplineId', 'importantOnly', 'actorId'] as const

/** Project Activity History (§13.14, FR-VIEW-03, FR-AUD-02/03): filters in the URL, read-only, exportable. */
export function ProjectActivityTab() {
  const p = useCurrentProject()
  const [sp, setSp] = useSearchParams()
  const open = useItemPanel()
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); if (k !== 'page') n.delete('page'); setSp(n, { replace: true }) }
  const params = { from: sp.get('from'), to: sp.get('to'), itemType: sp.get('itemType'), category: sp.get('category'), disciplineId: sp.get('disciplineId'),
    importantOnly: sp.get('importantOnly') === 'true', actorId: sp.get('actorId') }
  const page = Number(sp.get('page') ?? 1)
  const q = useQuery({ queryKey: ['p', p.id, 'activity', params, page], queryFn: () => get<{ items: ActivityRow[]; totalCount: number }>(`projects/${p.id}/activity${qs({ ...params, page, pageSize: 50 })}`) })
  // Every row of an actor-filtered page carries that person's name, so the picker and the token can show it.
  const actorName = params.actorId ? q.data?.items.find((r) => r.actorId === params.actorId)?.actorName : undefined
  const tokens = ([
    ['from', t('common.from'), params.from && fmtDate(params.from)],
    ['to', t('common.to'), params.to && fmtDate(params.to)],
    ['itemType', t('common.item'), params.itemType && t(`itemType.${params.itemType}`)],
    ['category', t('common.action'), params.category && t(`category.${params.category}`)],
    ['disciplineId', t('common.discipline'), p.disciplines.find((d) => d.id === params.disciplineId)?.name],
    ['importantOnly', t('activity.important'), t('common.yes')],
    ['actorId', t('activity.actor'), actorName],
  ] as const).filter(([k]) => sp.get(k))
  const clear = () => { const n = new URLSearchParams(sp); for (const k of FILTERS) n.delete(k); n.delete('page'); setSp(n, { replace: true }) }
  return (
    <Page title={t('ptab.activity')} actions={<ExportMenu path={`projects/${p.id}/activity/export`} params={params} name={`${p.projectNumber}-activity`} />}>
      <FilterBar>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t('common.from')} htmlFor="pa-from" className="w-full sm:w-40"><Input id="pa-from" type="date" value={sp.get('from') ?? ''} onChange={(e) => set('from', e.target.value)} /></Field>
          <Field label={t('common.to')} htmlFor="pa-to" className="w-full sm:w-40"><Input id="pa-to" type="date" value={sp.get('to') ?? ''} onChange={(e) => set('to', e.target.value)} /></Field>
          <Field label={t('common.item')} htmlFor="pa-item" className="w-full sm:w-44">
            <select id="pa-item" className={selectCls} value={sp.get('itemType') ?? ''} onChange={(e) => set('itemType', e.target.value)}>
              <option value="">{t('activity.anyItem')}</option>{TYPES.map((x) => <option key={x} value={x}>{t(`itemType.${x}`)}</option>)}
            </select>
          </Field>
          <Field label={t('common.action')} htmlFor="pa-action" className="w-full sm:w-48">
            <select id="pa-action" className={selectCls} value={sp.get('category') ?? ''} onChange={(e) => set('category', e.target.value)}>
              <option value="">{t('activity.anyAction')}</option>{CATEGORIES.map((x) => <option key={x} value={x}>{t(`category.${x}`)}</option>)}
            </select>
          </Field>
          <Field label={t('common.discipline')} htmlFor="pa-discipline" className="w-full sm:w-44">
            <select id="pa-discipline" className={selectCls} value={sp.get('disciplineId') ?? ''} onChange={(e) => set('disciplineId', e.target.value)}>
              <option value="">{t('projects.anyDiscipline')}</option>{p.disciplines.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          <Field label={t('activity.actor')} htmlFor="pa-actor" className="w-full sm:w-56">
            <PeoplePicker id="pa-actor" value={params.actorId} valueName={actorName} onChange={(v) => set('actorId', v)} placeholder={t('activity.anyActor')} />
          </Field>
          <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={params.importantOnly} onCheckedChange={(c) => set('importantOnly', c ? 'true' : null)} />{t('activity.important')}</label>
        </div>
        <ActiveFilters tokens={tokens.map(([key, label, value]) => ({ key, label, value: value ?? t('common.dash') }))} onRemove={(k) => set(k, null)} onClear={clear} />
      </FilterBar>
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {q.isPending ? <div className="rounded-lg border bg-card"><Loading /></div> : q.data && (q.data.items.length === 0 && tokens.length > 0
        ? <div className="rounded-lg border bg-card"><Empty action={<Button variant="outline" onClick={clear}>{t('filters.clear')}</Button>}>{t('activity.empty')}</Empty></div>
        : <ActivityTable rows={q.data.items} total={q.data.totalCount} page={page} pageSize={50} onPage={(n) => set('page', String(n))}
            onOpen={(r) => r.itemId && open(r.itemType, r.itemId)} />)}
    </Page>
  )
}
