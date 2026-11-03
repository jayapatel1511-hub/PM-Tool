import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Loading, Missing, Page, Spinner } from '@/components/hub/common'
import { PeoplePicker, weekOf } from '@/components/hub/people'
import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { Key, StatusPill } from '@/components/hub/pills'
import { itemHref } from '@/components/hub/search'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import type { components } from '@/gen/api'
import { typedRequest } from '@/lib/api'
import { useFlag, useMe } from '@/lib/auth'
import { fmtDate, today } from '@/lib/format'
import { t , runtimeText } from '@/lib/i18n'
import { invalidateChange } from '@/lib/keys'
import { accentOf } from '@/lib/utils'
import { errorText } from './projects/Tasks'

type OpenItem = components['schemas']['OpenItem']

const TYPE: Record<string, string> = { TaskAssignee: 'Task', TaskReviewer: 'Task', DeliverableOwner: 'Deliverable', DeliverableReviewer: 'Deliverable', DecisionOwner: 'Decision' }
const id = (i: OpenItem) => `${i.kind}:${i.id}`

/** Reassign work (FR-ADM-02, E-01, §13.15): everything one person owns across projects, reassigned one by one or all at once. */
export function ReassignWorkPage() {
  const { id: userId } = useParams()
  const me = useMe(), people = useFlag('people_screens')
  const bulkTasks = useFlag('work_screens')
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['open-work', userId, me.id, people], queryFn: () => typedRequest('get', '/api/v1/users/{id}/open-work', { params: { id: userId! } }) })
  const [picked, setPicked] = useState<Set<string>>(new Set())
  const [to, setTo] = useState<{ id: string; name?: string; active?: boolean } | null>(null)
  const [busy, setBusy] = useState(false)
  const byProject = useMemo(() => {
    const m = new Map<string, OpenItem[]>()
    for (const i of q.data?.items ?? []) m.set(`${i.projectNumber} ${i.projectName}`, [...(m.get(`${i.projectNumber} ${i.projectName}`) ?? []), i])
    return [...m.entries()]
  }, [q.data])
  const header = { eyebrow: t('nav.resources'), subtitle: t('reassign.subtitle') }
  if (q.isPending) return <Page {...header} title={t('reassign.action')}><Loading rows={6} /></Page>
  if (q.error) return <Page {...header} title={t('reassign.action')}><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const { person, items } = q.data
  const toggle = (k: string) => setPicked((s) => { const n = new Set(s); if (n.has(k)) n.delete(k); else n.add(k); return n })
  const submit = async () => {
    if (!to) return
    setBusy(true)
    try {
      const chosen = items.filter((i) => picked.has(id(i)))
      const r = await typedRequest('post', '/api/v1/users/{id}/reassign', { params: { id: userId! }, body: { items: chosen.map((i) => ({ kind: i.kind, id: i.id })), toUserId: to.id } })
      toast.success(t('reassign.done', { n: r.reassigned, name: to.name ?? '' }))
      r.skipped.forEach((s) => toast.warning(`${s.key}: ${s.reason}`))
      setPicked(new Set())
      void invalidateChange(qc, 'Task')
    } catch (e) { toast.error(errorText(e)) } finally { setBusy(false) }
  }
  return (
    <Page {...header} title={people ? <>{t('reassign.title', { name: '' })}<PersonPanelTrigger userId={person.id} name={person.displayName} active={person.isActive} focusKey={`reassign:${person.id}:source:heading`} /></> : t('reassign.title', { name: person.isActive ? person.displayName : t('common.inactiveSuffix', { name: person.displayName }) })}>
      {bulkTasks && <div><Button variant="outline" asChild><Link to={`/tasks?assigneeId=${encodeURIComponent(person.id)}&open=true&projects=all`}>{t('bulkCommand.departure')}</Link></Button></div>}
      {items.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('reassign.nothing')}</Empty></div> : (
        <>
          <div className="sticky top-0 z-10 flex flex-wrap items-center gap-3 rounded-lg border bg-card p-4 shadow-popover">
            <label className="flex min-h-(--control-h) items-center gap-2 text-sm font-medium"><Checkbox checked={picked.size === items.length} onCheckedChange={(c) => setPicked(c ? new Set(items.map(id)) : new Set())} />{t('reassign.all', { n: items.length })}</label>
            <span className="text-sm text-muted-foreground tabular-nums" aria-live="polite">{t('bulk.selected', { n: picked.size })}</span>
            <div className="flex-1" />
            <div className="w-60 max-w-full"><PeoplePicker value={to?.id} valueName={to?.name} exclude={[person.id]} onChange={(v, p) => setTo(v ? { id: v, name: p?.id === v ? p.displayName : undefined, active: p?.id === v ? p.isActive : undefined } : null)} placeholder={t('reassign.to')} label={t('reassign.to')} capacityWeek={weekOf(today())} /></div>
            {people && to?.name && <PersonPanelTrigger userId={to.id} name={to.name} active={to.active} focusKey={`reassign:${person.id}:destination:${to.id}:details`} className="inline-flex min-h-9 items-center text-sm">{t('common.details')}: {to.name}</PersonPanelTrigger>}
            <Button disabled={!to || picked.size === 0 || busy} onClick={submit}>{busy && <Spinner />}{t('reassign.go', { n: picked.size })}</Button>
          </div>
          {byProject.map(([project, list]) => (
            <section key={project} data-accent={accentOf(list[0].projectId)} className="overflow-hidden rounded-lg border border-t-4 border-t-(color:--acc-stripe) bg-card" aria-label={project}>
              <h2 className="border-b px-5 py-3 text-base/6 font-semibold">{project} <span className="ml-1 rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{list.length}</span></h2>
              <ul className="divide-y">
                {list.map((i) => (
                  <li key={id(i)} className="flex min-h-(--row-min) flex-wrap items-center gap-x-3 gap-y-1 px-5 py-(--cell-py) text-sm hover:bg-muted [&>.flex-1]:basis-full sm:[&>.flex-1]:basis-auto">
                    <Checkbox checked={picked.has(id(i))} onCheckedChange={() => toggle(id(i))} aria-label={t('reassign.pick', { item: `${i.key ?? ''} ${i.name}` })} />
                    <span className="w-32 text-xs text-muted-foreground">{runtimeText(`reassign.kind.${i.kind}`)}</span>
                    {i.key && <Key>{i.key}</Key>}
                    {TYPE[i.kind] ? <Link className="min-w-0 flex-1 break-words hover:underline" to={itemHref(TYPE[i.kind], i.projectNumber, i.id)}>{i.name}</Link> : <span className="min-w-0 flex-1 break-words">{i.name}</span>}
                    {i.status && <StatusPill status={i.status} />}
                    <span className="w-28 text-right text-xs text-muted-foreground tabular-nums">{i.dueDate ? fmtDate(i.dueDate) : <Missing />}</span>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </>
      )}
    </Page>
  )
}
