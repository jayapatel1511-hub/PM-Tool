import { useQuery, useQueryClient } from '@tanstack/react-query'
import { RotateCcw } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { AccentDot, Empty, ErrorBanner, Field, Loading, Page, Section, tdCls, thCls } from '@/components/hub/common'
import { Key, StatusPill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { SaveStatus } from '@/components/hub/fields'
import { del, get, put } from '@/lib/api'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import { FollowLevelSelect } from './projects/Follow'

interface Prefs {
  events: { code: string; app: boolean; email: boolean; defaultApp: boolean; defaultEmail: boolean; direct: boolean; custom: boolean }[]
  digest: { digestEnabled: boolean; time: string; orgTime: string; weekendDigests: boolean; sections: string[]; sectionsOff: string[]; weeklySummaryEnabled: boolean; managesProjects: boolean }
  follows: { projectId: string; projectNumber: string; name: string; status: string; level: string; source: string }[]
}

/** Preferences (§17.4, §13.17): channel per event, the daily digest, and follow level per project. A form page: the
 *  fields sit in a 760 px region (design §5). */
export function PreferencesPage() {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['preferences'], queryFn: () => get<Prefs>('me/preferences') })
  const [err, setErr] = useState<unknown>(null)
  const [preview, setPreview] = useState<{ subject: string; body: string } | null>(null)
  const [digestDraft, setDigestDraft] = useState<Partial<Prefs['digest']>>({})
  const [eventDraft, setEventDraft] = useState<Record<string, { app?: boolean; email?: boolean }>>({})
  const [eventFeedbackVersion, setEventFeedbackVersion] = useState<Record<string, number>>({})
  const [pending, setPending] = useState<Record<string, boolean>>({})
  const reload = () => qc.invalidateQueries({ queryKey: ['preferences'] })
  const run = async (key: string, f: () => Promise<unknown>, onSuccess?: () => void) => {
    if (pending[key]) return
    setErr(null)
    setPending((s) => ({ ...s, [key]: true }))
    try { await f(); await reload(); onSuccess?.(); return true } catch (e) { setErr(e); throw e } finally { setPending((s) => ({ ...s, [key]: false })) }
  }
  const header = { title: t('top.preferences'), subtitle: t('prefs.subtitle') }
  if (q.isPending) return <Page {...header}><div className="w-full max-w-[760px]"><Loading rows={8} /></div></Page>
  if (q.error) return <Page {...header}><div className="w-full max-w-[760px]"><ErrorBanner error={q.error} retry={() => q.refetch()} /></div></Page>
  const p = q.data
  const digest = { ...p.digest, ...digestDraft }
  const events = p.events.map((e) => ({ ...e, ...eventDraft[e.code], serverApp: e.app, serverEmail: e.email }))
  return (
    <Page {...header}>
      <div className="flex w-full max-w-[760px] flex-col gap-6">
        {err != null && <ErrorBanner error={err} />}
        <Section title={t('prefs.digest')} id="digest">
          <div className="divide-y">
            <div className="px-5 py-4">
              <label className="flex min-h-(--control-row-h) items-center gap-3 text-sm font-medium">
                <SaveStatus current={p.digest.digestEnabled}>{(track) => <Switch checked={digest.digestEnabled} disabled={!!pending.digest}
                  onCheckedChange={(on) => { setDigestDraft((d) => ({ ...d, digestEnabled: on })); void track(run('digest', () => put('me/preferences/digest', { enabled: on, time: digest.time }), () => setDigestDraft((d) => { const next = { ...d }; delete next.digestEnabled; return next })), on) }} />}</SaveStatus>{t('prefs.digestOn')}
              </label>
            </div>
            <div className="px-5 py-4">
              <Field label={t('prefs.digestTimeLabel')} htmlFor="digest-time" hint={digest.weekendDigests ? t('prefs.weekendOn') : t('prefs.weekendOff')}>
                <SaveStatus current={p.digest.time}>
                  {(track) => (
                    <Input id="digest-time" type="time" className="w-40" value={digest.time} disabled={!digest.digestEnabled || !!pending.digest}
                      onChange={(e) => setDigestDraft((d) => ({ ...d, time: e.target.value }))}
                      onBlur={(e) => { if (e.target.value && e.target.value !== p.digest.time) void track(run('digest', () => put('me/preferences/digest', { enabled: digest.digestEnabled, time: e.target.value }), () => setDigestDraft((d) => { const next = { ...d }; delete next.time; return next })), e.target.value) }} />
                  )}
                </SaveStatus>
              </Field>
            </div>
            <div className="px-5 py-4">
              <fieldset disabled={!digest.digestEnabled}>
                <legend className="text-sm font-medium">{t('prefs.sections')} <span className="font-normal text-muted-foreground">{t('prefs.sectionsHint')}</span></legend>
                <div className="mt-3 grid gap-x-4 gap-y-1 sm:grid-cols-2 md:grid-cols-3">
                  {digest.sections.map((code) => {
                    const on = !digest.sectionsOff.includes(code)
                    return (
                      <label key={code} className="flex min-h-(--control-row-h) items-center gap-2 text-sm">
                        <SaveStatus current={!p.digest.sectionsOff.includes(code)}>{(track) => <Checkbox checked={on} disabled={!!pending.digest} onCheckedChange={(c) => {
                          const sectionsOff = c ? digest.sectionsOff.filter((x) => x !== code) : [...digest.sectionsOff, code]
                          setDigestDraft((d) => ({ ...d, sectionsOff })); void track(run('digest', () => put('me/preferences/digest', { enabled: digest.digestEnabled, time: digest.time, sectionsOff }), () => setDigestDraft((d) => { const next = { ...d }; delete next.sectionsOff; return next })), !!c)
                        }} />}</SaveStatus>
                        {t(`prefs.section.${code}`)}
                      </label>
                    )
                  })}
                </div>
              </fieldset>
            </div>
            {digest.managesProjects && (
              <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-3 px-5 py-4">
                <div className="min-w-0 flex-1 space-y-1">
                  <label className="flex min-h-(--control-row-h) items-center gap-3 text-sm font-medium"><SaveStatus current={p.digest.weeklySummaryEnabled}>{(track) => <Switch checked={digest.weeklySummaryEnabled} disabled={!!pending.digest}
                    onCheckedChange={(on) => { setDigestDraft((d) => ({ ...d, weeklySummaryEnabled: on })); void track(run('digest', () => put('me/preferences/digest', { enabled: digest.digestEnabled, time: digest.time, weeklySummary: on }), () => setDigestDraft((d) => { const next = { ...d }; delete next.weeklySummaryEnabled; return next })), on) }} />}</SaveStatus>{t('prefs.weekly')}</label>
                  <p className="text-xs/[18px] text-muted-foreground">{t('prefs.weeklyHint')}</p>
                </div>
                <Button variant="outline" size="sm" disabled={!!pending.preview} onClick={() => void run('preview', async () => setPreview(await get('me/weekly-summary'))).catch(() => undefined)}>{t('prefs.weeklyPreview')}</Button>
              </div>
            )}
          </div>
        </Section>
        {preview && (
          <Dialog open onOpenChange={(o) => !o && setPreview(null)}>
            <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-2xl">
              <DialogHeader><DialogTitle>{preview.subject}</DialogTitle><DialogDescription>{t('prefs.weeklyPreviewHint')}</DialogDescription></DialogHeader>
              <pre className="whitespace-pre-wrap rounded-md bg-muted p-4 font-sans text-sm">{preview.body}</pre>
            </DialogContent>
          </Dialog>
        )}

        <Section title={t('prefs.events')} id="events">
          <div className="scroll-region overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-muted">
                <tr>
                  <th scope="col" className={thCls}>{t('prefs.event')}</th>
                  <th scope="col" className={cn(thCls, 'w-24 text-center')}>{t('prefs.inApp')}</th>
                  <th scope="col" className={cn(thCls, 'w-24 text-center')}>{t('prefs.email')}</th>
                  <th scope="col" className={cn(thCls, 'w-28')}><span className="sr-only">{t('common.actions')}</span></th>
                </tr>
              </thead>
              <tbody>
                {events.map((e) => {
                  const name = t(`event.${e.code}`)
                  return (
                    <tr key={e.code} className="border-t hover:bg-muted">
                      <td className={cn(tdCls, 'align-middle')}>{name}{e.direct && <span className="block text-xs/[18px] text-muted-foreground">{t('prefs.direct')}</span>}</td>
                      <td className={cn(tdCls, 'align-middle')}><div className="flex justify-center"><SaveStatus key={`app-${e.code}-${eventFeedbackVersion[e.code] ?? 0}`} current={e.serverApp}>{(track) => <Checkbox checked={e.app} disabled={!!pending[`event-${e.code}`]} aria-label={`${name} ${t('prefs.inApp')}`} onCheckedChange={(c) => { const app = !!c; setEventDraft((d) => ({ ...d, [e.code]: { ...d[e.code], app } })); void track(run(`event-${e.code}`, () => put(`me/preferences/events/${e.code}`, { app, email: e.email }), () => setEventDraft((d) => { const next = { ...d }; if (!next[e.code]?.email) delete next[e.code]; else { const entry = { ...next[e.code] }; delete entry.app; next[e.code] = entry } return next })), app) }} />}</SaveStatus></div></td>
                      <td className={cn(tdCls, 'align-middle')}><div className="flex justify-center"><SaveStatus key={`email-${e.code}-${eventFeedbackVersion[e.code] ?? 0}`} current={e.serverEmail}>{(track) => <Checkbox checked={e.email} disabled={!!pending[`event-${e.code}`]} aria-label={`${name} ${t('prefs.email')}`} onCheckedChange={(c) => { const email = !!c; setEventDraft((d) => ({ ...d, [e.code]: { ...d[e.code], email } })); void track(run(`event-${e.code}`, () => put(`me/preferences/events/${e.code}`, { app: e.app, email }), () => setEventDraft((d) => { const next = { ...d }; if (!next[e.code]?.app) delete next[e.code]; else { const entry = { ...next[e.code] }; delete entry.email; next[e.code] = entry } return next })), email) }} />}</SaveStatus></div></td>
                      <td className={cn(tdCls, 'align-middle text-right')}>{e.custom && <Button variant="ghost" size="sm" disabled={!!pending[`event-${e.code}`]} aria-label={t('prefs.resetEvent', { event: name })} onClick={() => void run(`event-${e.code}`, () => del(`me/preferences/events/${e.code}`), () => { setEventDraft((d) => { const next = { ...d }; delete next[e.code]; return next }); setEventFeedbackVersion((v) => ({ ...v, [e.code]: (v[e.code] ?? 0) + 1 })) }).catch(() => undefined)}><RotateCcw className="size-3.5" />{t('prefs.reset')}</Button>}</td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </Section>

        <Section title={t('prefs.follows')} count={p.follows.length} id="follows">
          {p.follows.length === 0 ? <Empty action={<Button asChild variant="outline"><Link to="/projects">{t('nav.projects')}</Link></Button>}>{t('notif.followNone')}</Empty> : (
            <div className="scroll-region overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-muted">
                  <tr>
                    <th scope="col" className={thCls}>{t('common.project')}</th>
                    <th scope="col" className={thCls}>{t('common.status')}</th>
                    <th scope="col" className={thCls}>{t('common.source')}</th>
                    <th scope="col" className={cn(thCls, 'w-56')}>{t('follow.level')}</th>
                  </tr>
                </thead>
                <tbody>
                  {p.follows.map((f) => (
                    <tr key={f.projectId} className="border-t hover:bg-muted">
                      <td className={cn(tdCls, 'align-middle')}><span className="flex items-center gap-2"><AccentDot id={f.projectId} /><span className="min-w-0 break-words"><Key>{f.projectNumber}</Key> {f.name}</span></span></td>
                      <td className={cn(tdCls, 'align-middle')}><StatusPill status={f.status} /></td>
                      <td className={cn(tdCls, 'align-middle text-xs/[18px] text-muted-foreground')}>{t(`follow.source.${f.source}`)}</td>
                      <td className={cn(tdCls, 'align-middle')}><FollowLevelSelect projectId={f.projectId} level={f.level} onChanged={reload} projectName={`${f.projectNumber} ${f.name}`} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Section>
      </div>
    </Page>
  )
}
