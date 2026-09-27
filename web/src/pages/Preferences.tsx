import { useQuery, useQueryClient } from '@tanstack/react-query'
import { RotateCcw } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { ErrorBanner, Loading, Page, Section } from '@/components/hub/common'
import { Key } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { del, get, put } from '@/lib/api'
import { t, tv } from '@/lib/i18n'
import { FollowLevelSelect } from './projects/Follow'

interface Prefs {
  events: { code: string; app: boolean; email: boolean; defaultApp: boolean; defaultEmail: boolean; direct: boolean; custom: boolean }[]
  digest: { digestEnabled: boolean; time: string; orgTime: string; weekendDigests: boolean; sections: string[]; sectionsOff: string[]; weeklySummaryEnabled: boolean; managesProjects: boolean }
  follows: { projectId: string; projectNumber: string; name: string; status: string; level: string; source: string }[]
}

/** Preferences (§17.4, §13.17): channel per event, the daily digest, and follow level per project. */
export function PreferencesPage() {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['preferences'], queryFn: () => get<Prefs>('me/preferences') })
  const [err, setErr] = useState<unknown>(null)
  const [preview, setPreview] = useState<{ subject: string; body: string } | null>(null)
  const reload = () => qc.invalidateQueries({ queryKey: ['preferences'] })
  const run = async (f: () => Promise<unknown>) => { setErr(null); try { await f(); reload() } catch (e) { setErr(e) } }
  if (q.isPending) return <Loading rows={8} />
  if (q.error) return <div className="p-6"><ErrorBanner error={q.error} /></div>
  const p = q.data
  return (
    <Page title={t('top.preferences')} subtitle={t('prefs.subtitle')} className="max-w-4xl">
      {err != null && <ErrorBanner error={err} />}
      <Section title={t('prefs.digest')} id="digest">
        <div className="flex flex-wrap items-center gap-4 p-4 text-sm">
          <label className="flex items-center gap-2"><Switch checked={p.digest.digestEnabled} onCheckedChange={(on) => run(() => put('me/preferences/digest', { enabled: on, time: p.digest.time }))} />{t('prefs.digestOn')}</label>
          <label className="flex items-center gap-2">{t('prefs.digestTime')}
            <Input type="time" className="h-8 w-32" defaultValue={p.digest.time} disabled={!p.digest.digestEnabled}
              onBlur={(e) => e.target.value && e.target.value !== p.digest.time && run(async () => { await put('me/preferences/digest', { enabled: true, time: e.target.value }); toast.success(t('prefs.saved')) })} />
          </label>
          <span className="text-xs text-muted-foreground">{p.digest.weekendDigests ? t('prefs.weekendOn') : t('prefs.weekendOff')}</span>
        </div>
        <fieldset className="border-t px-4 py-3 text-sm" disabled={!p.digest.digestEnabled}>
          <legend className="mb-2 font-medium">{t('prefs.sections')} <span className="font-normal text-muted-foreground">{t('prefs.sectionsHint')}</span></legend>
          <div className="grid gap-2 sm:grid-cols-3">
            {p.digest.sections.map((code) => {
              const on = !p.digest.sectionsOff.includes(code)
              return (
                <label key={code} className="flex items-center gap-2">
                  <Checkbox checked={on} onCheckedChange={(c) => run(() => put('me/preferences/digest', { enabled: p.digest.digestEnabled, time: p.digest.time,
                    sectionsOff: c ? p.digest.sectionsOff.filter((x) => x !== code) : [...p.digest.sectionsOff, code] }))} />
                  {t(`prefs.section.${code}`)}
                </label>
              )
            })}
          </div>
        </fieldset>
        {p.digest.managesProjects && (
          <div className="flex flex-wrap items-center gap-4 border-t px-4 py-3 text-sm">
            <label className="flex items-center gap-2"><Switch checked={p.digest.weeklySummaryEnabled}
              onCheckedChange={(on) => run(() => put('me/preferences/digest', { enabled: p.digest.digestEnabled, time: p.digest.time, weeklySummary: on }))} />{t('prefs.weekly')}</label>
            <span className="text-xs text-muted-foreground">{t('prefs.weeklyHint')}</span>
            <Button variant="outline" size="sm" onClick={() => run(async () => setPreview(await get('me/weekly-summary')))}>{t('prefs.weeklyPreview')}</Button>
          </div>
        )}
      </Section>
      {preview && (
        <Dialog open onOpenChange={(o) => !o && setPreview(null)}>
          <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-2xl">
            <DialogHeader><DialogTitle>{preview.subject}</DialogTitle><DialogDescription>{t('prefs.weeklyPreviewHint')}</DialogDescription></DialogHeader>
            <pre className="whitespace-pre-wrap rounded bg-muted/50 p-3 font-sans text-sm">{preview.body}</pre>
          </DialogContent>
        </Dialog>
      )}

      <Section title={t('prefs.events')} id="events">
        <table className="w-full text-sm">
          <thead className="bg-muted/60 text-left text-xs text-muted-foreground">
            <tr><th className="px-4 py-2 font-medium">{t('prefs.event')}</th><th className="px-3 py-2 font-medium">{t('prefs.inApp')}</th><th className="px-3 py-2 font-medium">{t('prefs.email')}</th><th><span className="sr-only">{t('common.actions')}</span></th></tr>
          </thead>
          <tbody>
            {p.events.map((e) => (
              <tr key={e.code} className="border-t">
                <td className="px-4 py-2">{t(`event.${e.code}`)}{e.direct && <span className="ml-2 text-xs text-muted-foreground">{t('prefs.direct')}</span>}</td>
                <td className="px-3 py-2"><Checkbox checked={e.app} aria-label={`${t(`event.${e.code}`)} ${t('prefs.inApp')}`} onCheckedChange={(c) => run(() => put(`me/preferences/events/${e.code}`, { app: !!c, email: e.email }))} /></td>
                <td className="px-3 py-2"><Checkbox checked={e.email} aria-label={`${t(`event.${e.code}`)} ${t('prefs.email')}`} onCheckedChange={(c) => run(() => put(`me/preferences/events/${e.code}`, { app: e.app, email: !!c }))} /></td>
                <td className="px-3 py-2 text-right">{e.custom && <Button variant="ghost" size="sm" onClick={() => run(() => del(`me/preferences/events/${e.code}`))}><RotateCcw className="size-3.5" />{t('prefs.reset')}</Button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Section>

      <Section title={t('prefs.follows')} count={p.follows.length} id="follows">
        {p.follows.length === 0 ? <p className="p-4 text-sm text-muted-foreground">{t('notif.followNone')}</p> : (
          <table className="w-full text-sm">
            <tbody>
              {p.follows.map((f) => (
                <tr key={f.projectId} className="border-t first:border-t-0">
                  <td className="px-4 py-2"><Key>{f.projectNumber}</Key> {f.name}</td>
                  <td className="px-3 py-2 text-xs text-muted-foreground">{tv(f.status)}</td>
                  <td className="px-3 py-2 text-xs text-muted-foreground">{t(`follow.source.${f.source}`)}</td>
                  <td className="w-56 px-3 py-2"><FollowLevelSelect projectId={f.projectId} level={f.level} onChanged={reload} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Section>
    </Page>
  )
}
