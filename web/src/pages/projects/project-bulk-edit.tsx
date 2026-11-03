import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Field, selectCls } from '@/components/hub/common'
import { Key } from '@/components/hub/pills'
import { Textarea } from '@/components/ui/textarea'
import { useReference } from '@/hooks/data'
import { ApiError, get, typedRequest } from '@/lib/api'
import { useFlag } from '@/lib/auth'
import { t, tv } from '@/lib/i18n'
import { errorText } from './Tasks'

export type SelectedProject = { id: string; projectNumber: string; name: string; rowVersion: number }
type FieldName = 'priority' | 'phaseId' | 'leadUnitId'
type Outcome = { project: SelectedProject; state: 'applied' | 'unchanged' | 'refused' | 'unknown'; reason?: string }

/** A bulk presentation of the existing project edit command. Every write has its
 * own original row version, authorization, validation and normal activity entry. */
export function ProjectBulkEdit({ selected, clear, changed }: { selected: SelectedProject[]; clear: () => void; changed: () => void }) {
  const structure = useFlag('org_structure')
  const reference = useReference()
  const units = useQuery({ queryKey: ['organisation'], queryFn: () => get<{ units: { id: string; name: string; closedOn?: string | null }[] }>('organisation'), enabled: structure })
  const [open, setOpen] = useState(false)
  const [field, setField] = useState<FieldName>('priority')
  const [value, setValue] = useState('High')
  const [reason, setReason] = useState('')
  const [working, setWorking] = useState(false)
  const [outcomes, setOutcomes] = useState<Outcome[] | null>(null)
  const allowedField = field === 'priority' || field === 'phaseId' || field === 'leadUnitId' && structure
  const apply = async () => {
    if (working || !allowedField) return
    const picked = [...selected]
    setWorking(true)
    const result: Outcome[] = []
    for (const project of picked) {
      if (!Number.isInteger(project.rowVersion)) { result.push({ project, state: 'refused', reason: t('projectBulk.missingVersion') }); continue }
      try {
        const response = await typedRequest('patch', '/api/v1/projects/{id}', { params: { id: project.id }, ifMatch: project.rowVersion,
          body: { [field]: value || null, rowVersion: project.rowVersion, ...(reason.trim() ? { reason: reason.trim() } : {}) } })
        if (!response || response.id !== project.id || !Number.isInteger(response.rowVersion) || response.rowVersion < project.rowVersion)
          result.push({ project, state: 'unknown', reason: t('projectBulk.unconfirmedResponse') })
        else result.push({ project, state: response.rowVersion === project.rowVersion ? 'unchanged' : 'applied' })
      } catch (error) {
        const detail = error instanceof ApiError ? Object.values(error.fieldErrors).flat().join('; ') || errorText(error) : errorText(error)
        // A lost acknowledgement or server failure can follow a committed write.
        // Request/client timeouts are uncertain too, even with a 4xx status.
        const refused = error instanceof ApiError && error.status >= 400 && error.status < 500 && ![408, 499].includes(error.status)
        result.push({ project, state: refused ? 'refused' : 'unknown', reason: detail })
      }
    }
    setOutcomes(result); setWorking(false); changed()
  }
  const close = () => { if (working) return; setOpen(false); if (outcomes) clear() }
  return <>
    <div className="flex flex-wrap items-center gap-3 rounded border bg-muted/30 px-3 py-2 text-sm" aria-label={t('projectBulk.selected', { n: selected.length })}>
      <span>{t('projectBulk.selected', { n: selected.length })}</span>
      <Button size="sm" variant="outline" onClick={() => { setOutcomes(null); setOpen(true) }}>{t('projectBulk.edit')}</Button>
      <Button size="sm" variant="ghost" onClick={clear}>{t('projectBulk.clear')}</Button>
    </div>
    <Dialog open={open} onOpenChange={next => !next && close()}><DialogContent className="sm:max-w-xl">
      <DialogHeader><DialogTitle>{t('projectBulk.title')}</DialogTitle><DialogDescription>{t('projectBulk.hint')}</DialogDescription></DialogHeader>
      {outcomes ? <div className="space-y-3 text-sm">
        <p role="status">{t('projectBulk.summary', { applied: outcomes.filter(r => r.state === 'applied').length, unchanged: outcomes.filter(r => r.state === 'unchanged').length, refused: outcomes.filter(r => r.state === 'refused').length, unknown: outcomes.filter(r => r.state === 'unknown').length })}</p>
        {outcomes.some(r => r.state === 'unknown') && <div className="space-y-2 rounded border p-3">
          <p>{t('projectBulk.unknownHint')}</p>
          <Button type="button" variant="outline" onClick={changed}>{t('projectBulk.refresh')}</Button>
        </div>}
        <h3 className="font-medium">{t('projectBulk.results')}</h3>
        <ul className="divide-y rounded border">{outcomes.map(row => <li key={row.project.id} className="space-y-1 px-3 py-2">
          <div className="flex flex-wrap justify-between gap-2"><span><Key>{row.project.projectNumber}</Key> {row.project.name}</span><span>{t(`projectBulk.${row.state}`)}</span></div>
          {row.reason && <p className="text-xs text-muted-foreground">{row.reason}</p>}
        </li>)}</ul>
        <DialogFooter><Button onClick={close}>{t('common.close')}</Button></DialogFooter>
      </div> : <form className="space-y-4" onSubmit={event => { event.preventDefault(); void apply() }} aria-busy={working}>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={t('projectBulk.field')} htmlFor="project-bulk-field"><select id="project-bulk-field" disabled={working} className={selectCls} value={field} onChange={event => { const next = event.target.value as FieldName; setField(next); setValue(next === 'priority' ? 'High' : '') }}>
            <option value="priority">{t('projects.col.priority')}</option><option value="phaseId">{t('projects.col.phase')}</option>{structure && <option value="leadUnitId">{t('org.leadUnit')}</option>}
          </select></Field>
          <Field label={t('projectBulk.value')} htmlFor="project-bulk-value"><select id="project-bulk-value" disabled={working} className={selectCls} value={value} onChange={event => setValue(event.target.value)}>
            {field === 'priority' ? ['Low', 'Medium', 'High', 'Critical'].map(priority => <option key={priority} value={priority}>{tv(priority)}</option>) : field === 'phaseId' ? <><option value="">{t('common.none')}</option>{reference.data?.phases.filter(phase => phase.isActive).map(phase => <option key={phase.id} value={phase.id}>{phase.name}</option>)}</> : <><option value="">{t('projectBulk.noUnit')}</option>{units.data?.units.filter(unit => !unit.closedOn).map(unit => <option key={unit.id} value={unit.id}>{unit.name}</option>)}</>}
          </select></Field>
        </div>
        <Field label={t('projectBulk.reason')} htmlFor="project-bulk-reason" hint={t('projectBulk.reasonHint')} optional><Textarea id="project-bulk-reason" value={reason} disabled={working} onChange={event => setReason(event.target.value)} /></Field>
        <section className="space-y-2 text-sm"><h3 className="font-medium">{t('projectBulk.review')}</h3><ul className="max-h-48 overflow-y-auto rounded border">{selected.map(project => <li key={project.id} className="border-b px-3 py-2 last:border-0"><Key>{project.projectNumber}</Key> {project.name}</li>)}</ul></section>
        {!allowedField && <p role="alert" className="text-sm">{t('projectBulk.invalidField')}</p>}
        <DialogFooter><Button type="button" variant="outline" disabled={working} onClick={close}>{t('common.cancel')}</Button><Button type="submit" disabled={working || !allowedField}>{t(working ? 'projectBulk.applying' : 'projectBulk.apply')}</Button></DialogFooter>
      </form>}
    </DialogContent></Dialog>
  </>
}
