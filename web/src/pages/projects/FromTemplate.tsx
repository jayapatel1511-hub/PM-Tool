import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'
import { ErrorBanner, Field, Section, selectCls } from '@/components/hub/common'
import { PeoplePicker } from '@/components/hub/people'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { useReference } from '@/hooks/data'
import { ApiError, get, post } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { plural, t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import type { ProjectDetail } from '@/lib/types'
import { CREATE_SOURCES, type CreateContext } from './CreateProject'
import { SETTINGS_SECTIONS } from './ProjectSettings'

export interface TemplateRow { id: string; familyId: string; name: string; description?: string; version?: number; status: string; milestones: number; deliverables: number; tasks: number; projects: number }
interface TemplateDetail { id: string; disciplines: { disciplineId: string; isDefaultIncluded: boolean }[]; basisSuggestions: { disciplineId: string }[] }
interface Preview {
  milestones: { id: string; name: string; milestoneType: string; date?: string | null; source: 'Contract' | 'Offset' | 'None' }[]
  counts: { milestones: number; deliverables: number; tasks: number; dependencies: number }; undated: { milestones: number; deliverables: number; tasks: number }
}
type Value = { templateId: string; dates: Record<string, string | null> } | null

export function usePublishedTemplates() {
  return useQuery({ queryKey: ['templates'], queryFn: () => get<{ canEdit: boolean; templates: TemplateRow[] }>('templates'), select: (d) => d.templates.filter((x) => x.status === 'Published') })
}

function usePreview(value: Value, ctx: CreateContext) {
  const body = value && { startDate: ctx.form.startDate || null, milestoneDates: value.dates, disciplineIds: ctx.disciplines.map((d) => d.disciplineId) }
  return useQuery({ queryKey: ['template-preview', body && { id: value!.templateId, ...body }], enabled: !!value,
    queryFn: () => post<Preview>(`templates/${value!.templateId}/preview`, body) })
}

/** Step 1 (§12.14 steps 1–2, FR-004): choose a template; its default disciplines are ticked for step 2, and each
 *  milestone shows the date its offset gives — type a contractual date over it, or leave it undated. */
function TemplateSource({ value, onChange, ctx }: { value: Value; onChange: (v: Value) => void; ctx: CreateContext }) {
  const list = usePublishedTemplates()
  const preview = usePreview(value, ctx)
  if (!list.data?.length) return null
  const choose = async (id: string) => {
    if (!id) { onChange(null); return }
    const detail = await get<TemplateDetail>(`templates/${id}`)
    ctx.setDisciplines(detail.disciplines.filter((d) => d.isDefaultIncluded).map((d) => ({ disciplineId: d.disciplineId, leadUserId: null })))
    onChange({ templateId: id, dates: {} })
  }
  const setDate = (id: string, v: string | null | undefined) => {
    const dates = { ...value!.dates }
    if (v === undefined) delete dates[id]; else dates[id] = v
    onChange({ ...value!, dates })
  }
  return (
    <div className="space-y-2 rounded-md border bg-muted/30 p-3">
      <Field label={t('tpl.from')} htmlFor="p-template" hint={t('tpl.recommended')}>
        <select id="p-template" className={selectCls} value={value?.templateId ?? ''} onChange={(e) => choose(e.target.value)}>
          <option value="">{t('tpl.noneChosen')}</option>
          {list.data.map((x) => <option key={x.id} value={x.id}>{x.name} (v{x.version})</option>)}
        </select>
      </Field>
      {value && (
        <>
          {!ctx.form.startDate && <p className="text-xs text-warn">{t('tpl.needStart')}</p>}
          <table className="w-full text-sm">
            <caption className="sr-only">{t('tpl.milestoneDates')}</caption>
            <thead className="text-left text-xs text-muted-foreground"><tr><th scope="col" className="py-1">{t('tpl.milestone')}</th><th scope="col">{t('tpl.date')}</th><th scope="col"><span className="sr-only">{t('tpl.undated')}</span></th></tr></thead>
            <tbody className="divide-y">
              {preview.data?.milestones.map((m) => {
                const typed = m.id in value.dates
                return (
                  <tr key={m.id}>
                    <td className="py-1 pr-2">{m.name} <span className="text-xs text-muted-foreground">{tv(m.milestoneType)}</span></td>
                    <td className="py-1 pr-2">
                      <Input type="date" className="h-8 w-40" aria-label={t('tpl.dateFor', { name: m.name })} value={m.date ?? ''} disabled={typed && value.dates[m.id] === null}
                        onChange={(e) => setDate(m.id, e.target.value || null)} />
                      <span className={cn('ml-2 text-xs', m.source === 'None' ? 'text-warn' : 'text-muted-foreground')}>{t(`tpl.source.${m.source}`)}</span>
                    </td>
                    <td className="py-1 text-xs">
                      <label className="flex items-center gap-1.5"><Checkbox checked={typed && value.dates[m.id] === null} onCheckedChange={(c) => setDate(m.id, c ? null : undefined)} />{t('tpl.undated')}</label>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
          <p className="text-xs text-muted-foreground">{t('tpl.disciplinesNext')}</p>
        </>
      )}
      {preview.error && <ErrorBanner error={preview.error} />}
    </div>
  )
}

/** The last step's summary (FR-005): what will be created, and what will be undated. Basis suggestions of the chosen
 *  disciplines are copied as Proposed only (031 FR-BAS-07, AC-BAS-05); the others are left out, as on the server. */
function TemplateSummary({ value, ctx }: { value: Value; ctx: CreateContext }) {
  const preview = usePreview(value, ctx)
  const detail = useQuery({ queryKey: ['template', value?.templateId], enabled: !!value, queryFn: () => get<TemplateDetail>(`templates/${value!.templateId}`) })
  const p = preview.data
  if (!p) return null
  const undated = p.undated.milestones + p.undated.deliverables + p.undated.tasks
  const basis = detail.data?.basisSuggestions ?? []
  const copied = basis.filter((b) => ctx.disciplines.some((d) => d.disciplineId === b.disciplineId)).length
  return (
    <section aria-labelledby="tpl-summary" className="rounded-md border bg-muted/30 p-3 text-sm">
      <h3 id="tpl-summary" className="font-semibold">{t('tpl.summary')}</h3>
      <p>{t('tpl.creates', { m: p.counts.milestones, d: p.counts.deliverables, k: p.counts.tasks, x: p.counts.dependencies })}</p>
      {basis.length > 0 && <p>{plural(copied, 'templates.basisCopiedOne', 'templates.basisCopied')}{copied < basis.length && ` ${t('templates.basisSkipped', { n: basis.length - copied })}`}</p>}
      {undated > 0 && <p className="text-warn" role="status">{t('tpl.undatedWarning', { m: p.undated.milestones, d: p.undated.deliverables, k: p.undated.tasks })}</p>}
      <p className="text-xs text-muted-foreground">{t('tpl.setupNote')}</p>
    </section>
  )
}

// Templates are the recommended start once any is published (FR-009), so this source comes first.
CREATE_SOURCES.unshift({
  id: 'template', label: 'tpl.from',
  render: (v, set, ctx) => <TemplateSource value={v} onChange={set} ctx={ctx} />,
  summary: (v, ctx) => <TemplateSummary value={v} ctx={ctx} />,
  body: (v: Value) => (v ? { templateId: v.templateId, template: { milestoneDates: v.dates } } : {}),
})

interface Proposal {
  templateId: string; name: string; version: number; projectMilestones: { id: string; key: string; name: string; date?: string }[]
  disciplines: { disciplineId: string; inProject: boolean; deliverables: number; tasks: number; mapping: { templateMilestoneId: string; name: string; projectMilestoneId?: string | null }[] }[]
}

/** Add a discipline pack from a template (FR-007, FR-TPL-02): its deliverables and tasks, dated from the project's own
 *  milestones matched by name — the PM confirms or changes each match first. */
export function AddFromTemplateDialog({ projectId, onClose }: { projectId: string; onClose: (added: boolean) => void }) {
  const ref = useReference()
  const qc = useQueryClient()
  const list = usePublishedTemplates()
  const [templateId, setTemplateId] = useState('')
  const [disciplineId, setDisciplineId] = useState('')
  const [lead, setLead] = useState<string | null>(null)
  const [mapping, setMapping] = useState<Record<string, string | null>>({})
  const [err, setErr] = useState<unknown>(null)
  const proposal = useQuery({ queryKey: ['template-pack', projectId, templateId], enabled: !!templateId,
    queryFn: () => get<Proposal>(`projects/${projectId}/template-packs?templateId=${templateId}`) })
  const pack = proposal.data?.disciplines.find((d) => d.disciplineId === disciplineId)
  const name = (id: string) => ref.data?.disciplines.find((x) => x.id === id)?.name ?? ''
  const pick = (id: string) => {
    setDisciplineId(id)
    const p = proposal.data?.disciplines.find((d) => d.disciplineId === id)
    setMapping(Object.fromEntries((p?.mapping ?? []).map((m) => [m.templateMilestoneId, m.projectMilestoneId ?? null])))
  }
  const add = async () => {
    setErr(null)
    try {
      const r = await post<{ deliverables: number; tasks: number }>(`projects/${projectId}/template-packs`, { templateId, disciplineId, leadUserId: lead, mapping })
      toast.success(t('tpl.packAdded', { d: r.deliverables, k: r.tasks, name: name(disciplineId) }))
      qc.invalidateQueries({ queryKey: ['p', projectId] })
      onClose(true)
    } catch (e) { setErr(e as ApiError) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose(false)}>
      <DialogContent className="max-w-xl">
        <DialogHeader><DialogTitle>{t('tpl.addPack')}</DialogTitle><DialogDescription>{t('tpl.addPackHint')}</DialogDescription></DialogHeader>
        <Field label={t('tpl.template')} htmlFor="pk-t">
          <select id="pk-t" className={selectCls} value={templateId} onChange={(e) => { setTemplateId(e.target.value); setDisciplineId('') }}>
            <option value="">{t('common.selectPlaceholder')}</option>{list.data?.map((x) => <option key={x.id} value={x.id}>{x.name} (v{x.version})</option>)}
          </select>
        </Field>
        {proposal.data && (
          <Field label={t('common.discipline')} htmlFor="pk-d">
            <select id="pk-d" className={selectCls} value={disciplineId} onChange={(e) => pick(e.target.value)}>
              <option value="">{t('common.selectPlaceholder')}</option>
              {proposal.data.disciplines.map((d) => <option key={d.disciplineId} value={d.disciplineId}>{name(d.disciplineId)} — {t('tpl.packCounts', { d: d.deliverables, k: d.tasks })}{d.inProject ? ` · ${t('tpl.inProject')}` : ''}</option>)}
            </select>
          </Field>
        )}
        {pack && (
          <>
            {!pack.inProject && <Field label={t('team.lead')} htmlFor="pk-lead"><PeoplePicker id="pk-lead" value={lead} onChange={setLead} placeholder={t('team.chooseLead')} /></Field>}
            <table className="w-full text-sm">
              <caption className="mb-1 text-left text-xs text-muted-foreground">{t('tpl.mappingHint')}</caption>
              <thead className="text-left text-xs text-muted-foreground"><tr><th scope="col" className="py-1">{t('tpl.templateMilestone')}</th><th scope="col">{t('tpl.projectMilestone')}</th></tr></thead>
              <tbody className="divide-y">
                {pack.mapping.map((m) => (
                  <tr key={m.templateMilestoneId}>
                    <td className="py-1 pr-2">{m.name}</td>
                    <td className="py-1">
                      <select className={selectCls} aria-label={t('tpl.mapFor', { name: m.name })} value={mapping[m.templateMilestoneId] ?? ''}
                        onChange={(e) => setMapping({ ...mapping, [m.templateMilestoneId]: e.target.value || null })}>
                        <option value="">{t('tpl.leaveUndated')}</option>
                        {proposal.data!.projectMilestones.map((pm) => <option key={pm.id} value={pm.id}>{pm.key} {pm.name}{pm.date ? ` · ${fmtDate(pm.date)}` : ''}</option>)}
                      </select>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
        {err != null && <ErrorBanner error={err} />}
        <DialogFooter>
          <Button variant="outline" onClick={() => onClose(false)}>{t('common.cancel')}</Button>
          <Button disabled={!pack} onClick={add}>{t('tpl.addPackButton')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** FR-008: which template and version the project was made from; later versions never change it (E-12). */
SETTINGS_SECTIONS.push({
  id: 'template-origin',
  render: (p: ProjectDetail) => p.createdFromTemplateId ? (
    <Section title={t('tpl.origin')}>
      <p className="p-4 text-sm">{t('tpl.originText', { name: p.templateName ?? t('tpl.aTemplate'), v: p.templateVersion ?? '' })}</p>
    </Section>
  ) : null,
})
