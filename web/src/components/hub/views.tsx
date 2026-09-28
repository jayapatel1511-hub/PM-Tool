import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Bookmark, Check, Star } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { ErrorBanner, Field, Spinner } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { ApiError, del, get, patch, post } from '@/lib/api'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'

interface SavedView { id: string; name: string; scope: 'Personal' | 'Project'; projectId?: string | null; isDefault: boolean; rowVersion: number; params: Record<string, string>; dropped: string[]; owner?: string; canEdit: boolean }
interface ViewList { views: SavedView[]; canShare: boolean }

/** The URL parameters that make up the current view (everything but the open panel and the view marker). */
function current(sp: URLSearchParams, extra?: Record<string, string | undefined>) {
  const params: Record<string, string> = {}
  for (const [k, v] of sp.entries()) if (k !== 'panel' && k !== 'view' && v) params[k] = v
  for (const [k, v] of Object.entries(extra ?? {})) if (v) params[k] = v
  return params
}

/** Saved views (§18.4, FR-VIEW-04): switch, save, share with the project (PM and leads), set a default, delete. The default
 *  applies when the list opens without parameters, so a shared link always wins. */
export function ViewMenu({ listType, projectId, extra, fixed }: { listType: string; projectId?: string; extra?: () => Record<string, string | undefined>; fixed?: Record<string, string> }) {
  const [sp, setSp] = useSearchParams()
  const qc = useQueryClient()
  const key = ['views', listType, projectId ?? null]
  const q = useQuery({ queryKey: key, queryFn: () => get<ViewList>(`views?listType=${listType}${projectId ? `&projectId=${projectId}` : ''}`) })
  const [saving, setSaving] = useState(false)
  const applied = useRef(false)
  const active = q.data?.views.find((v) => v.id === sp.get('view'))
  const apply = (v: SavedView, replace = false) => {
    const n = new URLSearchParams({ ...v.params, ...fixed, view: v.id })
    const panel = sp.get('panel')
    if (panel) n.set('panel', panel)
    setSp(n, { replace })
    if (v.dropped.length) toast.warning(t('views.dropped', { name: v.name, list: v.dropped.map((d) => t(`views.param.${d}`) === `views.param.${d}` ? d : t(`views.param.${d}`)).join(', ') }))
  }
  useEffect(() => {
    if (applied.current || !q.data) return
    applied.current = true
    const empty = [...sp.keys()].every((k) => k === 'panel' || (fixed?.[k] !== undefined && sp.get(k) === fixed[k]))
    const def = q.data.views.find((v) => v.isDefault)
    if (empty && def) apply(def, true)
  }, [q.data]) // eslint-disable-line react-hooks/exhaustive-deps
  const refresh = () => qc.invalidateQueries({ queryKey: key })
  const run = async (work: () => Promise<unknown>, done: string) => { try { await work(); toast.success(done); refresh() } catch (e) { toast.error((e as ApiError).message) } }
  const personal = q.data?.views.filter((v) => v.scope === 'Personal') ?? []
  const shared = q.data?.views.filter((v) => v.scope === 'Project') ?? []
  const item = (v: SavedView) => (
    <DropdownMenuItem key={v.id} onSelect={() => apply(v)} className="gap-2">
      <Check className={cn('size-3.5', active?.id === v.id ? 'opacity-100' : 'opacity-0')} />
      <span className="flex-1 truncate">{v.name}</span>
      {v.isDefault && <Star className="size-3.5 fill-current text-warn" aria-label={t('views.default')} />}
    </DropdownMenuItem>
  )
  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="outline" size="sm" className="max-w-56"><Bookmark className="size-4" /><span className="truncate">{active ? active.name : t('views.label')}</span></Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-64">
          {q.error && <div className="p-2"><ErrorBanner error={q.error} /></div>}
          {personal.length > 0 && <><DropdownMenuLabel>{t('views.mine')}</DropdownMenuLabel>{personal.map(item)}</>}
          {shared.length > 0 && <><DropdownMenuLabel>{t('views.project')}</DropdownMenuLabel>{shared.map(item)}</>}
          {personal.length + shared.length === 0 && <DropdownMenuLabel className="font-normal text-muted-foreground">{t('views.none')}</DropdownMenuLabel>}
          <DropdownMenuSeparator />
          <DropdownMenuItem onSelect={() => setSaving(true)}>{t('views.save')}</DropdownMenuItem>
          {active?.canEdit && <>
            <DropdownMenuItem onSelect={() => run(() => patch(`views/${active.id}`, { params: current(sp, extra?.()) }, active.rowVersion), t('views.updated', { name: active.name }))}>{t('views.update', { name: active.name })}</DropdownMenuItem>
            {!active.isDefault && active.scope === 'Personal' && <DropdownMenuItem onSelect={() => run(() => patch(`views/${active.id}`, { isDefault: true }, active.rowVersion), t('views.madeDefault', { name: active.name }))}>{t('views.makeDefault')}</DropdownMenuItem>}
            <DropdownMenuItem className="text-bad" onSelect={() => run(async () => { await del(`views/${active.id}`); const n = new URLSearchParams(sp); n.delete('view'); setSp(n, { replace: true }) }, t('views.deleted', { name: active.name }))}>{t('views.delete')}</DropdownMenuItem>
          </>}
          {sp.get('view') && <DropdownMenuItem onSelect={() => { const n = new URLSearchParams(fixed); const panel = sp.get('panel'); if (panel) n.set('panel', panel); setSp(n) }}>{t('views.clear')}</DropdownMenuItem>}
        </DropdownMenuContent>
      </DropdownMenu>
      {saving && <SaveDialog listType={listType} projectId={projectId} canShare={!!q.data?.canShare} params={current(sp, extra?.())}
        onClose={(id) => { setSaving(false); if (id) { refresh(); const n = new URLSearchParams(sp); n.set('view', id); setSp(n, { replace: true }) } }} />}
    </>
  )
}

function SaveDialog({ listType, projectId, canShare, params, onClose }: { listType: string; projectId?: string; canShare: boolean; params: Record<string, string>; onClose: (id?: string) => void }) {
  const [name, setName] = useState('')
  const [share, setShare] = useState(false)
  const [isDefault, setDefault] = useState(false)
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    setBusy(true); setErr(null)
    try {
      const r = await post<{ id: string }>('views', { name, listType, projectId, scope: share ? 'Project' : 'Personal', params, isDefault: isDefault && !share })
      toast.success(t('views.saved', { name }))
      onClose(r.id)
    } catch (e) { setErr(e as ApiError) } finally { setBusy(false) }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader><DialogTitle>{t('views.saveTitle')}</DialogTitle><DialogDescription>{t('views.saveHint')}</DialogDescription></DialogHeader>
        <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); submit() }}>
          <Field label={t('common.name')} htmlFor="view-name" error={err?.fieldErrors.name}><Input id="view-name" autoFocus required maxLength={100} value={name} onChange={(e) => setName(e.target.value)} /></Field>
          {canShare && <label className="flex items-center gap-2 text-sm"><Checkbox checked={share} onCheckedChange={(c) => setShare(!!c)} />{t('views.share')}</label>}
          {!share && <label className="flex items-center gap-2 text-sm"><Checkbox checked={isDefault} onCheckedChange={(c) => setDefault(!!c)} />{t('views.asDefault')}</label>}
          <p className="text-xs text-muted-foreground">{Object.keys(params).length ? t('views.includes', { n: Object.keys(params).length }) : t('views.plain')}</p>
          {err && !Object.keys(err.fieldErrors).length && <ErrorBanner error={err} />}
          <DialogFooter><Button type="button" variant="outline" onClick={() => onClose()}>{t('common.cancel')}</Button><Button type="submit" disabled={!name.trim() || busy}>{busy && <Spinner />}{t('common.save')}</Button></DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
