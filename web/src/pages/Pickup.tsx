import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Loading, Page } from '@/components/hub/common'
import { Key } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { typedRequest } from '@/lib/api'
import { invalidateChange } from '@/lib/keys'
import { t } from '@/lib/i18n'
import { weekOf } from '@/components/hub/people'
import { ago, fmtDate, today } from '@/lib/format'
import { errorText } from './projects/Tasks'
import { useScopeProjects, useScope } from '@/components/hub/workspace'

export interface PickupOffer {
  id: string; taskId: string; offeredById: string; offeredBy: string; reason: string; hoursRemaining?: number | null
  neededBy?: string | null; note?: string | null; openedAt: string
  task: { key: string; name: string; projectId: string; projectNumber?: string; projectDisciplineId: string; disciplineName?: string; dueDate?: string | null; rowVersion?: number }
  rowVersion?: number
  allowed?: { pickUp: boolean; assign: boolean; withdraw?: boolean }
  candidates?: { id: string; displayName: string }[]
}

/** Open for pickup (§53.3). The server remains authoritative for team and visibility rules. */
export function PickupPage() {
  const [sp, setSp] = useSearchParams()
  const scope = useScope()
  const projects = useScopeProjects(scope)
  const qc = useQueryClient()
  const filters = { projectId: sp.get('projectId') ?? '', disciplineId: sp.get('disciplineId') ?? '', neededBy: sp.get('neededBy') ?? '' }
  const q = useQuery({ queryKey: ['pickup-offers', filters], queryFn: () => typedRequest('get', '/api/v1/pickup-offers', { query: filters }) })
  const disciplines = [...new Map((q.data ?? []).map((o) => [o.task.projectDisciplineId, o.task.disciplineName ?? o.task.projectDisciplineId])).entries()]
  const [selected, setSelected] = useState<PickupOffer | null>(null)
  const [busy, setBusy] = useState(false)
  const pick = async () => {
    if (!selected) return
    setBusy(true)
    try { await typedRequest('post', '/api/v1/tasks/{id}/pickup', { params: { id: selected.taskId }, body: { rowVersion: selected.task.rowVersion ?? selected.rowVersion ?? null } }); toast.success('Task picked up'); setSelected(null); invalidateChange(qc, 'TaskPickupOffer', selected.task.projectId, selected.taskId) }
    catch (e) { toast.error(errorText(e)) } finally { setBusy(false) }
  }
  return <Page title="Open for pickup" subtitle="Work offered by people in your team">
    <div className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-4">
      <label className="grid gap-1 text-sm">Project<select className="h-9 rounded-md border bg-background px-2" value={filters.projectId} onChange={(e) => { const n = new URLSearchParams(sp); e.target.value ? n.set('projectId', e.target.value) : n.delete('projectId'); setSp(n, { replace: true }) }}><option value="">Any project</option>{projects.data?.map((p) => <option key={p.id} value={p.id}>{p.projectNumber} {p.name}</option>)}</select></label>
      <label className="grid gap-1 text-sm">Discipline<select className="h-9 rounded-md border bg-background px-2" value={filters.disciplineId} onChange={(e) => { const n = new URLSearchParams(sp); e.target.value ? n.set('disciplineId', e.target.value) : n.delete('disciplineId'); setSp(n, { replace: true }) }}><option value="">Any discipline</option>{disciplines.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label>
      <label className="grid gap-1 text-sm">Needed by<input type="date" className="h-9 rounded-md border bg-background px-2" value={filters.neededBy} onChange={(e) => { const n = new URLSearchParams(sp); e.target.value ? n.set('neededBy', e.target.value) : n.delete('neededBy'); setSp(n, { replace: true }) }} /></label>
    </div>
    {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
    {q.isPending ? <Loading rows={5} /> : q.data?.length ? <div className="overflow-hidden rounded-lg border bg-card">
      <ul className="divide-y" aria-label="Open for pickup">
        {q.data.map((o) => <li key={o.id} className="flex flex-wrap items-center gap-3 p-4">
          <Key>{o.task.key}</Key><span className="min-w-48 flex-1 font-medium">{o.task.name}</span>
          <span className="text-sm text-muted-foreground">{o.reason}</span>
          <span className="text-xs text-muted-foreground">{o.task.projectNumber ?? projects.data?.find(p => p.id === o.task.projectId)?.projectNumber} · {o.task.disciplineName} · {o.offeredBy} · {ago(o.openedAt)} · {t('pickup.remaining', { h: o.hoursRemaining ?? '—' })}</span>
          <span className="text-xs text-muted-foreground">Needed by {fmtDate(o.neededBy)} · Due {fmtDate(o.task.dueDate)}</span>
          <OwnLoadHint date={o.neededBy ?? undefined} />
          <Button size="sm" onClick={() => setSelected(o)}>Pick up</Button>
        </li>)}
      </ul>
    </div> : <div className="rounded-lg border bg-card"><Empty>No tasks are open for pickup.</Empty></div>}
    <Dialog open={selected !== null} onOpenChange={(open) => !open && setSelected(null)}>
      <DialogContent><DialogHeader><DialogTitle>Pick up {selected?.task.key}</DialogTitle></DialogHeader>
        <p className="text-sm text-muted-foreground">This reassigns the whole task to you immediately. The current assignee becomes a watcher.</p>
        <DialogFooter><Button variant="outline" onClick={() => setSelected(null)}>Cancel</Button><Button disabled={busy} onClick={pick}>{busy ? 'Picking up…' : 'Pick up task'}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  </Page>
}


function OwnLoadHint({ date }: { date?: string }) {
  const week = weekOf(date ?? today())
  const q = useQuery({ queryKey: ['pickup-own-load', week], queryFn: () => typedRequest('get', '/api/v1/pickup-offers/my-load', { query: { week } }) })
  if (q.error) return <span className="text-xs text-muted-foreground">{t('pickup.loadUnavailable')}</span>
  if (!q.data) return null
  return <span className="text-xs text-muted-foreground">{q.data.partial ? t('workload.partial') : t('pickup.ownLoad', { week: fmtDate(q.data.week), band: q.data.band ?? '—', hours: q.data.hours ?? '—', capacity: q.data.capacity })}</span>
}
