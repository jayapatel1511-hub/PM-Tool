import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertTriangle, CalendarDays, Check, ChartGantt, ChevronDown, EyeOff, FileStack, FolderKanban, KanbanSquare, Layers, LayoutDashboard, Link2, List, Plus, Users, type LucideIcon,
} from 'lucide-react'
import { createContext, useContext, useEffect, useState, useSyncExternalStore, type ReactNode } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ShellSlots } from '@/app/slots'
import { ConfirmDialog, Field, Notice, Spinner } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { ApiError, del, get, patch, post } from '@/lib/api'
import { useMe, type Me } from '@/lib/auth'
import { plural, t } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn } from '@/lib/utils'
import { ProjectsPicker } from '@/pages/Reports'

export interface WorkspaceRow { id: string; name: string; projectIds: string[]; hidden: number }
interface Stored { ws?: string; projects?: string }

/** The views that read the project scope (§36.1, FR-VIS-02); switching among them keeps it. */
export const SCOPED = ['/home', '/my-work', '/boards', '/tasks', '/timeline', '/calendar', '/files', '/team']

// The last scope shown is kept per viewer (a convenience: the URL carries it too, and wins).
const KEY = 'hub.scope'
const listeners = new Set<() => void>()
let stored: Stored = (() => { try { return JSON.parse(localStorage.getItem(KEY) ?? '{}') ?? {} } catch { return {} } })()
function remember(s: Stored) {
  if (s.ws === stored.ws && s.projects === stored.projects) return
  stored = s
  try { localStorage.setItem(KEY, JSON.stringify(s)) } catch { /* private window: the URL still carries the scope */ }
  listeners.forEach((l) => l())
}
const subscribe = (l: () => void) => { listeners.add(l); return () => { listeners.delete(l) } }

export function useWorkspaces() {
  return useQuery({ queryKey: ['workspaces'], queryFn: () => get<WorkspaceRow[]>('workspaces'), staleTime: 60_000 })
}

export interface Scope {
  /** False while a named workspace's projects are still loading. */
  ready: boolean
  /** The API's `projects` value: mine, all, or project ids. */
  api: string
  kind: 'mine' | 'all' | 'set' | 'workspace'
  ids: string[] | null
  ws?: WorkspaceRow
  label: string
  /** URL parameters that carry this scope to another view or into a saved view. */
  params: Record<string, string>
}

/** Inside a project the workspace views show that project only (FR-VIS-02: a project-specific view remains available). */
const Forced = createContext<string | null>(null)
export function ProjectScope({ projectId, children }: { projectId: string; children: ReactNode }) {
  return <Forced.Provider value={projectId}>{children}</Forced.Provider>
}

/** The selected project scope: the URL first (a shared link or saved view), else the last one chosen, else My projects.
 *  The server re-applies the reader's access to every value, so a scope can only narrow what someone sees. */
export function useScope(): Scope {
  const [sp] = useSearchParams()
  const { pathname } = useLocation()
  const list = useWorkspaces()
  const last = useSyncExternalStore(subscribe, () => stored)
  const forced = useContext(Forced)
  const urlWs = sp.get('ws') ?? undefined, urlProjects = sp.get('projects') ?? undefined
  const fromUrl = SCOPED.includes(pathname) && !!(urlWs || urlProjects)
  const s = fromUrl ? { ws: urlWs, projects: urlProjects } : last
  useEffect(() => { if (fromUrl) remember({ ws: urlWs, projects: urlProjects }) }, [fromUrl, urlWs, urlProjects])
  if (forced) return { ready: true, api: forced, kind: 'set', ids: [forced], label: '', params: { projects: forced } }
  const ws = s.ws ? list.data?.find((w) => w.id === s.ws) : undefined
  const ready = !s.ws || list.isFetched
  if (ws) return { ready, api: ws.projectIds.join(',') || 'none', kind: 'workspace', ids: ws.projectIds, ws, label: ws.name, params: { ws: ws.id } }
  const p = s.projects
  if (!p || p === 'mine') return { ready, api: 'mine', kind: 'mine', ids: null, label: t('scope.mine'), params: {} }
  if (p === 'all') return { ready, api: 'all', kind: 'all', ids: null, label: t('scope.all'), params: { projects: 'all' } }
  const ids = p.split(',').filter((x) => x && x !== 'none')
  return { ready, api: ids.join(',') || 'none', kind: 'set', ids, label: plural(ids.length, 'scope.oneProject', 'scope.nProjects', { n: ids.length }), params: { projects: p } }
}

/** Choosing a scope keeps it for the next view; on a scoped view it also replaces the URL's scope and sub-filters. */
export function useSetScope() {
  const [sp, setSp] = useSearchParams()
  const { pathname } = useLocation()
  return (s: Stored) => {
    remember(s)
    if (!SCOPED.includes(pathname)) return
    const n = new URLSearchParams(sp)
    for (const k of ['ws', 'projects', 'projectId', 'view', 'page']) n.delete(k)
    if (s.ws) n.set('ws', s.ws)
    else if (s.projects && s.projects !== 'mine') n.set('projects', s.projects)
    setSp(n)
  }
}

/** The scope's live projects by number and name, for filters and labels (visibility applied by the server). */
export function useScopeProjects(scope: Scope) {
  const params = scope.kind === 'mine' ? 'mine=true' : scope.kind === 'all' ? 'mine=false' : `mine=false&ids=${scope.api}`
  return useQuery({
    queryKey: ['scope-projects', scope.api], enabled: scope.ready,
    queryFn: async () => (await get<PageOf<{ id: string; projectNumber: string; name: string; status: string }>>(`projects?${params}&includeArchived=true&pageSize=200`)).items
      .filter((p) => p.status !== 'Archived' && p.status !== 'Cancelled'),
  })
}

/** A path with the scope's parameters and any filters, for drill-downs and view switching. */
export function scoped(scope: Scope, path: string, params: Record<string, string | undefined> = {}) {
  const n = new URLSearchParams(scope.params)
  for (const [k, v] of Object.entries(params)) if (v) n.set(k, v)
  const q = n.toString()
  return q ? `${path}?${q}` : path
}

/** Copies a deep link that keeps the view, filters and the projects themselves (FR-VIS-01); the reader's own access
 *  is applied when it opens, and a named workspace resolves only for its owner. */
async function share(scope: Scope) {
  const url = new URL(window.location.href)
  url.searchParams.delete('panel')
  if (scope.kind === 'all') url.searchParams.set('projects', 'all')
  else {
    const ids = scope.kind === 'mine' ? (await get<PageOf<{ id: string }>>('projects?mine=true&includeArchived=true&pageSize=200')).items.map((p) => p.id) : scope.ids ?? []
    url.searchParams.set('projects', ids.join(',') || 'none')
    if (scope.ws) url.searchParams.set('ws', scope.ws.id)
  }
  try { await navigator.clipboard.writeText(url.toString()); toast.success(t('scope.linkCopied')) }
  catch { toast.info(url.toString(), { duration: 15000 }) }
}

// ---------- Top-bar selector (ShellSlots.Workspace) ----------

type Mode = { mode: 'choose' | 'new' } | { mode: 'edit'; ws: WorkspaceRow }

function WorkspaceSelector() {
  const scope = useScope()
  const list = useWorkspaces()
  const setScope = useSetScope()
  const [dialog, setDialog] = useState<Mode | null>(null)
  const item = (label: string, on: boolean, pick: () => void, key: string) => (
    <DropdownMenuItem key={key} onSelect={pick} className="gap-2">
      <Check className={cn('size-3.5', on ? 'opacity-100' : 'opacity-0')} /><span className="flex-1 truncate">{label}</span>
    </DropdownMenuItem>
  )
  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="outline" className="max-w-52 gap-1.5" aria-label={t('scope.label', { name: scope.label })}>
            <Layers className="size-4" /><span className="hidden truncate sm:inline">{scope.label}</span><ChevronDown className="size-3.5 opacity-60" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-64">
          <DropdownMenuLabel>{t('scope.title')}</DropdownMenuLabel>
          {item(t('scope.mine'), scope.kind === 'mine', () => setScope({ projects: 'mine' }), 'mine')}
          {item(t('scope.all'), scope.kind === 'all', () => setScope({ projects: 'all' }), 'all')}
          {scope.kind === 'set' && item(scope.label, true, () => undefined, 'set')}
          {(list.data?.length ?? 0) > 0 && <><DropdownMenuSeparator /><DropdownMenuLabel>{t('scope.workspaces')}</DropdownMenuLabel></>}
          {list.data?.map((w) => item(w.name, scope.ws?.id === w.id, () => setScope({ ws: w.id }), w.id))}
          <DropdownMenuSeparator />
          <DropdownMenuItem onSelect={() => setDialog({ mode: 'choose' })}>{t('scope.choose')}</DropdownMenuItem>
          <DropdownMenuItem onSelect={() => setDialog({ mode: 'new' })}><Plus className="size-4" />{t('scope.new')}</DropdownMenuItem>
          {scope.ws && <DropdownMenuItem onSelect={() => setDialog({ mode: 'edit', ws: scope.ws! })}>{t('scope.edit', { name: scope.ws.name })}</DropdownMenuItem>}
        </DropdownMenuContent>
      </DropdownMenu>
      {dialog && <WorkspaceDialog m={dialog} initial={scope.ids} onClose={() => setDialog(null)} />}
    </>
  )
}

/** Choose projects for this view, optionally saving them as a named workspace; or edit or delete one (FR-VIS-02). */
function WorkspaceDialog({ m, initial, onClose }: { m: Mode; initial: string[] | null; onClose: () => void }) {
  const ws = m.mode === 'edit' ? m.ws : undefined
  const [ids, setIds] = useState((ws?.projectIds ?? (m.mode === 'choose' ? initial : null) ?? []).join(','))
  const [name, setName] = useState(ws?.name ?? '')
  const [save, setSave] = useState(m.mode !== 'choose')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const setScope = useSetScope()
  const qc = useQueryClient()
  const chosen = ids.split(',').filter(Boolean)
  const submit = async () => {
    if (!save) { setScope({ projects: ids }); onClose(); return }
    setBusy(true)
    try {
      const row = ws ? await patch<WorkspaceRow>(`workspaces/${ws.id}`, { name: name.trim(), projectIds: chosen }) : await post<WorkspaceRow>('workspaces', { name: name.trim(), projectIds: chosen })
      await qc.invalidateQueries({ queryKey: ['workspaces'] })
      setScope({ ws: row.id })
      toast.success(t('scope.saved', { name: row.name }))
      onClose()
    } catch (e) { const err = e as ApiError; setError(Object.values(err.fieldErrors ?? {})[0]?.[0] ?? err.message) } finally { setBusy(false) }
  }
  return (
    <>
      <Dialog open={!deleting} onOpenChange={(o) => !o && onClose()}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>{ws ? t('scope.editTitle', { name: ws.name }) : m.mode === 'new' ? t('scope.new') : t('scope.choose')}</DialogTitle>
            <DialogDescription>{t('scope.dialogHint')}</DialogDescription>
          </DialogHeader>
          <Field label={t('scope.projects')} htmlFor="ws-projects" hint={plural(chosen.length, 'scope.oneProject', 'scope.nProjects', { n: chosen.length })}>
            <ProjectsPicker id="ws-projects" value={ids || undefined} onChange={(v) => setIds(v ?? '')} placeholder={t('scope.pick')} />
          </Field>
          {m.mode === 'choose' && <label className="flex min-h-(--control-h) items-center gap-2 text-sm font-medium"><Checkbox checked={save} onCheckedChange={(c) => setSave(!!c)} />{t('scope.saveAs')}</label>}
          {save && <Field label={t('scope.name')} htmlFor="ws-name"><Input id="ws-name" value={name} maxLength={100} placeholder={t('scope.namePlaceholder')} onChange={(e) => setName(e.target.value)} /></Field>}
          {ws && ws.hidden > 0 && <Notice icon={EyeOff} title={plural(ws.hidden, 'scope.hiddenOne', 'scope.hiddenMany', { n: ws.hidden })} />}
          {error && <p role="alert" className="flex gap-2 rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-sm text-bad"><AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />{error}</p>}
          <DialogFooter>
            {ws && <Button variant="ghost" className="mr-auto text-bad hover:text-bad" onClick={() => setDeleting(true)}>{t('scope.delete')}</Button>}
            <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
            <Button disabled={busy || chosen.length === 0 || (save && !name.trim())} onClick={submit}>{busy && <Spinner />}{busy ? t('common.saving') : save ? t('common.save') : t('scope.apply')}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      {deleting && ws && (
        <ConfirmDialog open destructive title={t('scope.deleteTitle', { name: ws.name })} body={t('scope.deleteBody')} confirmLabel={t('scope.delete')}
          onOpenChange={(o) => { if (!o) setDeleting(false) }}
          onConfirm={async () => { await del(`workspaces/${ws.id}`); await qc.invalidateQueries({ queryKey: ['workspaces'] }); setScope({ projects: 'mine' }); onClose() }} />
      )}
    </>
  )
}

ShellSlots.Workspace = WorkspaceSelector

// ---------- View tabs (FR-VIS-01) ----------

interface View { id: string; path: string; icon: LucideIcon; label: string; show?: (me: Me) => boolean }
export const VIEWS: View[] = [
  { id: 'home', path: '/home', icon: LayoutDashboard, label: 'wsview.dashboard' },
  { id: 'boards', path: '/boards', icon: KanbanSquare, label: 'wsview.board' },
  { id: 'tasks', path: '/tasks', icon: List, label: 'wsview.list' },
  { id: 'timeline', path: '/timeline', icon: ChartGantt, label: 'wsview.timeline' },
  { id: 'calendar', path: '/calendar', icon: CalendarDays, label: 'wsview.calendar' },
  { id: 'files', path: '/files', icon: FileStack, label: 'wsview.files' },
  { id: 'team', path: '/team', icon: Users, label: 'wsview.team' },
  { id: 'workload', path: '/workload', icon: FolderKanban, label: 'wsview.workload', show: (m) => m.capabilities.workload },
]

/** The workspace's view tabs with the plus-tab view switcher and the share control; every tab keeps the scope. */
export function WorkspaceTabs() {
  const scope = useScope()
  const me = useMe()
  const { pathname } = useLocation()
  const forced = useContext(Forced)
  if (forced) return null // the project's own tabs lead there
  const views = VIEWS.filter((v) => !v.show || v.show(me))
  const search = new URLSearchParams(scope.params).toString()
  return (
    <div className="no-print flex flex-wrap items-center gap-x-1 border-b">
      <nav aria-label={t('wsview.label')} className="flex flex-wrap items-center">
        {views.map((v) => {
          const on = pathname === v.path
          return (
            <Link key={v.id} to={{ pathname: v.path, search }} aria-current={on ? 'page' : undefined}
              className={cn('inline-flex min-h-11 items-center gap-1.5 px-3 text-sm outline-offset-[-2px]', on ? 'font-semibold text-foreground shadow-[inset_0_-2px_0_var(--primary)]' : 'text-muted-foreground shadow-none hover:bg-muted hover:text-foreground')}>
              <v.icon className="size-4" aria-hidden />{t(v.label)}
            </Link>
          )
        })}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="ml-0.5 size-8 p-0" aria-label={t('wsview.switcher')} title={t('wsview.switcher')}><Plus className="size-4" /></Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start" className="w-56">
            <DropdownMenuLabel>{t('wsview.switcher')}</DropdownMenuLabel>
            {views.map((v) => <DropdownMenuItem key={v.id} asChild><Link to={{ pathname: v.path, search }}><v.icon className="size-4" />{t(v.label)}</Link></DropdownMenuItem>)}
            <DropdownMenuSeparator />
            <DropdownMenuItem asChild><Link to={{ pathname: '/my-work', search }}>{t('nav.myWork')}</Link></DropdownMenuItem>
            <DropdownMenuItem asChild><Link to="/projects">{t('nav.projects')}</Link></DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </nav>
      <div className="flex-1" />
      <span className="hidden items-center gap-1.5 text-sm text-muted-foreground md:inline-flex"><Layers className="size-4" aria-hidden />{scope.label}</span>
      <Button variant="ghost" onClick={() => share(scope)}><Link2 className="size-4" />{t('scope.share')}</Button>
    </div>
  )
}
