import { PersonPanelTrigger } from '@/components/hub/person-panel-trigger'
import { personOptionLabel } from '@/components/hub/person-option-label'
import { disciplineColour } from '@/lib/discipline-colour'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Monitor, Plus, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { NavLink, Navigate, useLocation, useParams, Link } from 'react-router'
import { ConfirmDialog, DesktopOnly, Empty, ErrorBanner, Field, FilterBar, Loading, Missing, Notice, Page, Section, Spinner, selectCls } from '@/components/hub/common'
import { ActivityTable } from '@/components/hub/activity'
import { DataTable } from '@/components/hub/registers'
import { useTable, type Column } from '@/components/hub/table'
import { Avatar } from '@/components/hub/people'
import { Chip, Pill } from '@/components/hub/pills'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import type { components } from '@/gen/api'
import { ApiError, typedRequest } from '@/lib/api'
import { fmtTime } from '@/lib/format'
import { invalidateChange } from '@/lib/keys'
import { runtimeText, settingLabel, t } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { CertificationVocabulary, PersonFactsEditor } from '@/components/hub/person-facts-editor'
import { useFlag, useMe } from '@/lib/auth'
import { AdminRoleExamples } from './admin-role-explanations'
import { ADMIN_EXTRA } from '@/app/extension-slots'
import { toast } from 'sonner'
import { DraftNotice } from '@/components/hub/drafts'
import { useSafeForm } from '@/hooks/drafts'
import { DeliverableReviewDefaults } from './deliverable-review-defaults'

const KINDS = ['disciplines', 'clients', 'offices', 'deliverableTypes', 'phases', 'projectTypes', 'issuePurposes', 'issueReturnCodes', 'decisionCategories', 'issueRootCauses', 'dateReasons'] as const
const REGISTER_KINDS: readonly string[] = ['issuePurposes', 'issueReturnCodes', 'decisionCategories', 'issueRootCauses', 'dateReasons']
type Kind = (typeof KINDS)[number]
type Schemas = components['schemas']

const h2 = 'text-lg/[26px] font-semibold tracking-[-0.2px]'
const card = 'rounded-lg border bg-card'
const Active = ({ on }: { on: boolean }) => <Pill tone={on ? 'ok' : 'idle'}>{on ? t('common.active') : t('common.inactive')}</Pill>

/** Administration (§13.15): plain forms and lifecycle tables under one row of tabs; desktop and tablet only (§13.0). */
export function AdminLayout({ children }: { children: React.ReactNode }) {
  const structure = useFlag('org_structure'), chain = useFlag('verifiable_log'), adminReview = useFlag('admin_review')
  const register = useFlag('register_screens')
  const visualSystem = useFlag('visual_system')
  const title = t('admin.title')
  const links = [...KINDS.filter(k => register || !REGISTER_KINDS.includes(k)).map((k) => ({ to: `/admin/reference/${k}`, label: runtimeText(`admin.${k}`) })),
    { to: '/admin/settings', label: t('admin.settings') }, { to: '/admin/users', label: t('admin.users') }, { to: '/admin/activity', label: t('admin.activity') },
    ...(structure ? [{ to: '/admin/structure', label: t('admin.structure.title') }] : []),
    ...(chain ? [{ to: '/admin/audit-chains', label: t('auditChains.title') }] : []),
    ...(adminReview ? [{ to: '/admin/review', label: t('adminReview.title') }] : []),
    ...ADMIN_EXTRA]
  return (
    <DesktopOnly notice={<Page title={title}><Notice icon={Monitor} title={t('app.phoneNotice')}
      action={<Button asChild variant="outline"><Link to="/my-work">{t('nav.myWork')}</Link></Button>}>{t('admin.phoneHint')}</Notice></Page>}>
      {visualSystem ? <Page title={title} tabs={<AdminBandTabs title={title} links={links} />}><div className="min-w-0">{children}</div></Page> : <Page title={title}>
        <nav aria-label={title} className="flex min-w-0 flex-wrap gap-1 shadow-[inset_0_-1px_0_var(--border)]">
          {links.map((l) => (
            <NavLink key={l.to} to={l.to} className={({ isActive }) => cn('inline-flex min-h-11 shrink-0 items-center whitespace-nowrap border-b-2 border-transparent px-3 text-sm text-muted-foreground hover:bg-muted hover:text-foreground',
              isActive && 'border-primary font-semibold text-foreground')}>{l.label}</NavLink>
          ))}
        </nav>
        <div className="min-w-0">{children}</div>
      </Page>}
    </DesktopOnly>
  )
}

/** 052 S07: with the visual system the Admin pages are one band tab row (FR-VSN-11). Like the band's other rows it
 *  scrolls sideways instead of wrapping; the current page is scrolled into view when the row overflows. */
function AdminBandTabs({ title, links }: { title: string; links: { to: string; label: string }[] }) {
  const nav = useRef<HTMLElement>(null)
  const { pathname } = useLocation()
  useEffect(() => {
    const row = nav.current, current = row?.querySelector('[aria-current="page"]')
    if (!row || !current) return
    const a = current.getBoundingClientRect(), r = row.getBoundingClientRect()
    if (a.left < r.left || a.right > r.right) row.scrollLeft += a.left - r.left - (r.width - a.width) / 2
  }, [pathname])
  return <nav ref={nav} aria-label={title} data-admin-tabs className="visual-area-tabs">{links.map((l) => <NavLink key={l.to} to={l.to}>{l.label}</NavLink>)}</nav>
}

export function AdminIndex() { return <Navigate to="/admin/reference/disciplines" replace /> }

// ---------- Reference data (FR-008..FR-015) ----------

/** One entry of any kind: the shared columns, and whichever of the kind's own columns it has (the contract's rows for each kind). */
type Ref = Schemas['ReferenceRow'] & Partial<Schemas['OfficeRow'] & Schemas['DisciplineRow'] & Schemas['ClientRow'] & Schemas['DeliverableTypeRow'] & Schemas['IssueReturnCodeRow']>

export function ReferenceData() {
  const { kind } = useParams()
  const register = useFlag('register_screens')
  return REGISTER_KINDS.includes(kind ?? '') && !register ? <Navigate to="/admin/reference/disciplines" replace /> : <ReferenceDataList />
}

function ReferenceDataList() {
  const { kind = 'disciplines' } = useParams() as { kind: Kind }
  const reviewDefaultsOn = useFlag('register_screens')
  const qc = useQueryClient()
  const path = `/api/v1/admin/${kind}` as const
  const list = useQuery({ queryKey: ['admin', kind], queryFn: (): Promise<Ref[]> => typedRequest('get', path, {}) })
  const disciplines = useQuery({ queryKey: ['admin', 'disciplines'], queryFn: () => typedRequest('get', '/api/v1/admin/disciplines', {}), enabled: kind === 'deliverableTypes' })
  const [editing, setEditing] = useState<Partial<Ref> | null>(null)
  const [deactivate, setDeactivate] = useState<{ row: Ref; projects: number } | null>(null)
  const [reviewDefaults, setReviewDefaults] = useState<{ typeId: string; name: string; typeActive: boolean; disciplines: Schemas['DisciplineRow'][] } | null>(null)
  const refresh = () => { void invalidateChange(qc, 'ReferenceData') }
  const save = useMutation({
    mutationFn: (r: Partial<Ref>) => r.id ? typedRequest('patch', `/api/v1/admin/${kind}/{id}`, { params: { id: r.id }, body: r, ifMatch: r.rowVersion }) : typedRequest('post', path, { body: r }),
    onSuccess: () => { refresh(); setEditing(null); toast.success(t('common.saved')) },
  })
  const rows = list.data ?? []
  const currentReviewType = rows.find(row => row.id === reviewDefaults?.typeId)
  const reviewCatalogueError = list.error ?? disciplines.error ?? (reviewDefaults && list.data && !currentReviewType ? new Error(t('common.unavailable')) : null)
  const move = async (i: number, dir: -1 | 1) => {
    const a = rows[i], b = rows[i + dir]
    if (!b) return
    await typedRequest('patch', `/api/v1/admin/${kind}/{id}`, { params: { id: a.id }, body: { sortOrder: b.sortOrder === a.sortOrder ? a.sortOrder + dir : b.sortOrder }, ifMatch: a.rowVersion })
    await typedRequest('patch', `/api/v1/admin/${kind}/{id}`, { params: { id: b.id }, body: { sortOrder: a.sortOrder }, ifMatch: b.rowVersion })
    refresh()
  }
  const hasCode = kind === 'disciplines' || kind === 'offices' || kind === 'issueReturnCodes'
  const title = runtimeText(`admin.${kind}`)
  const addLabel = t('admin.add', { kind: title.toLowerCase() })
  // FR-REC-14: the shared DataTable; no column sorts and none is optional, so the list keeps the server's order.
  const table = useTable<Ref>('hub.referenceColumns', [
    { id: 'order', label: t('common.order'), fixed: true, className: 'py-0.5', cell: (r) => {
      const i = rows.indexOf(r)
      return <div className="flex gap-1">
        <Button size="icon-sm" variant="ghost" disabled={i === 0} onClick={() => move(i, -1)} aria-label={t('admin.moveUp')}><ArrowUp className="size-4" /></Button>
        <Button size="icon-sm" variant="ghost" disabled={i === rows.length - 1} onClick={() => move(i, 1)} aria-label={t('admin.moveDown')}><ArrowDown className="size-4" /></Button>
      </div>
    } },
    { id: 'name', label: t('common.name'), fixed: true, className: 'font-medium', cell: (r) => r.name },
    ...(hasCode ? [{ id: 'code', label: t('common.code'), fixed: true, cell: (r: Ref) => <span className="key">{r.code}</span> }] : []),
    ...(kind === 'disciplines' ? [{ id: 'colour', label: t('admin.colour'), fixed: true, cell: (r: Ref) => <span className="inline-flex items-center gap-2"><span className="size-3 rounded-sm border" style={{ background: disciplineColour(r.colour) }} aria-hidden />{r.colour}</span> }] : []),
    ...(kind === 'offices' ? [{ id: 'timeZone', label: t('admin.timeZone'), fixed: true, cell: (r: Ref) => r.timeZone }] : []),
    ...(kind === 'clients' ? [{ id: 'shortName', label: t('admin.shortName'), fixed: true, cell: (r: Ref) => r.shortName || <Missing /> }] : []),
    { id: 'status', label: t('common.status'), fixed: true, cell: (r) => <Active on={r.isActive} /> },
    { id: 'actions', label: t('common.actions'), srOnly: true, fixed: true, className: 'py-0.5', cell: (r) => (
      <div className="flex justify-end gap-1">
        {kind === 'deliverableTypes' && reviewDefaultsOn && <Button type="button" variant="outline" size="sm" disabled={disciplines.isError || !disciplines.data}
          onClick={() => setReviewDefaults({ typeId: r.id, name: r.name, typeActive: r.isActive, disciplines: [...(disciplines.data ?? [])] })}>{t('reviewDefaults.edit')}</Button>}
        <Button size="sm" variant="outline" onClick={() => setEditing(r)}>{t('common.edit')}</Button>
        {r.isActive
          ? <Button size="sm" variant="ghost" onClick={async () => setDeactivate({ row: r, projects: (await typedRequest('get', `/api/v1/admin/${kind}/{id}/usage`, { params: { id: r.id } })).projects })}>{t('admin.deactivate')}</Button>
          : <Button size="sm" variant="ghost" onClick={() => save.mutate({ id: r.id, rowVersion: r.rowVersion, isActive: true })}>{t('admin.activate')}</Button>}
      </div>) },
  ], rows, (r) => [r.sortOrder])
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className={h2}>{title}</h2>
        <Button onClick={() => setEditing({ name: '', isActive: true })}><Plus className="size-4" />{addLabel}</Button>
      </div>
      {list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : (
        <DataTable table={table} rows={table.sorted} loading={list.isPending} wide caption={title} current={() => false}
          rowClass={(r) => !r.isActive && 'text-muted-foreground'} empty={<Empty>{t('common.empty')}</Empty>} />
      )}
      {kind === 'deliverableTypes' && reviewDefaultsOn && reviewDefaults && <DeliverableReviewDefaults key={reviewDefaults.typeId}
        typeId={reviewDefaults.typeId} name={reviewDefaults.name} typeActive={currentReviewType?.isActive ?? reviewDefaults.typeActive}
        disciplines={!disciplines.isError && disciplines.data ? disciplines.data : reviewDefaults.disciplines}
        catalogueError={reviewCatalogueError} cataloguePending={!list.data || !disciplines.data}
        onRetryCatalogue={() => { void list.refetch(); void disciplines.refetch() }} onClose={() => setReviewDefaults(null)} />}
      <Dialog open={!!editing} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent>
          <DialogHeader><DialogTitle>{editing?.id ? t('common.edit') : addLabel}</DialogTitle></DialogHeader>
          {editing && (
            <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); save.mutate(editing) }}>
              <Field label={t('common.name')} htmlFor="ref-name" error={(save.error as ApiError)?.fieldErrors?.name}>
                <Input id="ref-name" required value={editing.name ?? ''} onChange={(e) => setEditing({ ...editing, name: e.target.value })} />
              </Field>
              {hasCode && <Field label={t('common.code')} htmlFor="ref-code"><Input id="ref-code" required value={editing.code ?? ''} onChange={(e) => setEditing({ ...editing, code: e.target.value })} /></Field>}
              {kind === 'disciplines' && <Field label={t('admin.colour')} htmlFor="ref-colour"><Input id="ref-colour" type="color" value={editing.colour ?? '#64748b'} onChange={(e) => setEditing({ ...editing, colour: e.target.value })} className="w-20 p-1" /></Field>}
              {kind === 'offices' && <Field label={t('admin.timeZone')} htmlFor="ref-tz" error={(save.error as ApiError)?.fieldErrors?.timeZone}><Input id="ref-tz" required value={editing.timeZone ?? 'America/Halifax'} onChange={(e) => setEditing({ ...editing, timeZone: e.target.value })} /></Field>}
              {kind === 'clients' && <Field label={t('admin.shortName')} htmlFor="ref-short"><Input id="ref-short" value={editing.shortName ?? ''} onChange={(e) => setEditing({ ...editing, shortName: e.target.value })} /></Field>}
              {kind === 'deliverableTypes' && (
                <Field label={t('admin.defaultDiscipline')} htmlFor="ref-dd">
                  <select id="ref-dd" className={selectCls} value={editing.defaultDisciplineId ?? ''} onChange={(e) => setEditing({ ...editing, defaultDisciplineId: e.target.value || undefined })}>
                    <option value="">{t('common.none')}</option>
                    {(disciplines.data ?? []).filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                  </select>
                </Field>
              )}
              {save.error && <ErrorBanner error={save.error} />}
              <DialogFooter>
                <Button type="button" variant="outline" onClick={() => setEditing(null)}>{t('common.cancel')}</Button>
                <Button type="submit" disabled={save.isPending}>{save.isPending && <Spinner />}{save.isPending ? t('common.saving') : t('common.save')}</Button>
              </DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>
      <ConfirmDialog open={!!deactivate} onOpenChange={(o) => !o && setDeactivate(null)}
        title={t('admin.deactivateTitle', { name: deactivate?.row.name ?? '' })}
        body={deactivate && (deactivate.projects > 0 ? t('admin.deactivateBody', { n: deactivate.projects }) : t('admin.deactivateUnused'))}
        confirmLabel={t('admin.deactivate')}
        onConfirm={() => save.mutateAsync({ id: deactivate!.row.id, rowVersion: deactivate!.row.rowVersion, isActive: false })} />
    </div>
  )
}

// ---------- Settings (FR-014) ----------

type SettingRow = Schemas['SettingRow']

type ChannelValue = { app?: boolean; email?: boolean }

export function Settings() {
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['admin', 'settings'], queryFn: () => typedRequest('get', '/api/v1/admin/settings', {}) })
  const [draft, setDraft] = useState<Record<string, any>>({})
  const [errors, setErrors] = useState<Record<string, unknown>>({})
  const [pending, setPending] = useState<Record<string, boolean>>({})
  const clearDraft = (key: string) => setDraft((d) => { const { [key]: _, ...rest } = d; return rest })
  const save = async (key: string, value: any) => {
    if (pending[key]) return
    setDraft((d) => ({ ...d, [key]: value }))
    setErrors((e) => ({ ...e, [key]: null }))
    setPending((p) => ({ ...p, [key]: true }))
    try {
      await typedRequest('put', '/api/v1/admin/settings/{key}', { params: { key }, body: { value } })
      await invalidateChange(qc, 'Setting')
      if (key === 'certification_warn_days' || key === 'availability_window_days') await invalidateChange(qc, 'PersonFacts')
      clearDraft(key)
      toast.success(t('common.saved'))
    } catch (e) {
      const field = (e as ApiError).fieldErrors?.value?.[0]
      setErrors((x) => ({ ...x, [key]: field ? new Error(field) : e }))
    } finally { setPending((p) => ({ ...p, [key]: false })) }
  }
  if (list.isPending) return <div className={card}><Loading /></div>
  if (list.error) return <ErrorBanner error={list.error} retry={() => list.refetch()} />
  const groups = [...new Set(list.data.map((s) => s.group))]
  const label = (s: SettingRow) => settingLabel(s.key, s.packet ?? '')
  const fallback = (s: SettingRow) => s.default != null && typeof s.default === 'object'
    ? [(s.default as ChannelValue).app && t('pref.app'), (s.default as ChannelValue).email && t('pref.email')].filter(Boolean).join(' + ') : String(s.default)
  // Form page (design §5): a 760 px field region; each setting shows its current value, its default and its own error.
  return (
    <div className="max-w-[760px] space-y-6">
      <p className="text-base/6 text-muted-foreground">{t('admin.settingsIntro')}</p>
      <nav aria-label={t('admin.settingsSections')} className="flex flex-wrap gap-x-4 gap-y-1 text-sm">
        {groups.map((g) => <a key={g} href={`#set-group-${g}`} className="text-primary underline-offset-4 hover:underline" onClick={(e) => { e.preventDefault(); document.getElementById(`set-group-${g}`)?.scrollIntoView({ block: 'start' }) }}>{runtimeText(`admin.group.${g}`)}</a>)}
      </nav>
      {groups.map((g) => (
        <Section key={g} id={`set-group-${g}`} title={runtimeText(`admin.group.${g}`)}>
          <ul className="divide-y">
            {list.data.filter((s) => s.group === g).map((s) => {
              const v = s.key in draft ? draft[s.key] : s.value
              const id = `set-${s.key}`
              const err = errors[s.key]
              return (
                <li key={s.key} className="grid gap-x-6 gap-y-2 px-5 py-4 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center">
                  <label htmlFor={id} id={`${id}-l`} className="text-sm font-medium">{label(s)}
                    <span className="block text-xs/[18px] font-normal text-muted-foreground">{t('admin.default', { value: fallback(s) })}</span>
                  </label>
                  {s.kind === 'Bool' ? <Switch id={id} disabled={!!pending[s.key]} checked={!!v} onCheckedChange={(c) => save(s.key, c)} />
                    : s.kind === 'Channels' ? (
                      <div role="group" aria-labelledby={`${id}-l`} className="flex flex-wrap gap-x-5 gap-y-2 text-sm">
                        <label className="flex min-h-6 items-center gap-2"><Checkbox disabled={!!pending[s.key]} checked={!!v.app} onCheckedChange={(c) => save(s.key, { ...v, app: !!c })} />{t('pref.app')}</label>
                        <label className="flex min-h-6 items-center gap-2"><Checkbox disabled={!!pending[s.key]} checked={!!v.email} onCheckedChange={(c) => save(s.key, { ...v, email: !!c })} />{t('pref.email')}</label>
                      </div>
                    ) : (
                      <form className="flex items-center gap-2" onSubmit={(e) => { e.preventDefault(); save(s.key, s.kind === 'Int' ? Number(v) : v) }}>
                        <Input id={id} disabled={!!pending[s.key]} className={cn('w-full', s.kind === 'Text' ? 'sm:w-80' : 'sm:w-56')} type={s.kind === 'Int' ? 'number' : s.kind === 'Time' ? 'time' : 'text'} value={v ?? ''}
                          onChange={(e) => setDraft({ ...draft, [s.key]: e.target.value })} aria-invalid={!!err} aria-describedby={err ? `${id}-e` : undefined} />
                        {s.key in draft && <Button type="submit" disabled={!!pending[s.key]}>{t('common.save')}</Button>}
                      </form>
                    )}
                  {pending[s.key] && <p className="flex items-center gap-2 text-xs/[18px] text-muted-foreground sm:col-span-2" role="status"><Spinner />{t('common.saving')}</p>}
                  {err != null && <div id={`${id}-e`} className="sm:col-span-2"><ErrorBanner error={err} retryLabel={t('app.retry')} retry={() => save(s.key, s.kind === 'Int' ? Number(v) : v)} /></div>}
                </li>
              )
            })}
          </ul>
        </Section>
      ))}
    </div>
  )
}

// ---------- Users and roles (FR-004, FR-016, FR-017, FR-027, E-26) ----------

type UserRow = Schemas['AdminUserRow']

/** A system role with where it comes from: directory-group roles are filled, roles added in the Hub are outlined. */
const RoleBadge = ({ r, children }: { r: UserRow['roles'][number]; children: React.ReactNode }) => (
  <Badge variant={r.source === 'Group' ? 'secondary' : 'outline'} className="gap-1 rounded-md text-xs/[18px]" title={runtimeText(`role.source.${r.source}`)}>{children}</Badge>
)

export function Users() {
  const qc = useQueryClient()
  const screens = useFlag('people_screens'), structure = useFlag('org_structure'), me = useMe()
  const [q, setQ] = useState('')
  const [inactive, setInactive] = useState(false)
  const [noSup, setNoSup] = useState(false)
  const list = useQuery({ queryKey: screens ? ['admin', 'users', q, inactive, noSup, me.id, true, structure] : ['admin', 'users', q, inactive, noSup], queryFn: () => typedRequest('get', '/api/v1/admin/users', { query: { q, inactive, noSupervisor: noSup } }) })
  const everyone = useQuery({ queryKey: screens ? ['admin', 'users', 'all', me.id, true, structure] : ['admin', 'users', 'all'], queryFn: () => typedRequest('get', '/api/v1/admin/users', { query: { inactive: true } }) })
  const ref = useQuery({ queryKey: ['reference'], queryFn: () => typedRequest('get', '/api/v1/reference', {}) })
  const [edit, setEdit] = useState<UserRow | null>(null)
  const [creating, setCreating] = useState(false)
  const [handoff, setHandoff] = useState<{ id: string; email: string; displayName: string } | null>(null)
  const supervisor = (id: string, sourceId: string) => { const person = everyone.data?.find(u => u.id === id); return person ? <PersonPanelTrigger userId={person.id} name={person.displayName} active={person.isActive} focusKey={`admin:user:${sourceId}:supervisor`} /> : <Missing /> }
  const refresh = () => { void invalidateChange(qc, 'User') }
  const rows = screens ? list.isSuccess && !list.error ? list.data : [] : list.data ?? []
  const screen = (user: UserRow) => {
    const read = user.screen
    if (!screens || !read || read.userId !== user.id || read.userRowVersion !== user.rowVersion || read.homeDisciplineId !== user.homeDisciplineId || read.configuredInactive !== !user.isActive) return undefined
    const homeValid = read.homeDisciplineState === 'NotSet' ? user.homeDisciplineId == null && read.homeDiscipline == null
      : read.homeDisciplineState === 'Unknown' ? user.homeDisciplineId != null && read.homeDiscipline == null
      : read.homeDiscipline && read.homeDiscipline.id === user.homeDisciplineId && !!read.homeDiscipline.name.trim()
        && read.homeDisciplineState === (read.homeDiscipline.isActive ? 'Active' : 'Inactive')
    const expectedScope = !user.roles.some(role => role.role === 'Supervisor') ? 'NotApplicable' : structure ? 'OrgPeopleScope' : 'DirectReports'
    return homeValid && read.scopeBasis === expectedScope && read.capacityBasis === (user.weeklyCapacityHours == null ? 'OrganisationDefault' : 'Override')
      && (user.weeklyCapacityHours == null || read.baseWeeklyCapacityHours === user.weeklyCapacityHours) ? read : undefined
  }
  const unavailable = () => <span className="text-muted-foreground">{t('adminScreen.unknown')}</span>
  const extraColumns: Column<UserRow>[] = !screens ? [] : [
    { id: 'homeDiscipline', label: t('adminScreen.homeDiscipline'), cell: user => { const read = screen(user); return !read ? unavailable() : read.homeDisciplineState === 'NotSet' ? t('adminScreen.notSet')
      : read.homeDiscipline ? read.homeDiscipline.isActive ? read.homeDiscipline.name : t('common.inactiveSuffix', { name: read.homeDiscipline.name }) : unavailable() } },
    { id: 'baseCapacity', label: t('adminScreen.baseCapacity'), cell: user => { const read = screen(user); return read && Number.isFinite(read.baseWeeklyCapacityHours)
      && ['Override', 'OrganisationDefault'].includes(read.capacityBasis) ? t(read.capacityBasis === 'Override' ? 'adminScreen.override' : 'adminScreen.defaultCapacity', { hours: read.baseWeeklyCapacityHours }) : unavailable() } },
    { id: 'templateEditor', label: t('adminScreen.templateEditor'), cell: user => t(user.isTemplateEditor ? 'common.yes' : 'common.no') },
    { id: 'peopleScope', label: t('adminScreen.scope'), cell: user => { const read = screen(user)
      if (!read) return unavailable()
      if (read.scopeBasis === 'NotApplicable') return t('adminScreen.notApplicable')
      return read.peopleScopeCount != null && Number.isInteger(read.peopleScopeCount) && read.peopleScopeCount >= 0 && ['DirectReports', 'OrgPeopleScope'].includes(read.scopeBasis)
        ? <span className="block space-y-1"><span className="block">{t(read.scopeBasis === 'DirectReports' ? 'adminScreen.directReports' : 'adminScreen.orgScope', { count: read.peopleScopeCount })}</span>
          {read.scopeIncludesSelf && <span className="block text-xs text-muted-foreground">{t('adminScreen.includesSelf')}</span>}{read.configuredInactive && <span className="block text-xs text-muted-foreground">{t('adminScreen.configuredInactive')}</span>}</span> : unavailable() } },
  ]
  // FR-REC-14: the shared DataTable; no column sorts and none is optional, so the list keeps the server's order.
  const table = useTable<UserRow>('hub.userColumns', ([
    { id: 'name', label: t('common.name'), cell: (u) => (
      <div className="flex items-start gap-2.5"><Avatar id={u.id} name={u.displayName} />
        <div className="min-w-0"><div className="font-semibold"><PersonPanelTrigger userId={u.id} name={u.displayName} active={u.isActive} focusKey={`admin:user:${u.id}:name`}>{u.isActive ? u.displayName : t('common.inactiveSuffix', { name: u.displayName })}</PersonPanelTrigger></div>
          <div className="break-words text-xs/[18px] text-muted-foreground">{u.email}</div></div></div>) },
    { id: 'jobTitle', label: t('admin.jobTitle'), cell: (u) => u.jobTitle || <Missing /> },
    { id: 'office', label: t('admin.office'), cell: (u) => ref.data?.offices.find((o: any) => o.id === u.officeId)?.name ?? <Missing /> },
    { id: 'supervisor', label: t('admin.supervisor'), cell: (u) => u.supervisorId ? supervisor(u.supervisorId, u.id) : <Chip tone="warn">{t('admin.noSupervisor')}</Chip> },
    ...extraColumns,
    { id: 'roles', label: t('admin.roles'), cell: (u) => <div className="flex flex-wrap gap-1">{u.roles.map((r) => <RoleBadge key={r.role + r.source} r={r}>{runtimeText(`role.${r.role}`)}</RoleBadge>)}</div> },
    { id: 'status', label: t('common.status'), cell: (u) => <Active on={u.isActive} /> },
    { id: 'lastSignIn', label: t('admin.lastSignIn'), className: 'whitespace-nowrap tabular-nums', cell: (u) => u.lastSignInAt ? fmtTime(u.lastSignInAt) : <Missing /> },
    { id: 'actions', label: t('common.actions'), srOnly: true, className: 'py-0.5', cell: (u) => (
      <div className="flex justify-end gap-1">
        <Button size="sm" variant="ghost" asChild><Link to={`/people/${u.id}/reassign-work`}>{t('reassign.action')}</Link></Button>
        <Button size="sm" variant="outline" onClick={() => setEdit(u)}>{t('common.edit')}</Button>
      </div>) },
  ] as Column<UserRow>[]).map((c) => ({ ...c, fixed: true, className: cn('align-top', c.className) })), rows, (u) => [u.displayName])
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="max-w-3xl"><h2 className={h2}>{t('admin.users')}</h2>
          <p className="mt-1 text-base/6 text-muted-foreground">{t('admin.usersIntro')}</p></div>
        <><CertificationVocabulary /><Button onClick={() => setCreating(true)}><Plus className="size-4" />{t('admin.createUser')}</Button></>
      </div>
      <FilterBar search={<Field label={t('common.search')} htmlFor="users-q"><Input id="users-q" placeholder={t('common.search')} value={q} onChange={(e) => setQ(e.target.value)} /></Field>}
        quick={<>
          <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={inactive} onCheckedChange={(c) => setInactive(!!c)} />{t('common.showInactive')}</label>
          <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={noSup} onCheckedChange={(c) => setNoSup(!!c)} />{t('admin.noSupervisor')}</label>
        </>} />
      {list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : (
        <DataTable table={table} rows={table.sorted} loading={list.isPending} wide caption={t('admin.users')} current={() => false}
          rowClass={(u) => !u.isActive && 'text-muted-foreground'} empty={<Empty>{t('admin.noUsers')}</Empty>} />
      )}
      {screens && <AdminRoleExamples key={me.id} />}
      {edit && <UserDialog user={edit} users={everyone.data ?? []} offices={ref.data?.offices ?? []} disciplines={ref.data?.disciplines ?? []} onClose={() => { setEdit(null); refresh() }} />}
      {creating && <CreateUserDialog users={everyone.data ?? []} offices={ref.data?.offices ?? []}
        onClose={() => setCreating(false)} onCreated={(created) => { setCreating(false); setHandoff(created); refresh() }} />}
      {handoff && <CredentialHandoff user={handoff} onClose={() => setHandoff(null)} />}
    </div>
  )
}

function CreateUserDialog({ users, offices, onClose, onCreated }: { users: UserRow[]; offices: { id: string; name: string; isActive: boolean }[];
  onClose: () => void; onCreated: (user: { id: string; email: string; displayName: string }) => void }) {
  const factsEnabled = useFlag('capacity_screens')
  const [form, setForm] = useState({ email: '', displayName: '', jobTitle: '', officeId: '', supervisorId: '' })
  const [err, setErr] = useState<unknown>(null), [busy, setBusy] = useState(false)
  const safe = useSafeForm('user.new', form, setForm) // FR-SAV-03
  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setErr(null); setBusy(true)
    try {
      const result = await typedRequest('post', '/api/v1/admin/users', { body: { ...form, officeId: form.officeId || null, supervisorId: form.supervisorId || null } })
      safe.clear(); toast.success(t('common.saved')); onCreated({ id: result.id, email: form.email.trim(), displayName: form.displayName.trim() })
    } catch (e) { setErr(e) } finally { setBusy(false) }
  }
  return <Dialog open onOpenChange={(o) => !o && !busy && safe.close(onClose)}>
    <DialogContent className="max-w-lg"><DialogHeader><DialogTitle>{t('admin.createUser')}</DialogTitle></DialogHeader>
      <DraftNotice form={safe} />
      <form className="space-y-4" onSubmit={submit}><fieldset disabled={busy} className="space-y-4">
        <Field label={t('admin.email')} htmlFor="new-user-email" error={(err as ApiError)?.fieldErrors?.email}><Input id="new-user-email" type="email" required maxLength={200} value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></Field>
        <Field label={t('common.name')} htmlFor="new-user-name" error={(err as ApiError)?.fieldErrors?.displayName}><Input id="new-user-name" required maxLength={200} value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} /></Field>
        <Field label={t('admin.jobTitle')} htmlFor="new-user-title" optional><Input id="new-user-title" maxLength={200} value={form.jobTitle} onChange={(e) => setForm({ ...form, jobTitle: e.target.value })} /></Field>
        <Field label={t('admin.office')} htmlFor="new-user-office"><select id="new-user-office" className={selectCls} value={form.officeId} onChange={(e) => setForm({ ...form, officeId: e.target.value })}><option value="">{t('common.none')}</option>{offices.filter((o) => o.isActive).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}</select></Field>
        <Field label={t('admin.supervisor')} htmlFor="new-user-supervisor"><select id="new-user-supervisor" className={selectCls} value={form.supervisorId} onChange={(e) => setForm({ ...form, supervisorId: e.target.value })}><option value="">{t('admin.noSupervisor')}</option>{users.filter((u) => u.isActive).map((u) => <option key={u.id} value={u.id}>{personOptionLabel(u.displayName, u.personFacts?.notices, factsEnabled)}</option>)}</select></Field>
        {err != null && <ErrorBanner error={err} />}
      </fieldset><DialogFooter><Button type="button" variant="outline" onClick={() => safe.close(onClose)}>{t('common.cancel')}</Button>
        <Button type="submit" disabled={busy}>{busy && <Spinner />}{busy ? t('common.saving') : t('common.save')}</Button></DialogFooter></form>
    </DialogContent>
  </Dialog>
}

function CredentialHandoff({ user, onClose }: { user: { id: string; email: string; displayName: string }; onClose: () => void }) {
  return <Dialog open onOpenChange={(o) => !o && onClose()}>
    <DialogContent className="max-w-lg"><DialogHeader><DialogTitle>{t('admin.credentialHandoff')}</DialogTitle></DialogHeader>
      <div className="space-y-3 text-sm"><p>{t('admin.credentialHandoffIntro')}</p>
        <dl className="grid gap-3 rounded-md border bg-muted p-4">
          <div><dt className="text-xs/[18px] font-medium text-muted-foreground">{t('admin.email')}</dt><dd className="break-all">{user.email}</dd></div>
          <div><dt className="text-xs/[18px] font-medium text-muted-foreground">{t('auth.userId')}</dt><dd className="key break-all">{user.id}</dd></div>
        </dl>
        <p>{t('admin.credentialHandoffSteps')}</p>
        <p className="flex gap-2 rounded-md border border-warn/40 bg-warn-bg px-4 py-3 text-warn"><span aria-hidden>▲</span>{t('admin.credentialHandoffWarning')}</p></div>
      <DialogFooter><Button onClick={onClose}>{t('common.close')}</Button></DialogFooter>
    </DialogContent>
  </Dialog>
}

function UserDialog({ user, users, offices, disciplines, onClose }: { user: UserRow; users: UserRow[]; offices: any[]; disciplines: { id: string; name: string; isActive: boolean }[]; onClose: () => void }) {
  const factsEnabled = useFlag('capacity_screens'), screens = useFlag('people_screens')
  const [userVersion, setUserVersion] = useState(user.rowVersion)
  const [form, setForm] = useState({ supervisorId: user.supervisorId ?? '', officeId: user.officeId ?? '', homeDisciplineId: user.homeDisciplineId ?? '', weeklyCapacityHours: user.weeklyCapacityHours?.toString() ?? '', isTemplateEditor: user.isTemplateEditor, isActive: user.isActive })
  const [role, setRole] = useState('')
  const [err, setErr] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const [roles, setRoles] = useState(user.roles)
  // FR-SCR-82, AC-SCR-82: turning a person off shows what they still own and where to reassign it; it never blocks.
  const deactivating = screens && user.isActive && !form.isActive
  const openWork = useQuery({ queryKey: ['users', user.id, 'open-work'], enabled: deactivating,
    queryFn: () => typedRequest('get', '/api/v1/users/{id}/open-work', { params: { id: user.id } }) })
  const submit = async () => {
    setErr(null); setBusy(true)
    try {
      await typedRequest('patch', '/api/v1/admin/users/{id}', { params: { id: user.id }, body: { supervisorId: form.supervisorId || null, officeId: form.officeId || null, homeDisciplineId: form.homeDisciplineId || null, weeklyCapacityHours: form.weeklyCapacityHours === '' ? null : Number(form.weeklyCapacityHours), isTemplateEditor: form.isTemplateEditor, isActive: form.isActive }, ifMatch: userVersion })
      toast.success(t('common.saved')); onClose()
    } catch (e) { setErr(e) } finally { setBusy(false) }
  }
  const addRole = async () => { if (!role) return; try { await typedRequest('post', '/api/v1/admin/users/{id}/roles', { params: { id: user.id }, body: { role } }); setRoles([...roles, { role, source: 'Manual' }]); setRole('') } catch (e) { setErr(e) } }
  const removeRole = async (r: string) => { try { await typedRequest('delete', '/api/v1/admin/users/{id}/roles/{role}', { params: { id: user.id, role: r } }); setRoles(roles.filter((x) => !(x.role === r && x.source === 'Manual'))) } catch (e) { setErr(e) } }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className={cn('max-w-lg', factsEnabled && 'max-h-[90dvh] overflow-y-auto')}>
        <DialogHeader><DialogTitle><PersonPanelTrigger userId={user.id} name={user.displayName} active={user.isActive} focusKey={`admin:user:${user.id}:edit-dialog:title`} /></DialogTitle></DialogHeader>
        {factsEnabled && <details className="rounded-md border p-3"><summary className="cursor-pointer text-sm font-medium">{t('personFacts.adminSection')}</summary><div className="mt-3"><PersonFactsEditor adminUserId={user.id} onUserVersion={(nextVersion, priorVersion) => setUserVersion(baseline => baseline === priorVersion ? nextVersion : baseline)} /></div></details>}
        <div className="grid gap-4">
          <Field label={t('admin.supervisor')} htmlFor="u-sup">
            <select id="u-sup" className={selectCls} value={form.supervisorId} onChange={(e) => setForm({ ...form, supervisorId: e.target.value })}>
              <option value="">{t('admin.noSupervisor')}</option>
              {users.filter((u) => u.isActive && u.id !== user.id).map((u) => <option key={u.id} value={u.id}>{personOptionLabel(u.displayName, u.personFacts?.notices, factsEnabled)}</option>)}
            </select>
          </Field>
          <Field label={t('admin.office')} htmlFor="u-off">
            <select id="u-off" className={selectCls} value={form.officeId} onChange={(e) => setForm({ ...form, officeId: e.target.value })}>
              <option value="">{t('common.none')}</option>
              {offices.filter((o) => o.isActive || o.id === form.officeId).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
            </select>
          </Field>
          {/* FR-GRP-01 (D7): the person's home discipline, for grouping Workload and the Planner; edited here, not synced. */}
          <Field label={t('admin.homeDiscipline')} htmlFor="u-disc" optional>
            <select id="u-disc" className={selectCls} value={form.homeDisciplineId} onChange={(e) => setForm({ ...form, homeDisciplineId: e.target.value })}>
              <option value="">{t('staff.group.notSet')}</option>
              {disciplines.filter((d) => d.isActive || d.id === form.homeDisciplineId).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          <Field label={t('admin.capacity')} htmlFor="u-cap"><Input id="u-cap" className="w-32" type="number" min={0} max={80} value={form.weeklyCapacityHours} onChange={(e) => setForm({ ...form, weeklyCapacityHours: e.target.value })} /></Field>
          <label className="flex min-h-6 items-center gap-2 text-sm"><Checkbox checked={form.isTemplateEditor} onCheckedChange={(c) => setForm({ ...form, isTemplateEditor: !!c })} />{t('admin.templateEditor')}</label>
          <label className="flex min-h-6 items-center gap-2 text-sm"><Switch checked={form.isActive} onCheckedChange={(c) => setForm({ ...form, isActive: c })} />{t('admin.userActive')}</label>
          {deactivating && (openWork.error ? <ErrorBanner error={openWork.error} retry={() => openWork.refetch()} /> : !openWork.data ? <Loading rows={1} />
            : openWork.data.items.length > 0 && <p role="status" className="rounded-md bg-muted px-3 py-2 text-sm" data-deactivate-open-work={openWork.data.items.length}>
              {t('adminScreen.deactivateOpenWork', { count: openWork.data.items.length })} <Link className="font-medium text-primary underline" to={`/people/${user.id}/reassign-work`}>{t('reassign.action')}</Link></p>)}
          <div className="space-y-2">
            <div className="text-sm font-medium">{t('admin.roles')}</div>
            <div className="flex flex-wrap gap-1.5">
              {roles.map((r) => (
                <RoleBadge key={r.role + r.source} r={r}>
                  {runtimeText(`role.${r.role}`)} · {runtimeText(`role.source.${r.source}`)}
                  {r.source === 'Manual' && <button type="button" className="-my-0.5 -mr-1 grid size-6 place-items-center rounded-md text-muted-foreground hover:bg-muted hover:text-bad" onClick={() => removeRole(r.role)} aria-label={`${t('common.remove')} ${runtimeText(`role.${r.role}`)}`}><X className="size-3.5" aria-hidden /></button>}
                </RoleBadge>
              ))}
            </div>
            <div className="flex gap-2">
              <select className={selectCls} value={role} onChange={(e) => setRole(e.target.value)} aria-label={t('admin.addRole')}>
                <option value="">{t('admin.addRole')}</option>
                {['Admin', 'Executive', 'Supervisor', 'ProjectManager', 'ReadOnly'].filter((r) => !roles.some((x) => x.role === r && x.source === 'Manual')).map((r) => <option key={r} value={r}>{runtimeText(`role.${r}`)}</option>)}
              </select>
              <Button variant="outline" onClick={addRole} disabled={!role}>{t('common.add')}</Button>
            </div>
          </div>
          {err != null && <ErrorBanner error={err} />}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button onClick={submit} disabled={busy}>{busy && <Spinner />}{busy ? t('common.saving') : t('common.save')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function OrgActivity() {
  const [page, setPage] = useState(1)
  const [hideSignIns, setHideSignIns] = useState(true)
  const list = useQuery({ queryKey: ['admin', 'activity', page, hideSignIns], queryFn: () => typedRequest('get', '/api/v1/admin/activity', { query: { page, pageSize: 50, excludeAction: hideSignIns ? 'SignedIn' : undefined } }) })
  return (
    <div className="space-y-4">
      <div className="max-w-3xl"><h2 className={h2}>{t('admin.activity')}</h2>
        <p className="mt-1 text-base/6 text-muted-foreground">{t('admin.activityIntro')}</p></div>
      <label className="flex min-h-6 items-center gap-2 text-sm"><Checkbox checked={hideSignIns} onCheckedChange={(c) => { setHideSignIns(c === true); setPage(1) }} />{t('activity.hideSignIns')}</label>
      {list.error ? <ErrorBanner error={list.error} retry={() => list.refetch()} /> : list.isPending ? <div className={card}><Loading /></div>
        : <ActivityTable rows={list.data.items} total={list.data.totalCount} page={page} pageSize={50} onPage={setPage} />}
    </div>
  )
}
