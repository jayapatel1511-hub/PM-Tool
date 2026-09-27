import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Page } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { get, post } from '@/lib/api'
import { fmtDate, today } from '@/lib/format'
import { t } from '@/lib/i18n'
import { CommandForm, SelectField, type CoordOptions, WorkLink } from './CoordinationForms'
import { useCurrentProject } from './ProjectLayout'

type Allocation = { id: string; personId: string; personName: string; purpose: string; fromDate: string; throughDate: string; plannedHours: number; status: string; rowVersion: number }
type Detail = Allocation & { links: { workType: string; workId: string; workDate: string; reviewHours?: number }[]; days: { workDate: string; hours: number }[]; overCapacityWarningRecorded: boolean; canConfirm: boolean; canManage: boolean }
type Preview = { id: string; rowVersion: number; days: { date: string; availableHours: number; confirmedHours: number; proposedHours: number; resultingHours: number; overByHours: number; dateVersion: number }[] }

export function AllocationsTab() {
  const project = useCurrentProject(), qc = useQueryClient()
  const [selected, setSelected] = useState<string | null>(null), [adding, setAdding] = useState(false)
  const base = `projects/${project.id}/allocations`
  const list = useQuery({ queryKey: ['allocations', project.id], queryFn: () => get<Allocation[]>(base) })
  const options = useQuery({ queryKey: ['coord-options', project.id], queryFn: () => get<CoordOptions>(`projects/${project.id}/changes/options`) })
  const canPropose = project.permissions.isPm || (options.data?.manageDisciplineIds.length ?? 0) > 0
  const refresh = () => { qc.invalidateQueries({ queryKey: ['allocations', project.id] }); qc.invalidateQueries({ queryKey: ['workload'] }) }
  return <Page title={t('allocation.title')} subtitle={t('allocation.subtitle')}
    actions={canPropose && <Button size="sm" onClick={() => setAdding(true)}>{t('allocation.new')}</Button>}>
    {options.error && <ErrorBanner error={options.error} retry={() => options.refetch()} />}
    {list.isPending ? <Loading rows={4} /> : list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : !list.data.length ?
      <Empty>{t('allocation.empty')}</Empty> : <div className="overflow-x-auto rounded-lg border bg-card"><table className="w-full text-left text-sm">
        <caption className="sr-only">{t('allocation.title')}</caption><thead className="bg-muted/60"><tr>
          {[t('workload.person'), t('allocation.purpose'), t('allocation.dates'), t('allocation.hours'), t('common.status')].map(h => <th key={h} scope="col" className="p-3">{h}</th>)}
        </tr></thead><tbody>{list.data.map(a => <tr key={a.id} className="border-t">
          <td className="p-3"><button className="text-left font-medium text-primary underline" onClick={() => setSelected(a.id)}>{a.personName}</button></td>
          <td className="p-3">{t(`allocation.purpose.${a.purpose}`)}</td><td className="p-3 whitespace-nowrap">{fmtDate(a.fromDate)}–{fmtDate(a.throughDate)}</td>
          <td className="p-3 tabular-nums">{a.plannedHours}</td><td className="p-3">{a.status}</td>
        </tr>)}</tbody></table></div>}
    {adding && options.data && <Proposal base={base} options={options.data} close={() => setAdding(false)} done={id => { setAdding(false); refresh(); setSelected(id) }} />}
    {selected && <AllocationDetail base={base} id={selected} projectNumber={project.projectNumber} options={options.data}
      close={() => setSelected(null)} refresh={refresh} />}
  </Page>
}

function Proposal({ base, options, close, done }: { base: string; options: CoordOptions; close: () => void; done: (id: string) => void }) {
  const [personId, setPersonId] = useState(''), [fromDate, setFromDate] = useState(today()), [throughDate, setThroughDate] = useState(today())
  const [workDate, setWorkDate] = useState(today()), [plannedHours, setPlannedHours] = useState(''), [taskId, setTaskId] = useState('')
  const tasks = options.tasks.filter(w => w.ownerId === personId && !['Complete', 'Cancelled', 'On Hold'].includes(w.status))
  return <CommandForm path={base} title={t('allocation.new')} hint={t('allocation.proposalHint')} onClose={close} onDone={done}
    payload={() => ({ personId, purpose: 'Production', fromDate, throughDate, plannedHours: Number(plannedHours), days: [],
      links: [{ workType: 'Task', workId: taskId, workDate }] })}>
    <SelectField label={t('workload.person')} value={personId} onChange={v => { setPersonId(v); setTaskId('') }} choices={options.people.map(p => ({ value: p.id, label: p.displayName }))} />
    <SelectField label={t('allocation.task')} value={taskId} onChange={setTaskId} choices={tasks.map(w => ({ value: w.id, label: `${w.key} · ${w.name}` }))} />
    {!tasks.length && personId && <p className="text-sm text-muted-foreground">{t('allocation.noTasks')}</p>}
    <div className="grid gap-3 sm:grid-cols-2"><Field label={t('allocation.from')} htmlFor="allocation-from"><Input id="allocation-from" type="date" required value={fromDate} onChange={e => setFromDate(e.target.value)} /></Field>
      <Field label={t('allocation.through')} htmlFor="allocation-through"><Input id="allocation-through" type="date" required min={fromDate} value={throughDate} onChange={e => setThroughDate(e.target.value)} /></Field></div>
    <div className="grid gap-3 sm:grid-cols-2"><Field label={t('allocation.linkDate')} htmlFor="allocation-work"><Input id="allocation-work" type="date" required min={fromDate} max={throughDate} value={workDate} onChange={e => setWorkDate(e.target.value)} /></Field>
      <Field label={t('allocation.hours')} htmlFor="allocation-hours"><Input id="allocation-hours" type="number" required min="0.01" max="10000" step="0.01" value={plannedHours} onChange={e => setPlannedHours(e.target.value)} /></Field></div>
  </CommandForm>
}

function AllocationDetail({ base, id, projectNumber, options, close, refresh }: { base: string; id: string; projectNumber: string; options?: CoordOptions; close: () => void; refresh: () => void }) {
  const [preview, setPreview] = useState<Preview | null>(null), [action, setAction] = useState<'confirm' | 'decline' | 'cancel' | 'complete' | null>(null)
  const [error, setError] = useState<unknown>(null)
  const detail = useQuery({ queryKey: ['allocation-detail', base, id], queryFn: () => get<Detail>(`${base}/${id}`) })
  const row = detail.data
  const done = () => { setAction(null); setPreview(null); setError(null); detail.refetch(); refresh() }
  const startConfirm = async () => { setError(null); try { setPreview(await get<Preview>(`${base}/${id}/confirmation-preview`)); setAction('confirm') } catch (e) { setError(e) } }
  return <Dialog open onOpenChange={o => !o && close()}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
      <DialogHeader><DialogTitle>{t('allocation.detail')}</DialogTitle></DialogHeader>
      {detail.isPending ? <Loading rows={4} /> : detail.error ? <ErrorBanner error={detail.error} retry={() => detail.refetch()} /> : row && <div className="space-y-4 text-sm">
        <p>{row.personName} · {t(`allocation.purpose.${row.purpose}`)} · {row.status}</p>
        <p>{fmtDate(row.fromDate)}–{fmtDate(row.throughDate)} · {row.plannedHours} {t('allocation.hours')}</p>
        <h3 className="font-medium">{t('allocation.linked')}</h3><ul className="space-y-2">{row.links.map(l => <li key={`${l.workType}:${l.workId}:${l.workDate}`}>
          {l.workType === 'Review' ? <Link className="text-primary underline" to={`/projects/${projectNumber}/reviews`}>{t('allocation.reviewAssignment')}</Link> :
            options ? <WorkLink options={options} type={l.workType} id={l.workId} number={projectNumber} /> :
              <Link className="text-primary underline" to={`/projects/${projectNumber}/tasks?panel=Task:${l.workId}`}>{t('allocation.openTask')}</Link>} · {fmtDate(l.workDate)}{l.reviewHours != null && ` · ${l.reviewHours} h`}
        </li>)}</ul>
        <div className="flex flex-wrap gap-2">{row.status === 'Proposed' && row.canConfirm && <><Button size="sm" onClick={startConfirm}>{t('allocation.reviewCapacity')}</Button>
          <Button size="sm" variant="outline" onClick={() => setAction('decline')}>{t('allocation.decline')}</Button></>}
          {row.canManage && ['Proposed', 'Confirmed'].includes(row.status) && <Button size="sm" variant="outline" onClick={() => setAction('cancel')}>{t('allocation.cancel')}</Button>}
          {row.canManage && row.status === 'Confirmed' && <Button size="sm" variant="outline" onClick={() => setAction('complete')}>{t('allocation.complete')}</Button>}
        </div>{error != null && <ErrorBanner error={error} />}
      </div>}

    {action && row && <ConfirmDialog open onOpenChange={o => !o && setAction(null)} title={t(`allocation.${action}`)}
      body={action === 'confirm' ? t('allocation.confirmHint') : t('allocation.reasonHint')}
      reason={action === 'confirm' ? (preview?.days.some(d => d.overByHours > 0) ? true : false) : true}
      onConfirm={async reason => {
        if (action === 'confirm' && !preview) return
        const payload = action === 'confirm' ? { requestId: crypto.randomUUID(), rowVersion: preview!.rowVersion,
          dateVersions: preview!.days.map(d => ({ workDate: d.date, rowVersion: d.dateVersion })), overCapacityReason: reason || null } :
          { requestId: crypto.randomUUID(), rowVersion: row.rowVersion, reason }
        await post(`${base}/${id}/${action}`, payload); toast.success(t('common.saved')); done()
      }}>
      {action === 'confirm' && preview && <div className="max-h-56 overflow-auto rounded border text-sm"><table className="w-full text-left"><thead><tr>
        {[t('allocation.date'), t('allocation.available'), t('allocation.existing'), t('allocation.resulting'), t('allocation.over')].map(h => <th scope="col" key={h} className="p-2">{h}</th>)}
      </tr></thead><tbody>{preview.days.map(d => <tr key={d.date} className="border-t"><td className="p-2">{fmtDate(d.date)}</td><td className="p-2">{d.availableHours}</td>
        <td className="p-2">{d.confirmedHours}</td><td className="p-2">{d.resultingHours}</td><td className="p-2">{d.overByHours > 0 ? <strong className="text-bad">{d.overByHours}</strong> : '0'}</td></tr>)}</tbody></table></div>}
    </ConfirmDialog>}
  </DialogContent></Dialog>
}
