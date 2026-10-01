import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Plus } from 'lucide-react'
import { useState } from 'react'
import { NavLink, Navigate, useParams, Link } from 'react-router'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Page, selectCls } from '@/components/hub/common'
import { ActivityTable } from '@/components/hub/activity'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { ApiError, del, get, patch, post, put, qs } from '@/lib/api'
import { fmtTime } from '@/lib/format'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { toast } from 'sonner'

const KINDS = ['disciplines', 'clients', 'offices', 'deliverableTypes', 'phases', 'projectTypes'] as const
type Kind = (typeof KINDS)[number]

export function AdminLayout({ children }: { children: React.ReactNode }) {
  const links = [...KINDS.map((k) => ({ to: `/admin/reference/${k}`, label: t(`admin.${k}`) })),
    { to: '/admin/settings', label: t('admin.settings') }, { to: '/admin/users', label: t('admin.users') }, { to: '/admin/activity', label: t('admin.activity') },
    ...ADMIN_EXTRA]
  return (
    <Page title={t('admin.title')}>
      <div className="flex flex-col gap-4 lg:flex-row">
        <nav aria-label={t('admin.title')} className="lg:w-52">
          <ul className="flex flex-wrap gap-1 lg:flex-col">
            {links.map((l) => (
              <li key={l.to}>
                <NavLink to={l.to} className={({ isActive }) => cn('block rounded-md px-3 py-1.5 text-sm hover:bg-muted', isActive && 'bg-accent font-medium text-accent-foreground')}>{l.label}</NavLink>
              </li>
            ))}
          </ul>
        </nav>
        <div className="min-w-0 flex-1">{children}</div>
      </div>
    </Page>
  )
}

/** Extra admin screens registered by later packets (reassign work, holidays, templates). */
export const ADMIN_EXTRA: { to: string; label: string }[] = []

export function AdminIndex() { return <Navigate to="/admin/reference/disciplines" replace /> }

// ---------- Reference data (FR-008..FR-015) ----------

interface Ref { id: string; name: string; isActive: boolean; sortOrder: number; rowVersion: number; code?: string; colour?: string; timeZone?: string; shortName?: string; defaultDisciplineId?: string }

export function ReferenceData() {
  const { kind = 'disciplines' } = useParams() as { kind: Kind }
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['admin', kind], queryFn: () => get<Ref[]>(`admin/${kind}`) })
  const disciplines = useQuery({ queryKey: ['admin', 'disciplines'], queryFn: () => get<Ref[]>('admin/disciplines'), enabled: kind === 'deliverableTypes' })
  const [editing, setEditing] = useState<Partial<Ref> | null>(null)
  const [deactivate, setDeactivate] = useState<{ row: Ref; projects: number } | null>(null)
  const refresh = () => { qc.invalidateQueries({ queryKey: ['admin', kind] }); qc.invalidateQueries({ queryKey: ['reference'] }) }
  const save = useMutation({
    mutationFn: (r: Partial<Ref>) => r.id ? patch(`admin/${kind}/${r.id}`, r, r.rowVersion) : post(`admin/${kind}`, r),
    onSuccess: () => { refresh(); setEditing(null); toast.success(t('common.saved')) },
  })
  const rows = list.data ?? []
  const move = async (i: number, dir: -1 | 1) => {
    const a = rows[i], b = rows[i + dir]
    if (!b) return
    await patch(`admin/${kind}/${a.id}`, { sortOrder: b.sortOrder === a.sortOrder ? a.sortOrder + dir : b.sortOrder }, a.rowVersion)
    await patch(`admin/${kind}/${b.id}`, { sortOrder: a.sortOrder }, b.rowVersion)
    refresh()
  }
  const hasCode = kind === 'disciplines' || kind === 'offices'
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">{t(`admin.${kind}`)}</h2>
        <Button size="sm" onClick={() => setEditing({ name: '', isActive: true })}><Plus className="size-4" />{t('admin.add', { kind: t(`admin.${kind}`).toLowerCase() })}</Button>
      </div>
      {list.error && <ErrorBanner error={list.error} retry={() => list.refetch()} />}
      {list.isPending ? <Loading /> : rows.length === 0 ? <Empty>{t('common.empty')}</Empty> : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="bg-muted/60 text-left text-xs text-muted-foreground">
              <tr>
                <th className="px-3 py-2 font-medium">{t('common.order')}</th>
                <th className="px-3 py-2 font-medium">{t('common.name')}</th>
                {hasCode && <th className="px-3 py-2 font-medium">{t('common.code')}</th>}
                {kind === 'disciplines' && <th className="px-3 py-2 font-medium">{t('admin.colour')}</th>}
                {kind === 'offices' && <th className="px-3 py-2 font-medium">{t('admin.timeZone')}</th>}
                {kind === 'clients' && <th className="px-3 py-2 font-medium">{t('admin.shortName')}</th>}
                <th className="px-3 py-2 font-medium">{t('common.status')}</th>
                <th className="px-3 py-2"><span className="sr-only">{t('common.actions')}</span></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r, i) => (
                <tr key={r.id} className={cn('border-t', !r.isActive && 'text-muted-foreground')}>
                  <td className="px-3 py-1.5">
                    <div className="flex gap-1">
                      <Button size="icon" variant="ghost" className="size-7" disabled={i === 0} onClick={() => move(i, -1)} aria-label={t('admin.moveUp')}><ArrowUp className="size-3.5" /></Button>
                      <Button size="icon" variant="ghost" className="size-7" disabled={i === rows.length - 1} onClick={() => move(i, 1)} aria-label={t('admin.moveDown')}><ArrowDown className="size-3.5" /></Button>
                    </div>
                  </td>
                  <td className="px-3 py-1.5 font-medium">{r.name}</td>
                  {hasCode && <td className="px-3 py-1.5 key">{r.code}</td>}
                  {kind === 'disciplines' && <td className="px-3 py-1.5"><span className="inline-flex items-center gap-2"><span className="size-3 rounded-sm" style={{ background: r.colour }} aria-hidden />{r.colour}</span></td>}
                  {kind === 'offices' && <td className="px-3 py-1.5">{r.timeZone}</td>}
                  {kind === 'clients' && <td className="px-3 py-1.5">{r.shortName}</td>}
                  <td className="px-3 py-1.5">{r.isActive ? <Badge variant="secondary">{t('common.active')}</Badge> : <Badge variant="outline">{t('common.inactive')}</Badge>}</td>
                  <td className="space-x-1 whitespace-nowrap px-3 py-1.5 text-right">
                    <Button size="sm" variant="ghost" onClick={() => setEditing(r)}>{t('common.edit')}</Button>
                    {r.isActive
                      ? <Button size="sm" variant="ghost" onClick={async () => setDeactivate({ row: r, projects: (await get(`admin/${kind}/${r.id}/usage`)).projects })}>{t('admin.deactivate')}</Button>
                      : <Button size="sm" variant="ghost" onClick={() => save.mutate({ id: r.id, rowVersion: r.rowVersion, isActive: true })}>{t('admin.activate')}</Button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <Dialog open={!!editing} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent>
          <DialogHeader><DialogTitle>{editing?.id ? t('common.edit') : t('admin.add', { kind: t(`admin.${kind}`).toLowerCase() })}</DialogTitle></DialogHeader>
          {editing && (
            <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); save.mutate(editing) }}>
              <Field label={t('common.name')} htmlFor="ref-name" error={(save.error as ApiError)?.fieldErrors?.name}>
                <Input id="ref-name" required value={editing.name ?? ''} onChange={(e) => setEditing({ ...editing, name: e.target.value })} />
              </Field>
              {hasCode && <Field label={t('common.code')} htmlFor="ref-code"><Input id="ref-code" required value={editing.code ?? ''} onChange={(e) => setEditing({ ...editing, code: e.target.value })} /></Field>}
              {kind === 'disciplines' && <Field label={t('admin.colour')} htmlFor="ref-colour"><Input id="ref-colour" type="color" value={editing.colour ?? '#64748b'} onChange={(e) => setEditing({ ...editing, colour: e.target.value })} className="h-9 w-20 p-1" /></Field>}
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
                <Button type="submit" disabled={save.isPending}>{t('common.save')}</Button>
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

interface SettingRow { key: string; kind: string; group: string; default: any; value: any }

export function Settings() {
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['admin', 'settings'], queryFn: () => get<SettingRow[]>('admin/settings') })
  const [draft, setDraft] = useState<Record<string, any>>({})
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = async (key: string, value: any) => {
    try {
      await put(`admin/settings/${encodeURIComponent(key)}`, { value })
      setErrors((e) => ({ ...e, [key]: '' }))
      setDraft((d) => { const { [key]: _, ...rest } = d; return rest })
      qc.invalidateQueries({ queryKey: ['admin', 'settings'] }); qc.invalidateQueries({ queryKey: ['me'] })
      toast.success(t('common.saved'))
    } catch (e) { setErrors((x) => ({ ...x, [key]: (e as ApiError).fieldErrors?.value?.[0] ?? (e as Error).message })) }
  }
  if (list.isPending) return <Loading />
  if (list.error) return <ErrorBanner error={list.error} retry={() => list.refetch()} />
  const groups = [...new Set(list.data.map((s) => s.group))]
  const label = (s: SettingRow) => s.key.startsWith('rule_enabled.') ? t('rule.enabled', { rule: s.key.slice(13), name: t(`rule.${s.key.slice(13)}`) })
    : s.key.startsWith('notify_default.') ? t(`event.${s.key.slice(15)}`) : t(`setting.${s.key}`)
  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">{t('admin.settingsIntro')}</p>
      {groups.map((g) => (
        <section key={g} className="rounded-lg border bg-card">
          <h2 className="border-b px-4 py-2 text-sm font-semibold">{t(`admin.group.${g}`)}</h2>
          <ul className="divide-y">
            {list.data.filter((s) => s.group === g).map((s) => {
              const v = s.key in draft ? draft[s.key] : s.value
              const id = `set-${s.key}`
              return (
                <li key={s.key} className="flex flex-wrap items-center gap-3 px-4 py-2">
                  <label htmlFor={id} className="min-w-64 flex-1 text-sm">{label(s)}
                    <span className="block text-xs text-muted-foreground">{t('admin.default', { value: typeof s.default === 'object' ? `${s.default.app ? 'in-app' : ''}${s.default.email ? ' + email' : ''}` : String(s.default) })}</span>
                  </label>
                  {s.kind === 'Bool' ? <Switch id={id} checked={!!v} onCheckedChange={(c) => save(s.key, c)} />
                    : s.kind === 'Channels' ? (
                      <div className="flex gap-4 text-sm">
                        <label className="flex items-center gap-2"><Checkbox checked={!!v.app} onCheckedChange={(c) => save(s.key, { ...v, app: !!c })} />{t('pref.app')}</label>
                        <label className="flex items-center gap-2"><Checkbox checked={!!v.email} onCheckedChange={(c) => save(s.key, { ...v, email: !!c })} />{t('pref.email')}</label>
                      </div>
                    ) : (
                      <form className="flex items-center gap-2" onSubmit={(e) => { e.preventDefault(); save(s.key, s.kind === 'Int' ? Number(v) : v) }}>
                        <Input id={id} className="h-8 w-56" type={s.kind === 'Int' ? 'number' : s.kind === 'Time' ? 'time' : 'text'} value={v ?? ''}
                          onChange={(e) => setDraft({ ...draft, [s.key]: e.target.value })} aria-invalid={!!errors[s.key]} />
                        {s.key in draft && <Button size="sm" type="submit">{t('common.save')}</Button>}
                      </form>
                    )}
                  {errors[s.key] && <p className="w-full text-xs text-bad" role="alert">{errors[s.key]}</p>}
                </li>
              )
            })}
          </ul>
        </section>
      ))}
    </div>
  )
}

// ---------- Users and roles (FR-004, FR-016, FR-017, FR-027, E-26) ----------

interface UserRow { id: string; displayName: string; email: string; jobTitle?: string; officeId?: string; supervisorId?: string; isActive: boolean; weeklyCapacityHours?: number; isTemplateEditor: boolean; lastSignInAt?: string; rowVersion: number; roles: { role: string; source: string }[] }

export function Users() {
  const qc = useQueryClient()
  const [q, setQ] = useState('')
  const [inactive, setInactive] = useState(false)
  const [noSup, setNoSup] = useState(false)
  const list = useQuery({ queryKey: ['admin', 'users', q, inactive, noSup], queryFn: () => get<UserRow[]>(`admin/users${qs({ q, inactive, noSupervisor: noSup })}`) })
  const everyone = useQuery({ queryKey: ['admin', 'users', 'all'], queryFn: () => get<UserRow[]>('admin/users?inactive=true') })
  const ref = useQuery({ queryKey: ['reference'], queryFn: () => get('reference') })
  const [edit, setEdit] = useState<UserRow | null>(null)
  const [creating, setCreating] = useState(false)
  const [handoff, setHandoff] = useState<{ id: string; email: string; displayName: string } | null>(null)
  const name = (id?: string) => everyone.data?.find((u) => u.id === id)?.displayName ?? t('common.dash')
  const refresh = () => qc.invalidateQueries({ queryKey: ['admin', 'users'] })
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="text-lg font-semibold">{t('admin.users')}</h2>
        <p className="text-sm text-muted-foreground">{t('admin.usersIntro')}</p></div>
        <Button size="sm" onClick={() => setCreating(true)}><Plus className="size-4" />{t('admin.createUser')}</Button></div>
      <div className="flex flex-wrap items-center gap-3">
        <Input className="h-8 w-64" placeholder={t('common.search')} value={q} onChange={(e) => setQ(e.target.value)} aria-label={t('common.search')} />
        <label className="flex items-center gap-2 text-sm"><Checkbox checked={inactive} onCheckedChange={(c) => setInactive(!!c)} />{t('common.showInactive')}</label>
        <label className="flex items-center gap-2 text-sm"><Checkbox checked={noSup} onCheckedChange={(c) => setNoSup(!!c)} />{t('admin.noSupervisor')}</label>
      </div>
      {list.error && <ErrorBanner error={list.error} />}
      {list.isPending ? <Loading /> : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="bg-muted/60 text-left text-xs text-muted-foreground">
              <tr>{['common.name', 'admin.jobTitle', 'admin.office', 'admin.supervisor', 'admin.roles', 'common.status', 'admin.lastSignIn', ''].map((h) => <th key={h} className="px-3 py-2 font-medium">{h ? t(h) : <span className="sr-only">{t('common.actions')}</span>}</th>)}</tr>
            </thead>
            <tbody>
              {(list.data ?? []).map((u) => (
                <tr key={u.id} className="border-t">
                  <td className="px-3 py-1.5"><div className="font-medium">{u.isActive ? u.displayName : t('common.inactiveSuffix', { name: u.displayName })}</div><div className="text-xs text-muted-foreground">{u.email}</div></td>
                  <td className="px-3 py-1.5">{u.jobTitle}</td>
                  <td className="px-3 py-1.5">{ref.data?.offices.find((o: any) => o.id === u.officeId)?.name ?? t('common.dash')}</td>
                  <td className="px-3 py-1.5">{u.supervisorId ? name(u.supervisorId) : <span className="text-warn">{t('admin.noSupervisor')}</span>}</td>
                  <td className="px-3 py-1.5"><div className="flex flex-wrap gap-1">{u.roles.map((r) => <Badge key={r.role + r.source} variant={r.source === 'Group' ? 'secondary' : 'outline'} title={t(`role.source.${r.source}`)}>{t(`role.${r.role}`)}</Badge>)}</div></td>
                  <td className="px-3 py-1.5">{u.isActive ? t('common.active') : t('common.inactive')}</td>
                  <td className="px-3 py-1.5 whitespace-nowrap">{fmtTime(u.lastSignInAt)}</td>
                  <td className="whitespace-nowrap px-3 py-1.5 text-right">
                    <Button size="sm" variant="ghost" asChild><Link to={`/people/${u.id}/reassign-work`}>{t('reassign.action')}</Link></Button>
                    <Button size="sm" variant="ghost" onClick={() => setEdit(u)}>{t('common.edit')}</Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {edit && <UserDialog user={edit} users={everyone.data ?? []} offices={ref.data?.offices ?? []} onClose={() => { setEdit(null); refresh() }} />}
      {creating && <CreateUserDialog users={everyone.data ?? []} offices={ref.data?.offices ?? []}
        onClose={() => setCreating(false)} onCreated={(created) => { setCreating(false); setHandoff(created); refresh() }} />}
      {handoff && <CredentialHandoff user={handoff} onClose={() => setHandoff(null)} />}
    </div>
  )
}

function CreateUserDialog({ users, offices, onClose, onCreated }: { users: UserRow[]; offices: { id: string; name: string; isActive: boolean }[];
  onClose: () => void; onCreated: (user: { id: string; email: string; displayName: string }) => void }) {
  const [form, setForm] = useState({ email: '', displayName: '', jobTitle: '', officeId: '', supervisorId: '' })
  const [err, setErr] = useState<unknown>(null), [busy, setBusy] = useState(false)
  const submit = async (e: React.FormEvent) => {
    e.preventDefault(); setErr(null); setBusy(true)
    try {
      const result = await post<{ id: string }>('admin/users', { ...form, officeId: form.officeId || null, supervisorId: form.supervisorId || null })
      toast.success(t('common.saved')); onCreated({ id: result.id, email: form.email.trim(), displayName: form.displayName.trim() })
    } catch (e) { setErr(e) } finally { setBusy(false) }
  }
  return <Dialog open onOpenChange={(o) => !o && !busy && onClose()}>
    <DialogContent className="max-w-lg"><DialogHeader><DialogTitle>{t('admin.createUser')}</DialogTitle></DialogHeader>
      <form className="space-y-3" onSubmit={submit}><fieldset disabled={busy} className="space-y-3">
        <Field label={t('admin.email')} htmlFor="new-user-email" error={(err as ApiError)?.fieldErrors?.email}><Input id="new-user-email" type="email" required maxLength={200} value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></Field>
        <Field label={t('common.name')} htmlFor="new-user-name" error={(err as ApiError)?.fieldErrors?.displayName}><Input id="new-user-name" required maxLength={200} value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} /></Field>
        <Field label={t('admin.jobTitle')} htmlFor="new-user-title"><Input id="new-user-title" maxLength={200} value={form.jobTitle} onChange={(e) => setForm({ ...form, jobTitle: e.target.value })} /></Field>
        <Field label={t('admin.office')} htmlFor="new-user-office"><select id="new-user-office" className={selectCls} value={form.officeId} onChange={(e) => setForm({ ...form, officeId: e.target.value })}><option value="">{t('common.none')}</option>{offices.filter((o) => o.isActive).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}</select></Field>
        <Field label={t('admin.supervisor')} htmlFor="new-user-supervisor"><select id="new-user-supervisor" className={selectCls} value={form.supervisorId} onChange={(e) => setForm({ ...form, supervisorId: e.target.value })}><option value="">{t('admin.noSupervisor')}</option>{users.filter((u) => u.isActive).map((u) => <option key={u.id} value={u.id}>{u.displayName}</option>)}</select></Field>
        {err != null && <ErrorBanner error={err} />}
      </fieldset><DialogFooter><Button type="button" variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={busy}>{t('common.save')}</Button></DialogFooter></form>
    </DialogContent>
  </Dialog>
}

function CredentialHandoff({ user, onClose }: { user: { id: string; email: string; displayName: string }; onClose: () => void }) {
  return <Dialog open onOpenChange={(o) => !o && onClose()}>
    <DialogContent className="max-w-lg"><DialogHeader><DialogTitle>{t('admin.credentialHandoff')}</DialogTitle></DialogHeader>
      <div className="space-y-3 text-sm"><p>{t('admin.credentialHandoffIntro')}</p>
        <dl className="grid gap-2 rounded border p-3"><div><dt className="font-medium">{t('admin.email')}</dt><dd>{user.email}</dd></div><div><dt className="font-medium">{t('auth.userId')}</dt><dd className="break-all font-mono text-xs">{user.id}</dd></div></dl>
        <p>{t('admin.credentialHandoffSteps')}</p><p className="text-warn">{t('admin.credentialHandoffWarning')}</p></div>
      <DialogFooter><Button onClick={onClose}>{t('common.close')}</Button></DialogFooter>
    </DialogContent>
  </Dialog>
}

function UserDialog({ user, users, offices, onClose }: { user: UserRow; users: UserRow[]; offices: any[]; onClose: () => void }) {
  const [form, setForm] = useState({ supervisorId: user.supervisorId ?? '', officeId: user.officeId ?? '', weeklyCapacityHours: user.weeklyCapacityHours?.toString() ?? '', isTemplateEditor: user.isTemplateEditor, isActive: user.isActive })
  const [role, setRole] = useState('')
  const [err, setErr] = useState<unknown>(null)
  const [roles, setRoles] = useState(user.roles)
  const submit = async () => {
    setErr(null)
    try {
      await patch(`admin/users/${user.id}`, { supervisorId: form.supervisorId || null, officeId: form.officeId || null, weeklyCapacityHours: form.weeklyCapacityHours === '' ? null : Number(form.weeklyCapacityHours), isTemplateEditor: form.isTemplateEditor, isActive: form.isActive }, user.rowVersion)
      toast.success(t('common.saved')); onClose()
    } catch (e) { setErr(e) }
  }
  const addRole = async () => { if (!role) return; try { await post(`admin/users/${user.id}/roles`, { role }); setRoles([...roles, { role, source: 'Manual' }]); setRole('') } catch (e) { setErr(e) } }
  const removeRole = async (r: string) => { try { await del(`admin/users/${user.id}/roles/${r}`); setRoles(roles.filter((x) => !(x.role === r && x.source === 'Manual'))) } catch (e) { setErr(e) } }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader><DialogTitle>{user.displayName}</DialogTitle></DialogHeader>
        <div className="grid gap-3">
          <Field label={t('admin.supervisor')} htmlFor="u-sup">
            <select id="u-sup" className={selectCls} value={form.supervisorId} onChange={(e) => setForm({ ...form, supervisorId: e.target.value })}>
              <option value="">{t('admin.noSupervisor')}</option>
              {users.filter((u) => u.isActive && u.id !== user.id).map((u) => <option key={u.id} value={u.id}>{u.displayName}</option>)}
            </select>
          </Field>
          <Field label={t('admin.office')} htmlFor="u-off">
            <select id="u-off" className={selectCls} value={form.officeId} onChange={(e) => setForm({ ...form, officeId: e.target.value })}>
              <option value="">{t('common.none')}</option>
              {offices.filter((o) => o.isActive || o.id === form.officeId).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
            </select>
          </Field>
          <Field label={t('admin.capacity')} htmlFor="u-cap"><Input id="u-cap" type="number" min={0} max={80} value={form.weeklyCapacityHours} onChange={(e) => setForm({ ...form, weeklyCapacityHours: e.target.value })} /></Field>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={form.isTemplateEditor} onCheckedChange={(c) => setForm({ ...form, isTemplateEditor: !!c })} />{t('admin.templateEditor')}</label>
          <label className="flex items-center gap-2 text-sm"><Switch checked={form.isActive} onCheckedChange={(c) => setForm({ ...form, isActive: c })} />{t('admin.userActive')}</label>
          <div className="space-y-2">
            <div className="text-sm font-medium">{t('admin.roles')}</div>
            <div className="flex flex-wrap gap-1">
              {roles.map((r) => (
                <Badge key={r.role + r.source} variant={r.source === 'Group' ? 'secondary' : 'outline'} className="gap-1" title={t(`role.source.${r.source}`)}>
                  {t(`role.${r.role}`)} · {t(`role.source.${r.source}`)}
                  {r.source === 'Manual' && <button className="ml-1 text-muted-foreground hover:text-bad" onClick={() => removeRole(r.role)} aria-label={`${t('common.remove')} ${t(`role.${r.role}`)}`}>×</button>}
                </Badge>
              ))}
            </div>
            <div className="flex gap-2">
              <select className={selectCls} value={role} onChange={(e) => setRole(e.target.value)} aria-label={t('admin.addRole')}>
                <option value="">{t('admin.addRole')}</option>
                {['Admin', 'Executive', 'Supervisor', 'ProjectManager', 'ReadOnly'].filter((r) => !roles.some((x) => x.role === r && x.source === 'Manual')).map((r) => <option key={r} value={r}>{t(`role.${r}`)}</option>)}
              </select>
              <Button variant="outline" onClick={addRole} disabled={!role}>{t('common.add')}</Button>
            </div>
          </div>
          {err != null && <ErrorBanner error={err} />}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button onClick={submit}>{t('common.save')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function OrgActivity() {
  const [page, setPage] = useState(1)
  const list = useQuery({ queryKey: ['admin', 'activity', page], queryFn: () => get(`admin/activity${qs({ page, pageSize: 50 })}`) })
  return (
    <div className="space-y-3">
      <h2 className="text-lg font-semibold">{t('admin.activity')}</h2>
      <p className="text-sm text-muted-foreground">{t('admin.activityIntro')}</p>
      {list.error && <ErrorBanner error={list.error} />}
      {list.isPending ? <Loading /> : <ActivityTable rows={list.data.items} total={list.data.totalCount} page={page} pageSize={50} onPage={setPage} />}
    </div>
  )
}
