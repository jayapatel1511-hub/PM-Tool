import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router'
import { ActivityTable } from '@/components/hub/activity'
import { ErrorBanner, Loading } from '@/components/hub/common'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { get, qs } from '@/lib/api'
import { t } from '@/lib/i18n'
import { useItemPanel } from '@/components/hub/panel-host'
import { useCurrentProject } from './ProjectLayout'
import { ExportMenu } from '@/components/hub/export'
import { PeoplePicker } from '@/components/hub/people'

const CATEGORIES = ['status', 'assignment', 'date', 'deletion', 'creation', 'comment', 'decision', 'health', 'dependency', 'structure']
const TYPES = ['Project', 'Milestone', 'Deliverable', 'Task', 'Decision', 'Dependency', 'Comment', 'DocumentLink', 'Member', 'Discipline', 'TimeEntry', 'Risk', 'Issue', 'Action']

/** Project Activity History (§13.14, FR-VIEW-03, FR-AUD-02/03): filters in the URL, read-only, exportable. */
export function ProjectActivityTab() {
  const p = useCurrentProject()
  const [sp, setSp] = useSearchParams()
  const open = useItemPanel()
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); if (k !== 'page') n.delete('page'); setSp(n, { replace: true }) }
  const params = { from: sp.get('from'), to: sp.get('to'), itemType: sp.get('itemType'), category: sp.get('category'), disciplineId: sp.get('disciplineId'),
    importantOnly: sp.get('importantOnly') === 'true', actorId: sp.get('actorId') }
  const page = Number(sp.get('page') ?? 1)
  const q = useQuery({ queryKey: ['p', p.id, 'activity', params, page], queryFn: () => get(`projects/${p.id}/activity${qs({ ...params, page, pageSize: 50 })}`) })
  return (
    <div className="space-y-3 p-4">
      <div className="flex flex-wrap items-end gap-2">
        <label className="text-xs text-muted-foreground">{t('common.from')}<Input type="date" className="h-8 w-40" value={sp.get('from') ?? ''} onChange={(e) => set('from', e.target.value)} /></label>
        <label className="text-xs text-muted-foreground">{t('common.to')}<Input type="date" className="h-8 w-40" value={sp.get('to') ?? ''} onChange={(e) => set('to', e.target.value)} /></label>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={sp.get('itemType') ?? ''} onChange={(e) => set('itemType', e.target.value)} aria-label={t('common.item')}>
          <option value="">{t('activity.anyItem')}</option>{TYPES.map((x) => <option key={x} value={x}>{t(`itemType.${x}`)}</option>)}
        </select>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={sp.get('category') ?? ''} onChange={(e) => set('category', e.target.value)} aria-label={t('common.action')}>
          <option value="">{t('activity.anyAction')}</option>{CATEGORIES.map((x) => <option key={x} value={x}>{t(`category.${x}`)}</option>)}
        </select>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={sp.get('disciplineId') ?? ''} onChange={(e) => set('disciplineId', e.target.value)} aria-label={t('common.discipline')}>
          <option value="">{t('projects.anyDiscipline')}</option>{p.disciplines.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
        <div className="w-48"><PeoplePicker value={params.actorId} onChange={(v) => set('actorId', v)} placeholder={t('activity.anyActor')} label={t('activity.actor')} /></div>
        <label className="flex h-8 items-center gap-2 text-sm"><Checkbox checked={params.importantOnly} onCheckedChange={(c) => set('importantOnly', c ? 'true' : null)} />{t('activity.important')}</label>
        <div className="flex-1" />
        <ExportMenu path={`projects/${p.id}/activity/export`} params={params} name={`${p.projectNumber}-activity`} />
      </div>
      {q.error && <ErrorBanner error={q.error} />}
      {q.isPending ? <Loading /> : <ActivityTable rows={q.data.items} total={q.data.totalCount} page={page} pageSize={50} onPage={(n) => set('page', String(n))}
        onOpen={(r) => r.itemId && open(r.itemType, r.itemId)} />}
    </div>
  )
}
