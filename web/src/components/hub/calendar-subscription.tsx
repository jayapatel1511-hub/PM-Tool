import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { ErrorBanner, Field, Spinner } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { ApiError, api, get, qs } from '@/lib/api'
import { t } from '@/lib/i18n'

export function CalendarSubscriptionDialog({ onClose }: { onClose: () => void }) {
  const q = useQuery({ queryKey: ['calendar-subscription'], queryFn: () => get<{ enabled: boolean; rowVersion: number }>('me/calendar-subscription') })
  const [url, setUrl] = useState(''); const [busy, setBusy] = useState(false); const [error, setError] = useState<unknown>(null); const [stale, setStale] = useState(false)
  const rotate = async () => { if (!q.data) return; setBusy(true); setError(null); try { const result = await api<{ url: string }>('me/calendar-subscription', { body: { rowVersion: q.data.rowVersion } }); setUrl(result.url); await q.refetch() } catch (e) { setError(e); if (e instanceof ApiError && e.status === 409) setStale(true) } finally { setBusy(false) } }
  const revoke = async () => { if (!q.data) return; setBusy(true); setError(null); try { await api(`me/calendar-subscription${qs({ rowVersion: q.data.rowVersion })}`, { method: 'DELETE' }); setUrl(''); await q.refetch() } catch (e) { setError(e); if (e instanceof ApiError && e.status === 409) setStale(true) } finally { setBusy(false) } }
  const reload = async () => { const result = await q.refetch(); if (!result.error) { setStale(false); setError(null); setUrl('') } }
  return <Dialog open onOpenChange={(open) => !open && !busy && onClose()}><DialogContent><DialogHeader><DialogTitle>{t('calendar.subscription')}</DialogTitle><DialogDescription>{t('calendar.subscriptionHint')}</DialogDescription></DialogHeader>{(error ?? q.error) != null && <ErrorBanner error={error ?? q.error} retry={reload} />}
    <p className="text-sm">{t('calendar.subscriptionPrivacy')}</p><p className="text-sm text-muted-foreground">{t('calendar.subscriptionCadence')}</p><p className="text-sm">{t(q.data?.enabled ? 'calendar.subscriptionEnabled' : 'calendar.subscriptionDisabled')}</p>
    {url && <Field label={t('calendar.subscriptionUrl')} htmlFor="calendar-feed-url"><Input id="calendar-feed-url" readOnly value={url} onFocus={(e) => e.target.select()} autoComplete="off" /><p className="mt-2 text-xs text-muted-foreground">{t('calendar.subscriptionOnce')}</p></Field>}
    <DialogFooter><Button variant="outline" disabled={busy} onClick={onClose}>{t('common.close')}</Button>{q.data?.enabled && <Button variant="outline" disabled={busy || stale || q.isFetching || q.isError} onClick={revoke}>{t('calendar.subscriptionRevoke')}</Button>}<Button disabled={busy || stale || q.isFetching || !q.data || q.isError} onClick={rotate}>{busy && <Spinner />}{t(q.data?.enabled ? 'calendar.subscriptionRotate' : 'calendar.subscriptionCreate')}</Button></DialogFooter>
  </DialogContent></Dialog>
}
