import { useInfiniteQuery, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCheck, Settings } from 'lucide-react'
import { useMemo } from 'react'
import { Link, useSearchParams } from 'react-router'
import { ChangeList, type ActivityRow } from '@/components/hub/activity'
import { Empty, ErrorBanner, Loading, Page } from '@/components/hub/common'
import { TabBar } from '@/components/hub/fields'
import { NotificationItem, useMarkRead, useOpenNotification, useUnread, type NotificationRow } from '@/components/hub/notifications'
import { Key } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { get, post, qs } from '@/lib/api'
import { fmtDate, fmtTime, localIso, today, addDays } from '@/lib/format'
import { t } from '@/lib/i18n'
import type { Page as PageOf } from '@/lib/types'
import { cn } from '@/lib/utils'

const EVENTS = ['TaskAssigned', 'ReviewRequested', 'ReviewOutcome', 'Mention', 'CommentOnItem', 'TaskBlocked', 'TaskUnblocked', 'DueDateChanged',
  'MilestoneStatus', 'MilestoneDateChanged', 'DecisionAssigned', 'DecisionOverdue', 'AddedToProject', 'AttentionCritical']

/** "Today", "Yesterday" or the date, for grouping by day (§13.17). */
function dayLabel(ts: string) {
  const d = localIso(new Date(ts))
  return d === today() ? t('rel.todayCap') : d === addDays(today(), -1) ? t('rel.yesterdayCap') : fmtDate(d)
}

function byDay<T>(rows: T[], ts: (r: T) => string) {
  const m = new Map<string, T[]>()
  for (const r of rows) { const k = dayLabel(ts(r)); m.set(k, [...(m.get(k) ?? []), r]) }
  return [...m.entries()]
}

/** Notification Centre (§13.17): personal notifications and the Following feed. */
export function NotificationsPage() {
  const [sp, setSp] = useSearchParams()
  const tab = sp.get('tab') === 'following' ? 'following' : 'notifications'
  const unread = useUnread()
  return (
    <Page title={t('nav.notifications')} actions={<Button asChild variant="outline" size="sm"><Link to="/preferences"><Settings className="size-4" />{t('top.preferences')}</Link></Button>}>
      <div className="rounded-lg border bg-card">
        <TabBar tabs={[{ id: 'notifications', label: t('notif.personal'), count: unread.data?.notifications }, { id: 'following', label: t('notif.following'), count: unread.data?.following }]}
          value={tab} onChange={(v) => setSp(v === 'following' ? { tab: v } : {}, { replace: true })} />
        {tab === 'notifications' ? <Personal /> : <Following />}
      </div>
    </Page>
  )
}

function Personal() {
  const [sp, setSp] = useSearchParams()
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const filters = { unread: sp.get('unread') === 'true', type: sp.get('type') ?? undefined, projectId: sp.get('projectId') ?? undefined }
  const q = useInfiniteQuery({
    queryKey: ['notifications', 'list', filters],
    queryFn: ({ pageParam }) => get<PageOf<NotificationRow>>(`me/notifications${qs({ ...filters, page: pageParam, pageSize: 50 })}`),
    initialPageParam: 1, getNextPageParam: (last) => (last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined),
  })
  const rows = useMemo(() => q.data?.pages.flatMap((p) => p.items) ?? [], [q.data])
  const projects = useMemo(() => [...new Map(rows.filter((r) => r.projectId).map((r) => [r.projectId!, r.projectNumber!])).entries()], [rows])
  const markRead = useMarkRead()
  const open = useOpenNotification()
  return (
    <div>
      <div className="flex flex-wrap items-center gap-2 border-b px-4 py-2">
        <label className="flex items-center gap-1.5 text-sm"><input type="checkbox" checked={filters.unread} onChange={(e) => set('unread', e.target.checked ? 'true' : undefined)} />{t('notif.unreadOnly')}</label>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={filters.type ?? ''} onChange={(e) => set('type', e.target.value || undefined)} aria-label={t('common.type')}>
          <option value="">{t('notif.anyType')}</option>{EVENTS.map((e) => <option key={e} value={e}>{t(`event.${e}`)}</option>)}
        </select>
        <select className="h-8 rounded-md border bg-card px-2 text-sm" value={filters.projectId ?? ''} onChange={(e) => set('projectId', e.target.value || undefined)} aria-label={t('nav.projects')}>
          <option value="">{t('notif.anyProject')}</option>{projects.map(([id, num]) => <option key={id} value={id}>{num}</option>)}
        </select>
        <div className="flex-1" />
        <Button size="sm" variant="ghost" onClick={() => markRead({ all: true })}><CheckCheck className="size-4" />{t('notif.markAll')}</Button>
      </div>
      {q.error && <div className="p-3"><ErrorBanner error={q.error} /></div>}
      {q.isPending ? <Loading rows={5} /> : rows.length === 0 ? <Empty>{filters.unread ? t('notif.noneUnread') : t('notif.none')}</Empty> : (
        <div>
          {byDay(rows, (r) => r.updatedAt).map(([day, list]) => (
            <section key={day} aria-label={day}>
              <h2 className="bg-muted/40 px-4 py-1 text-xs font-semibold text-muted-foreground">{day}</h2>
              <div className="divide-y">{list.map((n) => <NotificationItem key={n.id} n={n} onOpen={open} />)}</div>
            </section>
          ))}
          {q.hasNextPage && <div className="p-3 text-center"><Button variant="outline" size="sm" onClick={() => q.fetchNextPage()}>{t('notif.more')}</Button></div>}
        </div>
      )}
    </div>
  )
}

interface FeedItem { entry: ActivityRow & { projectId: string }; count: number; projectNumber: string; projectName: string; notified: boolean; unread: boolean }
interface Feed { projects: { projectId: string; projectNumber: string; name: string; status: string; unread: number }[]; items: FeedItem[]; totalCount: number }

/** Following feed (ASG-05): changes by others on projects followed at All activity, grouped by project then day. */
function Following() {
  const [sp, setSp] = useSearchParams()
  const qc = useQueryClient()
  const projectId = sp.get('projectId') ?? undefined
  const important = sp.get('important') === 'true'
  const set = (k: string, v?: string) => { const n = new URLSearchParams(sp); if (v) n.set(k, v); else n.delete(k); setSp(n, { replace: true }) }
  const q = useQuery({ queryKey: ['following', projectId, important], queryFn: () => get<Feed>(`me/following${qs({ projectId, importantOnly: important, pageSize: 200 })}`) })
  const markRead = async (id: string) => { await post(`me/following/${id}/read`); qc.invalidateQueries({ queryKey: ['following'] }); qc.invalidateQueries({ queryKey: ['unread'] }) }
  if (q.isPending) return <Loading rows={5} />
  if (q.error) return <div className="p-3"><ErrorBanner error={q.error} /></div>
  const { projects, items } = q.data
  const groups = new Map<string, FeedItem[]>()
  for (const i of items) groups.set(i.projectNumber, [...(groups.get(i.projectNumber) ?? []), i])
  return (
    <div className="grid md:grid-cols-[14rem_1fr]">
      <aside className="border-b p-3 md:border-b-0 md:border-r" aria-label={t('notif.followedProjects')}>
        {projects.length === 0 ? <p className="text-sm text-muted-foreground">{t('notif.followNone')}</p> : (
          <ul className="space-y-0.5 text-sm">
            <li><button className={cn('w-full rounded px-2 py-1 text-left hover:bg-muted', !projectId && 'bg-accent font-medium')} onClick={() => set('projectId')}>{t('notif.allProjects')}</button></li>
            {projects.map((p) => (
              <li key={p.projectId} className="flex items-center gap-1">
                <button className={cn('flex min-w-0 flex-1 items-center gap-2 rounded px-2 py-1 text-left hover:bg-muted', projectId === p.projectId && 'bg-accent font-medium')} onClick={() => set('projectId', p.projectId)}>
                  <span className="truncate"><Key>{p.projectNumber}</Key> {p.name}</span>
                  {p.unread > 0 && <span className="ml-auto rounded-full bg-primary px-1.5 text-xs text-primary-foreground">{p.unread}</span>}
                </button>
                {p.unread > 0 && <Button variant="ghost" size="icon" className="size-7" aria-label={t('notif.markProjectRead', { number: p.projectNumber })} onClick={() => markRead(p.projectId)}><CheckCheck className="size-3.5" /></Button>}
              </li>
            ))}
          </ul>
        )}
        <label className="mt-3 flex items-center gap-1.5 text-sm"><input type="checkbox" checked={important} onChange={(e) => set('important', e.target.checked ? 'true' : undefined)} />{t('activity.important')}</label>
      </aside>
      <div className="min-w-0">
        {items.length === 0 ? <Empty>{t('notif.feedEmpty')}</Empty> : [...groups.entries()].map(([num, list]) => (
          <section key={num} aria-label={num} className="border-b last:border-b-0">
            <h2 className="px-4 pt-3 text-sm font-semibold"><Key>{num}</Key> {list[0].projectName}</h2>
            {byDay(list, (i) => i.entry.occurredAt).map(([day, rows]) => (
              <div key={day} className="px-4 pb-2">
                <div className="py-1 text-xs font-medium text-muted-foreground">{day}</div>
                <ol className="space-y-2">
                  {rows.map((i) => (
                    <li key={i.entry.id} className={cn('rounded-md border-l-2 pl-3 text-sm', i.unread ? 'border-primary' : 'border-transparent')}>
                      <div className="flex flex-wrap items-baseline gap-x-2">
                        <span className="font-medium">{i.entry.actorName ?? t('common.system')}</span>
                        <span className="text-muted-foreground">{t(`action.${i.entry.action}`)}</span>
                        {i.entry.itemKey && <Key>{i.entry.itemKey}</Key>}<span>{i.entry.itemName}</span>
                        {i.count > 1 && <span className="rounded bg-muted px-1.5 text-xs">{t('notif.changes', { n: i.count })}</span>}
                        {i.notified && <span className="text-xs text-muted-foreground">{t('notif.notified')}</span>}
                        <time className="ml-auto text-xs text-muted-foreground" dateTime={i.entry.occurredAt}>{fmtTime(i.entry.occurredAt)}</time>
                      </div>
                      {i.count === 1 && <div className="text-[13px]"><ChangeList row={i.entry} /></div>}
                      {i.entry.reason && <div className="text-xs text-muted-foreground">{t('common.reason')}: {i.entry.reason}</div>}
                    </li>
                  ))}
                </ol>
              </div>
            ))}
          </section>
        ))}
      </div>
    </div>
  )
}
