import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ListPlus, OctagonAlert, Plus, ShieldAlert, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Page, Spinner, selectCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { FieldRow, HistoryList, InlineDate, InlinePerson, InlineSelect, InlineText, TabBar } from '@/components/hub/fields'
import { PANELS, useItemPanel, type PanelProps } from '@/components/hub/panel-host'
import { PeoplePicker } from '@/components/hub/people'
import { Chip, Key, StatusPill } from '@/components/hub/pills'
import { useTable, type Column } from '@/components/hub/table'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useProject, useProjectRefresh } from '@/hooks/data'
import { useMe } from '@/lib/auth'
import { ApiError, del, get, patch, post, qs } from '@/lib/api'
import { fmtDate, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import type { ProjectDiscipline } from '@/lib/types'
import { cn } from '@/lib/utils'
import { ItemPicker, type Pick } from './Decisions'
import { ActionForm } from './Meetings'
import { useCurrentProject } from './ProjectLayout'
import { CommentsSlot, ItemSlots, type RaiseProps } from './slots-items'
import { errorText } from './Tasks'

// Risk and Issue Registers (§12.10, §13.13, packet 014).

export interface RiskRow {
  id: string; projectId: string; key: string; title: string; status: string; ownerId: string; ownerName?: string; probability: number; impact: number
  score: number; band: string; mitigation?: string; triggerIndicator?: string; reviewDate?: string; isReviewOverdue: boolean; reviewOverdueDays: number
  projectDisciplineId?: string; disciplineName?: string; realisedIssueId?: string; realisedIssueKey?: string; rowVersion: number
}
export interface IssueRow {
  id: string; projectId: string; key: string; title: string; status: string; raisedById: string; raisedByName?: string; ownerId: string; ownerName?: string
  severity: string; dateRaised: string; targetResolutionDate?: string; isOverdue: boolean; daysOverdue: number; resolution?: string; resolvedDate?: string
  originRiskId?: string; originRiskKey?: string; projectDisciplineId?: string; disciplineName?: string; rowVersion: number
  locationSummary?: string; documentSummary?: string; verificationStatus?: string
}
interface Perm { ok: boolean; reason?: string | null }
interface LinkRow { id: string; targetType: string; targetId: string; key: string; name: string; status?: string; date?: string; person?: string }
interface Detail { description?: string; project: { id: string; projectNumber: string; name: string }; links: LinkRow[]; permissions: { edit: Perm; transitions: { to: string; ok: boolean; reason?: string | null }[]; comment: boolean } }
interface RiskDetail extends Detail { risk: RiskRow; realisedIssue?: { id: string; key: string; title: string; status: string } | null }
interface IssueDetail extends Detail { issue: IssueRow; originRisk?: { id: string; key: string; title: string; status: string } | null }
interface IssueLocation { id: string; kind: string; siteArea?: string; building?: string; level?: string; room?: string; assetSystem?: string; alignment?: string; startStation?: number; endStation?: number; stationUnits?: string; coordinateX?: number; coordinateY?: number; coordinateZ?: number; coordinateReferenceSystem?: string; coordinateUnits?: string; rowVersion: number }
interface IssueDocument { id: string; kind: string; identifier: string; revision: string; sourceUrl: string; externalTopicId?: string; modelElementGuid?: string; viewpointUrl?: string; isAvailable: boolean; rowVersion: number }
interface IssueVerification { id: string; verifierId: string; status: string; evidenceUrl?: string; note?: string; verifiedAt?: string; rowVersion: number }
type Target = { type: 'Task' | 'Deliverable'; id: string; key: string; name: string }

const RISK_STATUSES = ['Open', 'Monitoring', 'Closed', 'Realised']
const ISSUE_STATUSES = ['Open', 'In Progress', 'Resolved', 'Cancelled']
const SEVERITIES = ['High', 'Medium', 'Low']
const LEVELS = [1, 2, 3]
const TONE = { High: 'bad', Medium: 'warn', Low: 'idle' } as const
const FILTERS = ['q', 'status', 'severity', 'ownerId', 'disciplineId', 'indicator'] as const
/** Live preview only; saved risks carry the band worked out by the server (§12.10: Low 1–2, Medium 3–4, High 6–9). */
const bandOf = (score: number) => (score >= 6 ? 'High' : score >= 3 ? 'Medium' : 'Low')

function Severity({ level, score }: { level: string; score?: number }) {
  return <Chip tone={TONE[level as keyof typeof TONE] ?? 'idle'}>{tv(level)}{score != null && ` · ${score}`}</Chip>
}

function useRegisterRefresh() {
  const qc = useQueryClient()
  const refresh = useProjectRefresh()
  return (projectId: string, kind?: 'risk' | 'issue', id?: string) => {
    if (kind && id) qc.invalidateQueries({ queryKey: [kind, id] })
    qc.invalidateQueries({ queryKey: ['coordination'] })
    refresh(projectId)
  }
}

// ---------- Register tables (FR-008) ----------

function useFilters() {
  const [sp, setSp] = useSearchParams()
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const filters = Object.fromEntries(FILTERS.map((k) => [k, sp.get(k)])) as Record<(typeof FILTERS)[number], string | null>
  const clear = () => { const n = new URLSearchParams(sp); FILTERS.forEach((k) => n.delete(k)); setSp(n, { replace: true }) }
  return { filters, set, clear, active: FILTERS.some((k) => sp.has(k)) }
}

function FilterBar({ f, statuses, open, indicator, owners, disciplines, menu }: {
  f: ReturnType<typeof useFilters>; statuses: string[]; open: string; indicator: [string, string]; owners: [string, string][]; disciplines: ProjectDiscipline[]; menu: React.ReactNode
}) {
  const sel = 'h-8 rounded-md border bg-card px-2 text-sm'
  const on = f.filters.indicator === indicator[0]
  return (
    <div className="flex flex-wrap items-center gap-2">
      <Input key={f.filters.q ? 'q' : 'empty'} className="h-8 w-52" type="search" placeholder={t('common.search')} defaultValue={f.filters.q ?? ''} onChange={(e) => f.set('q', e.target.value)} aria-label={t('common.search')} />
      <select className={sel} value={f.filters.status ?? ''} onChange={(e) => f.set('status', e.target.value)} aria-label={t('common.status')}>
        <option value="">{t('register.anyStatus')}</option><option value={open}>{t('register.openStatuses')}</option>
        {statuses.map((s) => <option key={s} value={s}>{tv(s)}</option>)}
      </select>
      <select className={sel} value={f.filters.severity ?? ''} onChange={(e) => f.set('severity', e.target.value)} aria-label={t('register.severity')}>
        <option value="">{t('register.anySeverity')}</option>{SEVERITIES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}
      </select>
      <select className={cn(sel, 'max-w-48')} value={f.filters.ownerId ?? ''} onChange={(e) => f.set('ownerId', e.target.value)} aria-label={t('common.owner')}>
        <option value="">{t('decision.anyOwner')}</option>{owners.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
      </select>
      <select className={cn(sel, 'max-w-48')} value={f.filters.disciplineId ?? ''} onChange={(e) => f.set('disciplineId', e.target.value)} aria-label={t('common.discipline')}>
        <option value="">{t('register.anyDiscipline')}</option>{disciplines.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
      </select>
      <button type="button" aria-pressed={on} onClick={() => f.set('indicator', on ? null : indicator[0])}
        className={cn('rounded-full border px-2.5 py-0.5 text-xs', on ? 'border-primary bg-primary text-primary-foreground' : 'bg-card hover:bg-muted')}>{t(indicator[1])}</button>
      {f.active && <button type="button" className="px-2 text-xs text-muted-foreground hover:text-foreground" onClick={f.clear}>{t('common.clear')}</button>}
      <div className="flex-1" />
      {menu}
    </div>
  )
}

function owners(rows: { ownerId: string; ownerName?: string }[] | undefined): [string, string][] {
  const m = new Map<string, string>()
  for (const r of rows ?? []) if (r.ownerName) m.set(r.ownerId, r.ownerName)
  return [...m.entries()].sort((a, b) => a[1].localeCompare(b[1]))
}

function RegisterTable<T extends { id: string }>({ table, rows, loading, empty, hot }: {
  table: { visible: Column<T>[]; header: (c: Column<T>) => React.ReactNode; cell: (c: Column<T>, r: T) => React.ReactNode }; rows: T[]; loading: boolean; empty: React.ReactNode; hot: (r: T) => boolean
}) {
  if (loading) return <Loading rows={6} />
  if (!rows.length) return <div className="rounded-lg border bg-card">{empty}</div>
  return (
    <div className="overflow-x-auto rounded-lg border bg-card">
      <table className="w-full text-[13px]">
        <thead className="bg-muted/60 text-left text-xs text-muted-foreground"><tr>{table.visible.map(table.header)}</tr></thead>
        <tbody>{rows.map((r) => <tr key={r.id} className={cn('border-t hover:bg-muted/30', hot(r) && 'bg-bad-bg/30')}>{table.visible.map((c) => table.cell(c, r))}</tr>)}</tbody>
      </table>
    </div>
  )
}

export function RisksTab() {
  const p = useCurrentProject()
  const f = useFilters()
  const openPanel = useItemPanel()
  const [raising, setRaising] = useState(false)
  const q = useQuery({ queryKey: ['p', p.id, 'risks', f.filters], queryFn: () => get<RiskRow[]>(`projects/${p.id}/risks${qs(f.filters)}`) })
  const table = useTable<RiskRow>('hub.riskColumns', [
    { id: 'key', label: t('milestone.key'), fixed: true, sort: (r) => r.key, className: 'whitespace-nowrap', cell: (r) => <Key>{r.key}</Key> },
    { id: 'title', label: t('register.title'), fixed: true, sort: (r) => r.title.toLowerCase(), className: 'min-w-[14rem] font-medium',
      cell: (r) => <button className="text-left hover:underline" onClick={() => openPanel('Risk', r.id)}>{r.title}</button> },
    { id: 'severity', label: t('register.severity'), sort: (r) => -r.score, className: 'whitespace-nowrap', cell: (r) => <Severity level={r.band} score={r.score} /> },
    { id: 'status', label: t('common.status'), sort: (r) => r.status, cell: (r) => <StatusPill status={r.status} /> },
    { id: 'owner', label: t('common.owner'), sort: (r) => r.ownerName, className: 'whitespace-nowrap', cell: (r) => r.ownerName },
    { id: 'discipline', label: t('common.discipline'), sort: (r) => r.disciplineName, className: 'whitespace-nowrap', cell: (r) => r.disciplineName ?? t('common.dash') },
    { id: 'review', label: t('risk.reviewDate'), sort: (r) => r.reviewDate, className: 'whitespace-nowrap',
      cell: (r) => <span className={cn(r.isReviewOverdue && 'font-medium text-bad')}>{fmtDate(r.reviewDate)}{r.isReviewOverdue && ` · ${t('risk.reviewOverdue')}`}</span> },
    { id: 'mitigation', label: t('risk.mitigation'), cell: (r) => <span className="line-clamp-2 text-muted-foreground">{r.mitigation}</span> },
    { id: 'realised', label: t('risk.realisedAs'), optional: true, cell: (r) => r.realisedIssueId ? <button onClick={() => openPanel('Issue', r.realisedIssueId!)} className="hover:underline"><Key>{r.realisedIssueKey}</Key></button> : null },
  ], q.data ?? [], (r) => [r.reviewDate, r.key])
  const can = p.permissions.raiseRegister
  return (
    <Page title={t('ptab.risks')} subtitle={t('risk.subtitle')}
      actions={<>
        <ExportMenu path={`projects/${p.id}/risks/export`} params={f.filters} name={`${p.projectNumber}-risks`} />
        {can.ok && <Button onClick={() => setRaising(true)}><Plus className="size-4" />{t('risk.new')}</Button>}
      </>}>
      {!can.ok && can.reason && <p className="text-sm text-muted-foreground">{can.reason}</p>}
      <FilterBar f={f} statuses={RISK_STATUSES} open="Open,Monitoring" indicator={['reviewOverdue', 'risk.reviewOverdue']} owners={owners(q.data)} disciplines={p.disciplines} menu={table.menu} />
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      <RegisterTable table={table} rows={table.sorted} loading={q.isPending} hot={(r) => r.isReviewOverdue}
        empty={<Empty action={can.ok && !f.active && <Button onClick={() => setRaising(true)}>{t('risk.new')}</Button>}>{f.active ? t('register.noMatch') : t('risk.empty')}</Empty>} />
      {raising && <RiskForm projectId={p.id} onClose={() => setRaising(false)} />}
    </Page>
  )
}

export function IssuesTab() {
  const p = useCurrentProject()
  const f = useFilters()
  const openPanel = useItemPanel()
  const [raising, setRaising] = useState(false)
  const q = useQuery({ queryKey: ['p', p.id, 'issues', f.filters], queryFn: () => get<IssueRow[]>(`projects/${p.id}/issues${qs(f.filters)}`) })
  const table = useTable<IssueRow>('hub.issueColumns', [
    { id: 'key', label: t('milestone.key'), fixed: true, sort: (r) => r.key, className: 'whitespace-nowrap', cell: (r) => <Key>{r.key}</Key> },
    { id: 'title', label: t('register.title'), fixed: true, sort: (r) => r.title.toLowerCase(), className: 'min-w-[14rem] font-medium',
      cell: (r) => <button className="text-left hover:underline" onClick={() => openPanel('Issue', r.id)}>{r.title}</button> },
    { id: 'severity', label: t('register.severity'), sort: (r) => SEVERITIES.indexOf(r.severity), cell: (r) => <Severity level={r.severity} /> },
    { id: 'status', label: t('common.status'), sort: (r) => r.status, cell: (r) => <StatusPill status={r.status} /> },
    { id: 'owner', label: t('common.owner'), sort: (r) => r.ownerName, className: 'whitespace-nowrap', cell: (r) => r.ownerName },
    { id: 'discipline', label: t('common.discipline'), sort: (r) => r.disciplineName, className: 'whitespace-nowrap', cell: (r) => r.disciplineName ?? t('common.dash') },
    { id: 'raised', label: t('issue.dateRaised'), sort: (r) => r.dateRaised, className: 'whitespace-nowrap', cell: (r) => fmtDate(r.dateRaised) },
    { id: 'target', label: t('issue.target'), sort: (r) => r.targetResolutionDate, className: 'whitespace-nowrap',
      cell: (r) => <span className={cn(r.isOverdue && 'font-medium text-bad')}>{fmtDate(r.targetResolutionDate)}{r.isOverdue && ` · ${t('ind.overdueD', { n: r.daysOverdue })}`}</span> },
    { id: 'raisedBy', label: t('issue.raisedBy'), optional: true, sort: (r) => r.raisedByName, className: 'whitespace-nowrap', cell: (r) => r.raisedByName },
    { id: 'origin', label: t('issue.fromRisk'), optional: true, cell: (r) => r.originRiskId ? <button onClick={() => openPanel('Risk', r.originRiskId!)} className="hover:underline"><Key>{r.originRiskKey}</Key></button> : null },
    { id: 'location', label: t('issue.locations'), sort: (r) => r.locationSummary, className: 'min-w-48', cell: (r) => r.locationSummary || t('common.dash') },
    { id: 'documents', label: t('issue.documentReferences'), optional: true, sort: (r) => r.documentSummary, className: 'min-w-48', cell: (r) => r.documentSummary || t('common.dash') },
    { id: 'verification', label: t('issue.verificationFlow'), sort: (r) => r.verificationStatus, className: 'whitespace-nowrap', cell: (r) => r.verificationStatus ? <StatusPill status={r.verificationStatus} /> : t('common.dash') },
  ], q.data ?? [], (r) => [r.targetResolutionDate, r.key])
  const can = p.permissions.raiseRegister
  return (
    <Page title={t('ptab.issues')} subtitle={t('issue.subtitle')}
      actions={<>
        <ExportMenu path={`projects/${p.id}/issues/export`} params={f.filters} name={`${p.projectNumber}-issues`} />
        {can.ok && <Button onClick={() => setRaising(true)}><Plus className="size-4" />{t('issue.new')}</Button>}
      </>}>
      {!can.ok && can.reason && <p className="text-sm text-muted-foreground">{can.reason}</p>}
      <FilterBar f={f} statuses={ISSUE_STATUSES} open="Open,In Progress" indicator={['overdue', 'ind.overdue']} owners={owners(q.data)} disciplines={p.disciplines} menu={table.menu} />
      {q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
      <RegisterTable table={table} rows={table.sorted} loading={q.isPending} hot={(r) => r.isOverdue || (r.severity === 'High' && ['Open', 'In Progress'].includes(r.status))}
        empty={<Empty action={can.ok && !f.active && <Button onClick={() => setRaising(true)}>{t('issue.new')}</Button>}>{f.active ? t('register.noMatch') : t('issue.empty')}</Empty>} />
      {raising && <IssueForm projectId={p.id} onClose={() => setRaising(false)} />}
    </Page>
  )
}

// ---------- Raise (FR-001, FR-002, FR-005) ----------

/** Probability and impact from three labelled levels with plain-language anchors (§12.10, FR-002). */
function ScoreFields({ p, i, onChange, errors }: { p?: number; i?: number; onChange: (v: { probability?: number; impact?: number }) => void; errors?: string[] }) {
  const score = p && i ? p * i : null
  const group = (name: 'probability' | 'impact', value?: number) => (
    <fieldset className="space-y-1">
      <legend className="text-sm font-medium">{t(`risk.${name}`)}</legend>
      {LEVELS.map((n) => (
        <label key={n} className={cn('grid cursor-pointer grid-cols-[auto_1fr] items-start gap-x-2 rounded-md border px-2 py-1.5 text-sm', value === n && 'border-primary bg-accent')}>
          <input type="radio" name={`risk-${name}`} className="row-span-2 mt-1" checked={value === n} onChange={() => onChange({ [name]: n })} />
          <span className="font-medium">{n} · {t(`risk.${name}.${n}`)}</span>
          <span className="text-xs text-muted-foreground">{t(`risk.${name}.${n}.hint`)}</span>
        </label>
      ))}
    </fieldset>
  )
  return (
    <div className="grid gap-3 sm:col-span-2 sm:grid-cols-2">
      {group('probability', p)}{group('impact', i)}
      <p className="text-sm sm:col-span-2" aria-live="polite">
        {score ? <>{t('risk.severityIs')} <Severity level={bandOf(score)} score={score} /></> : <span className="text-muted-foreground">{t('risk.chooseBoth')}</span>}
      </p>
      {errors?.map((e) => <p key={e} className="text-xs text-bad sm:col-span-2" role="alert">{e}</p>)}
    </div>
  )
}

function DisciplineSelect({ id, projectId, value, onChange }: { id: string; projectId: string; value?: string | null; onChange: (v: string | null) => void }) {
  const project = useProject(projectId)
  return (
    <select id={id} className={selectCls} value={value ?? ''} onChange={(e) => onChange(e.target.value || null)}>
      <option value="">{t('register.noDiscipline')}</option>
      {(project.data?.disciplines ?? []).filter((d) => d.isActive || d.id === value).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
    </select>
  )
}

function RiskForm({ projectId, link, disciplineId, onClose }: { projectId: string; link?: Target; disciplineId?: string | null; onClose: () => void }) {
  const done = useRegisterRefresh()
  const openPanel = useItemPanel()
  const [f, setF] = useState<Record<string, any>>({ projectDisciplineId: disciplineId ?? null })
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    setErr(null); setBusy(true)
    try {
      const r = await post(`projects/${projectId}/risks`, { title: f.title, description: f.description, probability: f.probability, impact: f.impact, ownerId: f.ownerId || null,
        reviewDate: f.reviewDate || null, mitigation: f.mitigation, triggerIndicator: f.triggerIndicator, projectDisciplineId: f.projectDisciplineId || null,
        links: link ? [{ targetType: link.type, targetId: link.id }] : [] })
      toast.success(t('risk.raised', { key: r.key })); done(projectId); onClose(); openPanel('Risk', r.id)
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  const fe = err?.fieldErrors ?? {}
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader><DialogTitle>{t('risk.new')}</DialogTitle><DialogDescription>{link ? t('register.linkedTo', { key: link.key, name: link.name }) : t('risk.newHint')}</DialogDescription></DialogHeader>
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); submit() }}>
          <Field label={t('register.title')} htmlFor="r-title" error={fe.title} className="sm:col-span-2"><Input id="r-title" required autoFocus placeholder={t('risk.titlePlaceholder')} value={f.title ?? ''} onChange={(e) => setF({ ...f, title: e.target.value })} /></Field>
          <Field label={t('common.description')} htmlFor="r-desc" error={fe.description} className="sm:col-span-2"><Textarea id="r-desc" rows={2} value={f.description ?? ''} onChange={(e) => setF({ ...f, description: e.target.value })} /></Field>
          <ScoreFields p={f.probability} i={f.impact} onChange={(v) => setF({ ...f, ...v })} errors={[...(fe.probability ?? []), ...(fe.impact ?? [])]} />
          <Field label={t('common.owner')} htmlFor="r-owner" error={fe.ownerId}>
            <PeoplePicker id="r-owner" value={f.ownerId} valueName={f.ownerName} placeholder={t('decision.me')} onChange={(id, person) => setF({ ...f, ownerId: id, ownerName: person?.displayName })} />
          </Field>
          <Field label={t('risk.reviewDate')} htmlFor="r-review" error={fe.reviewDate} hint={t('risk.reviewHint')}><Input id="r-review" type="date" value={f.reviewDate ?? ''} onChange={(e) => setF({ ...f, reviewDate: e.target.value })} /></Field>
          <Field label={t('common.discipline')} htmlFor="r-disc" error={fe.projectDisciplineId}><DisciplineSelect id="r-disc" projectId={projectId} value={f.projectDisciplineId} onChange={(v) => setF({ ...f, projectDisciplineId: v })} /></Field>
          <Field label={t('risk.mitigation')} htmlFor="r-mit" error={fe.mitigation} className="sm:col-span-2"><Textarea id="r-mit" rows={2} value={f.mitigation ?? ''} onChange={(e) => setF({ ...f, mitigation: e.target.value })} /></Field>
          <Field label={t('risk.trigger')} htmlFor="r-trig" error={fe.triggerIndicator} hint={t('risk.triggerHint')} className="sm:col-span-2"><Input id="r-trig" value={f.triggerIndicator ?? ''} onChange={(e) => setF({ ...f, triggerIndicator: e.target.value })} /></Field>
          {err && !Object.keys(fe).length && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="sm:col-span-2">
            <Button type="button" variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={busy || !f.probability || !f.impact}>{busy && <Spinner />}{t('risk.raise')}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** The issue's own fields, shared by "Raise issue" and "Realised as a new issue". */
function IssueFields({ projectId, f, setF, fe, prefix }: { projectId: string; f: Record<string, any>; setF: (v: Record<string, any>) => void; fe: Record<string, string[]>; prefix: string }) {
  return (
    <>
      <Field label={t('register.title')} htmlFor={`${prefix}-title`} error={fe.title} className="sm:col-span-2"><Input id={`${prefix}-title`} required value={f.title ?? ''} onChange={(e) => setF({ ...f, title: e.target.value })} /></Field>
      <Field label={t('common.description')} htmlFor={`${prefix}-desc`} error={fe.description} className="sm:col-span-2"><Textarea id={`${prefix}-desc`} rows={2} value={f.description ?? ''} onChange={(e) => setF({ ...f, description: e.target.value })} /></Field>
      <fieldset className="sm:col-span-2">
        <legend className="text-sm font-medium">{t('register.severity')}</legend>
        <div className="mt-1 flex flex-wrap gap-2">{SEVERITIES.map((s) => (
          <label key={s} className={cn('flex cursor-pointer items-center gap-2 rounded-md border px-2.5 py-1 text-sm', f.severity === s && 'border-primary bg-accent')}>
            <input type="radio" name={`${prefix}-severity`} checked={f.severity === s} onChange={() => setF({ ...f, severity: s })} />{tv(s)}
          </label>))}</div>
        <p className="mt-1 text-xs text-muted-foreground">{t('issue.highHint')}</p>
        {fe.severity?.map((e) => <p key={e} className="text-xs text-bad" role="alert">{e}</p>)}
      </fieldset>
      <Field label={t('common.owner')} htmlFor={`${prefix}-owner`} error={fe.ownerId}>
        <PeoplePicker id={`${prefix}-owner`} value={f.ownerId} valueName={f.ownerName} placeholder={t('decision.me')} onChange={(id, person) => setF({ ...f, ownerId: id, ownerName: person?.displayName })} />
      </Field>
      <Field label={t('issue.target')} htmlFor={`${prefix}-target`} error={fe.targetResolutionDate}><Input id={`${prefix}-target`} type="date" min={f.dateRaised ?? today()} value={f.targetResolutionDate ?? ''} onChange={(e) => setF({ ...f, targetResolutionDate: e.target.value })} /></Field>
      <Field label={t('issue.dateRaised')} htmlFor={`${prefix}-raised`} error={fe.dateRaised}><Input id={`${prefix}-raised`} type="date" max={today()} value={f.dateRaised ?? today()} onChange={(e) => setF({ ...f, dateRaised: e.target.value })} /></Field>
      <Field label={t('common.discipline')} htmlFor={`${prefix}-disc`} error={fe.projectDisciplineId}><DisciplineSelect id={`${prefix}-disc`} projectId={projectId} value={f.projectDisciplineId} onChange={(v) => setF({ ...f, projectDisciplineId: v })} /></Field>
    </>
  )
}

const issueBody = (f: Record<string, any>) => ({
  title: f.title, description: f.description, severity: f.severity, ownerId: f.ownerId || null, targetResolutionDate: f.targetResolutionDate || null,
  dateRaised: f.dateRaised || null, projectDisciplineId: f.projectDisciplineId || null,
})

function IssueForm({ projectId, link, disciplineId, onClose }: { projectId: string; link?: Target; disciplineId?: string | null; onClose: () => void }) {
  const done = useRegisterRefresh()
  const openPanel = useItemPanel()
  const [f, setF] = useState<Record<string, any>>({ projectDisciplineId: disciplineId ?? null })
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    setErr(null); setBusy(true)
    try {
      const r = await post(`projects/${projectId}/issues`, { ...issueBody(f), links: link ? [{ targetType: link.type, targetId: link.id }] : [] })
      toast.success(t('issue.raised', { key: r.key })); done(projectId); onClose(); openPanel('Issue', r.id)
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  const fe = err?.fieldErrors ?? {}
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader><DialogTitle>{t('issue.new')}</DialogTitle><DialogDescription>{link ? t('register.linkedTo', { key: link.key, name: link.name }) : t('issue.newHint')}</DialogDescription></DialogHeader>
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); submit() }}>
          <IssueFields projectId={projectId} f={f} setF={setF} fe={fe} prefix="i" />
          {err && !Object.keys(fe).length && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="sm:col-span-2">
            <Button type="button" variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={busy || !f.severity}>{busy && <Spinner />}{t('issue.raise')}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** "Raise from here" on task and deliverable panels (§13.13): the new risk or issue is linked to the item. */
function RaiseMenu({ projectId, targetType, targetId, targetKey, targetName, disciplineId }: RaiseProps) {
  const [open, setOpen] = useState<'risk' | 'issue' | 'action' | null>(null)
  const link: Target = { type: targetType, id: targetId, key: targetKey, name: targetName }
  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild><Button size="sm" variant="outline"><Plus className="size-3.5" />{t('register.raise')}</Button></DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem onSelect={() => setOpen('risk')}><ShieldAlert className="size-4" />{t('risk.new')}</DropdownMenuItem>
          <DropdownMenuItem onSelect={() => setOpen('issue')}><OctagonAlert className="size-4" />{t('issue.new')}</DropdownMenuItem>
          <DropdownMenuItem onSelect={() => setOpen('action')}><ListPlus className="size-4" />{t('action.new')}</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      {open === 'risk' && <RiskForm projectId={projectId} link={link} disciplineId={disciplineId} onClose={() => setOpen(null)} />}
      {open === 'issue' && <IssueForm projectId={projectId} link={link} disciplineId={disciplineId} onClose={() => setOpen(null)} />}
      {open === 'action' && <ActionForm projectId={projectId} onClose={() => setOpen(null)} related={targetType === 'Task' ? { taskId: targetId, label: `${targetKey} ${targetName}` } : { label: `${targetKey} ${targetName}` }}
        links={targetType === 'Deliverable' ? [{ targetType, targetId }] : undefined} />}
    </>
  )
}

// ---------- Realise, resolve (RSK-03, ISS-02) ----------

/** RSK-03: Realised needs the issue it became — a new one, filled from the risk, or one already raised. */
function RealiseDialog({ r, onClose }: { r: RiskRow; onClose: () => void }) {
  const done = useRegisterRefresh()
  const openPanel = useItemPanel()
  const [mode, setMode] = useState<'new' | 'existing'>('new')
  const [f, setF] = useState<Record<string, any>>({ title: r.title, severity: r.band, ownerId: r.ownerId, ownerName: r.ownerName, projectDisciplineId: r.projectDisciplineId ?? null })
  const [issueId, setIssueId] = useState('')
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const issues = useQuery({ queryKey: ['p', r.projectId, 'issues', { pick: true }], queryFn: () => get<IssueRow[]>(`projects/${r.projectId}/issues`) })
  const choices = (issues.data ?? []).filter((x) => !x.originRiskId)
  const save = async () => {
    setErr(null); setBusy(true)
    try {
      const res = await post(`risks/${r.id}/transition`, { toStatus: 'Realised', ...(mode === 'new' ? { issue: issueBody(f) } : { issueId }) }, r.rowVersion)
      toast.success(t('risk.realisedToast', { key: r.key, issue: res.issueKey })); done(r.projectId, 'risk', r.id); onClose(); openPanel('Issue', res.issueId)
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  const fe = err?.fieldErrors ?? {}
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader><DialogTitle>{t('risk.realiseTitle', { key: r.key })}</DialogTitle><DialogDescription>{t('risk.realiseHint')}</DialogDescription></DialogHeader>
        <div className="flex gap-4 text-sm" role="radiogroup" aria-label={t('risk.realiseTitle', { key: r.key })}>
          {(['new', 'existing'] as const).map((m) => <label key={m} className="flex items-center gap-1.5"><input type="radio" name="realise-mode" checked={mode === m} onChange={() => setMode(m)} />{t(`risk.realise.${m}`)}</label>)}
        </div>
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); save() }}>
          {mode === 'new' ? <IssueFields projectId={r.projectId} f={f} setF={setF} fe={fe} prefix="ri" /> : (
            <Field label={t('risk.existingIssue')} htmlFor="ri-existing" error={fe.issueId} className="sm:col-span-2">
              <select id="ri-existing" className={selectCls} value={issueId} onChange={(e) => setIssueId(e.target.value)} required>
                <option value="">{choices.length ? t('risk.chooseIssue') : t('risk.noIssues')}</option>
                {choices.map((x) => <option key={x.id} value={x.id}>{x.key} · {x.title} ({tv(x.status)})</option>)}
              </select>
            </Field>
          )}
          {err && (!Object.keys(fe).length || fe.issueId) && mode === 'new' && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
          <DialogFooter className="sm:col-span-2">
            <Button type="button" variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={busy || (mode === 'new' ? !f.severity : !issueId)}>{busy && <Spinner />}{t('risk.realise')}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function ResolveDialog({ i, onClose }: { i: IssueRow; onClose: () => void }) {
  const done = useRegisterRefresh()
  const [text, setText] = useState('')
  const [date, setDate] = useState(today())
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const save = async () => {
    setErr(null); setBusy(true)
    try {
      await post(`issues/${i.id}/transition`, { toStatus: 'Resolved', resolution: text, resolvedDate: date }, i.rowVersion)
      toast.success(t('issue.resolvedToast', { key: i.key })); done(i.projectId, 'issue', i.id); onClose()
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>{t('issue.resolveTitle', { key: i.key })}</DialogTitle><DialogDescription>{i.title}</DialogDescription></DialogHeader>
        <div className="space-y-3">
          <Field label={t('issue.resolution')} htmlFor="res-text" error={err?.fieldErrors.resolution} hint={t('issue.resolutionHint')}><Textarea id="res-text" autoFocus rows={4} value={text} onChange={(e) => setText(e.target.value)} /></Field>
          <Field label={t('issue.resolvedDate')} htmlFor="res-date" error={err?.fieldErrors.resolvedDate}><Input id="res-date" type="date" max={today()} value={date} onChange={(e) => setDate(e.target.value)} /></Field>
          {err && !Object.keys(err.fieldErrors).length && <ErrorBanner error={err} />}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button disabled={!text.trim() || busy} onClick={save}>{busy && <Spinner />}{t('issue.resolve')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ---------- Panels ----------

const STEP: Record<string, string> = {
  Open: 'register.toOpen', Monitoring: 'risk.toMonitoring', Closed: 'risk.close', Realised: 'risk.realise',
  'In Progress': 'issue.toInProgress', Resolved: 'issue.resolve', Cancelled: 'issue.cancel',
}

function Links({ links, canEdit, projectId, onAdd, onChange }: { links: LinkRow[]; canEdit: boolean; projectId: string; onAdd: (x: Pick) => Promise<unknown>; onChange: () => void }) {
  const openPanel = useItemPanel()
  const [adding, setAdding] = useState(false)
  return (
    <div className="space-y-2 p-4">
      {links.length === 0 ? <p className="text-sm text-muted-foreground">{t('decision.noLinks')}</p> : (
        <ul className="divide-y rounded border">{links.map((l) => (
          <li key={l.id} className="flex items-center gap-2 px-3 py-1.5 text-[13px]">
            <span className="w-20 shrink-0 text-xs text-muted-foreground">{t(`itemType.${l.targetType}`)}</span><Key>{l.key}</Key>
            <button className="min-w-0 flex-1 truncate text-left hover:underline" onClick={() => openPanel(l.targetType, l.targetId)}>{l.name}</button>
            {l.status && <StatusPill status={l.status} />}<span className="whitespace-nowrap text-xs">{fmtDate(l.date)}</span>
            {canEdit && <Button variant="ghost" size="icon" className="size-7" aria-label={t('decision.unlink', { key: l.key })}
              onClick={async () => { try { await del(`item-links/${l.id}`); onChange() } catch (e) { toast.error(errorText(e)) } }}><Trash2 className="size-3.5" /></Button>}
          </li>))}</ul>
      )}
      {canEdit && (adding ? (
        <div className="space-y-2 rounded-md border p-2">
          <ItemPicker projectId={projectId} exclude={links.map((l) => l.targetId)} onPick={async (x) => { try { await onAdd(x); onChange() } catch (e) { toast.error(errorText(e)) } }} />
          <div className="flex justify-end"><Button size="sm" variant="ghost" onClick={() => setAdding(false)}>{t('common.close')}</Button></div>
        </div>
      ) : <Button size="sm" variant="outline" onClick={() => setAdding(true)}><Plus className="size-4" />{t('register.addLink')}</Button>)}
    </div>
  )
}

function Steps({ transitions, label, onStep }: { transitions: Detail['permissions']['transitions']; label: (to: string) => string; onStep: (to: string) => void }) {
  return (
    <div className="flex flex-wrap gap-1.5 pt-1">
      {transitions.map((s) => <Button key={s.to} size="sm" variant={['Realised', 'Resolved'].includes(s.to) ? 'default' : 'outline'} disabled={!s.ok} title={s.reason ?? undefined} onClick={() => onStep(s.to)}>{label(s.to)}</Button>)}
    </div>
  )
}

const levelOptions = (name: 'probability' | 'impact') => LEVELS.map((n) => ({ value: String(n), label: `${n} · ${t(`risk.${name}.${n}`)}` }))

function RiskPanel({ id }: PanelProps) {
  const q = useQuery({ queryKey: ['risk', id], queryFn: () => get<RiskDetail>(`risks/${id}`) })
  const done = useRegisterRefresh()
  const openPanel = useItemPanel()
  const project = useProject(q.data?.project.id)
  const [tab, setTab] = useState<'links' | 'comments' | 'history'>('links')
  const [step, setStep] = useState<string | null>(null)
  if (q.isPending) return <Loading />
  if (q.error) return <div className="p-4"><ErrorBanner error={q.error} /></div>
  const { risk: r, permissions: perm } = q.data
  const can = perm.edit.ok
  const refresh = () => done(r.projectId, 'risk', r.id)
  const save = (body: object) => patch(`risks/${r.id}`, body, r.rowVersion).then(() => { refresh(); return true }).catch((e) => { toast.error(errorText(e)); return false })
  const go = (to: string, reason?: string) => post(`risks/${r.id}/transition`, { toStatus: to, reason }, r.rowVersion).then(refresh)
  const onStep = async (to: string) => {
    if (to === 'Realised' || to === 'Closed') { setStep(to); return }
    try { await go(to) } catch (e) { toast.error(errorText(e)) }
  }
  const disciplines = (project.data?.disciplines ?? []).filter((d) => d.isActive || d.id === r.projectDisciplineId).map((d) => ({ value: d.id, label: d.name }))
  return (
    <div>
      <div className="space-y-2 border-b p-4">
        <div className="flex flex-wrap items-center gap-2"><ShieldAlert className="size-4 text-muted-foreground" aria-hidden /><Key>{r.key}</Key><StatusPill status={r.status} /><Severity level={r.band} score={r.score} />
          {r.isReviewOverdue && <Chip tone="bad">{t('risk.reviewOverdue')}</Chip>}</div>
        <h2 className="text-lg font-semibold">{r.title}</h2>
        {q.data.realisedIssue && <p className="text-sm">{t('risk.realisedAsLabel')} <button className="hover:underline" onClick={() => openPanel('Issue', q.data.realisedIssue!.id)}><Key>{q.data.realisedIssue.key}</Key> {q.data.realisedIssue.title}</button></p>}
        <Steps transitions={perm.transitions} label={(to) => t(to === 'Open' && r.status === 'Closed' ? 'risk.reopen' : STEP[to])} onStep={onStep} />
      </div>
      <div className="px-4 py-2">
        <FieldRow label={t('register.title')}><InlineText value={r.title} disabled={!can} title={perm.edit.reason ?? undefined} onSave={(v) => save({ title: v })} /></FieldRow>
        <FieldRow label={t('common.description')}><InlineText value={q.data.description ?? ''} multiline disabled={!can} onSave={(v) => save({ description: v })} /></FieldRow>
        <FieldRow label={t('risk.probability')}><InlineSelect value={String(r.probability)} options={levelOptions('probability')} disabled={!can} onSave={(v) => save({ probability: Number(v) })} title={t('risk.probability')} /></FieldRow>
        <FieldRow label={t('risk.impact')}><InlineSelect value={String(r.impact)} options={levelOptions('impact')} disabled={!can} onSave={(v) => save({ impact: Number(v) })} title={t('risk.impact')} /></FieldRow>
        <FieldRow label={t('common.owner')}><InlinePerson value={r.ownerId} name={r.ownerName} disabled={!can} allowClear={false} onSave={(v) => save({ ownerId: v })} /></FieldRow>
        <FieldRow label={t('risk.reviewDate')}><InlineDate value={r.reviewDate} disabled={!can} onSave={(v) => save({ reviewDate: v })} /></FieldRow>
        <FieldRow label={t('common.discipline')}><InlineSelect value={r.projectDisciplineId} allowEmpty options={disciplines} disabled={!can} onSave={(v) => save({ projectDisciplineId: v || null })} title={t('common.discipline')} /></FieldRow>
        <FieldRow label={t('risk.mitigation')}><InlineText value={r.mitigation ?? ''} multiline disabled={!can} onSave={(v) => save({ mitigation: v })} /></FieldRow>
        <FieldRow label={t('risk.trigger')}><InlineText value={r.triggerIndicator ?? ''} disabled={!can} onSave={(v) => save({ triggerIndicator: v })} /></FieldRow>
      </div>
      <TabBar tabs={[{ id: 'links' as const, label: t('decision.links'), count: q.data.links.length }, ...(ItemSlots.Comments ? [{ id: 'comments' as const, label: t('common.comments') }] : []), { id: 'history' as const, label: t('common.history') }]} value={tab} onChange={setTab} />
      {tab === 'links' && <Links links={q.data.links} canEdit={can} projectId={r.projectId} onChange={refresh} onAdd={(x) => post(`risks/${r.id}/links`, { targetType: x.targetType, targetId: x.targetId })} />}
      {tab === 'comments' && <CommentsSlot type="Risk" id={r.id} projectId={r.projectId} />}
      {tab === 'history' && <HistoryList type="Risk" id={r.id} />}
      {step === 'Realised' && <RealiseDialog r={r} onClose={() => setStep(null)} />}
      {step === 'Closed' && <ConfirmDialog open onOpenChange={(o) => !o && setStep(null)} reason="optional" title={t('risk.closeTitle', { key: r.key })} body={t('risk.closeHint')}
        confirmLabel={t('risk.close')} onConfirm={(reason) => go('Closed', reason || undefined)} />}
    </div>
  )
}

function IssuePanel({ id }: PanelProps) {
  const q = useQuery({ queryKey: ['issue', id], queryFn: () => get<IssueDetail>(`issues/${id}`) })
  const done = useRegisterRefresh()
  const openPanel = useItemPanel()
  const project = useProject(q.data?.project.id)
  const [tab, setTab] = useState<'links' | 'comments' | 'history'>('links')
  const [step, setStep] = useState<string | null>(null)
  if (q.isPending) return <Loading />
  if (q.error) return <div className="p-4"><ErrorBanner error={q.error} /></div>
  const { issue: i, permissions: perm } = q.data
  const can = perm.edit.ok
  const open = ['Open', 'In Progress'].includes(i.status)
  const refresh = () => done(i.projectId, 'issue', i.id)
  const save = (body: object) => patch(`issues/${i.id}`, body, i.rowVersion).then(() => { refresh(); return true }).catch((e) => { toast.error(errorText(e)); return false })
  const go = (to: string, reason?: string) => post(`issues/${i.id}/transition`, { toStatus: to, reason }, i.rowVersion).then(refresh)
  const reopening = (to: string) => to === 'In Progress' && i.status === 'Resolved'
  const onStep = async (to: string) => {
    if (to === 'Resolved' || to === 'Cancelled' || reopening(to)) { setStep(to); return }
    try { await go(to) } catch (e) { toast.error(errorText(e)) }
  }
  const disciplines = (project.data?.disciplines ?? []).filter((d) => d.isActive || d.id === i.projectDisciplineId).map((d) => ({ value: d.id, label: d.name }))
  return (
    <div>
      <div className="space-y-2 border-b p-4">
        <div className="flex flex-wrap items-center gap-2"><OctagonAlert className="size-4 text-muted-foreground" aria-hidden /><Key>{i.key}</Key><StatusPill status={i.status} /><Severity level={i.severity} />
          {i.isOverdue && <Chip tone="bad">{t('ind.overdueD', { n: i.daysOverdue })}</Chip>}
          {i.severity === 'High' && open && <Chip tone="bad">{t('issue.attention')}</Chip>}</div>
        <h2 className="text-lg font-semibold">{i.title}</h2>
        {q.data.originRisk && <p className="text-sm">{t('issue.fromRiskLabel')} <button className="hover:underline" onClick={() => openPanel('Risk', q.data.originRisk!.id)}><Key>{q.data.originRisk.key}</Key> {q.data.originRisk.title}</button></p>}
        <Steps transitions={perm.transitions} label={(to) => t(reopening(to) ? 'issue.reopen' : STEP[to])} onStep={onStep} />
      </div>
      <div className="px-4 py-2">
        {i.status === 'Resolved' && (
          <div className="mb-2 rounded-md border border-done/30 bg-done-bg p-3 text-sm">
            <div className="text-xs text-muted-foreground">{t('issue.resolvedOn', { date: fmtDate(i.resolvedDate) })}</div>
            <InlineText value={i.resolution ?? ''} multiline disabled={!can} onSave={(v) => save({ resolution: v })} />
          </div>
        )}
        <FieldRow label={t('register.title')}><InlineText value={i.title} disabled={!can} title={perm.edit.reason ?? undefined} onSave={(v) => save({ title: v })} /></FieldRow>
        <FieldRow label={t('common.description')}><InlineText value={q.data.description ?? ''} multiline disabled={!can} onSave={(v) => save({ description: v })} /></FieldRow>
        <FieldRow label={t('register.severity')}><InlineSelect value={i.severity} options={SEVERITIES.map((x) => ({ value: x, label: tv(x) }))} disabled={!can} onSave={(v) => save({ severity: v })} title={t('register.severity')} /></FieldRow>
        <FieldRow label={t('common.owner')}><InlinePerson value={i.ownerId} name={i.ownerName} disabled={!can} allowClear={false} onSave={(v) => save({ ownerId: v })} /></FieldRow>
        <FieldRow label={t('issue.raisedBy')}><InlinePerson value={i.raisedById} name={i.raisedByName} disabled={!can} allowClear={false} onSave={(v) => save({ raisedById: v })} /></FieldRow>
        <FieldRow label={t('issue.dateRaised')}><InlineDate value={i.dateRaised} disabled={!can} onSave={(v) => save({ dateRaised: v })} /></FieldRow>
        <FieldRow label={t('issue.target')}><InlineDate value={i.targetResolutionDate} disabled={!can} onSave={(v) => save({ targetResolutionDate: v })} /></FieldRow>
        <FieldRow label={t('common.discipline')}><InlineSelect value={i.projectDisciplineId} allowEmpty options={disciplines} disabled={!can} onSave={(v) => save({ projectDisciplineId: v || null })} title={t('common.discipline')} /></FieldRow>
        <IssueMetadata issue={i} canEdit={can} onChanged={refresh} />
      </div>
      <TabBar tabs={[{ id: 'links' as const, label: t('decision.links'), count: q.data.links.length }, ...(ItemSlots.Comments ? [{ id: 'comments' as const, label: t('common.comments') }] : []), { id: 'history' as const, label: t('common.history') }]} value={tab} onChange={setTab} />
      {tab === 'links' && <Links links={q.data.links} canEdit={can} projectId={i.projectId} onChange={refresh} onAdd={(x) => post(`issues/${i.id}/links`, { targetType: x.targetType, targetId: x.targetId })} />}
      {tab === 'comments' && <CommentsSlot type="Issue" id={i.id} projectId={i.projectId} />}
      {tab === 'history' && <HistoryList type="Issue" id={i.id} />}
      {step === 'Resolved' && <ResolveDialog i={i} onClose={() => setStep(null)} />}
      {step === 'Cancelled' && <ConfirmDialog open onOpenChange={(o) => !o && setStep(null)} destructive reason title={t('issue.cancelTitle', { key: i.key })} body={t('issue.cancelHint')}
        confirmLabel={t('issue.cancel')} onConfirm={(reason) => go('Cancelled', reason)} />}
      {step === 'In Progress' && <ConfirmDialog open onOpenChange={(o) => !o && setStep(null)} reason title={t('issue.reopenTitle', { key: i.key })} body={t('issue.reopenHint')}
        confirmLabel={t('issue.reopen')} onConfirm={(reason) => go('In Progress', reason)} />}
    </div>
  )
}

function IssueMetadata({ issue, canEdit, onChanged }: { issue: IssueRow; canEdit: boolean; onChanged: () => void }) {
  const me = useMe()
  const eligible = useQuery({ queryKey: ['issue-verifier-eligible', issue.projectId],
    queryFn: () => get<{ people: { id: string; displayName: string }[] }>(`projects/${issue.projectId}/changes/options`) })
  const locations = useQuery({ queryKey: ['issue-locations', issue.id], queryFn: () => get<IssueLocation[]>(`issues/${issue.id}/locations`) })
  const documents = useQuery({ queryKey: ['issue-documents', issue.id], queryFn: () => get<IssueDocument[]>(`issues/${issue.id}/documents`) })
  const verification = useQuery({ queryKey: ['issue-verification', issue.id], queryFn: () => get<IssueVerification[]>(`issues/${issue.id}/verification`) })
  const [kind, setKind] = useState('SiteArea'); const [siteArea, setSiteArea] = useState(''); const [building, setBuilding] = useState(''); const [level, setLevel] = useState(''); const [room, setRoom] = useState(''); const [assetSystem, setAssetSystem] = useState(''); const [alignment, setAlignment] = useState(''); const [start, setStart] = useState(''); const [end, setEnd] = useState(''); const [units, setUnits] = useState('m'); const [coordinateX, setCoordinateX] = useState(''); const [coordinateY, setCoordinateY] = useState(''); const [coordinateZ, setCoordinateZ] = useState(''); const [coordinateCrs, setCoordinateCrs] = useState(''); const [coordinateUnits, setCoordinateUnits] = useState('m')
  const [docKind, setDocKind] = useState('Drawing'); const [identifier, setIdentifier] = useState(''); const [revision, setRevision] = useState(''); const [sourceUrl, setSourceUrl] = useState(''); const [externalTopicId, setExternalTopicId] = useState(''); const [modelElementGuid, setModelElementGuid] = useState(''); const [viewpointUrl, setViewpointUrl] = useState(''); const [available, setAvailable] = useState(true)
  const [verifier, setVerifier] = useState<string | null>(null); const [note, setNote] = useState(''); const [evidence, setEvidence] = useState(''); const [busy, setBusy] = useState(false)
  const reload = async () => { await Promise.all([locations.refetch(), documents.refetch(), verification.refetch()]); onChanged() }
  const submit = async (path: string, body: Record<string, unknown>) => { setBusy(true); try { await post(path, { ...body, rowVersion: issue.rowVersion }, issue.rowVersion); await reload(); toast.success(t('issue.metadataSaved')) } catch (e) { toast.error(errorText(e)) } finally { setBusy(false) } }
  const addLocation = () => {
    if (kind === 'SiteArea' && !siteArea.trim()) { toast.error(t('issue.locationRequired')); return }
    if (kind === 'Building' && !building.trim()) { toast.error(t('issue.locationRequired')); return }
    if (kind === 'Alignment' && (!alignment.trim() || !start || !end || !units.trim())) { toast.error(t('issue.alignmentRequired')); return }
    if (kind === 'Coordinate' && (!coordinateX || !coordinateY || !coordinateCrs.trim() || !coordinateUnits.trim())) { toast.error(t('issue.coordinateRequired')); return }
    const numberOrNull = (value: string) => value.trim() ? Number(value) : null
    if ([coordinateX, coordinateY, coordinateZ, start, end].some((value) => value.trim() && !Number.isFinite(Number(value)))) { toast.error(t('issue.numberRequired')); return }
    submit(`issues/${issue.id}/locations`, { kind,
      siteArea: siteArea || null,
      building: kind === 'Building' ? building || null : null,
      level: kind === 'Building' ? level || null : null,
      room: kind === 'Building' ? room || null : null,
      assetSystem: assetSystem || null,
      alignment: kind === 'Alignment' ? alignment || null : null,
      startStation: kind === 'Alignment' ? numberOrNull(start) : null,
      endStation: kind === 'Alignment' ? numberOrNull(end) : null,
      stationUnits: kind === 'Alignment' ? units || null : null,
      coordinateX: kind === 'Coordinate' ? numberOrNull(coordinateX) : null,
      coordinateY: kind === 'Coordinate' ? numberOrNull(coordinateY) : null,
      coordinateZ: kind === 'Coordinate' ? numberOrNull(coordinateZ) : null,
      coordinateReferenceSystem: kind === 'Coordinate' ? coordinateCrs || null : null,
      coordinateUnits: kind === 'Coordinate' ? coordinateUnits || null : null })
  }
  const addDocument = () => {
    if (!identifier.trim() || !revision.trim() || !sourceUrl.trim()) { toast.error(t('issue.documentRequired')); return }
    submit(`issues/${issue.id}/documents`, { kind: docKind, identifier, revision, sourceUrl, externalTopicId: externalTopicId || null, modelElementGuid: modelElementGuid || null, viewpointUrl: viewpointUrl || null, isAvailable: available })
  }
  const appoint = () => verifier && submit(`issues/${issue.id}/verification`, { verifierId: verifier, status: 'Proposed', note })
  const decide = (v: IssueVerification, status: 'Verified' | 'Rejected') => submit(`issues/${issue.id}/verification`, { verifierId: v.verifierId, status, evidenceUrl: evidence || null, note })
  const latestVerification = verification.data?.[0]
  return <section className="mt-4 space-y-3 rounded-lg border bg-card p-3" aria-labelledby="issue-metadata-title">
    <h3 id="issue-metadata-title" className="font-semibold">{t('issue.locationEvidence')}</h3>
    <div className="space-y-2">
      <div className="text-sm font-medium">{t('issue.locations')}</div>
      {(locations.data ?? []).map((x) => <div key={x.id} className="rounded border p-2 text-sm"><Chip tone="idle">{x.kind}</Chip> <span>{[x.siteArea, x.building, x.level, x.room, x.assetSystem, x.alignment].filter(Boolean).join(' · ')}</span>{x.startStation != null && ` · ${x.startStation}–${x.endStation} ${x.stationUnits ?? ''}`}{x.coordinateX != null && ` · (${x.coordinateX}, ${x.coordinateY}${x.coordinateZ != null ? `, ${x.coordinateZ}` : ''}) ${x.coordinateReferenceSystem ?? ''} ${x.coordinateUnits ?? ''}`}</div>)}
      {!locations.data?.length && <p className="text-sm text-muted-foreground">{t('issue.noLocations')}</p>}
      {canEdit && <div className="grid gap-2 md:grid-cols-4">
        <select className={selectCls} value={kind} onChange={(e) => setKind(e.target.value)} aria-label={t('issue.locationKind')}><option>SiteArea</option><option>Building</option><option>Alignment</option><option>Coordinate</option></select>
        <Input value={siteArea} onChange={(e) => setSiteArea(e.target.value)} placeholder={t('issue.siteArea')} aria-label={t('issue.siteArea')} />
        <Input value={building} onChange={(e) => setBuilding(e.target.value)} placeholder={t('issue.building')} aria-label={t('issue.building')} />
        <Input value={level} onChange={(e) => setLevel(e.target.value)} placeholder={t('issue.level')} aria-label={t('issue.level')} />
        <Input value={room} onChange={(e) => setRoom(e.target.value)} placeholder={t('issue.room')} aria-label={t('issue.room')} />
        <Input value={assetSystem} onChange={(e) => setAssetSystem(e.target.value)} placeholder={t('issue.assetSystem')} aria-label={t('issue.assetSystem')} />
        {(kind === 'Alignment' || kind === 'Building') && <><Input value={alignment} onChange={(e) => setAlignment(e.target.value)} placeholder={t('issue.alignment')} aria-label={t('issue.alignment')} /><Input value={start} onChange={(e) => setStart(e.target.value)} type="number" placeholder={t('issue.startStation')} aria-label={t('issue.startStation')} /><Input value={end} onChange={(e) => setEnd(e.target.value)} type="number" placeholder={t('issue.endStation')} aria-label={t('issue.endStation')} /><Input value={units} onChange={(e) => setUnits(e.target.value)} placeholder={t('issue.units')} aria-label={t('issue.units')} /></>}
        {kind === 'Coordinate' && <><Input value={coordinateX} onChange={(e) => setCoordinateX(e.target.value)} type="number" placeholder={t('issue.coordinateX')} aria-label={t('issue.coordinateX')} /><Input value={coordinateY} onChange={(e) => setCoordinateY(e.target.value)} type="number" placeholder={t('issue.coordinateY')} aria-label={t('issue.coordinateY')} /><Input value={coordinateZ} onChange={(e) => setCoordinateZ(e.target.value)} type="number" placeholder={t('issue.coordinateZ')} aria-label={t('issue.coordinateZ')} /><Input value={coordinateCrs} onChange={(e) => setCoordinateCrs(e.target.value)} placeholder={t('issue.coordinateCrs')} aria-label={t('issue.coordinateCrs')} /><Input value={coordinateUnits} onChange={(e) => setCoordinateUnits(e.target.value)} placeholder={t('issue.coordinateUnits')} aria-label={t('issue.coordinateUnits')} /></>}
        <Button type="button" disabled={busy} onClick={addLocation}>{t('common.add')}</Button>
      </div>}
    </div>
    <div className="space-y-2">
      <div className="text-sm font-medium">{t('issue.documentReferences')}</div>
      {(documents.data ?? []).map((x) => <div key={x.id} className="rounded border p-2 text-sm"><Chip tone={x.isAvailable ? 'done' : 'bad'}>{x.isAvailable ? t('issue.available') : t('issue.unavailable')}</Chip> {x.kind} · <strong>{x.identifier}</strong> · {t('issue.revision')} {x.revision} · {x.isAvailable ? <a className="underline" href={x.sourceUrl} target="_blank" rel="noreferrer">{t('issue.openSource')}</a> : <span className="text-muted-foreground">{t('issue.sourceUnavailable')}</span>} {(x.externalTopicId || x.modelElementGuid || x.viewpointUrl) && <span className="block text-muted-foreground">{x.externalTopicId && `${t('issue.externalTopic')} ${x.externalTopicId}`} {x.modelElementGuid && ` · ${t('issue.modelGuid')} ${x.modelElementGuid}`} {x.viewpointUrl && <> · <a className="underline" href={x.viewpointUrl} target="_blank" rel="noreferrer">{t('issue.viewpoint')}</a></>}</span>}</div>)}
      {canEdit && <div className="grid gap-2 md:grid-cols-4"><select className={selectCls} value={docKind} onChange={(e) => setDocKind(e.target.value)} aria-label={t('issue.documentKind')}><option>Drawing</option><option>Model</option><option>Markup</option><option>Screenshot</option></select><Input value={identifier} onChange={(e) => setIdentifier(e.target.value)} placeholder={t('issue.identifier')} aria-label={t('issue.identifier')} /><Input value={revision} onChange={(e) => setRevision(e.target.value)} placeholder={t('issue.revision')} aria-label={t('issue.revision')} /><Input value={sourceUrl} onChange={(e) => setSourceUrl(e.target.value)} placeholder={t('issue.sourceUrl')} aria-label={t('issue.sourceUrl')} /><Input value={externalTopicId} onChange={(e) => setExternalTopicId(e.target.value)} placeholder={t('issue.externalTopic')} aria-label={t('issue.externalTopic')} /><Input value={modelElementGuid} onChange={(e) => setModelElementGuid(e.target.value)} placeholder={t('issue.modelGuid')} aria-label={t('issue.modelGuid')} /><Input value={viewpointUrl} onChange={(e) => setViewpointUrl(e.target.value)} placeholder={t('issue.viewpointUrl')} aria-label={t('issue.viewpointUrl')} /><label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={available} onChange={(e) => setAvailable(e.target.checked)} />{t('issue.available')}</label><Button type="button" disabled={busy} onClick={addDocument}>{t('common.add')}</Button></div>}
    </div>
    <div className="space-y-2">
      <div className="text-sm font-medium">{t('issue.verificationFlow')}</div>
      {(verification.data ?? []).map((v) => <div key={v.id} className="rounded border p-2 text-sm"><StatusPill status={v.status} /> {v.verifierId === me.id && <span>{t('issue.assignedToYou')}</span>} {v.note && <span className="text-muted-foreground">· {v.note}</span>}{v.id === latestVerification?.id && v.status === 'Proposed' && v.verifierId === me.id && <div className="mt-2 flex gap-2"><Input value={evidence} onChange={(e) => setEvidence(e.target.value)} placeholder={t('issue.evidenceUrl')} aria-label={t('issue.evidenceUrl')} /><Button type="button" disabled={busy} onClick={() => decide(v, 'Verified')}>{t('issue.verify')}</Button><Button type="button" variant="outline" disabled={busy} onClick={() => decide(v, 'Rejected')}>{t('issue.reject')}</Button></div>}</div>)}
      {canEdit && latestVerification?.status !== 'Proposed' && <div className="grid gap-2 md:grid-cols-3"><PeoplePicker value={verifier} onChange={(id) => setVerifier(id)} label={t('issue.verifier')}
        disabled={!eligible.data} candidates={eligible.data?.people.filter(p => p.id !== issue.ownerId && p.id !== issue.raisedById)} /><Textarea value={note} onChange={(e) => setNote(e.target.value)} placeholder={t('issue.appointmentReason')} aria-label={t('issue.appointmentReason')} /><Button type="button" disabled={busy || !verifier || !note.trim()} onClick={appoint}>{t('issue.appointVerifier')}</Button></div>}
    </div>
  </section>
}

PANELS.Risk = { component: RiskPanel }
PANELS.Issue = { component: IssuePanel }
ItemSlots.Raise = RaiseMenu
