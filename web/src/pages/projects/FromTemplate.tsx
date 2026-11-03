import { useFlag, useMe } from '@/lib/auth'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'
import { ErrorBanner, Field, Section, Spinner, selectCls, tdCls, thCls } from '@/components/hub/common'
import { PeoplePicker } from '@/components/hub/people'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { useProjectRefresh, useReference } from '@/hooks/data'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { runtimeText, plural, t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import type { ProjectDetail } from '@/lib/types'
import type { CreateContext } from './CreateProject'
import { TemplateRiskPreview } from '../template-risk-seeds'
import { ProjectTemplateProvenance } from '../template-version-provenance'

type Schemas = components['schemas']
export type TemplateRow = Schemas['TemplateListRow']
type Value = { templateId: string; dates: Record<string, string | null> } | null

export function usePublishedTemplates() {
  const me = useMe(); const people = useFlag('people_screens'); const roles = useFlag('template_roles')
  return useQuery({ queryKey: ['templates', me.id, people, roles], queryFn: () => typedRequest('get', '/api/v1/templates', {}), select: (d) => d.templates.filter((x) => x.status === 'Published') })
}

function useTemplateDetail(id: string | undefined) {
  const me = useMe(); const people = useFlag('people_screens'); const risks = useFlag('register_screens')
  const roles = useFlag('template_roles'); const working = useFlag('template_working_days')
  return useQuery({ queryKey: ['template', id, me.id, people, risks, roles, working], enabled: !!id,
    queryFn: () => typedRequest('get', '/api/v1/templates/{id}', { params: { id: id! } }) })
}

function usePreview(value: Value, ctx: CreateContext) {
  const body = value && { officeId: ctx.form.officeId || null, startDate: ctx.form.startDate || null, milestoneDates: value.dates, disciplineIds: ctx.disciplines.map((d) => d.disciplineId) }
  return useQuery({ queryKey: ['template-preview', body && { id: value!.templateId, ...body }], enabled: !!value,
    queryFn: () => typedRequest('post', '/api/v1/templates/{id}/preview', { params: { id: value!.templateId }, body: body! }) })
}

/** Step 1 (§12.14 steps 1–2, FR-004): choose a template; its default disciplines are ticked for step 2, and each
 *  milestone shows the date its offset gives — type a contractual date over it, or leave it undated. */
export function TemplateSource({ value, onChange, ctx }: { value: Value; onChange: (v: Value) => void; ctx: CreateContext }) {
  const list = usePublishedTemplates()
  const preview = usePreview(value, ctx)
  const detail = useTemplateDetail(value?.templateId)
  if (!list.data?.length) return null
  const choose = async (id: string) => {
    if (!id) { onChange(null); return }
    const detail = await typedRequest('get', '/api/v1/templates/{id}', { params: { id } })
    ctx.setDisciplines(detail.disciplines.filter((d) => d.isDefaultIncluded).map((d) => ({ disciplineId: d.disciplineId, leadUserId: null })))
    onChange({ templateId: id, dates: {} })
  }
  const setDate = (id: string, v: string | null | undefined) => {
    const dates = { ...value!.dates }
    if (v === undefined) delete dates[id]; else dates[id] = v
    onChange({ ...value!, dates })
  }
  return (
    <div className="space-y-3 rounded-lg border bg-muted p-4">
      <Field label={t('tpl.from')} htmlFor="p-template" hint={t('tpl.recommended')} optional>
        <select id="p-template" className={selectCls} value={value?.templateId ?? ''} onChange={(e) => choose(e.target.value)}>
          <option value="">{t('tpl.noneChosen')}</option>
          {list.data.map((x) => <option key={x.id} value={x.id}>{x.name} (v{x.version})</option>)}
        </select>
      </Field>
      {value && (
        <>
          {detail.data?.offsetDayKind === 'WorkingDays' && <p className="text-xs/[18px] text-muted-foreground">{t('rcv.workingDaysNote')}</p>}
          {!ctx.form.startDate && <p className="text-xs/[18px] text-warn"><span aria-hidden>▲ </span>{t('tpl.needStart')}</p>}
          <div className="scroll-region overflow-x-auto rounded-md border bg-card">
            <table className="w-full text-sm">
              <caption className="sr-only">{t('tpl.milestoneDates')}</caption>
              <thead className="bg-muted"><tr><th scope="col" className={thCls}>{t('tpl.milestone')}</th><th scope="col" className={thCls}>{t('tpl.date')}</th><th scope="col" className={thCls}><span className="sr-only">{t('tpl.undated')}</span></th></tr></thead>
              <tbody>
                {preview.data?.milestones.map((m) => {
                  const typed = m.id in value.dates
                  return (
                    <tr key={m.id} className="border-t">
                      <td className={tdCls}>{m.name} <span className="text-xs/[18px] text-muted-foreground">{tv(m.milestoneType)}</span></td>
                      <td className={cn(tdCls, 'whitespace-nowrap')}>
                        <Input type="date" className="min-h-(--control-row-h) w-40" aria-label={t('tpl.dateFor', { name: m.name })} value={m.date ?? ''} disabled={typed && value.dates[m.id] === null}
                          onChange={(e) => setDate(m.id, e.target.value || null)} />
                        <span className={cn('ml-2 text-xs/[18px]', m.source === 'None' ? 'text-warn' : 'text-muted-foreground')}>{runtimeText(`tpl.source.${m.source}`)}</span>
                      </td>
                      <td className={tdCls}>
                        <label className="flex min-h-(--control-row-h) items-center gap-1.5 whitespace-nowrap"><Checkbox aria-label={t('tpl.undatedFor', { name: m.name })} checked={typed && value.dates[m.id] === null} onCheckedChange={(c) => setDate(m.id, c ? null : undefined)} />{t('tpl.undated')}</label>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
          <p className="text-xs/[18px] text-muted-foreground">{t('tpl.disciplinesNext')}</p>
        </>
      )}
      {preview.error && <ErrorBanner error={preview.error} />}
    </div>
  )
}

/** The last step's summary (FR-005): what will be created, and what will be undated. Basis suggestions of the chosen
 *  disciplines are copied as Proposed only (031 FR-BAS-07, AC-BAS-05); the others are left out, as on the server. */
export function TemplateSummary({ value, ctx }: { value: Value; ctx: CreateContext }) {
  const rolesOn = useFlag('template_roles')
  const risksOn = useFlag('register_screens')
  const preview = usePreview(value, ctx)
  const detail = useTemplateDetail(value?.templateId)
  const p = preview.data
  if (!p) return null
  const undated = p.undated.milestones + p.undated.deliverables + p.undated.tasks
  const basis = detail.data?.basisSuggestions ?? []
  const copied = basis.filter((b) => ctx.disciplines.some((d) => d.disciplineId === b.disciplineId)).length
  return (
    <section aria-labelledby="tpl-summary" className="space-y-1.5 rounded-lg border bg-muted p-4 text-sm">
      <h3 id="tpl-summary" className="text-base/6 font-semibold">{t('tpl.summary')}</h3>
      {detail.data?.offsetDayKind === 'WorkingDays' && <p className="text-xs/[18px] text-muted-foreground">{t('rcv.workingDaysNote')}</p>}
      <p>{t('tpl.creates', { m: p.counts.milestones, d: p.counts.deliverables, k: p.counts.tasks, x: p.counts.dependencies })}{rolesOn && ` · ${t('tpl.roleCount', { count: p.counts.roles ?? 0 })}`}</p>
      {risksOn && (detail.error ? <ErrorBanner error={detail.error} retry={() => detail.refetch()} /> : detail.isPending ? <Spinner />
        : Array.isArray(detail.data?.risks) ? <TemplateRiskPreview rows={detail.data.risks} /> : <ErrorBanner error={new Error(t('tpl.dataUnavailable'))} />)}
      {rolesOn && (p.roles ?? []).length > 0 && <ul className="space-y-1 text-xs/[18px]">{p.roles?.map(r => <li key={r.id} className={r.skipReason ? 'text-warn' : 'text-muted-foreground'}>{r.label} · {r.disciplineName ?? t('tpl.noDiscipline')} · {r.hours} h · {r.skipReason ? runtimeText(`tpl.roleSkip.${r.skipReason}`) : `${t('tpl.roleWillCreate')} ${fmtDate(r.from)} → ${fmtDate(r.through)}`}</li>)}</ul>}
      {basis.length > 0 && <p>{plural(copied, 'templates.basisCopiedOne', 'templates.basisCopied')}{copied < basis.length && ` ${t('templates.basisSkipped', { n: basis.length - copied })}`}</p>}
      {undated > 0 && <p className="text-warn" role="status"><span aria-hidden>▲ </span>{t('tpl.undatedWarning', { m: p.undated.milestones, d: p.undated.deliverables, k: p.undated.tasks })}</p>}
      <p className="text-xs/[18px] text-muted-foreground">{t('tpl.setupNote')}</p>
    </section>
  )
}

// Templates are the recommended start once any is published (FR-009), so this source comes first.

/** Add a discipline pack from a template (FR-007, FR-TPL-02): its deliverables and tasks, dated from the project's own
 *  milestones matched by name — the PM confirms or changes each match first. */
export function AddFromTemplateDialog({ projectId, onClose }: { projectId: string; onClose: (added: boolean) => void }) {
  const ref = useReference()
  const refresh = useProjectRefresh() // the project refresh reaches the Team tab and everything the new items change, through keys.ts
  const list = usePublishedTemplates()
  const [templateId, setTemplateId] = useState('')
  const [disciplineId, setDisciplineId] = useState('')
  const [lead, setLead] = useState<string | null>(null)
  const [mapping, setMapping] = useState<Record<string, string | null>>({})
  const [err, setErr] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const proposal = useQuery({ queryKey: ['template-pack', projectId, templateId], enabled: !!templateId,
    queryFn: () => typedRequest('get', '/api/v1/projects/{id}/template-packs', { params: { id: projectId }, query: { templateId } }) })
  const pack = proposal.data?.disciplines.find((d) => d.disciplineId === disciplineId)
  const name = (id: string) => ref.data?.disciplines.find((x) => x.id === id)?.name ?? ''
  const pick = (id: string) => {
    setDisciplineId(id)
    const p = proposal.data?.disciplines.find((d) => d.disciplineId === id)
    setMapping(Object.fromEntries((p?.mapping ?? []).map((m) => [m.templateMilestoneId, m.projectMilestoneId ?? null])))
  }
  const add = async () => {
    setErr(null); setBusy(true)
    try {
      const r = await typedRequest('post', '/api/v1/projects/{id}/template-packs', { params: { id: projectId }, body: { templateId, disciplineId, leadUserId: lead, mapping } })
      toast.success(t('tpl.packAdded', { d: r.deliverables, k: r.tasks, name: name(disciplineId) }))
      refresh(projectId)
      onClose(true)
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose(false)}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
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
            {!pack.inProject && <Field label={t('team.lead')} htmlFor="pk-lead" optional><PeoplePicker id="pk-lead" value={lead} onChange={setLead} placeholder={t('team.chooseLead')} /></Field>}
            <div className="scroll-region overflow-x-auto rounded-md border">
              <table className="w-full text-sm">
                <caption className="border-b px-(--cell-px) py-2 text-left text-sm text-muted-foreground">{t('tpl.mappingHint')}</caption>
                <thead className="bg-muted"><tr><th scope="col" className={thCls}>{t('tpl.templateMilestone')}</th><th scope="col" className={thCls}>{t('tpl.projectMilestone')}</th></tr></thead>
                <tbody>
                  {pack.mapping.map((m) => (
                    <tr key={m.templateMilestoneId} className="border-t">
                      <td className={tdCls}>{m.name}</td>
                      <td className={tdCls}>
                        <select className={cn(selectCls, 'h-(--control-row-h) min-w-48')} aria-label={t('tpl.mapFor', { name: m.name })} value={mapping[m.templateMilestoneId] ?? ''}
                          onChange={(e) => setMapping({ ...mapping, [m.templateMilestoneId]: e.target.value || null })}>
                          <option value="">{t('tpl.leaveUndated')}</option>
                          {proposal.data!.projectMilestones.map((pm) => <option key={pm.id} value={pm.id}>{pm.key} {pm.name}{pm.date ? ` · ${fmtDate(pm.date)}` : ''}</option>)}
                        </select>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
        {err != null && <ErrorBanner error={err} />}
        <DialogFooter>
          <Button variant="outline" onClick={() => onClose(false)}>{t('common.cancel')}</Button>
          <Button disabled={!pack || busy} onClick={add}>{busy ? <><Spinner />{t('common.saving')}</> : t('tpl.addPackButton')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** FR-008: which template and version the project was made from; later versions never change it (E-12). */

export function TemplateSettings({ project: p }: { project: ProjectDetail }) {
  const enhanced = useFlag('people_screens')
  if (enhanced && p.createdFromTemplateId) return <ProjectTemplateProvenance key={p.id} projectId={p.id} />
  return p.createdFromTemplateId ? <Section title={t('tpl.origin')}><p className="px-5 py-4 text-sm">{t('tpl.originText', { name: p.templateName ?? t('tpl.aTemplate'), v: p.templateVersion ?? '' })}</p></Section> : null
}
