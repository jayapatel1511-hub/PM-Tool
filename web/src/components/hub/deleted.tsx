import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { toast } from 'sonner'
import { Empty, ErrorBanner, Field, Loading, TableRegion, tdCls, thCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { ApiError, get, post, qs } from '@/lib/api'
import { useFlag } from '@/lib/auth'
import { fmtTime } from '@/lib/format'
import { t, tv } from '@/lib/i18n'
import type { Page, ProjectDetail } from '@/lib/types'

type Kind = 'tasks' | 'deliverables' | 'milestones'
interface DeletedRow { id: string; key: string; name: string; discipline?: string; parentKey?: string; status?: string; deletedAt: string; deletedBy?: string; restoreBlocked?: string; rowVersion: number }

export function useShowDeleted(p: ProjectDetail) {
  const enabled = useFlag('show_deleted')
  const [sp] = useSearchParams()
  return enabled && p.permissions.restore?.ok === true && sp.get('deleted') === 'true'
}

/** URL-only: deliberately excluded from the saved-view keys. */
export function DeletedToggle({ project }: { project: ProjectDetail }) {
  const enabled = useFlag('show_deleted')
  const [sp, setSp] = useSearchParams()
  if (!enabled || !project.permissions.restore?.ok) return null
  return <label className="flex min-h-(--control-h) items-center gap-2 text-sm"><Checkbox checked={sp.get('deleted') === 'true'} onCheckedChange={(on) => {
    const next = new URLSearchParams(sp); next.delete('deletedPage'); if (on) next.set('deleted', 'true'); else next.delete('deleted'); setSp(next, { replace: true })
  }} />{t('rcv.showDeleted')}</label>
}

export function DeletedList({ project, kind }: { project: ProjectDetail; kind: Kind }) {
  const [sp, setSp] = useSearchParams()
  const qc = useQueryClient()
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<unknown>(null)
  const page = Math.max(1, Number(sp.get('deletedPage')) || 1)
  const term = sp.get('q') ?? ''
  const path = `projects/${project.id}/${kind}/deleted`
  const q = useQuery({ queryKey: ['p', project.id, kind, 'deleted', page, term], queryFn: () => get<Page<DeletedRow>>(`${path}${qs({ page, q: term, pageSize: 200 })}`) })
  const change = (key: string, value: string) => { const next = new URLSearchParams(sp); next.set(key, value); if (key === 'q') next.delete('deletedPage'); setSp(next, { replace: true }) }
  const restore = async (row: DeletedRow) => {
    if (busy) return
    setBusy(row.id); setError(null)
    try { await post(`${kind}/${row.id}/restore`, {}, row.rowVersion); toast.success(t('rcv.restored')); await qc.invalidateQueries({ queryKey: ['p', project.id] }) }
    catch (e) { if (e instanceof ApiError && e.status === 404) await q.refetch(); else setError(e) }
    finally { setBusy(null) }
  }
  const action = (r: DeletedRow, form: string) => <div><Button variant="outline" size="sm" disabled={!!busy || !!r.restoreBlocked} onClick={() => restore(r)} aria-describedby={r.restoreBlocked ? `restore-${form}-${r.id}` : undefined}>{t('rcv.restore')}</Button>{r.restoreBlocked && <p id={`restore-${form}-${r.id}`} className="mt-1 text-xs text-muted-foreground">{r.restoreBlocked}</p>}</div>
  return <div className="space-y-4">
    <div className="flex flex-wrap items-end justify-between gap-3"><Field label={t('common.search')} htmlFor="deleted-search"><Input id="deleted-search" type="search" value={term} onChange={(e) => change('q', e.target.value)} /></Field><ExportMenu path={`${path}/export`} params={{ q: term }} name={`${project.projectNumber}-deleted-${kind}`} /></div>
    {error != null && <ErrorBanner error={error} />}{q.error && <ErrorBanner error={q.error} retry={() => q.refetch()} />}
    {q.isPending ? <Loading rows={4} /> : q.data && (q.data.items.length === 0 ? <Empty>{t('rcv.none')}</Empty> : <>
      <TableRegion className="hidden sm:block"><table className="w-full text-sm"><caption className="sr-only">{t('rcv.restoreTitle')}</caption><thead><tr>{['milestone.key', 'common.name', 'common.discipline', 'rcv.parent', 'common.status', 'rcv.deletedAt', 'rcv.deletedBy', 'common.actions'].map((key) => <th key={key} scope="col" className={thCls}>{t(key)}</th>)}</tr></thead><tbody>{q.data.items.map((r) => <tr key={r.id} className="border-t"><td className={tdCls}>{r.key}</td><td className={tdCls}>{r.name}</td><td className={tdCls}>{r.discipline}</td><td className={tdCls}>{r.parentKey}</td><td className={tdCls}>{r.status && tv(r.status)}</td><td className={tdCls}>{fmtTime(r.deletedAt)}</td><td className={tdCls}>{r.deletedBy ?? t('common.system')}</td><td className={tdCls}>{action(r, 'table')}</td></tr>)}</tbody></table></TableRegion>
      <ul className="space-y-3 sm:hidden">{q.data.items.map((r) => <li key={r.id} className="space-y-2 rounded-lg border bg-card p-4"><p className="font-semibold">{r.key} · {r.name}</p><p>{[r.discipline, r.parentKey, r.status && tv(r.status)].filter(Boolean).join(' · ')}</p><p className="text-sm">{t('rcv.deletedAt')}: {fmtTime(r.deletedAt)} · {t('rcv.deletedBy')}: {r.deletedBy ?? t('common.system')}</p>{action(r, 'card')}</li>)}</ul>
    </>)}
    {q.data && q.data.totalCount > 200 && <nav aria-label={t('rcv.restoreTitle')} className="flex items-center gap-3"><Button variant="outline" disabled={page <= 1} onClick={() => change('deletedPage', String(page - 1))}>{t('common.previous')}</Button><span>{page} / {Math.ceil(q.data.totalCount / 200)}</span><Button variant="outline" disabled={page * 200 >= q.data.totalCount} onClick={() => change('deletedPage', String(page + 1))}>{t('common.next')}</Button></nav>}
  </div>
}
