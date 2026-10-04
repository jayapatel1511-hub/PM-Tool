import { ViewMenu } from '@/components/hub/views'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ListPlus, Lock, OctagonAlert, Plus, ShieldAlert, Trash2 } from 'lucide-react'
import { useId, useState, type ReactElement, type ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ActiveFilters, ChipToggle, ConfirmDialog, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Notice, Page, Section, Spinner, TableRegion, selectCls } from '@/components/hub/common'
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
import { fmtDate, fmtTime, today } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import type { ProjectDiscipline } from '@/lib/types'
import { cn } from '@/lib/utils'
import { DateText, FieldGroup, GroupRow, HeadCell, ItemPicker, PanelHead, Person, RegisterCards, SELECTED_ROW, TITLE_LINK, type Pick } from './Decisions'
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
  id: string; projectId: string; key: string; title: string; status: string; issueType: string; raisedById: string; raisedByName?: string; ownerId: string; ownerName?: string
  severity: string; dateRaised: string; targetResolutionDate?: string; isOverdue: boolean; daysOverdue: number; resolution?: string; resolvedDate?: string
  originRiskId?: string; originRiskKey?: string; projectDisciplineId?: string; disciplineName?: string; rowVersion: number
  locationSummary?: string; locationLabels?: string[]; affectedDisciplineIds?: string[]; affectedDisciplineNames?: string[]; affectedDisciplineSummary?: string
  documentSummary?: string; documentIdentifiers?: string[]; documentRevisions?: string[]; verificationStatus?: string
  groupLabel?: string
}
interface Perm { ok: boolean; reason?: string | null }
interface LinkRow { id: string; targetType: string; targetId: string; key: string; name: string; status?: string; date?: string; person?: string }
interface Detail { description?: string; project: { id: string; projectNumber: string; name: string }; links: LinkRow[]; permissions: { edit: Perm; changeType?: Perm; transitions: { to: string; ok: boolean; reason?: string | null }[]; comment: boolean } }
interface RiskDetail extends Detail { risk: RiskRow; realisedIssue?: { id: string; key: string; title: string; status: string } | null }
interface IssueDetail extends Detail { issue: IssueRow; originRisk?: { id: string; key: string; title: string; status: string } | null }
interface IssueLocation { id: string; kind: string; siteArea?: string; building?: string; level?: string; room?: string; assetSystem?: string; alignment?: string; startStation?: number; endStation?: number; stationUnits?: string; coordinateX?: number; coordinateY?: number; coordinateZ?: number; coordinateReferenceSystem?: string; coordinateUnits?: string; rowVersion: number }
interface IssueDocument { sourceSystem?: string; stableSourceId?: string; registeredBy?: string; registeredAt?: string; registrationMethod?: string; replacedById?: string | null; id: string; kind: string; identifier: string; revision: string; sourceUrl: string; externalTopicId?: string; modelElementGuid?: string; viewpointUrl?: string; isAvailable: boolean; rowVersion: number }
interface IssueVerification { id: string; verifierId: string; status: string; evidenceUrl?: string; note?: string; verifiedAt?: string; rowVersion: number }
interface IssueReferenceImpact {
  id: string; documentReferenceId: string; previousRevisionId: string; currentRevisionId: string; ownerId: string; verifierId?: string | null
  status: 'Pending' | 'Unaffected' | 'ReopenRequested'; ownerDisposition?: 'Unaffected' | 'Reopen' | null; verifierDisposition?: 'Unaffected' | 'Reopen' | null
  ownerDecidedAt?: string | null; verifierDecidedAt?: string | null; ownerReason?: string | null; verifierReason?: string | null; rowVersion: number
  previousRevision?: { sourceKey?: string; externalIdentifier?: string; revision?: string } | null
  currentRevision?: { sourceKey?: string; externalIdentifier?: string; revision?: string } | null
  documentReference?: { identifier?: string; revision?: string; kind?: string } | null
}
type Target = { type: 'Task' | 'Deliverable'; id: string; key: string; name: string }

const RISK_STATUSES = ['Open', 'Monitoring', 'Closed', 'Realised']
const ISSUE_STATUSES = ['Open', 'In Progress', 'Resolved', 'Cancelled']
const SEVERITIES = ['High', 'Medium', 'Low']
const ISSUE_TYPES = ['General', 'Coordination']
const LEVELS = [1, 2, 3]
const TONE = { High: 'bad', Medium: 'warn', Low: 'idle' } as const
const FILTERS = ['q', 'status', 'severity', 'ownerId', 'disciplineId', 'indicator'] as const
const SOURCE_FILTERS = ['issueType', 'location', 'document', 'revision', 'verification', 'alignment', 'stationFrom', 'stationTo', 'stationUnits'] as const
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
  const set = (k: string, v?: string | null) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); n.delete('page'); setSp(n, { replace: true }) }
  const filters = Object.fromEntries(FILTERS.map((k) => [k, sp.get(k)])) as Record<(typeof FILTERS)[number], string | null>
  const clear = () => { const n = new URLSearchParams(sp); FILTERS.forEach((k) => n.delete(k)); n.delete('page'); setSp(n, { replace: true }) }
  return { filters, set, clear, active: FILTERS.some((k) => sp.has(k)) }
}

type Token = { key: string; label: string; value: ReactNode }

/** Labelled register filters, the indicator as a quick chip and active filters as removable tokens (§13.0 Filters). */
function RegisterFilters({ f, statuses, open, indicator, owners, disciplines, extra, extraTokens = [], onClear = f.clear }: {
  f: ReturnType<typeof useFilters>; statuses: string[]; open: string; indicator: [string, string]; owners: [string, string][]; disciplines: ProjectDiscipline[]
  extra?: ReactNode; extraTokens?: Token[]; onClear?: () => void
}) {
  const { q, status, severity, ownerId, disciplineId, indicator: shown } = f.filters
  const on = shown === indicator[0]
  const tokens = [
    q && { key: 'q', label: t('common.search'), value: q },
    status && { key: 'status', label: t('common.status'), value: status === open ? t('register.openStatuses') : tv(status) },
    severity && { key: 'severity', label: t('register.severity'), value: tv(severity) },
    ownerId && { key: 'ownerId', label: t('common.owner'), value: owners.find(([id]) => id === ownerId)?.[1] ?? <Missing /> },
    disciplineId && { key: 'disciplineId', label: t('common.discipline'), value: disciplines.find((d) => d.id === disciplineId)?.name ?? <Missing /> },
    shown && { key: 'indicator', label: t('deliverable.indicator'), value: on ? t(indicator[1]) : shown },
  ].filter(Boolean) as Token[]
  return (
    <FilterBar>
      <div className="flex flex-wrap items-end gap-3">
        <Field label={t('common.search')} htmlFor="register-search" className="w-full sm:w-56">
          <Input id="register-search" type="search" value={q ?? ''} onChange={(e) => f.set('q', e.target.value)} />
        </Field>
        <Field label={t('common.status')} htmlFor="register-status" className="w-full sm:w-44">
          <select id="register-status" className={selectCls} value={status ?? ''} onChange={(e) => f.set('status', e.target.value)}>
            <option value="">{t('register.anyStatus')}</option><option value={open}>{t('register.openStatuses')}</option>
            {statuses.map((s) => <option key={s} value={s}>{tv(s)}</option>)}
          </select>
        </Field>
        <Field label={t('register.severity')} htmlFor="register-severity" className="w-full sm:w-40">
          <select id="register-severity" className={selectCls} value={severity ?? ''} onChange={(e) => f.set('severity', e.target.value)}>
            <option value="">{t('register.anySeverity')}</option>{SEVERITIES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}
          </select>
        </Field>
        <Field label={t('common.owner')} htmlFor="register-owner" className="w-full sm:w-48">
          <select id="register-owner" className={selectCls} value={ownerId ?? ''} onChange={(e) => f.set('ownerId', e.target.value)}>
            <option value="">{t('decision.anyOwner')}</option>{owners.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
          </select>
        </Field>
        <Field label={t('common.discipline')} htmlFor="register-discipline" className="w-full sm:w-48">
          <select id="register-discipline" className={selectCls} value={disciplineId ?? ''} onChange={(e) => f.set('disciplineId', e.target.value)}>
            <option value="">{t('register.anyDiscipline')}</option>{disciplines.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        </Field>
      </div>
      <div className="flex flex-wrap items-center gap-2" role="group" aria-label={t('task.quickFilters')}>
        <ChipToggle on={on} onClick={() => f.set('indicator', on ? null : indicator[0])}>{t(indicator[1])}</ChipToggle>
      </div>
      {extra}
      <ActiveFilters tokens={[...tokens, ...extraTokens]} onRemove={(k) => f.set(k, null)} onClear={onClear} />
    </FilterBar>
  )
}

function owners(rows: { ownerId: string; ownerName?: string }[] | undefined): [string, string][] {
  const m = new Map<string, string>()
  for (const r of rows ?? []) if (r.ownerName) m.set(r.ownerId, r.ownerName)
  return [...m.entries()].sort((a, b) => a[1].localeCompare(b[1]))
}

function RegisterTable<T extends { id: string }>({ table, rows, loading, empty, current, groupBy, caption }: {
  table: { visible: Column<T>[]; header: (c: Column<T>) => ReactElement; cell: (c: Column<T>, r: T) => ReactNode }; rows: T[]; loading: boolean; empty: ReactNode
  current: (r: T) => boolean; groupBy?: (r: T) => string; caption: string
}) {
  if (loading) return <div className="rounded-lg border bg-card"><Loading rows={6} /></div>
  if (!rows.length) return <div className="rounded-lg border bg-card">{empty}</div>
  const groups: { label: string; items: T[] }[] = []
  for (const row of rows) {
    const label = groupBy?.(row) ?? ''
    if (groups.at(-1)?.label !== label) groups.push({ label, items: [] })
    groups.at(-1)!.items.push(row)
  }
  return (<>
    <RegisterCards groups={groups.map((g) => ({ label: g.label, rows: g.items }))} columns={table.visible} current={current} />
    <TableRegion className="hidden md:block">
      <table className="w-full text-sm">
        <caption className="sr-only">{caption}</caption>
        <thead className="bg-muted text-left text-muted-foreground"><tr>{table.visible.map((c) => <HeadCell key={c.id} th={table.header(c)} right={c.className?.includes('text-right')} />)}</tr></thead>
        {groups.map((group, index) => <tbody key={`${index}-${group.label}`}>
          {groupBy && <GroupRow span={table.visible.length} label={group.label} count={group.items.length} />}
          {group.items.map((r) => <tr key={r.id} className={cn('border-t hover:bg-muted', current(r) && SELECTED_ROW)}>{table.visible.map((c) => table.cell(c, r))}</tr>)}
        </tbody>)}
      </table>
    </TableRegion>
  </>)
}

export function RisksTab() {
  const p = useCurrentProject()
  const f = useFilters()
  const openPanel = useItemPanel()
  const [sp] = useSearchParams()
  const panel = sp.get('panel')
  const [raising, setRaising] = useState(false)
  const q = useQuery({ queryKey: ['p', p.id, 'risks', f.filters], queryFn: () => get<RiskRow[]>(`projects/${p.id}/risks${qs(f.filters)}`) })
  const table = useTable<RiskRow>('hub.riskColumns', [
    { id: 'key', label: t('milestone.key'), fixed: true, sort: (r) => r.key, className: 'whitespace-nowrap', cell: (r) => <Key>{r.key}</Key> },
    { id: 'title', label: t('register.title'), fixed: true, sort: (r) => r.title.toLowerCase(), className: 'min-w-[14rem]',
      cell: (r) => <button className={TITLE_LINK} aria-current={panel === `Risk:${r.id}` || undefined} onClick={() => openPanel('Risk', r.id)}>{r.title}</button> },
    { id: 'severity', label: t('register.severity'), sort: (r) => -r.score, className: 'whitespace-nowrap', cell: (r) => <Severity level={r.band} score={r.score} /> },
    { id: 'status', label: t('common.status'), sort: (r) => r.status, cell: (r) => <StatusPill status={r.status} /> },
    { id: 'owner', label: t('common.owner'), sort: (r) => r.ownerName, className: 'whitespace-nowrap', cell: (r) => <Person id={r.ownerId} name={r.ownerName} /> },
    { id: 'discipline', label: t('common.discipline'), sort: (r) => r.disciplineName, className: 'whitespace-nowrap', cell: (r) => r.disciplineName ?? <Missing /> },
    { id: 'review', label: t('risk.reviewDate'), sort: (r) => r.reviewDate, className: 'whitespace-nowrap text-right',
      cell: (r) => <span className="inline-flex flex-wrap items-center justify-end gap-x-2 gap-y-1"><DateText date={r.reviewDate} />{r.isReviewOverdue && <Chip tone="bad">{t('risk.reviewOverdue')}</Chip>}</span> },
    { id: 'mitigation', label: t('risk.mitigation'), className: 'min-w-48', cell: (r) => r.mitigation ? <span className="line-clamp-2 text-muted-foreground">{r.mitigation}</span> : <Missing /> },
    { id: 'realised', label: t('risk.realisedAs'), optional: true, cell: (r) => r.realisedIssueId ? <button onClick={() => openPanel('Issue', r.realisedIssueId!)} className="hover:underline"><Key>{r.realisedIssueKey}</Key></button> : null },
  ], q.data ?? [], (r) => [r.reviewDate, r.key])
  const can = p.permissions.raiseRegister
  return (
    <Page title={t('ptab.risks')} subtitle={t('risk.subtitle')}
      actions={<>
        {table.menu}
        <ExportMenu path={`projects/${p.id}/risks/export`} params={f.filters} name={`${p.projectNumber}-risks`} />
        {can.ok && <Button onClick={() => setRaising(true)}><Plus className="size-4" />{t('risk.new')}</Button>}
      </>}>
      {!can.ok && can.reason && <Notice icon={Lock} title={can.reason} />}
      <RegisterFilters f={f} statuses={RISK_STATUSES} open="Open,Monitoring" indicator={['reviewOverdue', 'risk.reviewOverdue']} owners={owners(q.data)} disciplines={p.disciplines} />
      {q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : (
        <RegisterTable table={table} rows={table.sorted} loading={q.isPending} caption={t('ptab.risks')} current={(r) => panel === `Risk:${r.id}`}
          empty={f.active
            ? <Empty title={t('register.noMatch')} action={<Button variant="outline" onClick={f.clear}>{t('filters.clear')}</Button>}>{t('register.noMatchHint')}</Empty>
            : <Empty title={t('risk.empty')} action={can.ok && <Button variant="outline" onClick={() => setRaising(true)}><Plus className="size-4" />{t('risk.new')}</Button>}>{t('risk.emptyHint')}</Empty>} />
      )}
      {raising && <RiskForm projectId={p.id} onClose={() => setRaising(false)} />}
    </Page>
  )
}

export function IssuesTab() {
  const p = useCurrentProject()
  const f = useFilters()
  const [sp, setSp] = useSearchParams()
  const issueFilters = { ...f.filters, location: sp.get('location'), document: sp.get('document'),
    revision: sp.get('revision'), verification: sp.get('verification'), alignment: sp.get('alignment'),
    stationFrom: sp.get('stationFrom'), stationTo: sp.get('stationTo'), stationUnits: sp.get('stationUnits'), issueType: sp.get('issueType') }
  const sourceFilterActive = SOURCE_FILTERS.some((key) => sp.has(key))
  const setSourceFilter = (key: string, value: string) => {
    const next = new URLSearchParams(sp)
    if (value) next.set(key, value); else next.delete(key)
    next.delete('page')
    setSp(next, { replace: true })
  }
  // One Clear for both filter rows; grouping is a view choice and stays.
  const clearFilters = () => {
    const next = new URLSearchParams(sp)
    for (const key of [...FILTERS, ...SOURCE_FILTERS, 'page']) next.delete(key)
    setSp(next, { replace: true })
  }
  const openPanel = useItemPanel()
  const panel = sp.get('panel')
  const [raising, setRaising] = useState(false)
  const q = useQuery({ queryKey: ['p', p.id, 'issues', issueFilters], queryFn: () => get<IssueRow[]>(`projects/${p.id}/issues${qs(issueFilters)}`) })
  const verificationLabel = (v: string) => v === 'None' ? t('issue.noVerification') : v === 'Stale' ? t('issue.staleVerification') : tv(v)
  const table = useTable<IssueRow>('hub.issueColumns', [
    { id: 'key', label: t('milestone.key'), fixed: true, sort: (r) => r.key, className: 'whitespace-nowrap', cell: (r) => <Key>{r.key}</Key> },
    { id: 'title', label: t('register.title'), fixed: true, sort: (r) => r.title.toLowerCase(), className: 'min-w-[14rem]',
      cell: (r) => <button className={TITLE_LINK} aria-current={panel === `Issue:${r.id}` || undefined} onClick={() => openPanel('Issue', r.id)}>{r.title}</button> },
    { id: 'type', label: t('issue.type'), sort: (r) => r.issueType, className: 'whitespace-nowrap', cell: (r) => tv(r.issueType) },
    { id: 'severity', label: t('register.severity'), sort: (r) => SEVERITIES.indexOf(r.severity), cell: (r) => <Severity level={r.severity} /> },
    { id: 'status', label: t('common.status'), sort: (r) => r.status, cell: (r) => <StatusPill status={r.status} /> },
    { id: 'owner', label: t('common.owner'), sort: (r) => r.ownerName, className: 'whitespace-nowrap', cell: (r) => <Person id={r.ownerId} name={r.ownerName} /> },
    { id: 'discipline', label: t('common.discipline'), sort: (r) => r.disciplineName, className: 'whitespace-nowrap', cell: (r) => r.disciplineName ?? <Missing /> },
    { id: 'affected', label: t('issue.affectedDisciplines'), optional: true, sort: (r) => r.affectedDisciplineSummary, className: 'min-w-40', cell: (r) => r.affectedDisciplineSummary || <Missing /> },
    { id: 'raised', label: t('issue.dateRaised'), sort: (r) => r.dateRaised, className: 'text-right', cell: (r) => <DateText date={r.dateRaised} /> },
    { id: 'target', label: t('issue.target'), sort: (r) => r.targetResolutionDate, className: 'whitespace-nowrap text-right',
      cell: (r) => <span className="inline-flex flex-wrap items-center justify-end gap-x-2 gap-y-1"><DateText date={r.targetResolutionDate} />{r.isOverdue && <Chip tone="bad">{t('ind.overdueD', { n: r.daysOverdue })}</Chip>}</span> },
    { id: 'raisedBy', label: t('issue.raisedBy'), optional: true, sort: (r) => r.raisedByName, className: 'whitespace-nowrap', cell: (r) => r.raisedByName ?? <Missing /> },
    { id: 'origin', label: t('issue.fromRisk'), optional: true, cell: (r) => r.originRiskId ? <button onClick={() => openPanel('Risk', r.originRiskId!)} className="hover:underline"><Key>{r.originRiskKey}</Key></button> : null },
    { id: 'location', label: t('issue.locations'), sort: (r) => r.locationSummary, className: 'min-w-48', cell: (r) => r.locationSummary || <Missing /> },
    { id: 'documents', label: t('issue.documentReferences'), optional: true, sort: (r) => r.documentSummary, className: 'min-w-48', cell: (r) => r.documentSummary || <Missing /> },
    { id: 'verification', label: t('issue.verificationFlow'), sort: (r) => r.verificationStatus, className: 'whitespace-nowrap', cell: (r) =>
      r.verificationStatus === 'None' ? t('issue.noVerification') : r.verificationStatus === 'Stale' ? <Chip tone="warn">{t('issue.staleVerification')}</Chip> :
        r.verificationStatus ? <StatusPill status={r.verificationStatus} /> : <Missing /> },
  ], q.data ?? [], (r) => [r.targetResolutionDate, r.key])
  const group = sp.get('group')
  // An issue appears once under each of its locations, disciplines (primary and affected), documents or revisions.
  const multiGroup: Record<string, [(r: IssueRow) => (string | undefined)[] | undefined, string]> = {
    location: [(r) => r.locationLabels, t('issue.noLocationGroup')],
    discipline: [(r) => [r.disciplineName, ...(r.affectedDisciplineNames ?? [])], t('register.noDiscipline')],
    document: [(r) => r.documentIdentifiers, t('issue.noDocumentGroup')],
    revision: [(r) => r.documentRevisions, t('issue.noRevisionGroup')],
  }
  const multi = group ? multiGroup[group] : undefined
  const groupBy = multi ? (r: IssueRow) => r.groupLabel || t('common.dash')
    : group === 'owner' ? (r: IssueRow) => r.ownerName || t('common.dash')
      : group === 'verification' ? (r: IssueRow) => r.verificationStatus || t('issue.noVerification') : undefined
  const pageSize = 50, total = table.sorted.length
  const page = Math.max(1, Math.min(Math.ceil(total / pageSize) || 1, Math.floor(Number(sp.get('page')) || 1)))
  // ponytail: load the filtered register to keep global sort/group order; move paging into SQL if the list payload fails the performance gate.
  const pageRows = table.sorted.slice((page - 1) * pageSize, page * pageSize)
  const expandedRows = multi
    ? pageRows.flatMap((row) => {
      const labels = [...new Set(multi[0](row)?.filter((x): x is string => !!x))]
      return (labels.length ? labels : [multi[1]]).map((groupLabel) => ({ ...row, groupLabel }))
    }) : pageRows
  // Array.sort is stable, so the register's selected sort order remains intact within each group.
  const displayRows = groupBy ? [...expandedRows].sort((a, b) => groupBy(a).localeCompare(groupBy(b))) : expandedRows
  const can = p.permissions.raiseRegister
  const sourceTokens = [
    issueFilters.issueType && { key: 'issueType', label: t('issue.filterType'), value: tv(issueFilters.issueType) },
    issueFilters.location && { key: 'location', label: t('issue.filterLocation'), value: issueFilters.location },
    issueFilters.document && { key: 'document', label: t('issue.filterDocument'), value: issueFilters.document },
    issueFilters.revision && { key: 'revision', label: t('issue.filterRevision'), value: issueFilters.revision },
    issueFilters.alignment && { key: 'alignment', label: t('issue.filterAlignment'), value: issueFilters.alignment },
    issueFilters.stationFrom && { key: 'stationFrom', label: t('issue.stationFrom'), value: issueFilters.stationFrom },
    issueFilters.stationTo && { key: 'stationTo', label: t('issue.stationTo'), value: issueFilters.stationTo },
    issueFilters.stationUnits && { key: 'stationUnits', label: t('issue.stationUnits'), value: issueFilters.stationUnits },
    issueFilters.verification && { key: 'verification', label: t('issue.filterVerification'), value: verificationLabel(issueFilters.verification) },
  ].filter(Boolean) as Token[]
  const pages = Math.max(1, Math.ceil(total / pageSize))
  const toPage = (n: number) => { const next = new URLSearchParams(sp); next.set('page', String(n)); setSp(next) }
  return (
    <Page title={t('ptab.issues')} subtitle={t('issue.subtitle')}
      actions={<>
        {table.menu}
        <ViewMenu listType="issues" projectId={p.id} />
        <ExportMenu path={`projects/${p.id}/issues/export`} params={issueFilters} name={`${p.projectNumber}-issues`} />
        {can.ok && <Button onClick={() => setRaising(true)}><Plus className="size-4" />{t('issue.new')}</Button>}
      </>}>
      {!can.ok && can.reason && <Notice icon={Lock} title={can.reason} />}
      <RegisterFilters f={f} statuses={ISSUE_STATUSES} open="Open,In Progress" indicator={['overdue', 'ind.overdue']} owners={owners(q.data)} disciplines={p.disciplines}
        extraTokens={sourceTokens} onClear={clearFilters} extra={
          <div role="group" aria-labelledby="issue-source-filters" className="space-y-3 border-t pt-4">
            <p id="issue-source-filters" className="text-sm font-medium">{t('issue.sourceFilters')}</p>
            <div className="flex flex-wrap items-end gap-3">
              <Field label={t('issue.filterType')} htmlFor="issue-type-filter" className="w-full sm:w-40">
                <select id="issue-type-filter" className={selectCls} value={issueFilters.issueType ?? ''} onChange={(e) => setSourceFilter('issueType', e.target.value)}>
                  <option value="">{t('issue.anyType')}</option>
                  {ISSUE_TYPES.map((x) => <option key={x} value={x}>{tv(x)}</option>)}
                </select>
              </Field>
              <Field label={t('issue.filterLocation')} htmlFor="issue-location-filter" className="w-full sm:w-44">
                <Input id="issue-location-filter" type="search" value={issueFilters.location ?? ''} onChange={(e) => setSourceFilter('location', e.target.value)} />
              </Field>
              <Field label={t('issue.filterDocument')} htmlFor="issue-document-filter" className="w-full sm:w-48">
                <Input id="issue-document-filter" type="search" value={issueFilters.document ?? ''} onChange={(e) => setSourceFilter('document', e.target.value)} />
              </Field>
              <Field label={t('issue.filterRevision')} htmlFor="issue-revision-filter" className="w-full sm:w-28">
                <Input id="issue-revision-filter" type="search" value={issueFilters.revision ?? ''} onChange={(e) => setSourceFilter('revision', e.target.value)} />
              </Field>
              <Field label={t('issue.filterAlignment')} htmlFor="issue-alignment-filter" className="w-full sm:w-36">
                <Input id="issue-alignment-filter" type="search" value={issueFilters.alignment ?? ''} onChange={(e) => setSourceFilter('alignment', e.target.value)} />
              </Field>
              <Field label={t('issue.stationFrom')} htmlFor="issue-station-from" className="w-full sm:w-28">
                <Input id="issue-station-from" type="number" className="tabular-nums" value={issueFilters.stationFrom ?? ''} onChange={(e) => setSourceFilter('stationFrom', e.target.value)} />
              </Field>
              <Field label={t('issue.stationTo')} htmlFor="issue-station-to" className="w-full sm:w-28">
                <Input id="issue-station-to" type="number" className="tabular-nums" value={issueFilters.stationTo ?? ''} onChange={(e) => setSourceFilter('stationTo', e.target.value)} />
              </Field>
              <Field label={t('issue.stationUnits')} htmlFor="issue-station-units" className="w-full sm:w-24">
                <Input id="issue-station-units" value={issueFilters.stationUnits ?? ''} onChange={(e) => setSourceFilter('stationUnits', e.target.value)} />
              </Field>
              <Field label={t('issue.filterVerification')} htmlFor="issue-verification-filter" className="w-full sm:w-48">
                <select id="issue-verification-filter" className={selectCls} value={issueFilters.verification ?? ''} onChange={(e) => setSourceFilter('verification', e.target.value)}>
                  <option value="">{t('issue.anyVerification')}</option>
                  {['Proposed', 'Verified', 'Rejected'].map((status) => <option key={status} value={status}>{tv(status)}</option>)}
                  <option value="Stale">{t('issue.staleVerification')}</option>
                  <option value="None">{t('issue.noVerification')}</option>
                </select>
              </Field>
              <Field label={t('issue.groupBy')} htmlFor="issue-group" className="w-full sm:w-52">
                <select id="issue-group" className={selectCls} value={group ?? ''} onChange={(e) => setSourceFilter('group', e.target.value)}>
                  <option value="">{t('common.noGroup')}</option><option value="location">{t('issue.groupLocation')}</option>
                  <option value="discipline">{t('issue.groupDiscipline')}</option><option value="owner">{t('issue.groupOwner')}</option>
                  <option value="document">{t('issue.groupDocument')}</option><option value="revision">{t('issue.groupRevision')}</option>
                  <option value="verification">{t('issue.groupVerification')}</option>
                </select>
              </Field>
            </div>
          </div>} />
      {q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : (
        <RegisterTable table={table} rows={displayRows} loading={q.isPending} groupBy={groupBy} caption={t('ptab.issues')} current={(r) => panel === `Issue:${r.id}`}
          empty={f.active || sourceFilterActive
            ? <Empty title={t('register.noMatch')} action={<Button variant="outline" onClick={clearFilters}>{t('filters.clear')}</Button>}>{t('register.noMatchHint')}</Empty>
            : <Empty title={t('issue.empty')} action={can.ok && <Button variant="outline" onClick={() => setRaising(true)}><Plus className="size-4" />{t('issue.new')}</Button>}>{t('issue.emptyHint')}</Empty>} />
      )}
      {!q.isPending && !q.error && <nav aria-label={t('issue.pager')} className="flex items-center justify-end gap-3">
        <Button variant="outline" disabled={page <= 1} onClick={() => toPage(page - 1)}>{t('handoff.previous')}</Button>
        <span className="text-sm tabular-nums" aria-live="polite">{t('common.pageOf', { page, pages })}</span>
        <Button variant="outline" disabled={page * pageSize >= total} onClick={() => toPage(page + 1)}>{t('handoff.next')}</Button>
      </nav>}
      {raising && <IssueForm projectId={p.id} onClose={() => setRaising(false)} />}
    </Page>
  )
}

// ---------- Raise (FR-001, FR-002, FR-005) ----------

/** Probability and impact from three labelled levels with plain-language anchors (§12.10, FR-002). */
function ScoreFields({ p, i, onChange, errors }: { p?: number; i?: number; onChange: (v: { probability?: number; impact?: number }) => void; errors?: string[] }) {
  const score = p && i ? p * i : null
  const group = (name: 'probability' | 'impact', value?: number) => (
    <fieldset className="space-y-1.5">
      <legend className="mb-1.5 text-sm font-medium">{t(`risk.${name}`)}</legend>
      {LEVELS.map((n) => (
        <label key={n} className={cn('grid cursor-pointer grid-cols-[auto_1fr] items-start gap-x-2.5 rounded-md border px-3 py-2 text-sm hover:bg-muted', value === n && 'border-primary bg-accent hover:bg-accent')}>
          <input type="radio" name={`risk-${name}`} className="row-span-2 mt-0.5 size-4 accent-(--primary)" checked={value === n} onChange={() => onChange({ [name]: n })} />
          <span className="font-medium">{n} · {t(`risk.${name}.${n}`)}</span>
          <span className="text-xs/[18px] text-muted-foreground">{t(`risk.${name}.${n}.hint`)}</span>
        </label>
      ))}
    </fieldset>
  )
  return (
    <div className="grid gap-4 sm:col-span-2 sm:grid-cols-2">
      {group('probability', p)}{group('impact', i)}
      <p className="flex flex-wrap items-center gap-2 text-sm sm:col-span-2" aria-live="polite">
        {score ? <>{t('risk.severityIs')} <Severity level={bandOf(score)} score={score} /></> : <span className="text-muted-foreground">{t('risk.chooseBoth')}</span>}
      </p>
      {errors?.map((e) => <p key={e} className="text-xs/[18px] text-bad sm:col-span-2" role="alert">{e}</p>)}
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
            <Button type="submit" disabled={busy || !f.probability || !f.impact}>{busy && <Spinner />}{busy ? t('common.saving') : t('risk.raise')}</Button>
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
        <div className="mt-1.5 flex flex-wrap gap-2">{SEVERITIES.map((s) => (
          <label key={s} className={cn(CHOICE, f.severity === s && CHOICE_ON)}>
            <input type="radio" className="size-4 accent-(--primary)" name={`${prefix}-severity`} checked={f.severity === s} onChange={() => setF({ ...f, severity: s })} />{tv(s)}
          </label>))}</div>
        <p className="mt-1.5 text-xs/[18px] text-muted-foreground">{t('issue.highHint')}</p>
        {fe.severity?.map((e) => <p key={e} className="text-xs/[18px] text-bad" role="alert">{e}</p>)}
      </fieldset>
      <Field label={t('common.owner')} htmlFor={`${prefix}-owner`} error={fe.ownerId}>
        <PeoplePicker id={`${prefix}-owner`} value={f.ownerId} valueName={f.ownerName} placeholder={t('decision.me')} onChange={(id, person) => setF({ ...f, ownerId: id, ownerName: person?.displayName })} />
      </Field>
      <Field label={t('issue.target')} htmlFor={`${prefix}-target`} error={fe.targetResolutionDate}><Input id={`${prefix}-target`} type="date" min={f.dateRaised ?? today()} value={f.targetResolutionDate ?? ''} onChange={(e) => setF({ ...f, targetResolutionDate: e.target.value })} /></Field>
      <Field label={t('issue.dateRaised')} htmlFor={`${prefix}-raised`} error={fe.dateRaised}><Input id={`${prefix}-raised`} type="date" max={today()} value={f.dateRaised ?? today()} onChange={(e) => setF({ ...f, dateRaised: e.target.value })} /></Field>
      <Field label={t('common.discipline')} htmlFor={`${prefix}-disc`} error={fe.projectDisciplineId}><DisciplineSelect id={`${prefix}-disc`} projectId={projectId} value={f.projectDisciplineId} onChange={(v) => setF({ ...f, projectDisciplineId: v })} /></Field>
      <fieldset className="sm:col-span-2">
        <legend className="text-sm font-medium">{t('issue.type')}</legend>
        <div className="mt-1.5 flex flex-wrap gap-2">{ISSUE_TYPES.map((x) => (
          <label key={x} className={cn(CHOICE, (f.issueType ?? 'General') === x && CHOICE_ON)}>
            <input type="radio" className="size-4 accent-(--primary)" name={`${prefix}-type`} checked={(f.issueType ?? 'General') === x} onChange={() => setF({ ...f, issueType: x })} />{tv(x)}
          </label>))}</div>
        <p className="mt-1.5 text-xs/[18px] text-muted-foreground">{t('issue.typeHint')}</p>
        {fe.issueType?.map((e) => <p key={e} className="text-xs/[18px] text-bad" role="alert">{e}</p>)}
      </fieldset>
      {f.issueType === 'Coordination' && <fieldset className="grid gap-3 rounded-lg border bg-muted p-4 sm:col-span-2 sm:grid-cols-2">
        <legend className="px-1 text-sm font-semibold">{t('issue.firstReference')}</legend>
        <div className="flex flex-wrap gap-2 text-sm sm:col-span-2" role="radiogroup" aria-label={t('issue.firstReference')}>
          {(['location', 'document'] as const).map((k) => <label key={k} className={cn(CHOICE, 'bg-card', (f.referenceKind ?? 'location') === k && CHOICE_ON)}>
            <input type="radio" className="size-4 accent-(--primary)" name={`${prefix}-reference`} checked={(f.referenceKind ?? 'location') === k} onChange={() => setF({ ...f, referenceKind: k })} />
            {t(k === 'location' ? 'issue.referenceByLocation' : 'issue.referenceByDocument')}</label>)}
        </div>
        {(f.referenceKind ?? 'location') === 'location'
          ? <LocationInputs value={f.location ?? NEW_LOCATION} onChange={(v) => setF({ ...f, location: v })} />
          : <DocumentInputs value={f.document ?? NEW_DOCUMENT} onChange={(v) => setF({ ...f, document: v })} />}
        {Object.entries(fe).filter(([k]) => !ISSUE_FORM_FIELDS.includes(k)).flatMap(([, v]) => v)
          .map((e) => <p key={e} className="text-xs/[18px] text-bad sm:col-span-2" role="alert">{e}</p>)}
      </fieldset>}
    </>
  )
}

/** A radio choice drawn as a chip; selected adds the outline and tint to the checked radio (never tint alone). */
const CHOICE = 'flex min-h-(--control-row-h) cursor-pointer items-center gap-2 rounded-md border border-input px-3 text-sm hover:bg-muted'
const CHOICE_ON = 'border-primary bg-accent font-medium hover:bg-accent'

const ISSUE_FORM_FIELDS = ['title', 'description', 'severity', 'ownerId', 'targetResolutionDate', 'dateRaised', 'projectDisciplineId', 'issueType', 'issueId']

/** A Coordination issue is raised with its first location or drawing/model reference (FR-LOC-01); others can add them later. */
const issueBody = (f: Record<string, any>) => {
  const coordination = f.issueType === 'Coordination'
  const byLocation = (f.referenceKind ?? 'location') === 'location'
  const reference = coordination ? (byLocation ? locationBody(f.location ?? NEW_LOCATION) : documentBody(f.document ?? NEW_DOCUMENT)) : null
  if (typeof reference === 'string') throw new Error(t(reference))
  return {
    title: f.title, description: f.description, severity: f.severity, ownerId: f.ownerId || null, targetResolutionDate: f.targetResolutionDate || null,
    dateRaised: f.dateRaised || null, projectDisciplineId: f.projectDisciplineId || null, issueType: f.issueType ?? 'General',
    locations: reference && byLocation ? [reference] : [], documents: reference && !byLocation ? [reference] : [],
  }
}

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
            <Button type="submit" disabled={busy || !f.severity}>{busy && <Spinner />}{busy ? t('common.saving') : t('issue.raise')}</Button>
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
        <DropdownMenuTrigger asChild><Button size="sm" variant="outline"><Plus className="size-4" />{t('register.raise')}</Button></DropdownMenuTrigger>
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
        <div className="flex flex-wrap gap-2" role="radiogroup" aria-label={t('risk.realiseTitle', { key: r.key })}>
          {(['new', 'existing'] as const).map((m) => <label key={m} className={cn(CHOICE, mode === m && CHOICE_ON)}><input type="radio" className="size-4 accent-(--primary)" name="realise-mode" checked={mode === m} onChange={() => setMode(m)} />{t(`risk.realise.${m}`)}</label>)}
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
            <Button type="submit" disabled={busy || (mode === 'new' ? !f.severity : !issueId)}>{busy && <Spinner />}{busy ? t('common.saving') : t('risk.realise')}</Button>
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
          {/* Resolution gates (verification, references, impact checks) report on their own fields. */}
          {Object.entries(err?.fieldErrors ?? {}).filter(([k]) => k !== 'resolution' && k !== 'resolvedDate').flatMap(([, v]) => v)
            .map((m) => <p key={m} className="text-sm text-bad" role="alert">{m}</p>)}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button disabled={!text.trim() || busy} onClick={save}>{busy && <Spinner />}{busy ? t('common.saving') : t('issue.resolve')}</Button>
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
    <div className="space-y-3 p-4">
      {links.length === 0 ? <p className="text-sm text-muted-foreground">{t('decision.noLinks')}</p> : (
        <ul className="divide-y rounded-md border">{links.map((l) => (
          <li key={l.id} className="flex min-h-(--row-min) flex-wrap items-center gap-x-2.5 gap-y-1 px-3 py-1 text-sm">
            <span className="w-20 shrink-0 text-xs/[18px] text-muted-foreground">{t(`itemType.${l.targetType}`)}</span><Key>{l.key}</Key>
            <button className="min-w-0 flex-1 truncate text-left hover:underline" onClick={() => openPanel(l.targetType, l.targetId)}>{l.name}</button>
            {l.status && <StatusPill status={l.status} />}<DateText date={l.date} />
            {canEdit && <Button variant="ghost" size="icon-sm" aria-label={t('decision.unlink', { key: l.key })}
              onClick={async () => { try { await del(`item-links/${l.id}`); onChange() } catch (e) { toast.error(errorText(e)) } }}><Trash2 className="size-4" /></Button>}
          </li>))}</ul>
      )}
      {canEdit && (adding ? (
        <div className="space-y-2 rounded-lg border bg-muted p-3">
          <ItemPicker projectId={projectId} exclude={links.map((l) => l.targetId)} onPick={async (x) => { try { await onAdd(x); onChange() } catch (e) { toast.error(errorText(e)) } }} />
          <div className="flex justify-end"><Button size="sm" variant="ghost" onClick={() => setAdding(false)}>{t('common.close')}</Button></div>
        </div>
      ) : <Button size="sm" variant="outline" onClick={() => setAdding(true)}><Plus className="size-4" />{t('register.addLink')}</Button>)}
    </div>
  )
}

function Steps({ transitions, label, onStep }: { transitions: Detail['permissions']['transitions']; label: (to: string) => string; onStep: (to: string) => void }) {
  return (
    <div className="flex flex-wrap gap-2">
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
      <PanelHead title={r.title} meta={<>
        <ShieldAlert className="size-4 text-muted-foreground" aria-hidden /><Key>{r.key}</Key><StatusPill status={r.status} /><Severity level={r.band} score={r.score} />
        {r.isReviewOverdue && <Chip tone="bad">{t('risk.reviewOverdue')}</Chip>}
      </>}>
        {q.data.realisedIssue && <p className="text-sm">{t('risk.realisedAsLabel')} <button className="hover:underline" onClick={() => openPanel('Issue', q.data.realisedIssue!.id)}><Key>{q.data.realisedIssue.key}</Key> {q.data.realisedIssue.title}</button></p>}
        <Steps transitions={perm.transitions} label={(to) => t(to === 'Open' && r.status === 'Closed' ? 'risk.reopen' : STEP[to])} onStep={onStep} />
      </PanelHead>
      <FieldGroup title={t('common.details')}>
        <FieldRow label={t('register.title')}><InlineText value={r.title} disabled={!can} title={perm.edit.reason ?? undefined} onSave={(v) => save({ title: v })} /></FieldRow>
        <FieldRow label={t('common.description')}><InlineText value={q.data.description ?? ''} multiline disabled={!can} onSave={(v) => save({ description: v })} /></FieldRow>
      </FieldGroup>
      <FieldGroup title={t('register.group.assessment')}>
        <FieldRow label={t('risk.probability')}><InlineSelect value={String(r.probability)} options={levelOptions('probability')} disabled={!can} onSave={(v) => save({ probability: Number(v) })} title={t('risk.probability')} /></FieldRow>
        <FieldRow label={t('risk.impact')}><InlineSelect value={String(r.impact)} options={levelOptions('impact')} disabled={!can} onSave={(v) => save({ impact: Number(v) })} title={t('risk.impact')} /></FieldRow>
      </FieldGroup>
      <FieldGroup title={t('register.group.ownership')}>
        <FieldRow label={t('common.owner')}><InlinePerson value={r.ownerId} name={r.ownerName} disabled={!can} allowClear={false} onSave={(v) => save({ ownerId: v })} /></FieldRow>
        <FieldRow label={t('common.discipline')}><InlineSelect value={r.projectDisciplineId} allowEmpty options={disciplines} disabled={!can} onSave={(v) => save({ projectDisciplineId: v || null })} title={t('common.discipline')} /></FieldRow>
        <FieldRow label={t('risk.reviewDate')}><InlineDate value={r.reviewDate} disabled={!can} onSave={(v) => save({ reviewDate: v })} /></FieldRow>
      </FieldGroup>
      <FieldGroup title={t('register.group.response')}>
        <FieldRow label={t('risk.mitigation')}><InlineText value={r.mitigation ?? ''} multiline disabled={!can} onSave={(v) => save({ mitigation: v })} /></FieldRow>
        <FieldRow label={t('risk.trigger')}><InlineText value={r.triggerIndicator ?? ''} disabled={!can} onSave={(v) => save({ triggerIndicator: v })} /></FieldRow>
      </FieldGroup>
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
      <PanelHead title={i.title} meta={<>
        <OctagonAlert className="size-4 text-muted-foreground" aria-hidden /><Key>{i.key}</Key><StatusPill status={i.status} /><Severity level={i.severity} />
        {i.issueType === 'Coordination' && <Chip tone="idle">{tv(i.issueType)}</Chip>}
        {i.isOverdue && <Chip tone="bad">{t('ind.overdueD', { n: i.daysOverdue })}</Chip>}
        {i.severity === 'High' && open && <Chip tone="bad">{t('issue.attention')}</Chip>}
      </>}>
        {q.data.originRisk && <p className="text-sm">{t('issue.fromRiskLabel')} <button className="hover:underline" onClick={() => openPanel('Risk', q.data.originRisk!.id)}><Key>{q.data.originRisk.key}</Key> {q.data.originRisk.title}</button></p>}
        <Steps transitions={perm.transitions} label={(to) => t(reopening(to) ? 'issue.reopen' : STEP[to])} onStep={onStep} />
      </PanelHead>
      {i.status === 'Resolved' && (
        <div className="mx-4 mb-4 rounded-md border border-done/30 bg-done-bg px-4 py-3 text-sm">
          <div className="text-xs/[18px] text-muted-foreground">{t('issue.resolvedOn', { date: fmtDate(i.resolvedDate) })}</div>
          <InlineText value={i.resolution ?? ''} multiline disabled={!can} onSave={(v) => save({ resolution: v })} />
        </div>
      )}
      <FieldGroup title={t('common.details')}>
        <FieldRow label={t('register.title')}><InlineText value={i.title} disabled={!can} title={perm.edit.reason ?? undefined} onSave={(v) => save({ title: v })} /></FieldRow>
        <FieldRow label={t('common.description')}><InlineText value={q.data.description ?? ''} multiline disabled={!can} onSave={(v) => save({ description: v })} /></FieldRow>
        <FieldRow label={t('register.severity')}><InlineSelect value={i.severity} options={SEVERITIES.map((x) => ({ value: x, label: tv(x) }))} disabled={!can} onSave={(v) => save({ severity: v })} title={t('register.severity')} /></FieldRow>
        <FieldRow label={t('issue.type')}><InlineSelect value={i.issueType} options={ISSUE_TYPES.map((x) => ({ value: x, label: tv(x) }))} disabled={!perm.changeType?.ok}
          onSave={(v) => save({ issueType: v })} title={perm.changeType?.ok ? t('issue.type') : perm.changeType?.reason ?? t('issue.type')} /></FieldRow>
      </FieldGroup>
      <FieldGroup title={t('register.group.ownership')}>
        <FieldRow label={t('common.owner')}><InlinePerson value={i.ownerId} name={i.ownerName} disabled={!can} allowClear={false} onSave={(v) => save({ ownerId: v })} /></FieldRow>
        <FieldRow label={t('issue.raisedBy')}><InlinePerson value={i.raisedById} name={i.raisedByName} disabled={!can} allowClear={false} onSave={(v) => save({ raisedById: v })} /></FieldRow>
        <FieldRow label={t('common.discipline')}><InlineSelect value={i.projectDisciplineId} allowEmpty options={disciplines} disabled={!can} onSave={(v) => save({ projectDisciplineId: v || null })} title={t('common.discipline')} /></FieldRow>
        <FieldRow label={t('issue.affectedDisciplines')}>
          <fieldset className="flex flex-wrap gap-x-4 gap-y-1 px-2 py-1.5 text-sm" disabled={!can} title={perm.edit.reason ?? undefined}>
            <legend className="sr-only">{t('issue.affectedDisciplines')}</legend>
            {(project.data?.disciplines ?? []).filter((d) => i.affectedDisciplineIds?.includes(d.id) || (d.isActive && d.id !== i.projectDisciplineId)).map((d) => {
              const on = i.affectedDisciplineIds?.includes(d.id) ?? false
              return <label key={d.id} className="inline-flex min-h-6 items-center gap-2">
                <input type="checkbox" className="size-4 accent-(--primary)" checked={on} onChange={() => save({ affectedDisciplineIds: on ? i.affectedDisciplineIds!.filter((x) => x !== d.id) : [...(i.affectedDisciplineIds ?? []), d.id] })} />
                {d.name}</label>
            })}
            {!can && !i.affectedDisciplineIds?.length && <span className="text-muted-foreground">{t('issue.affectedDisciplinesNone')}</span>}
          </fieldset>
        </FieldRow>
      </FieldGroup>
      <FieldGroup title={t('allocation.dates')}>
        <FieldRow label={t('issue.dateRaised')}><InlineDate value={i.dateRaised} disabled={!can} onSave={(v) => save({ dateRaised: v })} /></FieldRow>
        <FieldRow label={t('issue.target')}><InlineDate value={i.targetResolutionDate} disabled={!can} onSave={(v) => save({ targetResolutionDate: v })} /></FieldRow>
      </FieldGroup>
      <div className="space-y-4 border-t p-4">
        <IssueMetadata issue={i} canEdit={can} onChanged={refresh} />
        <IssueReferenceImpacts issue={i} />
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

function IssueReferenceImpacts({ issue }: { issue: IssueRow }) {
  const me = useMe()
  const q = useQuery({ queryKey: ['issue-reference-impacts', issue.id], queryFn: () => get<IssueReferenceImpact[]>(`issues/${issue.id}/reference-impacts`) })
  const [reason, setReason] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<unknown>(null)
  const decide = async (impact: IssueReferenceImpact, disposition: 'Unaffected' | 'Reopen') => {
    setBusy(impact.id); setError(null)
    try {
      const text = reason[impact.id]?.trim() ?? ''
      if (text.length < 5) { setError(new ApiError(400, { detail: t('issue.referenceImpactReasonRequired') })); return }
      await post(`issues/${issue.id}/reference-impacts/${impact.id}`, { requestId: crypto.randomUUID(), rowVersion: impact.rowVersion, disposition, reason: text })
      await q.refetch(); setReason((x) => ({ ...x, [impact.id]: '' })); toast.success(t('issue.referenceImpactSaved'))
    } catch (e) { setError(e) } finally { setBusy(null) }
  }
  if (q.isPending) return <Loading rows={2} className="p-0" />
  if (q.error) return <ErrorBanner error={q.error} retry={() => q.refetch()} />
  const impacts = q.data ?? []
  return <Section id="issue-reference-impacts" title={t('issue.referenceImpacts')} count={impacts.length}>
    <div className="space-y-3 p-4">
      <p className="text-sm text-muted-foreground">{t('issue.referenceImpactsHint')}</p>
      {error != null ? <ErrorBanner error={error} retry={() => setError(null)} /> : null}
      {!impacts.length && <p className="text-sm text-muted-foreground">{t('issue.noReferenceImpacts')}</p>}
      {impacts.map((impact) => {
        const isOwner = impact.ownerId === me.id, isVerifier = impact.verifierId === me.id
        const canDecide = impact.status === 'Pending' && ((isOwner && !impact.ownerDisposition) || (isVerifier && !impact.verifierDisposition))
        const previous = impact.previousRevision, current = impact.currentRevision
        const source = impact.documentReference?.identifier || previous?.externalIdentifier || t('issue.referenceSourceUnavailable')
        const previousLabel = [previous?.sourceKey, previous?.revision].filter(Boolean).join(' · ') || t('issue.referenceRevisionUnavailable')
        const currentLabel = [current?.sourceKey, current?.revision].filter(Boolean).join(' · ') || t('issue.referenceRevisionUnavailable')
        return <article key={impact.id} className="space-y-2 rounded-md border p-4" aria-labelledby={`impact-${impact.id}`}>
          <div className="flex flex-wrap items-center gap-2">
            <h4 id={`impact-${impact.id}`} className="font-medium">{source}</h4>
            <StatusPill status={impact.status} />
            {impact.ownerDisposition && <Chip tone="idle">{t('issue.ownerDisposition')}: {impact.ownerDisposition}</Chip>}
            {impact.verifierDisposition && <Chip tone="idle">{t('issue.verifierDisposition')}: {impact.verifierDisposition}</Chip>}
          </div>
          <p className="text-sm">{t('issue.referenceChanged', { previous: previousLabel, current: currentLabel })}</p>
          {impact.ownerReason && <p className="text-sm text-muted-foreground">{t('issue.ownerDisposition')}: {impact.ownerReason}</p>}
          {impact.verifierReason && <p className="text-sm text-muted-foreground">{t('issue.verifierDisposition')}: {impact.verifierReason}</p>}
          {canDecide && <div className="space-y-2" role="group" aria-label={t('issue.referenceImpactDecision')}>
            <label htmlFor={`impact-reason-${impact.id}`} className="text-sm font-medium">{t('issue.referenceImpactReasonLabel')}</label>
            <Textarea id={`impact-reason-${impact.id}`} value={reason[impact.id] ?? ''} onChange={(e) => setReason((x) => ({ ...x, [impact.id]: e.target.value }))} placeholder={t('issue.referenceImpactReason')} rows={2} />
            <div className="flex flex-wrap gap-2"><Button type="button" disabled={busy === impact.id} onClick={() => decide(impact, 'Unaffected')}>{t('issue.referenceUnaffected')}</Button><Button type="button" variant="outline" disabled={busy === impact.id} onClick={() => decide(impact, 'Reopen')}>{t('issue.referenceReopen')}</Button>{busy === impact.id && <Spinner />}</div>
          </div>}
          {!canDecide && impact.status === 'Pending' && <p className="text-sm text-muted-foreground">{t(isOwner || isVerifier ? 'issue.referenceImpactWaiting' : 'issue.referenceImpactAssigned')}</p>}
        </article>
      })}
    </div>
  </Section>
}

type LocationDraft = Record<'kind' | 'siteArea' | 'building' | 'level' | 'room' | 'assetSystem' | 'alignment' | 'start' | 'end' | 'units'
  | 'coordinateX' | 'coordinateY' | 'coordinateZ' | 'coordinateCrs' | 'coordinateUnits', string>
const NEW_LOCATION: LocationDraft = { kind: 'SiteArea', siteArea: '', building: '', level: '', room: '', assetSystem: '', alignment: '', start: '', end: '',
  units: 'm', coordinateX: '', coordinateY: '', coordinateZ: '', coordinateCrs: '', coordinateUnits: 'm' }
interface DocumentDraft { sourceSystem: string; stableSourceId: string; kind: string; identifier: string; revision: string; sourceUrl: string; externalTopicId: string; modelElementGuid: string; viewpointUrl: string; available: boolean }
const NEW_DOCUMENT: DocumentDraft = { sourceSystem: '', stableSourceId: '', kind: 'Drawing', identifier: '', revision: '', sourceUrl: '', externalTopicId: '', modelElementGuid: '', viewpointUrl: '', available: true }

/** The location's API body, or the message key for the first missing value; the server repeats every check. */
function locationBody(l: LocationDraft): Record<string, unknown> | string {
  const k = l.kind
  if (k === 'SiteArea' && !l.siteArea.trim()) return 'issue.locationRequired'
  if (k === 'Building' && !l.building.trim()) return 'issue.locationRequired'
  if (k === 'Alignment' && (!l.alignment.trim() || !l.start || !l.end || !l.units.trim())) return 'issue.alignmentRequired'
  if (k === 'Coordinate' && (!l.coordinateX || !l.coordinateY || !l.coordinateCrs.trim() || !l.coordinateUnits.trim())) return 'issue.coordinateRequired'
  const numericValues = k === 'Coordinate' ? [l.coordinateX, l.coordinateY, l.coordinateZ] : k === 'Alignment' ? [l.start, l.end] : []
  if (numericValues.some((value) => value.trim() && !Number.isFinite(Number(value)))) return 'issue.numberRequired'
  const numberOrNull = (value: string) => value.trim() ? Number(value) : null
  return { kind: k, siteArea: l.siteArea || null,
    building: k === 'Building' ? l.building || null : null, level: k === 'Building' ? l.level || null : null, room: k === 'Building' ? l.room || null : null,
    assetSystem: l.assetSystem || null, alignment: k === 'Alignment' ? l.alignment || null : null,
    startStation: k === 'Alignment' ? numberOrNull(l.start) : null, endStation: k === 'Alignment' ? numberOrNull(l.end) : null, stationUnits: k === 'Alignment' ? l.units || null : null,
    coordinateX: k === 'Coordinate' ? numberOrNull(l.coordinateX) : null, coordinateY: k === 'Coordinate' ? numberOrNull(l.coordinateY) : null,
    coordinateZ: k === 'Coordinate' ? numberOrNull(l.coordinateZ) : null, coordinateReferenceSystem: k === 'Coordinate' ? l.coordinateCrs || null : null,
    coordinateUnits: k === 'Coordinate' ? l.coordinateUnits || null : null }
}

function documentBody(d: DocumentDraft): Record<string, unknown> | string {
  if (!d.identifier.trim() || !d.revision.trim() || !d.sourceUrl.trim()) return 'issue.documentRequired'
  return { kind: d.kind, identifier: d.identifier, revision: d.revision, sourceUrl: d.sourceUrl, sourceSystem: d.sourceSystem || null, stableSourceId: d.stableSourceId || null, externalTopicId: d.externalTopicId || null,
    modelElementGuid: d.modelElementGuid || null, viewpointUrl: d.viewpointUrl || null, isAvailable: d.available }
}

/** Location entry shared by the issue panel and the Coordination issue form; the project's own conventions are kept as entered. */
function LocationInputs({ value: l, onChange }: { value: LocationDraft; onChange: (v: LocationDraft) => void }) {
  const id = useId()
  const input = (key: keyof LocationDraft, label: string, type?: string) =>
    <Field label={t(label)} htmlFor={`${id}-${key}`}><Input id={`${id}-${key}`} value={l[key]} onChange={(e) => onChange({ ...l, [key]: e.target.value })} type={type} /></Field>
  return <>
    <Field label={t('issue.locationKind')} htmlFor={`${id}-kind`}>
      <select id={`${id}-kind`} className={selectCls} value={l.kind} onChange={(e) => onChange({ ...l, kind: e.target.value })}><option>SiteArea</option><option>Building</option><option>Alignment</option><option>Coordinate</option></select>
    </Field>
    {input('siteArea', 'issue.siteArea')}{input('assetSystem', 'issue.assetSystem')}
    {l.kind === 'Building' && <>{input('building', 'issue.building')}{input('level', 'issue.level')}{input('room', 'issue.room')}</>}
    {l.kind === 'Alignment' && <>{input('alignment', 'issue.alignment')}{input('start', 'issue.startStation', 'number')}{input('end', 'issue.endStation', 'number')}{input('units', 'issue.units')}</>}
    {l.kind === 'Coordinate' && <>{input('coordinateX', 'issue.coordinateX', 'number')}{input('coordinateY', 'issue.coordinateY', 'number')}{input('coordinateZ', 'issue.coordinateZ', 'number')}{input('coordinateCrs', 'issue.coordinateCrs')}{input('coordinateUnits', 'issue.coordinateUnits')}</>}
  </>
}

function DocumentInputs({ value: d, onChange }: { value: DocumentDraft; onChange: (v: DocumentDraft) => void }) {
  const id = useId()
  const input = (key: Exclude<keyof DocumentDraft, 'available' | 'kind'>, label: string) =>
    <Field label={t(label)} htmlFor={`${id}-${key}`}><Input id={`${id}-${key}`} value={d[key]} onChange={(e) => onChange({ ...d, [key]: e.target.value })} /></Field>
  return <>
    <Field label={t('issue.documentKind')} htmlFor={`${id}-kind`}>
      <select id={`${id}-kind`} className={selectCls} value={d.kind} onChange={(e) => onChange({ ...d, kind: e.target.value })}><option>Drawing</option><option>Model</option><option>Markup</option><option>Screenshot</option></select>
    </Field>
    {input('identifier', 'issue.identifier')}{input('revision', 'issue.revision')}{input('sourceSystem', 'basis.sourceSystem')}{input('stableSourceId', 'basis.sourceId')}{input('sourceUrl', 'issue.sourceUrl')}{input('externalTopicId', 'issue.externalTopic')}
    {input('modelElementGuid', 'issue.modelGuid')}{input('viewpointUrl', 'issue.viewpointUrl')}
    <label className="flex min-h-(--control-h) items-center gap-2 self-end text-sm"><input type="checkbox" className="size-4 accent-(--primary)" checked={d.available} onChange={(e) => onChange({ ...d, available: e.target.checked })} />{t('issue.available')}</label>
  </>
}

function IssueMetadata({ issue, canEdit, onChanged }: { issue: IssueRow; canEdit: boolean; onChanged: () => void }) {
  const me = useMe()
  const uid = useId()
  const eligible = useQuery({ queryKey: ['issue-verifier-eligible', issue.projectId],
    queryFn: () => get<{ people: { id: string; displayName: string }[] }>(`projects/${issue.projectId}/changes/options`) })
  const locations = useQuery({ queryKey: ['issue-locations', issue.id], queryFn: () => get<IssueLocation[]>(`issues/${issue.id}/locations`) })
  const documents = useQuery({ queryKey: ['issue-documents', issue.id], queryFn: () => get<IssueDocument[]>(`issues/${issue.id}/documents`) })
  const verification = useQuery({ queryKey: ['issue-verification', issue.id], queryFn: () => get<IssueVerification[]>(`issues/${issue.id}/verification`) })
  const [loc, setLoc] = useState(NEW_LOCATION); const [doc, setDoc] = useState(NEW_DOCUMENT)
  const [replacing, setReplacing] = useState<IssueDocument | null>(null), [replacementReason, setReplacementReason] = useState('')
  const [verifier, setVerifier] = useState<string | null>(null); const [note, setNote] = useState(''); const [evidence, setEvidence] = useState(''); const [busy, setBusy] = useState(false)
  const reload = async () => { await Promise.all([locations.refetch(), documents.refetch(), verification.refetch()]); onChanged() }
  const submit = async (path: string, body: Record<string, unknown>) => { setBusy(true); try { await post(path, { ...body, rowVersion: issue.rowVersion }, issue.rowVersion); await reload(); toast.success(t('issue.metadataSaved')); return true } catch (e) { toast.error(errorText(e)); return false } finally { setBusy(false) } }
  const add = (path: string, body: Record<string, unknown> | string) => { if (typeof body === 'string') toast.error(t(body)); else submit(path, body) }
  const appoint = () => verifier && submit(`issues/${issue.id}/verification`, { verifierId: verifier, status: 'Proposed', note })
  const decide = (v: IssueVerification, status: 'Verified' | 'Rejected') => submit(`issues/${issue.id}/verification`, { verifierId: v.verifierId, status, evidenceUrl: evidence || null, note })
  const replace = (x: IssueDocument) => {
    setReplacing(x); setReplacementReason(''); setDoc({ kind: x.kind, identifier: x.identifier, revision: x.revision,
      sourceUrl: x.sourceUrl, sourceSystem: x.sourceSystem ?? '', stableSourceId: x.stableSourceId ?? '',
      externalTopicId: x.externalTopicId ?? '', modelElementGuid: x.modelElementGuid ?? '', viewpointUrl: x.viewpointUrl ?? '', available: true })
  }
  const saveReference = async () => {
    const body = documentBody(doc)
    if (typeof body === 'string') { toast.error(t(body)); return }
    if (!replacing) { add(`issues/${issue.id}/documents`, body); return }
    if (!replacementReason.trim()) { toast.error(t('coord.reasonRequired')); return }
    if (await submit(`issues/${issue.id}/documents/${replacing.id}/replace`, { requestId: crypto.randomUUID(),
      documentRowVersion: replacing.rowVersion, document: body, reason: replacementReason })) {
      setReplacing(null); setReplacementReason(''); setDoc(NEW_DOCUMENT)
    }
  }
  const latestVerification = verification.data?.[0]
  const sub = 'text-sm font-semibold'
  const form = 'grid gap-3 rounded-lg bg-muted p-3 sm:grid-cols-2'
  return <section className="overflow-hidden rounded-lg border bg-card" aria-labelledby="issue-metadata-title">
    <h3 id="issue-metadata-title" className="border-b px-4 py-3 text-base/6 font-semibold">{t('issue.locationEvidence')}</h3>
    <div className="space-y-5 p-4">
      <div className="space-y-2">
        <h4 className={sub}>{t('issue.locations')}</h4>
        {(locations.data ?? []).map((x) => <div key={x.id} className="rounded-md border px-3 py-2 text-sm"><span className="mr-2 rounded-md bg-secondary px-1.5 py-0.5 text-xs font-medium text-muted-foreground">{x.kind}</span><span>{[x.siteArea, x.building, x.level, x.room, x.assetSystem, x.alignment].filter(Boolean).join(' · ')}</span>{x.startStation != null && ` · ${x.startStation}–${x.endStation} ${x.stationUnits ?? ''}`}{x.coordinateX != null && ` · (${x.coordinateX}, ${x.coordinateY}${x.coordinateZ != null ? `, ${x.coordinateZ}` : ''}) ${x.coordinateReferenceSystem ?? ''} ${x.coordinateUnits ?? ''}`}</div>)}
        {!locations.data?.length && <p className="text-sm text-muted-foreground">{t('issue.noLocations')}</p>}
        {canEdit && <div className={form}>
          <LocationInputs value={loc} onChange={setLoc} />
          <div className="flex items-end"><Button type="button" variant="outline" disabled={busy} onClick={() => add(`issues/${issue.id}/locations`, locationBody(loc))}>{t('common.add')}</Button></div>
        </div>}
      </div>
      <div className="space-y-2">
        <h4 className={sub}>{t('issue.documentReferences')}</h4>
        {(documents.data ?? []).map((x) => <div key={x.id} className="space-y-2 rounded-md border px-3 py-2 text-sm"><div><Chip tone={x.replacedById ? 'idle' : x.isAvailable ? 'done' : 'bad'}>{x.replacedById ? t('issue.referenceHistorical') : x.isAvailable ? t('issue.available') : t('issue.unavailable')}</Chip> {x.kind} · <strong>{x.identifier}</strong> · {t('issue.revision')} {x.revision} · <span>{t('basis.manual')}</span> · {x.sourceSystem && <span>{t('basis.sourceSystem')}: {x.sourceSystem} · </span>}{x.stableSourceId && <span>{t('basis.sourceId')}: {x.stableSourceId} · </span>}{x.registeredAt && <span className="tabular-nums">{fmtTime(x.registeredAt)} · </span>}{x.isAvailable ? <a className="text-primary underline underline-offset-4" href={x.sourceUrl} target="_blank" rel="noreferrer">{t('issue.openSource')}</a> : <span className="text-muted-foreground">{t('issue.sourceUnavailable')}</span>} {(x.externalTopicId || x.modelElementGuid || x.viewpointUrl) && <span className="block text-muted-foreground">{x.externalTopicId && `${t('issue.externalTopic')} ${x.externalTopicId}`} {x.modelElementGuid && ` · ${t('issue.modelGuid')} ${x.modelElementGuid}`} {x.viewpointUrl && <> · <a className="text-primary underline underline-offset-4" href={x.viewpointUrl} target="_blank" rel="noreferrer">{t('issue.viewpoint')}</a></>}</span>}</div>
          {canEdit && !x.replacedById && ['Open', 'In Progress'].includes(issue.status) && <Button type="button" size="sm" variant="outline" disabled={busy} onClick={() => replace(x)}>{t('issue.replaceReference')}</Button>}</div>)}
        {canEdit && <div className={form}>
          {replacing && <p className="text-sm sm:col-span-2" role="status">{t('issue.referenceRecoveryHint')}</p>}
          <DocumentInputs value={doc} onChange={setDoc} />
          {replacing && <Field label={t('coord.reason')} htmlFor={`${uid}-reason`} className="sm:col-span-2"><Textarea id={`${uid}-reason`} value={replacementReason} maxLength={2000} onChange={e => setReplacementReason(e.target.value)} /></Field>}
          <div className="flex flex-wrap items-end gap-2 sm:col-span-2">
            <Button type="button" variant={replacing ? 'default' : 'outline'} disabled={busy || !!replacing && !replacementReason.trim()} onClick={saveReference}>{t(replacing ? 'issue.replaceReference' : 'common.add')}</Button>
            {replacing && <Button type="button" variant="outline" disabled={busy} onClick={() => { setReplacing(null); setDoc(NEW_DOCUMENT); setReplacementReason('') }}>{t('common.cancel')}</Button>}
          </div>
        </div>}
      </div>
      <div className="space-y-2">
        <h4 className={sub}>{t('issue.verificationFlow')}</h4>
        {(verification.data ?? []).map((v) => <div key={v.id} className="rounded-md border px-3 py-2 text-sm"><StatusPill status={v.status} /> {v.verifierId === me.id && <span>{t('issue.assignedToYou')}</span>} {v.note && <span className="text-muted-foreground">· {v.note}</span>}{v.id === latestVerification?.id && v.status === 'Proposed' && v.verifierId === me.id && <div className="mt-2 flex flex-wrap items-end gap-2">
          <Field label={t('issue.evidenceUrl')} htmlFor={`${uid}-evidence`} className="min-w-48 flex-1"><Input id={`${uid}-evidence`} value={evidence} onChange={(e) => setEvidence(e.target.value)} /></Field>
          <Button type="button" disabled={busy} onClick={() => decide(v, 'Verified')}>{t('issue.verify')}</Button><Button type="button" variant="outline" disabled={busy} onClick={() => decide(v, 'Rejected')}>{t('issue.reject')}</Button></div>}</div>)}
        {canEdit && latestVerification?.status !== 'Proposed' && <div className={form}>
          <Field label={t('issue.verifier')} htmlFor={`${uid}-verifier`}><PeoplePicker id={`${uid}-verifier`} value={verifier} onChange={(id) => setVerifier(id)} label={t('issue.verifier')}
            disabled={!eligible.data} candidates={eligible.data?.people.filter(p => me.settings.allowSelfReview || p.id !== issue.ownerId && p.id !== issue.raisedById)} /></Field>
          <Field label={t('issue.appointmentReason')} htmlFor={`${uid}-note`}><Textarea id={`${uid}-note`} rows={2} value={note} onChange={(e) => setNote(e.target.value)} /></Field>
          <div className="sm:col-span-2"><Button type="button" variant="outline" disabled={busy || !verifier || !note.trim()} onClick={appoint}>{t('issue.appointVerifier')}</Button></div>
        </div>}
      </div>
    </div>
  </section>
}

PANELS.Risk = { component: RiskPanel }
PANELS.Issue = { component: IssuePanel }
ItemSlots.Raise = RaiseMenu
