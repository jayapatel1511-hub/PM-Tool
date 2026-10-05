import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Lock, Monitor, Plus, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, DesktopOnly, Empty, ErrorBanner, Field, Loading, Notice, Page, Section, Spinner, TableRegion, selectCls, tdCls, thCls } from '@/components/hub/common'
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
import { FieldsCommand } from './projects/CoordinationForms'
import type { TemplateRow } from './projects/FromTemplate'

const MILESTONE_TYPES = ['Kickoff', 'Field Work', 'Design Submission', 'Client Workshop', 'Permit Submission', 'Tender', 'Construction', 'IFC', 'Record Drawings', 'Closeout', 'Other']
const ANCHORS = ['ProjectStart', 'PreviousMilestone', 'None']
const ROLES = ['Unassigned', 'DisciplineLead', 'PM']
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical']

interface Ms { ref: string; name: string; milestoneType: string; anchor: string; offset?: number | null; completesPhaseId?: string | null; isClientFacing: boolean }
interface Dl { ref: string; disciplineId: string; name: string; deliverableTypeId: string; milestoneRef?: string | null; offset?: number | null; requiresReview: boolean; description?: string | null }
interface Tk { ref: string; disciplineId: string; deliverableRef?: string | null; name: string; description?: string | null; requiresReview: boolean; priority: string; estimatedHours?: number | null; offset?: number | null; assignTo: string }
interface Dep { predecessor: string; successor: string }
interface Basis { id: string; disciplineId: string; kind: string; title: string; scope: string; statement: string; numericValue?: number | null; units?: string | null
  sourceSystem?: string | null; stableSourceId?: string | null; sourceUrl?: string | null; declaredRevision?: string | null }
interface Detail {
  id: string; familyId: string; name: string; description?: string | null; projectTypeId?: string | null; version?: number | null; status: string; publishedAt?: string; rowVersion: number
  canEdit: boolean; family: { id: string; version?: number | null; status: string; publishedAt?: string }[]
  disciplines: { disciplineId: string; isDefaultIncluded: boolean; templateDisciplineId?: string }[]; milestones: Ms[]; deliverables: Dl[]; tasks: Tk[]; dependencies: Dep[]
  basisSuggestions: Basis[]
}

const tone = (s: string) => (s === 'Published' ? 'ok' : s === 'Draft' ? 'work' : 'idle') as 'ok' | 'work' | 'idle'
const newRef = () => `new-${Math.random().toString(36).slice(2, 10)}`
const num = (v: string) => (v === '' ? null : Number(v))
const versionLabel = (v: { version?: number | null; status: string }) => (v.version ? `v${v.version} · ${tv(v.status)}` : t('tpl.draft'))

/** Templates are desktop and tablet screens; phones get a notice instead (§13.0 Responsive). */
function PhoneNotice() {
  return (
    <Page title={t('nav.templates')} subtitle={t('tpl.subtitle')}>
      <Notice icon={Monitor} title={t('app.phoneNotice')} action={<Button asChild variant="outline"><Link to="/my-work">{t('nav.myWork')}</Link></Button>}>{t('tpl.phoneHint')}</Notice>
    </Page>
  )
}

/** An editor table that scrolls inside its card; it takes focus so keyboard users can scroll it even when its controls are
 *  read-only (axe scrollable-region-focusable). */
function Scroll({ label, className, children }: { label: string; className?: string; children: ReactNode }) {
  // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- keyboard users scroll the table
  return <div className={cn('scroll-region overflow-x-auto', className)} role="region" aria-label={label} tabIndex={0}>{children}</div>
}

/** Templates (§12.14, §13.15, FR-ADM-04): every family with its versions for editors; published ones for everyone else. */
export function TemplatesPage() {
  return <DesktopOnly notice={<PhoneNotice />}><Templates /></DesktopOnly>
}

function Templates() {
  const navigate = useNavigate()
  const q = useQuery({ queryKey: ['templates'], queryFn: () => get<{ canEdit: boolean; templates: TemplateRow[] }>('templates') })
  const [creating, setCreating] = useState(false)
  const [name, setName] = useState('')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<unknown>(null)
  const create = async () => {
    if (busy) return
    setErr(null); setBusy(true)
    try { const r = await post<{ id: string }>('templates', { name: name.trim() }); navigate(`/templates/${r.id}`) }
    catch (e) { setErr(e) } finally { setBusy(false) }
  }
  const header = { title: t('nav.templates'), subtitle: t('tpl.subtitle') }
  if (q.isPending) return <Page {...header}><div className="rounded-lg border bg-card"><Loading rows={6} /></div></Page>
  if (q.error) return <Page {...header}><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const families = [...new Set(q.data.templates.map((x) => x.familyId))].map((f) => q.data.templates.filter((x) => x.familyId === f))
  return (
    <Page {...header} actions={q.data.canEdit && <Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('tpl.new')}</Button>}>
      {!q.data.canEdit && <Notice icon={Lock} title={t('tpl.readOnlyTitle')}>{t('tpl.editorsOnly')}</Notice>}
      {families.length === 0 ? <div className="rounded-lg border bg-card"><Empty>{t('tpl.none')}</Empty></div> : (
        <TableRegion>
          <table className="w-full text-sm">
            <caption className="sr-only">{t('nav.templates')}</caption>
            <thead className="bg-muted">
              <tr>{['name', 'versions', 'structure', 'projects'].map((c) => <th key={c} scope="col" className={cn(thCls, c === 'projects' && 'text-right')}>{t(`tpl.col.${c}`)}</th>)}</tr>
            </thead>
            <tbody>
              {families.map((rows) => {
                const lead = rows.find((r) => r.status === 'Published') ?? rows[0]
                return (
                  <tr key={lead.familyId} className="border-t hover:bg-muted">
                    <td className={tdCls}><Link to={`/templates/${lead.id}`} className="font-semibold hover:underline">{lead.name}</Link>
                      {lead.description && <div className="max-w-md text-xs/[18px] text-muted-foreground">{lead.description}</div>}</td>
                    <td className={tdCls}><div className="flex flex-wrap gap-1.5">{rows.map((r) => (
                      <Link key={r.id} to={`/templates/${r.id}`} className="inline-flex min-h-6 items-center rounded-md hover:opacity-80"><Pill tone={tone(r.status)}>{versionLabel(r)}</Pill></Link>))}</div></td>
                    <td className={cn(tdCls, 'tabular-nums')}>{t('tpl.structureCounts', { m: lead.milestones, d: lead.deliverables, k: lead.tasks })}</td>
                    <td className={cn(tdCls, 'text-right tabular-nums')}>{rows.reduce((a, r) => a + r.projects, 0)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </TableRegion>
      )}
      {creating && (
        <Dialog open onOpenChange={(o) => !o && !busy && setCreating(false)}>
          <DialogContent className="max-w-sm">
            <DialogHeader><DialogTitle>{t('tpl.new')}</DialogTitle></DialogHeader>
            <Field label={t('common.name')} htmlFor="tpl-name"><Input id="tpl-name" disabled={busy} value={name} maxLength={200} onChange={(e) => setName(e.target.value)} /></Field>
            {err != null && <ErrorBanner error={err} />}
            <DialogFooter>
              <Button variant="outline" onClick={() => setCreating(false)}>{t('common.cancel')}</Button>
              <Button disabled={!name.trim() || busy} onClick={create}>{busy && <Spinner />}{busy ? t('common.saving') : t('common.create')}</Button>
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
  return <DesktopOnly notice={<PhoneNotice />}><TemplateEditor /></DesktopOnly>
}

function TemplateEditor() {
  const { id } = useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const ref = useReference()
  const me = useMe()
  const q = useQuery({ queryKey: ['template', id], queryFn: () => get<Detail>(`templates/${id}`) })
  const [d, setD] = useState<Detail | null>(null)
  const [dirty, setDirty] = useState(false)
  const [err, setErr] = useState<unknown>(null)
  const [busy, setBusy] = useState<'save' | 'publish' | 'draft' | null>(null)
  const [confirm, setConfirm] = useState<'retire' | 'discard' | null>(null)
  const [removingBasis, setRemovingBasis] = useState<Basis | null>(null)
  const [addingBasis, setAddingBasis] = useState(false)
  const [start, setStart] = useState('')
  useEffect(() => { if (q.data) { setD(q.data); setDirty(false) } }, [q.data])
  const preview = useQuery({ queryKey: ['template', id, 'preview', start], enabled: !!q.data,
    queryFn: () => post<{ counts: Record<string, number>; undated: Record<string, number>; milestones: { id: string; name: string; date?: string; source: string }[] }>(`templates/${id}/preview`, { startDate: start || null }) })
  const disciplines = useMemo(() => new Map(ref.data?.disciplines.map((x) => [x.id, x.name])), [ref.data])
  // A failed load says so (it no longer spins forever); a failed refetch keeps the editor and its unsaved changes on screen.
  const header = { eyebrow: t('nav.templates'), title: t('nav.templates') }
  if (q.isPending || (q.data && !d)) return <Page {...header}><div className="rounded-lg border bg-card"><Loading rows={8} /></div></Page>
  if (!q.data || !d) return <Page {...header}><ErrorBanner error={q.error} retry={() => q.refetch()} /></Page>
  const saved = q.data
  const edit = d.canEdit
  const change = (patch: Partial<Detail>) => { if (busy) return; setD({ ...d, ...patch }); setDirty(true) }
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
  // Saving and publishing show their pending state and cannot be started twice.
  const run = (kind: 'save' | 'publish' | 'draft', fn: () => Promise<unknown>) => async () => { setBusy(kind); try { await fn() } finally { setBusy(null) } }
  const newDraft = async () => {
    setErr(null)
    try { const r = await post<{ id: string }>(`templates/${id}/draft`, {}); navigate(`/templates/${r.id}`) }
    catch (e) { setErr(e) }
  }
  const inTemplate = d.disciplines.map((x) => x.disciplineId)
  // Compact controls inside table rows (32 px); disabled read-only values stay legible.
  const cls = cn(selectCls, 'h-(--control-row-h) w-auto px-2')
  const inputCls = 'min-h-(--control-row-h) py-1'
  const cell = cn(tdCls, 'py-1')
  const box = 'flex h-(--control-row-h) items-center' // a lone checkbox lines up with the row's controls
  const rowBtn = (label: string, onClick: () => void, icon: ReactNode, disabled = false) => (
    <Button type="button" variant="ghost" size="icon-sm" aria-label={label} disabled={disabled || !!busy} onClick={onClick}>{icon}</Button>)
  const move = <T,>(list: T[], i: number, by: number) => { const n = [...list]; [n[i], n[i + by]] = [n[i + by], n[i]]; return n }
  const head = (cols: string[], prefix: string, numeric: string[] = []) => (
    <thead className="bg-muted"><tr>{cols.map((c) => <th key={c} scope="col" className={cn(thCls, numeric.includes(c) && 'text-right')}>{t(`${prefix}.${c}`)}</th>)}
      <th scope="col" className={thCls}><span className="sr-only">{t('common.actions')}</span></th></tr></thead>
  )

  return (
    <Page eyebrow={t('nav.templates')} title={d.name}
      subtitle={<span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1"><Pill tone={tone(d.status)}>{versionLabel(d)}</Pill>{d.publishedAt && <span className="text-sm tabular-nums">{t('tpl.publishedOn', { date: fmtTime(d.publishedAt) })}</span>}</span>}
      actions={<>
        {edit && <Button variant="outline" disabled={!dirty || !!busy} onClick={run('save', save)}>{busy === 'save' && <Spinner />}{busy === 'save' ? t('common.saving') : t('common.save')}</Button>}
        {edit && <Button disabled={!!busy} onClick={run('publish', publish)}>{busy === 'publish' && <Spinner />}{busy === 'publish' ? t('common.saving') : t('tpl.publish')}</Button>}
        {edit && <Button variant="ghost" className="text-bad hover:text-bad" disabled={!!busy} onClick={() => setConfirm('discard')}>{t('tpl.discard')}</Button>}
        {!edit && me.capabilities.templates && saved.status !== 'Draft' && <Button disabled={!!busy} onClick={run('draft', newDraft)}>{busy === 'draft' && <Spinner />}{busy === 'draft' ? t('common.saving') : t('tpl.editDraft')}</Button>}
        {!edit && me.capabilities.templates && saved.status === 'Published' && <Button variant="ghost" disabled={!!busy} onClick={() => setConfirm('retire')}>{t('tpl.retire')}</Button>}
      </>}>
      {q.error != null && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      {err != null && <ErrorBanner error={err} retry={(err as ApiError).code === 'concurrency_conflict' ? () => { setErr(null); q.refetch() } : undefined} />}
      {!edit && <Notice icon={Lock} title={t('tpl.readOnlyTitle')}>{saved.status === 'Draft' ? t('tpl.editorsOnly') : t('tpl.readOnly')}</Notice>}
      <nav aria-label={t('tpl.col.versions')} className="flex flex-wrap gap-2">{d.family.map((v) => {
        const on = v.id === d.id
        return (
          <Link key={v.id} to={`/templates/${v.id}`} aria-current={on ? 'page' : undefined}
            className={cn('inline-flex min-h-(--control-row-h) items-center gap-1 rounded-md border px-3 text-sm', on ? 'border-primary bg-accent font-semibold text-accent-foreground' : 'border-input bg-card hover:bg-muted')}>
            {on && <span aria-hidden>✓</span>}{versionLabel(v)}</Link>
        )
      })}</nav>

      <Section title={t('tpl.about')}>
        <div className="grid max-w-[760px] gap-4 p-5 sm:grid-cols-2">
          <Field label={t('common.name')} htmlFor="t-name"><Input id="t-name" value={d.name} disabled={!edit || !!busy} onChange={(e) => change({ name: e.target.value })} /></Field>
          <Field label={t('projects.type')} htmlFor="t-type">
            <select id="t-type" className={selectCls} value={d.projectTypeId ?? ''} disabled={!edit || !!busy} onChange={(e) => change({ projectTypeId: e.target.value || null })}>
              <option value="">{t('common.none')}</option>{ref.data?.projectTypes.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </Field>
          <Field label={t('common.description')} htmlFor="t-desc" className="sm:col-span-2"><Textarea id="t-desc" rows={2} value={d.description ?? ''} disabled={!edit || !!busy} onChange={(e) => change({ description: e.target.value })} /></Field>
        </div>
      </Section>

      <Section title={t('team.disciplines')} count={d.disciplines.length}>
        <ul className="grid gap-x-6 gap-y-2 p-5 sm:grid-cols-2 lg:grid-cols-3">
          {ref.data?.disciplines.filter((x) => x.isActive || inTemplate.includes(x.id)).map((x) => {
            const row = d.disciplines.find((y) => y.disciplineId === x.id)
            return (
              <li key={x.id} className="flex min-h-6 flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                <label className="flex items-center gap-2"><Checkbox checked={!!row} disabled={!edit || !!busy}
                  onCheckedChange={(c) => change({ disciplines: c ? [...d.disciplines, { disciplineId: x.id, isDefaultIncluded: true }] : d.disciplines.filter((y) => y.disciplineId !== x.id) })} />{x.name}</label>
                {row && <label className="flex items-center gap-1.5 text-xs/[18px] text-muted-foreground"><Checkbox checked={row.isDefaultIncluded} disabled={!edit || !!busy}
                  onCheckedChange={(c) => change({ disciplines: d.disciplines.map((y) => (y.disciplineId === x.id ? { ...y, isDefaultIncluded: !!c } : y)) })} />{t('tpl.byDefault')}</label>}
              </li>
            )
          })}
        </ul>
      </Section>

      <Section title={t('ptab.milestones')} count={d.milestones.length} actions={edit && <Button size="sm" variant="outline" disabled={!!busy} onClick={() => change({ milestones: [...d.milestones, { ref: newRef(), name: '', milestoneType: 'Other', anchor: 'PreviousMilestone', offset: 30, isClientFacing: false }] })}><Plus className="size-4" />{t('common.add')}</Button>}>
        <Scroll label={t('ptab.milestones')}>
          <table className="w-full text-sm">
            {head(['#', 'name', 'type', 'anchor', 'offset', 'phase', 'client'], 'tpl.mcol', ['offset'])}
            <tbody>
              {d.milestones.map((m, i) => {
                const set = (p: Partial<Ms>) => change({ milestones: d.milestones.map((x) => (x.ref === m.ref ? { ...x, ...p } : x)) })
                const planned = preview.data?.milestones[i]
                return (
                  <tr key={m.ref} className="border-t">
                    <td className={cn(tdCls, 'tabular-nums text-muted-foreground')}>M{String(i + 1).padStart(2, '0')}</td>
                    <td className={cell}><Input className={cn(inputCls, 'min-w-48')} aria-label={t('tpl.mcol.name')} value={m.name} disabled={!edit || !!busy} onChange={(e) => set({ name: e.target.value })} />
                      {planned?.date && <div className="mt-0.5 text-xs/[18px] text-muted-foreground tabular-nums">{t('tpl.wouldBe', { date: fmtDate(planned.date) })}</div>}</td>
                    <td className={cell}><select className={cls} aria-label={t('tpl.mcol.type')} value={m.milestoneType} disabled={!edit || !!busy} onChange={(e) => set({ milestoneType: e.target.value })}>{MILESTONE_TYPES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select></td>
                    <td className={cell}><select className={cls} aria-label={t('tpl.mcol.anchor')} value={m.anchor} disabled={!edit || !!busy} onChange={(e) => set({ anchor: e.target.value })}>{ANCHORS.map((x) => <option key={x} value={x}>{t(`tpl.anchor.${x}`)}</option>)}</select></td>
                    <td className={cn(cell, 'text-right')}><Input type="number" className={cn(inputCls, 'w-20 text-right tabular-nums')} aria-label={t('tpl.mcol.offset')} value={m.offset ?? ''} disabled={!edit || !!busy} onChange={(e) => set({ offset: num(e.target.value) })} /></td>
                    <td className={cell}><select className={cls} aria-label={t('tpl.mcol.phase')} value={m.completesPhaseId ?? ''} disabled={!edit || !!busy} onChange={(e) => set({ completesPhaseId: e.target.value || null })}>
                      <option value="">{t('common.none')}</option>{ref.data?.phases.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</select></td>
                    <td className={cell}><div className={box}><Checkbox aria-label={t('tpl.mcol.client')} checked={m.isClientFacing} disabled={!edit || !!busy} onCheckedChange={(c) => set({ isClientFacing: !!c })} /></div></td>
                    <td className={cn(cell, 'whitespace-nowrap')}>{edit && <>
                      {rowBtn(t('tpl.moveUp'), () => change({ milestones: move(d.milestones, i, -1) }), <ArrowUp className="size-4" />, i === 0)}
                      {rowBtn(t('tpl.moveDown'), () => change({ milestones: move(d.milestones, i, 1) }), <ArrowDown className="size-4" />, i === d.milestones.length - 1)}
                      {rowBtn(t('common.remove'), () => change({ milestones: d.milestones.filter((x) => x.ref !== m.ref), deliverables: d.deliverables.map((x) => (x.milestoneRef === m.ref ? { ...x, milestoneRef: null } : x)) }), <Trash2 className="size-4" />)}
                    </>}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </Scroll>
      </Section>

      <Section title={t('ptab.deliverables')} count={d.deliverables.length} actions={edit && inTemplate.length > 0 && <Button size="sm" variant="outline"
        disabled={!!busy} onClick={() => change({ deliverables: [...d.deliverables, { ref: newRef(), disciplineId: inTemplate[0], name: '', deliverableTypeId: ref.data?.deliverableTypes[0]?.id ?? '', milestoneRef: null, offset: 0, requiresReview: true }] })}><Plus className="size-4" />{t('common.add')}</Button>}>
        <Scroll label={t('ptab.deliverables')}>
          <table className="w-full text-sm">
            {head(['discipline', 'name', 'type', 'target', 'offset', 'review'], 'tpl.dcol', ['offset'])}
            <tbody>
              {d.deliverables.map((x) => {
                const set = (p: Partial<Dl>) => change({ deliverables: d.deliverables.map((y) => (y.ref === x.ref ? { ...y, ...p } : y)) })
                return (
                  <tr key={x.ref} className="border-t">
                    <td className={cell}><select className={cls} aria-label={t('tpl.dcol.discipline')} value={x.disciplineId} disabled={!edit || !!busy} onChange={(e) => set({ disciplineId: e.target.value })}>{inTemplate.map((id) => <option key={id} value={id}>{disciplines.get(id)}</option>)}</select></td>
                    <td className={cell}><Input className={cn(inputCls, 'min-w-48')} aria-label={t('tpl.dcol.name')} value={x.name} disabled={!edit || !!busy} onChange={(e) => set({ name: e.target.value })} /></td>
                    <td className={cell}><select className={cls} aria-label={t('tpl.dcol.type')} value={x.deliverableTypeId} disabled={!edit || !!busy} onChange={(e) => set({ deliverableTypeId: e.target.value })}>{ref.data?.deliverableTypes.map((y) => <option key={y.id} value={y.id}>{y.name}</option>)}</select></td>
                    <td className={cell}><select className={cls} aria-label={t('tpl.dcol.target')} value={x.milestoneRef ?? ''} disabled={!edit || !!busy} onChange={(e) => set({ milestoneRef: e.target.value || null })}>
                      <option value="">{t('common.none')}</option>{d.milestones.map((m, i) => <option key={m.ref} value={m.ref}>M{String(i + 1).padStart(2, '0')} {m.name}</option>)}</select></td>
                    <td className={cn(cell, 'text-right')}><Input type="number" className={cn(inputCls, 'w-20 text-right tabular-nums')} aria-label={t('tpl.dcol.offset')} value={x.offset ?? ''} disabled={!edit || !!busy} onChange={(e) => set({ offset: num(e.target.value) })} /></td>
                    <td className={cell}><div className={box}><Checkbox aria-label={t('tpl.dcol.review')} checked={x.requiresReview} disabled={!edit || !!busy} onCheckedChange={(c) => set({ requiresReview: !!c })} /></div></td>
                    <td className={cell}>{edit && rowBtn(t('common.remove'), () => change({ deliverables: d.deliverables.filter((y) => y.ref !== x.ref), tasks: d.tasks.map((k) => (k.deliverableRef === x.ref ? { ...k, deliverableRef: null } : k)) }), <Trash2 className="size-4" />)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </Scroll>
      </Section>

      <Section title={t('ptab.tasks')} count={d.tasks.length} actions={edit && inTemplate.length > 0 && <Button size="sm" variant="outline"
        disabled={!!busy} onClick={() => change({ tasks: [...d.tasks, { ref: newRef(), disciplineId: inTemplate[0], deliverableRef: null, name: '', requiresReview: false, priority: 'Medium', estimatedHours: null, offset: null, assignTo: 'Unassigned' }] })}><Plus className="size-4" />{t('common.add')}</Button>}>
        <Scroll label={t('ptab.tasks')} className="max-h-[32rem] overflow-auto">
          <table className="w-full text-sm">
            <thead className="sticky top-0 z-10 bg-muted"><tr>{['deliverable', 'name', 'discipline', 'assign', 'estimate', 'offset', 'review'].map((c) => <th key={c} scope="col" className={cn(thCls, ['estimate', 'offset'].includes(c) && 'text-right')}>{t(`tpl.tcol.${c}`)}</th>)}
              <th scope="col" className={thCls}><span className="sr-only">{t('common.actions')}</span></th></tr></thead>
            <tbody>
              {d.tasks.map((k) => {
                const set = (p: Partial<Tk>) => change({ tasks: d.tasks.map((y) => (y.ref === k.ref ? { ...y, ...p } : y)) })
                return (
                  <tr key={k.ref} className="border-t">
                    <td className={cell}><select className={cn(cls, 'max-w-44')} aria-label={t('tpl.tcol.deliverable')} value={k.deliverableRef ?? ''} disabled={!edit || !!busy} onChange={(e) => set({ deliverableRef: e.target.value || null })}>
                      <option value="">{t('common.none')}</option>{d.deliverables.map((x) => <option key={x.ref} value={x.ref}>{x.name}</option>)}</select></td>
                    <td className={cell}><Input className={cn(inputCls, 'min-w-48')} aria-label={t('tpl.tcol.name')} value={k.name} disabled={!edit || !!busy} onChange={(e) => set({ name: e.target.value })} /></td>
                    <td className={cell}><select className={cls} aria-label={t('tpl.tcol.discipline')} value={k.disciplineId} disabled={!edit || !!busy} onChange={(e) => set({ disciplineId: e.target.value })}>{inTemplate.map((id) => <option key={id} value={id}>{disciplines.get(id)}</option>)}</select></td>
                    <td className={cell}><select className={cls} aria-label={t('tpl.tcol.assign')} value={k.assignTo} disabled={!edit || !!busy} onChange={(e) => set({ assignTo: e.target.value })}>{ROLES.map((x) => <option key={x} value={x}>{t(`tpl.role.${x}`)}</option>)}</select></td>
                    <td className={cn(cell, 'text-right')}><Input type="number" className={cn(inputCls, 'w-20 text-right tabular-nums')} aria-label={t('tpl.tcol.estimate')} value={k.estimatedHours ?? ''} disabled={!edit || !!busy} onChange={(e) => set({ estimatedHours: num(e.target.value) })} /></td>
                    <td className={cn(cell, 'text-right')}><Input type="number" className={cn(inputCls, 'w-20 text-right tabular-nums')} aria-label={t('tpl.tcol.offset')} value={k.offset ?? ''} disabled={!edit || !!busy} onChange={(e) => set({ offset: num(e.target.value) })} /></td>
                    <td className={cell}><div className="flex items-center gap-2"><Checkbox aria-label={t('tpl.tcol.review')} checked={k.requiresReview} disabled={!edit || !!busy} onCheckedChange={(c) => set({ requiresReview: !!c })} />
                      <select className={cls} aria-label={t('common.priority')} value={k.priority} disabled={!edit || !!busy} onChange={(e) => set({ priority: e.target.value })}>{PRIORITIES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}</select></div></td>
                    <td className={cell}>{edit && rowBtn(t('common.remove'), () => change({ tasks: d.tasks.filter((y) => y.ref !== k.ref), dependencies: d.dependencies.filter((x) => x.predecessor !== k.ref && x.successor !== k.ref) }), <Trash2 className="size-4" />)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </Scroll>
      </Section>

      <Section title={t('tpl.dependencies')} count={d.dependencies.length} actions={edit && d.tasks.length > 1 && <Button size="sm" variant="outline"
        disabled={!!busy} onClick={() => change({ dependencies: [...d.dependencies, { predecessor: d.tasks[0].ref, successor: d.tasks[1].ref }] })}><Plus className="size-4" />{t('common.add')}</Button>}>
        {d.dependencies.length === 0 ? <Empty>{t('tpl.noDependencies')}</Empty> : (
          <ul className="divide-y text-sm">
            {d.dependencies.map((x, i) => {
              const set = (p: Partial<Dep>) => change({ dependencies: d.dependencies.map((y, j) => (j === i ? { ...y, ...p } : y)) })
              const opts = d.tasks.map((k) => <option key={k.ref} value={k.ref}>{k.name || t('tpl.unnamed')}</option>)
              return (
                <li key={i} className="flex flex-wrap items-center gap-2 px-5 py-2">
                  <select className={cn(cls, 'max-w-72')} aria-label={t('tpl.predecessor')} value={x.predecessor} disabled={!edit || !!busy} onChange={(e) => set({ predecessor: e.target.value })}>{opts}</select>
                  <span aria-hidden className="text-muted-foreground">→</span>
                  <select className={cn(cls, 'max-w-72')} aria-label={t('tpl.successor')} value={x.successor} disabled={!edit || !!busy} onChange={(e) => set({ successor: e.target.value })}>{opts}</select>
                  {edit && rowBtn(t('common.remove'), () => change({ dependencies: d.dependencies.filter((_, j) => j !== i) }), <Trash2 className="size-4" />)}
                </li>
              )
            })}
          </ul>
        )}
      </Section>

      {/* A suggestion saves on its own and the refetch after it would drop unsaved structure edits, so Add waits for Save. */}
      <Section title={t('templates.basisTitle')} count={d.basisSuggestions.length} actions={edit && saved.disciplines.length > 0 &&
        <Button size="sm" variant="outline" disabled={dirty || !!busy} onClick={() => setAddingBasis(true)}><Plus className="size-4" />{t('templates.basisAdd')}</Button>}>
        <p className="px-5 pt-4 text-sm text-muted-foreground">{t('templates.basisHint')}{edit && dirty && <span className="text-warn"> {t('templates.basisSaveFirst')}</span>}</p>
        {d.basisSuggestions.length === 0 ? <Empty>{t('templates.basisNone')}</Empty> : (
          <ul className="divide-y text-sm">
            {d.basisSuggestions.map((b) => (
              <li key={b.id} className="space-y-1 px-5 py-3">
                <p className="flex items-start gap-2"><span className="min-w-0 flex-1"><span className="font-semibold">{b.title}</span> <span className="text-xs/[18px] text-muted-foreground">{t(b.kind === 'Criterion' ? 'basis.criterion' : 'basis.assumption')} · {disciplines.get(b.disciplineId)}</span></span>
                  {edit && <Button type="button" variant="ghost" size="icon-sm" className="text-bad hover:text-bad" aria-label={`${t('common.remove')} ${b.title}`} disabled={dirty || !!busy} onClick={() => setRemovingBasis(b)}><Trash2 className="size-4" /></Button>}</p>
                <p className="text-xs/[18px] text-muted-foreground">{t('basis.scope')}: {b.scope}</p>
                <p className="whitespace-pre-wrap">{b.statement}{b.numericValue != null && <span className="tabular-nums"> · {b.numericValue} {b.units ?? ''}</span>}</p>
                {(b.sourceSystem || b.stableSourceId || b.declaredRevision || b.sourceUrl) && <p className="text-xs/[18px] text-muted-foreground">
                  {t('common.source')}: {[b.sourceSystem, b.stableSourceId, b.declaredRevision].filter(Boolean).join(' · ')}{' '}
                  {b.sourceUrl && <a className="break-all text-primary underline underline-offset-4" href={b.sourceUrl} target="_blank" rel="noopener noreferrer">{b.sourceUrl}</a>}</p>}
              </li>
            ))}
          </ul>
        )}
      </Section>

      <Section title={t('tpl.preview')}>
        <div className="flex flex-wrap items-end gap-x-6 gap-y-3 p-5 text-sm">
          <Field label={t('field.StartDate')} htmlFor="tpl-start" className="w-44"><Input id="tpl-start" type="date" value={start} onChange={(e) => setStart(e.target.value)} /></Field>
          <div className="min-w-0 space-y-1 pb-2">
            {preview.data && <p className="tabular-nums">{t('tpl.creates', { m: preview.data.counts.milestones, d: preview.data.counts.deliverables, k: preview.data.counts.tasks, x: preview.data.counts.dependencies })}</p>}
            {preview.data && <p className="text-muted-foreground tabular-nums">{t('tpl.undatedShort', { m: preview.data.undated.milestones, d: preview.data.undated.deliverables, k: preview.data.undated.tasks })}</p>}
            {dirty && <p className="text-xs/[18px] text-warn"><span aria-hidden>▲ </span>{t('tpl.saveToPreview')}</p>}
          </div>
        </div>
      </Section>

      {addingBasis && (
        <FieldsCommand path={`templates/${id}/design-basis`} title={t('templates.basisAdd')} hint={t('templates.basisHint')} submitLabel={t('templates.basisAdd')}
          initial={{ kind: 'Assumption', templateDisciplineId: saved.disciplines.length === 1 ? saved.disciplines[0].templateDisciplineId ?? '' : '' }}
          fields={[
            { name: 'kind', label: 'basis.kind', choices: [{ value: 'Criterion', label: t('basis.criterion') }, { value: 'Assumption', label: t('basis.assumption') }] },
            { name: 'title', label: 'coord.title' },
            { name: 'templateDisciplineId', label: 'basis.discipline', choices: saved.disciplines.map((x) => ({ value: x.templateDisciplineId ?? '', label: disciplines.get(x.disciplineId) ?? '' })) },
            { name: 'scope', label: 'basis.scope' },
            { name: 'statement', label: 'basis.statement', type: 'textarea' },
            { name: 'numericValue', label: 'basis.numeric', type: 'number', optional: true },
            { name: 'units', label: 'basis.units', optional: true },
            { name: 'sourceSystem', label: 'basis.sourceSystem', optional: true },
            { name: 'stableSourceId', label: 'basis.sourceId', optional: true },
            { name: 'sourceUrl', label: 'basis.sourceUrl', type: 'url', optional: true },
            { name: 'declaredRevision', label: 'basis.revision', optional: true },
          ]}
          build={(v) => {
            const s = (k: string) => v[k]?.trim() || null
            // The endpoint leaves "numeric value needs units" to a database check (a server error), so the form refuses it first.
            if (s('numericValue') && !s('units')) throw new Error(t('templates.basisUnitsRequired'))
            return { templateDisciplineId: v.templateDisciplineId, kind: v.kind, title: v.title, scope: v.scope, statement: v.statement,
              numericValue: s('numericValue') ? Number(v.numericValue) : null, units: s('units'), sourceSystem: s('sourceSystem'),
              stableSourceId: s('stableSourceId'), sourceUrl: s('sourceUrl'), declaredRevision: s('declaredRevision') }
          }}
          onClose={() => setAddingBasis(false)} onDone={() => { setAddingBasis(false); refresh() }} />
      )}
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
      {removingBasis && (
        <ConfirmDialog open destructive title={t('templates.basisRemoveTitle', { name: removingBasis.title })} body={t('templates.basisRemoveBody')} confirmLabel={t('common.remove')}
          onOpenChange={(o) => !o && setRemovingBasis(null)}
          onConfirm={async () => {
            await del(`templates/${id}/design-basis/${removingBasis.id}`)
            setRemovingBasis(null)
            refresh()
          }} />
      )}
    </Page>
  )
}
