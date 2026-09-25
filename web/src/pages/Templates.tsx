import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Page, Section, selectCls } from '@/components/hub/common'
import { Pill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useReference } from '@/hooks/data'
import { ApiError, del, get, post, put } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { fmtDate, fmtTime } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import type { TemplateRow } from './projects/FromTemplate'

const MILESTONE_TYPES = ['Kickoff', 'Field Work', 'Design Submission', 'Client Workshop', 'Permit Submission', 'Tender', 'Construction', 'IFC', 'Record Drawings', 'Closeout', 'Other']
const ANCHORS = ['ProjectStart', 'PreviousMilestone', 'None']
const ROLES = ['Unassigned', 'DisciplineLead', 'PM']
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical']

interface Ms { ref: string; name: string; milestoneType: string; anchor: string; offset?: number | null; completesPhaseId?: string | null; isClientFacing: boolean }
interface Dl { ref: string; disciplineId: string; name: string; deliverableTypeId: string; milestoneRef?: string | null; offset?: number | null; requiresReview: boolean; description?: string | null }
interface Tk { ref: string; disciplineId: string; deliverableRef?: string | null; name: string; description?: string | null; requiresReview: boolean; priority: string; estimatedHours?: number | null; offset?: number | null; assignTo: string }
interface Dep { predecessor: string; successor: string }
interface Detail {
  id: string; familyId: string; name: string; description?: string | null; projectTypeId?: string | null; version?: number | null; status: string; publishedAt?: string; rowVersion: number
  canEdit: boolean; family: { id: string; version?: number | null; status: string; publishedAt?: string }[]
  disciplines: { disciplineId: string; isDefaultIncluded: boolean }[]; milestones: Ms[]; deliverables: Dl[]; tasks: Tk[]; dependencies: Dep[]
}

const tone = (s: string) => (s === 'Published' ? 'ok' : s === 'Draft' ? 'work' : 'idle') as 'ok' | 'work' | 'idle'
const newRef = () => `new-${Math.random().toString(36).slice(2, 10)}`
const num = (v: string) => (v === '' ? null : Number(v))

/** Templates (§12.14, §13.15, FR-ADM-04): every family with its versions for editors; published ones for everyone else. */
export function TemplatesPage() {
  const navigate = useNavigate()
  const q = useQuery({ queryKey: ['templates'], queryFn: () => get<{ canEdit: boolean; templates: TemplateRow[] }>('templates') })
  const [creating, setCreating] = useState(false)
  const [name, setName] = useState('')
  if (q.isPending) return <Loading rows={6} />
  if (q.error) return <div className="p-6"><ErrorBanner error={q.error} /></div>
  const families = [...new Set(q.data.templates.map((x) => x.familyId))].map((f) => q.data.templates.filter((x) => x.familyId === f))
  return (
    <Page title={t('nav.templates')} subtitle={t('tpl.subtitle')} actions={q.data.canEdit && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('tpl.new')}</Button>}>
      {families.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('tpl.none')}</Empty></div> : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40 text-left text-xs text-muted-foreground">
              <tr>{['name', 'versions', 'structure', 'projects'].map((c) => <th key={c} scope="col" className="px-3 py-2 font-medium">{t(`tpl.col.${c}`)}</th>)}</tr>
            </thead>
            <tbody className="divide-y">
              {families.map((rows) => {
                const lead = rows.find((r) => r.status === 'Published') ?? rows[0]
                return (
                  <tr key={lead.familyId} className="align-top">
                    <td className="px-3 py-2"><Link to={`/templates/${lead.id}`} className="font-medium hover:underline">{lead.name}</Link>
                      {lead.description && <div className="max-w-md text-xs text-muted-foreground">{lead.description}</div>}</td>
                    <td className="px-3 py-2"><div className="flex flex-wrap gap-1">{rows.map((r) => (
                      <Link key={r.id} to={`/templates/${r.id}`} className="hover:opacity-80"><Pill tone={tone(r.status)}>{r.version ? `v${r.version} · ${tv(r.status)}` : t('tpl.draft')}</Pill></Link>))}</div></td>
                    <td className="px-3 py-2 text-xs tabular-nums">{t('tpl.structureCounts', { m: lead.milestones, d: lead.deliverables, k: lead.tasks })}</td>
                    <td className="px-3 py-2 tabular-nums">{rows.reduce((a, r) => a + r.projects, 0)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
      {creating && (
        <Dialog open onOpenChange={(o) => !o && setCreating(false)}>
          <DialogContent className="max-w-sm">
            <DialogHeader><DialogTitle>{t('tpl.new')}</DialogTitle></DialogHeader>
            <Field label={t('common.name')} htmlFor="tpl-name"><Input id="tpl-name" value={name} maxLength={200} onChange={(e) => setName(e.target.value)} /></Field>
            <DialogFooter>
              <Button variant="outline" onClick={() => setCreating(false)}>{t('common.cancel')}</Button>
              <Button disabled={!name.trim()} onClick={async () => { const r = await post<{ id: string }>('templates', { name: name.trim() }); navigate(`/templates/${r.id}`) }}>{t('common.create')}</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </Page>
  )
}

/** One template version. A Draft is edited as a whole and saved at once; published and retired versions are read-only
 *  and change only through a new Draft (§12.14). */
export function TemplatePage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const ref = useReference()
  const me = useMe()
  const q = useQuery({ queryKey: ['template', id], queryFn: () => get<Detail>(`templates/${id}`) })
  const [d, setD] = useState<Detail | null>(null)
  const [dirty, setDirty] = useState(false)
  const [err, setErr] = useState<unknown>(null)
  const [confirm, setConfirm] = useState<'retire' | 'discard' | null>(null)
  const [start, setStart] = useState('')
  useEffect(() => { if (q.data) { setD(q.data); setDirty(false) } }, [q.data])
  const preview = useQuery({ queryKey: ['template', id, 'preview', start], enabled: !!q.data,
    queryFn: () => post<{ counts: Record<string, number>; undated: Record<string, number>; milestones: { id: string; name: string; date?: string; source: string }[] }>(`templates/${id}/preview`, { startDate: start || null }) })
  const disciplines = useMemo(() => new Map(ref.data?.disciplines.map((x) => [x.id, x.name])), [ref.data])
  if (q.isPending || !d) return <Loading rows={8} />
  if (q.error) return <div className="p-6"><ErrorBanner error={q.error} /></div>
  const edit = d.canEdit
  const change = (patch: Partial<Detail>) => { setD({ ...d, ...patch }); setDirty(true) }
  const refresh = () => { qc.invalidateQueries({ queryKey: ['template', id] }); qc.invalidateQueries({ queryKey: ['templates'] }) }
  const save = async () => {
    setErr(null)
    try {
      const r = await put<Detail>(`templates/${id}/structure`, {
        rowVersion: d.rowVersion, header: { name: d.name, description: d.description || null, projectTypeId: d.projectTypeId || null },
        disciplines: d.disciplines, milestones: d.milestones, deliverables: d.deliverables, tasks: d.tasks, dependencies: d.dependencies,
      })
      qc.setQueryData(['template', id], r); setD(r); setDirty(false); toast.success(t('tpl.saved')); refresh()
      return r
    } catch (e) { setErr(e as ApiError); return null }
  }
  const publish = async () => {
    const current = dirty ? await save() : d
    if (!current) return
    try { const r = await post<{ version: number }>(`templates/${id}/publish`, { rowVersion: current.rowVersion }); toast.success(t('tpl.published', { v: r.version })); refresh() }
    catch (e) { setErr(e as ApiError) }
  }
  const newDraft = async () => { const r = await post<{ id: string }>(`templates/${id}/draft`, {}); navigate(`/templates/${r.id}`) }
  const inTemplate = d.disciplines.map((x) => x.disciplineId)
  const cls = 'h-8 rounded-md border bg-card px-1.5 text-sm disabled:opacity-100'
  const rowBtn = (label: string, onClick: () => void, icon: ReactNode, disabled = false) => (
    <Button type="button" variant="ghost" size="sm" className="size-7 p-0" aria-label={label} disabled={disabled} onClick={onClick}>{icon}</Button>)
  const move = <T,>(list: T[], i: number, by: number) => { const n = [...list]; [n[i], n[i + by]] = [n[i + by], n[i]]; return n }

  return (
    <Page title={d.name} subtitle={<><Pill tone={tone(d.status)}>{d.version ? `v${d.version} · ${tv(d.status)}` : t('tpl.draft')}</Pill>{d.publishedAt && <span className="ml-2">{t('tpl.publishedOn', { date: fmtTime(d.publishedAt) })}</span>}</>}
      actions={<>
        {edit && <Button variant="outline" disabled={!dirty} onClick={save}>{t('common.save')}</Button>}
        {edit && <Button onClick={publish}>{t('tpl.publish')}</Button>}
        {edit && <Button variant="ghost" className="text-bad" onClick={() => setConfirm('discard')}>{t('tpl.discard')}</Button>}
        {!edit && me.capabilities.templates && q.data.status !== 'Draft' && <Button variant="outline" onClick={newDraft}>{t('tpl.editDraft')}</Button>}
        {!edit && me.capabilities.templates && q.data.status === 'Published' && <Button variant="ghost" onClick={() => setConfirm('retire')}>{t('tpl.retire')}</Button>}
      </>}>
      {err != null && <ErrorBanner error={err} />}
      {!edit && <p className="text-sm text-muted-foreground">{q.data.status === 'Draft' ? t('tpl.editorsOnly') : t('tpl.readOnly')}</p>}
      <div className="flex flex-wrap gap-1.5 text-xs">{d.family.map((v) => (
        <Link key={v.id} to={`/templates/${v.id}`} aria-current={v.id === d.id ? 'page' : undefined} className={cn('rounded-full border px-2 py-0.5', v.id === d.id ? 'border-primary bg-accent' : 'bg-card hover:bg-muted')}>
          {v.version ? `v${v.version} · ${tv(v.status)}` : t('tpl.draft')}</Link>))}</div>

      <Section title={t('tpl.about')}>
        <div className="grid gap-3 p-4 sm:grid-cols-2">
          <Field label={t('common.name')} htmlFor="t-name"><Input id="t-name" value={d.name} disabled={!edit} onChange={(e) => change({ name: e.target.value })} /></Field>
          <Field label={t('projects.type')} htmlFor="t-type">
            <select id="t-type" className={selectCls} value={d.projectTypeId ?? ''} disabled={!edit} onChange={(e) => change({ projectTypeId: e.target.value || null })}>
              <option value="">{t('common.none')}</option>{ref.data?.projectTypes.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </Field>
          <Field label={t('common.description')} htmlFor="t-desc" className="sm:col-span-2"><Textarea id="t-desc" rows={2} value={d.description ?? ''} disabled={!edit} onChange={(e) => change({ description: e.target.value })} /></Field>
        </div>
      </Section>

      <Section title={t('team.disciplines')} count={d.disciplines.length}>
        <ul className="grid gap-1 p-4 sm:grid-cols-3">
          {ref.data?.disciplines.filter((x) => x.isActive || inTemplate.includes(x.id)).map((x) => {
            const row = d.disciplines.find((y) => y.disciplineId === x.id)
            return (
              <li key={x.id} className="flex items-center gap-2 text-sm">
                <label className="flex items-center gap-2"><Checkbox checked={!!row} disabled={!edit}
                  onCheckedChange={(c) => change({ disciplines: c ? [...d.disciplines, { disciplineId: x.id, isDefaultIncluded: true }] : d.disciplines.filter((y) => y.disciplineId !== x.id) })} />{x.name}</label>
                {row && <label className="flex items-center gap-1 text-xs text-muted-foreground"><Checkbox checked={row.isDefaultIncluded} disabled={!edit}
                  onCheckedChange={(c) => change({ disciplines: d.disciplines.map((y) => (y.disciplineId === x.id ? { ...y, isDefaultIncluded: !!c } : y)) })} />{t('tpl.byDefault')}</label>}
              </li>
            )
          })}
        </ul>
      </Section>

      <Section title={t('ptab.milestones')} count={d.milestones.length} actions={edit && <Button size="sm" variant="outline" onClick={() => change({ milestones: [...d.milestones, { ref: newRef(), name: '', milestoneType: 'Other', anchor: 'PreviousMilestone', offset: 30, isClientFacing: false }] })}><Plus className="size-3.5" />{t('common.add')}</Button>}>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="text-left text-xs text-muted-foreground"><tr>{['#', 'name', 'type', 'anchor', 'offset', 'phase', 'client'].map((c) => <th key={c} scope="col" className="px-2 py-1 font-medium">{t(`tpl.mcol.${c}`)}</th>)}<th scope="col"><span className="sr-only">{t('common.actions')}</span></th></tr></thead>
            <tbody className="divide-y">
              {d.milestones.map((m, i) => {
                const set = (p: Partial<Ms>) => change({ milestones: d.milestones.map((x) => (x.ref === m.ref ? { ...x, ...p } : x)) })
                const planned = preview.data?.milestones[i]
                return (
                  <tr key={m.ref}>
                    <td className="px-2 py-1 tabular-nums text-muted-foreground">M{String(i + 1).padStart(2, '0')}</td>
                    <td className="px-2 py-1"><Input className="h-8 min-w-48" aria-label={t('tpl.mcol.name')} value={m.name} disabled={!edit} onChange={(e) => set({ name: e.target.value })} />
                      {planned?.date && <div className="text-[11px] text-muted-foreground">{t('tpl.wouldBe', { date: fmtDate(planned.date) })}</div>}</td>
                    <td className="px-2 py-1"><select className={cls} aria-label={t('tpl.mcol.type')} value={m.milestoneType} disabled={!edit} onChange={(e) => set({ milestoneType: e.target.value })}>{MILESTONE_TYPES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select></td>
                    <td className="px-2 py-1"><select className={cls} aria-label={t('tpl.mcol.anchor')} value={m.anchor} disabled={!edit} onChange={(e) => set({ anchor: e.target.value })}>{ANCHORS.map((x) => <option key={x} value={x}>{t(`tpl.anchor.${x}`)}</option>)}</select></td>
                    <td className="px-2 py-1"><Input type="number" className="h-8 w-20" aria-label={t('tpl.mcol.offset')} value={m.offset ?? ''} disabled={!edit} onChange={(e) => set({ offset: num(e.target.value) })} /></td>
                    <td className="px-2 py-1"><select className={cls} aria-label={t('tpl.mcol.phase')} value={m.completesPhaseId ?? ''} disabled={!edit} onChange={(e) => set({ completesPhaseId: e.target.value || null })}>
                      <option value="">{t('common.none')}</option>{ref.data?.phases.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</select></td>
                    <td className="px-2 py-1"><Checkbox aria-label={t('tpl.mcol.client')} checked={m.isClientFacing} disabled={!edit} onCheckedChange={(c) => set({ isClientFacing: !!c })} /></td>
                    <td className="whitespace-nowrap px-1 py-1">{edit && <>
                      {rowBtn(t('tpl.moveUp'), () => change({ milestones: move(d.milestones, i, -1) }), <ArrowUp className="size-3.5" />, i === 0)}
                      {rowBtn(t('tpl.moveDown'), () => change({ milestones: move(d.milestones, i, 1) }), <ArrowDown className="size-3.5" />, i === d.milestones.length - 1)}
                      {rowBtn(t('common.remove'), () => change({ milestones: d.milestones.filter((x) => x.ref !== m.ref), deliverables: d.deliverables.map((x) => (x.milestoneRef === m.ref ? { ...x, milestoneRef: null } : x)) }), <Trash2 className="size-3.5" />)}
                    </>}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </Section>

      <Section title={t('ptab.deliverables')} count={d.deliverables.length} actions={edit && inTemplate.length > 0 && <Button size="sm" variant="outline"
        onClick={() => change({ deliverables: [...d.deliverables, { ref: newRef(), disciplineId: inTemplate[0], name: '', deliverableTypeId: ref.data?.deliverableTypes[0]?.id ?? '', milestoneRef: null, offset: 0, requiresReview: true }] })}><Plus className="size-3.5" />{t('common.add')}</Button>}>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="text-left text-xs text-muted-foreground"><tr>{['discipline', 'name', 'type', 'target', 'offset', 'review'].map((c) => <th key={c} scope="col" className="px-2 py-1 font-medium">{t(`tpl.dcol.${c}`)}</th>)}<th scope="col"><span className="sr-only">{t('common.actions')}</span></th></tr></thead>
            <tbody className="divide-y">
              {d.deliverables.map((x) => {
                const set = (p: Partial<Dl>) => change({ deliverables: d.deliverables.map((y) => (y.ref === x.ref ? { ...y, ...p } : y)) })
                return (
                  <tr key={x.ref}>
                    <td className="px-2 py-1"><select className={cls} aria-label={t('tpl.dcol.discipline')} value={x.disciplineId} disabled={!edit} onChange={(e) => set({ disciplineId: e.target.value })}>{inTemplate.map((id) => <option key={id} value={id}>{disciplines.get(id)}</option>)}</select></td>
                    <td className="px-2 py-1"><Input className="h-8 min-w-48" aria-label={t('tpl.dcol.name')} value={x.name} disabled={!edit} onChange={(e) => set({ name: e.target.value })} /></td>
                    <td className="px-2 py-1"><select className={cls} aria-label={t('tpl.dcol.type')} value={x.deliverableTypeId} disabled={!edit} onChange={(e) => set({ deliverableTypeId: e.target.value })}>{ref.data?.deliverableTypes.map((y) => <option key={y.id} value={y.id}>{y.name}</option>)}</select></td>
                    <td className="px-2 py-1"><select className={cls} aria-label={t('tpl.dcol.target')} value={x.milestoneRef ?? ''} disabled={!edit} onChange={(e) => set({ milestoneRef: e.target.value || null })}>
                      <option value="">{t('common.none')}</option>{d.milestones.map((m, i) => <option key={m.ref} value={m.ref}>M{String(i + 1).padStart(2, '0')} {m.name}</option>)}</select></td>
                    <td className="px-2 py-1"><Input type="number" className="h-8 w-20" aria-label={t('tpl.dcol.offset')} value={x.offset ?? ''} disabled={!edit} onChange={(e) => set({ offset: num(e.target.value) })} /></td>
                    <td className="px-2 py-1"><Checkbox aria-label={t('tpl.dcol.review')} checked={x.requiresReview} disabled={!edit} onCheckedChange={(c) => set({ requiresReview: !!c })} /></td>
                    <td className="px-1 py-1">{edit && rowBtn(t('common.remove'), () => change({ deliverables: d.deliverables.filter((y) => y.ref !== x.ref), tasks: d.tasks.map((k) => (k.deliverableRef === x.ref ? { ...k, deliverableRef: null } : k)) }), <Trash2 className="size-3.5" />)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </Section>

      <Section title={t('ptab.tasks')} count={d.tasks.length} actions={edit && inTemplate.length > 0 && <Button size="sm" variant="outline"
        onClick={() => change({ tasks: [...d.tasks, { ref: newRef(), disciplineId: inTemplate[0], deliverableRef: null, name: '', requiresReview: false, priority: 'Medium', estimatedHours: null, offset: null, assignTo: 'Unassigned' }] })}><Plus className="size-3.5" />{t('common.add')}</Button>}>
        <div className="max-h-[32rem] overflow-auto">
          <table className="w-full text-sm">
            <thead className="sticky top-0 bg-card text-left text-xs text-muted-foreground"><tr>{['deliverable', 'name', 'discipline', 'assign', 'estimate', 'offset', 'review'].map((c) => <th key={c} scope="col" className="px-2 py-1 font-medium">{t(`tpl.tcol.${c}`)}</th>)}<th scope="col"><span className="sr-only">{t('common.actions')}</span></th></tr></thead>
            <tbody className="divide-y">
              {d.tasks.map((k) => {
                const set = (p: Partial<Tk>) => change({ tasks: d.tasks.map((y) => (y.ref === k.ref ? { ...y, ...p } : y)) })
                return (
                  <tr key={k.ref}>
                    <td className="px-2 py-1"><select className={cn(cls, 'max-w-44')} aria-label={t('tpl.tcol.deliverable')} value={k.deliverableRef ?? ''} disabled={!edit} onChange={(e) => set({ deliverableRef: e.target.value || null })}>
                      <option value="">{t('common.none')}</option>{d.deliverables.map((x) => <option key={x.ref} value={x.ref}>{x.name}</option>)}</select></td>
                    <td className="px-2 py-1"><Input className="h-8 min-w-48" aria-label={t('tpl.tcol.name')} value={k.name} disabled={!edit} onChange={(e) => set({ name: e.target.value })} /></td>
                    <td className="px-2 py-1"><select className={cls} aria-label={t('tpl.tcol.discipline')} value={k.disciplineId} disabled={!edit} onChange={(e) => set({ disciplineId: e.target.value })}>{inTemplate.map((id) => <option key={id} value={id}>{disciplines.get(id)}</option>)}</select></td>
                    <td className="px-2 py-1"><select className={cls} aria-label={t('tpl.tcol.assign')} value={k.assignTo} disabled={!edit} onChange={(e) => set({ assignTo: e.target.value })}>{ROLES.map((x) => <option key={x} value={x}>{t(`tpl.role.${x}`)}</option>)}</select></td>
                    <td className="px-2 py-1"><Input type="number" className="h-8 w-20" aria-label={t('tpl.tcol.estimate')} value={k.estimatedHours ?? ''} disabled={!edit} onChange={(e) => set({ estimatedHours: num(e.target.value) })} /></td>
                    <td className="px-2 py-1"><Input type="number" className="h-8 w-20" aria-label={t('tpl.tcol.offset')} value={k.offset ?? ''} disabled={!edit} onChange={(e) => set({ offset: num(e.target.value) })} /></td>
                    <td className="px-2 py-1"><Checkbox aria-label={t('tpl.tcol.review')} checked={k.requiresReview} disabled={!edit} onCheckedChange={(c) => set({ requiresReview: !!c })} />
                      <select className={cn(cls, 'ml-1')} aria-label={t('common.priority')} value={k.priority} disabled={!edit} onChange={(e) => set({ priority: e.target.value })}>{PRIORITIES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select></td>
                    <td className="px-1 py-1">{edit && rowBtn(t('common.remove'), () => change({ tasks: d.tasks.filter((y) => y.ref !== k.ref), dependencies: d.dependencies.filter((x) => x.predecessor !== k.ref && x.successor !== k.ref) }), <Trash2 className="size-3.5" />)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </Section>

      <Section title={t('tpl.dependencies')} count={d.dependencies.length} actions={edit && d.tasks.length > 1 && <Button size="sm" variant="outline"
        onClick={() => change({ dependencies: [...d.dependencies, { predecessor: d.tasks[0].ref, successor: d.tasks[1].ref }] })}><Plus className="size-3.5" />{t('common.add')}</Button>}>
        {d.dependencies.length === 0 ? <Empty>{t('tpl.noDependencies')}</Empty> : (
          <ul className="divide-y text-sm">
            {d.dependencies.map((x, i) => {
              const set = (p: Partial<Dep>) => change({ dependencies: d.dependencies.map((y, j) => (j === i ? { ...y, ...p } : y)) })
              const opts = d.tasks.map((k) => <option key={k.ref} value={k.ref}>{k.name || t('tpl.unnamed')}</option>)
              return (
                <li key={i} className="flex flex-wrap items-center gap-2 px-4 py-1.5">
                  <select className={cn(cls, 'max-w-72')} aria-label={t('tpl.predecessor')} value={x.predecessor} disabled={!edit} onChange={(e) => set({ predecessor: e.target.value })}>{opts}</select>
                  <span aria-hidden>→</span>
                  <select className={cn(cls, 'max-w-72')} aria-label={t('tpl.successor')} value={x.successor} disabled={!edit} onChange={(e) => set({ successor: e.target.value })}>{opts}</select>
                  {edit && rowBtn(t('common.remove'), () => change({ dependencies: d.dependencies.filter((_, j) => j !== i) }), <Trash2 className="size-3.5" />)}
                </li>
              )
            })}
          </ul>
        )}
      </Section>

      <Section title={t('tpl.preview')}>
        <div className="flex flex-wrap items-center gap-3 p-4 text-sm">
          <label className="text-xs text-muted-foreground">{t('field.StartDate')} <Input type="date" className="inline-flex h-8 w-40" value={start} onChange={(e) => setStart(e.target.value)} /></label>
          {preview.data && <span>{t('tpl.creates', { m: preview.data.counts.milestones, d: preview.data.counts.deliverables, k: preview.data.counts.tasks, x: preview.data.counts.dependencies })}</span>}
          {preview.data && <span className="text-muted-foreground">{t('tpl.undatedShort', { m: preview.data.undated.milestones, d: preview.data.undated.deliverables, k: preview.data.undated.tasks })}</span>}
          {dirty && <span className="text-xs text-warn">{t('tpl.saveToPreview')}</span>}
        </div>
      </Section>

      {confirm && (
        <ConfirmDialog open destructive={confirm === 'discard'} title={t(confirm === 'retire' ? 'tpl.retireTitle' : 'tpl.discardTitle', { name: d.name })}
          body={t(confirm === 'retire' ? 'tpl.retireBody' : 'tpl.discardBody')} confirmLabel={t(confirm === 'retire' ? 'tpl.retire' : 'tpl.discard')}
          onOpenChange={(o) => !o && setConfirm(null)}
          onConfirm={async () => {
            if (confirm === 'retire') { await post(`templates/${id}/retire`, {}); refresh() }
            else { await del(`templates/${id}`); qc.invalidateQueries({ queryKey: ['templates'] }); navigate('/templates') }
            setConfirm(null)
          }} />
      )}
    </Page>
  )
}
