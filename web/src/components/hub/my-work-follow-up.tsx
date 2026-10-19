import { Link } from 'react-router'
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Empty, ErrorBanner, Loading, Page, TableRegion, tdCls, thCls } from '@/components/hub/common'
import { useItemPanel } from '@/components/hub/panel-host'
import { Button } from '@/components/ui/button'
import { typedRequest } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { fmtDate } from '@/lib/format'
import { t, tv  } from '@/lib/i18n'

export function MyWorkFollowUp({ view, menu }: { view: 'watching' | 'requested'; menu: React.ReactNode }) {
  const me = useMe()
  const open = useItemPanel()
  const [error, setError] = useState<unknown>(null)
  const q = useQuery({ queryKey: ['my-work-follow-up', me.id, view], queryFn: () => typedRequest('get', `/api/v1/me/work/${view}`, {}) })
  return <Page title={t(`mywork.tab.${view}`)} actions={menu}>{error != null && <ErrorBanner error={error} />}{q.isPending ? <Loading /> : q.error ? <ErrorBanner error={q.error} retry={() => q.refetch()} /> : <>
    <p className="mb-3 text-sm text-muted-foreground">{t('mywork.followUpCount', { count: q.data.total })}</p>
    {q.data.items.length === 0 ? <Empty>{t('mywork.noFollowUp')}</Empty> : <TableRegion role="region" aria-label={t(`mywork.tab.${view}`)}><table className="w-full text-sm"><thead><tr>{(['type', 'key', 'title', 'project', 'status', 'due', 'owner'] as const).map(key => <th key={key} scope="col" className={thCls}>{t(`col.${key}`)}</th>)}{view === 'watching' && <th scope="col" className={thCls}>{t('common.actions')}</th>}</tr></thead><tbody>{q.data.items.map(row => <tr key={`${row.itemType}:${row.id}`}><td className={tdCls}>{tv(row.itemType)}</td><td className={tdCls}>{['Handoff', 'ChangeNotice', 'OpenRole'].includes(row.itemType) ? <Link className="text-primary underline" to={`/projects/${encodeURIComponent(row.projectNumber)}/${row.itemType === 'Handoff' ? 'handoffs' : row.itemType === 'ChangeNotice' ? 'changes' : 'allocations'}?${row.itemType === 'OpenRole' ? 'view=roles' : `panel=${row.itemType}:${row.id}`}`}>{row.key || row.title}</Link> : <button className="text-primary underline" onClick={() => open(row.itemType, row.id)}>{row.key || row.title}</button>}</td><td className={tdCls}>{row.title}</td><td className={tdCls}>{row.projectNumber}</td><td className={tdCls}>{tv(row.status)}</td><td className={tdCls}>{fmtDate(row.dueDate)}</td><td className={tdCls}>{row.ownerName ?? '—'}</td>{view === 'watching' && <td className={tdCls}><Button size="sm" variant="ghost" onClick={async () => { try { await typedRequest('delete', '/api/v1/items/{type}/{id}/watchers/{userId}', { params: { type: row.itemType, id: row.id, userId: me.id } }); q.refetch() } catch (e) { setError(e) } }}>{t('mywork.stopWatching')}</Button></td>}</tr>)}</tbody></table></TableRegion>}
  </>}</Page>
}
