import { useQuery } from '@tanstack/react-query'
import { Copy, ExternalLink, Lock, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, ErrorBanner, Field, Notice, Page, Section, Spinner, selectCls } from '@/components/hub/common'
import { PeoplePicker } from '@/components/hub/people'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { useProjectRefresh, useReference } from '@/hooks/data'
import { ApiError, del, get, patch, post } from '@/lib/api'
import { t, tv } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'
import { cn } from '@/lib/utils'
import { useCurrentProject } from './ProjectLayout'

/** Extra settings sections from later packets (health override, export at archive). */
export const SETTINGS_SECTIONS: { id: string; render: (p: ProjectDetail) => React.ReactNode }[] = []

/** Project Settings tab (§13.16): information, links, coordination day, visibility, status, danger zone. */
export function ProjectSettingsTab() {
  const p = useCurrentProject()
  const ref = useReference()
  const refresh = useProjectRefresh()
  const navigate = useNavigate()
  const canEdit = p.permissions.edit.ok
  const [f, setF] = useState<Record<string, any>>({})
  const [reason, setReason] = useState('')
  const [err, setErr] = useState<ApiError | null>(null)
  const [saving, setSaving] = useState(false)
  const [transition, setTransition] = useState<string | null>(null)
  const v = (k: keyof ProjectDetail) => (k in f ? f[k] : (p[k] ?? '')) as string
  const set = (k: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => setF({ ...f, [k]: e.target.value })
  const dirty = Object.keys(f).length > 0
  const save = async () => {
    setErr(null); setSaving(true)
    try {
      const body: Record<string, unknown> = {}
      for (const [k, val] of Object.entries(f)) body[k] = val === '' ? null : val
      if (p.permissions.needsReason) body.reason = reason
      const res = await patch(`projects/${p.id}`, body, p.rowVersion)
      toast.success(t('common.saved'))
      setF({}); setReason('')
      if (body.projectNumber) navigate(`/projects/${encodeURIComponent(String(body.projectNumber))}/settings`, { replace: true })
      refresh(p.id)
      return res
    } catch (e) { setErr(e as ApiError) } finally { setSaving(false) }
  }
  const fe = err?.fieldErrors ?? {}

  return (
    <Page title={t('ptab.settings')}>
      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <Section title={t('settings.information')}>
          <form className="grid max-w-[760px] gap-4 p-5 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); save() }}>
            {!canEdit && p.permissions.edit.reason && <Notice icon={Lock} title={p.permissions.edit.reason} className="sm:col-span-2" />}
            <Field label={t('projects.col.number')} htmlFor="s-num" error={fe.projectNumber} hint={!p.permissions.changeNumber ? t('settings.numberAdminOnly') : undefined}>
              <Input id="s-num" className="key" value={v('projectNumber')} onChange={set('projectNumber')} disabled={!p.permissions.changeNumber} />
            </Field>
            <Field label={t('common.name')} htmlFor="s-name" error={fe.name}><Input id="s-name" value={v('name')} onChange={set('name')} disabled={!canEdit} /></Field>
            <Field label={t('projects.col.client')} htmlFor="s-client">
              <select id="s-client" className={selectCls} value={v('clientId')} onChange={set('clientId')} disabled={!canEdit}>
                {ref.data?.clients.filter((c) => c.isActive || c.id === p.clientId).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </Field>
            <Field label={t('projects.clientRef')} htmlFor="s-cref" optional><Input id="s-cref" value={v('clientReference')} onChange={set('clientReference')} disabled={!canEdit} /></Field>
            <Field label={t('projects.col.pm')} htmlFor="s-pm" hint={t('settings.pmHint')}>
              <PeoplePicker id="s-pm" value={f.projectManagerId ?? p.projectManagerId} valueName={f.projectManagerId ? undefined : p.pmName} allowClear={false} disabled={!canEdit}
                onChange={(id) => id && setF({ ...f, projectManagerId: id })} />
            </Field>
            <Field label={t('projects.col.office')} htmlFor="s-office">
              <select id="s-office" className={selectCls} value={v('officeId')} onChange={set('officeId')} disabled={!canEdit}>
                {ref.data?.offices.filter((o) => o.isActive || o.id === p.officeId).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
              </select>
            </Field>
            <Field label={t('projects.type')} htmlFor="s-type" optional>
              <select id="s-type" className={selectCls} value={v('projectTypeId')} onChange={set('projectTypeId')} disabled={!canEdit}>
                <option value="">{t('common.none')}</option>
                {ref.data?.projectTypes.filter((o) => o.isActive || o.id === p.projectTypeId).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
              </select>
            </Field>
            <Field label={t('projects.col.phase')} htmlFor="s-phase" optional>
              <select id="s-phase" className={selectCls} value={v('phaseId')} onChange={set('phaseId')} disabled={!canEdit}>
                <option value="">{t('common.none')}</option>
                {ref.data?.phases.filter((o) => o.isActive || o.id === p.phaseId).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
              </select>
            </Field>
            <Field label={t('field.StartDate')} htmlFor="s-start" optional><Input id="s-start" type="date" value={v('startDate')} onChange={set('startDate')} disabled={!canEdit} /></Field>
            <Field label={t('field.TargetCompletionDate')} htmlFor="s-target" optional><Input id="s-target" type="date" value={v('targetCompletionDate')} onChange={set('targetCompletionDate')} disabled={!canEdit} /></Field>
            <Field label={t('common.priority')} htmlFor="s-prio">
              <select id="s-prio" className={selectCls} value={v('priority')} onChange={set('priority')} disabled={!canEdit}>{['Low', 'Medium', 'High', 'Critical'].map((x) => <option key={x} value={x}>{t(`value.${x}`)}</option>)}</select>
            </Field>
            <Field label={t('field.CoordinationDay')} htmlFor="s-day">
              <select id="s-day" className={selectCls} value={v('coordinationDay')} onChange={set('coordinationDay')} disabled={!canEdit}>
                <option value="">{t('projects.mondayDefault')}</option>
                {['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map((d) => <option key={d} value={d}>{t(`day.${d}`)}</option>)}
              </select>
            </Field>
            <Field label={t('field.Location')} htmlFor="s-loc" optional className="sm:col-span-2"><Input id="s-loc" value={v('location')} onChange={set('location')} disabled={!canEdit} /></Field>
            <Field label={t('common.description')} htmlFor="s-desc" optional className="sm:col-span-2"><Textarea id="s-desc" rows={3} value={v('description')} onChange={set('description')} disabled={!canEdit} /></Field>
            <Field label={t('field.InternalNotes')} htmlFor="s-notes" optional className="sm:col-span-2" hint={t('settings.notesHint')}><Textarea id="s-notes" rows={2} value={v('internalNotes')} onChange={set('internalNotes')} disabled={!canEdit} /></Field>
            <label className="flex min-h-6 items-center gap-2 text-sm font-medium sm:col-span-2">
              <Switch checked={'allowViewerComments' in f ? f.allowViewerComments : p.allowViewerComments} disabled={!canEdit} onCheckedChange={(c) => setF({ ...f, allowViewerComments: c })} />{t('field.AllowViewerComments')}
            </label>
            {p.permissions.setVisibility && (
              <Field label={t('field.Visibility')} htmlFor="s-vis" hint={t('settings.visibilityHint')}>
                <select id="s-vis" className={selectCls} value={v('visibility')} onChange={set('visibility')}><option value="Open">{t('value.Open')}</option><option value="Restricted">{t('value.Restricted')}</option></select>
              </Field>
            )}
            {p.permissions.needsReason && dirty && (
              <Field label={t('common.reason')} htmlFor="s-reason" error={fe.reason} hint={t('settings.correctionHint')} className="sm:col-span-2">
                <Input id="s-reason" value={reason} onChange={(e) => setReason(e.target.value)} />
              </Field>
            )}
            {err && <div className="sm:col-span-2"><ErrorBanner error={err} /></div>}
            {canEdit && (
              <div className="flex flex-wrap gap-2 sm:col-span-2">
                <Button type="submit" disabled={!dirty || saving}>{saving ? <><Spinner />{t('common.saving')}</> : t('common.save')}</Button>
                {dirty && <Button type="button" variant="outline" onClick={() => setF({})}>{t('common.cancel')}</Button>}
              </div>
            )}
          </form>
        </Section>

        <div className="space-y-6">
          <LinksSection p={p} onChange={() => refresh(p.id)} />
          {SETTINGS_SECTIONS.map((s) => <div key={s.id}>{s.render(p)}</div>)}
          <Section title={t('settings.status')}>
            <div className="space-y-3 px-5 py-4 text-sm">
              <p>{t('settings.currentStatus', { status: t(`value.${p.status}`) })}</p>
              {p.permissions.transitions.some((s) => s !== 'Cancelled') && (
                <div className="flex flex-wrap gap-2">
                  {p.permissions.transitions.filter((s) => s !== 'Cancelled').map((s) => <Button key={s} variant="outline" size="sm" onClick={() => setTransition(s)}>{t(`settings.to.${s}`, { from: t(`value.${p.status}`) })}</Button>)}
                </div>
              )}
              {p.permissions.transitions.length === 0 && <p className="text-muted-foreground">{t('settings.noTransitions')}</p>}
            </div>
          </Section>
          {p.permissions.transitions.includes('Cancelled') && (
            <Section title={t('settings.danger')} className="border-bad/40">
              <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-4 text-sm">
                <p className="min-w-0 flex-1">{t('settings.cancelHint')}</p>
                <Button variant="destructive" size="sm" onClick={() => setTransition('Cancelled')}>{t('settings.cancelProject')}</Button>
              </div>
            </Section>
          )}
        </div>
      </div>
      {transition && <TransitionDialog p={p} to={transition} onClose={() => { setTransition(null); refresh(p.id) }} />}
    </Page>
  )
}

function LinksSection({ p, onChange }: { p: ProjectDetail; onChange: () => void }) {
  const [url, setUrl] = useState('')
  const [title, setTitle] = useState('')
  const [err, setErr] = useState<unknown>(null)
  const canEdit = p.permissions.edit.ok
  return (
    <Section title={t('settings.links')} count={p.links.length}>
      {p.links.length === 0 ? <p className="px-5 py-4 text-sm text-muted-foreground">{t('links.none')}</p> : (
        <ul className="divide-y">
          {p.links.map((l) => (
            <li key={l.id} className="flex min-h-(--row-min) items-center gap-3 px-5 py-2 text-sm">
              <span className="w-28 shrink-0 text-xs/[18px] text-muted-foreground">{t(`linkType.${l.linkType}`)}</span>
              {l.linkType === 'Network Folder'
                ? <><span className="min-w-0 truncate font-mono text-xs" title={l.url}>{l.url}</span><Button size="icon-sm" variant="ghost" aria-label={t('links.copyPath')} onClick={() => { navigator.clipboard?.writeText(l.url); toast.success(t('links.pathCopied')) }}><Copy className="size-4" /></Button></>
                : <a href={l.url} target="_blank" rel="noreferrer noopener" className="inline-flex min-w-0 items-center gap-1 text-primary underline underline-offset-4 hover:text-foreground"><span className="truncate">{l.title}</span><ExternalLink className="size-3.5 shrink-0" aria-hidden /></a>}
              {canEdit && <Button size="icon-sm" variant="ghost" className="ml-auto shrink-0 text-bad hover:text-bad" aria-label={t('links.remove', { title: l.title || l.url })} onClick={async () => { await del(`projects/${p.id}/links/${l.id}`); onChange() }}><Trash2 className="size-4" /></Button>}
            </li>
          ))}
        </ul>
      )}
      {canEdit && (
        <form className="grid gap-3 border-t px-5 py-4 sm:grid-cols-[minmax(0,1fr)_minmax(0,11rem)_auto] sm:items-end" onSubmit={async (e) => {
          e.preventDefault(); setErr(null)
          try { await post(`projects/${p.id}/links`, { url, title }); setUrl(''); setTitle(''); onChange() } catch (x) { setErr(x) }
        }}>
          <Field label={t('field.Url')} htmlFor="s-link-url"><Input id="s-link-url" placeholder={t('links.urlPlaceholder')} value={url} onChange={(e) => setUrl(e.target.value)} /></Field>
          <Field label={t('field.Title')} htmlFor="s-link-title" optional><Input id="s-link-title" value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
          <Button type="submit" variant="outline" disabled={!url}>{t('common.add')}</Button>
          {err != null && <div className="sm:col-span-3"><ErrorBanner error={err} /></div>}
        </form>
      )}
    </Section>
  )
}

/** Status change with consequence in numbers, reason (P-03) and the closeout checklist (P-04). */
function TransitionDialog({ p, to, onClose }: { p: ProjectDetail; to: string; onClose: () => void }) {
  const preview = useQuery({ queryKey: ['closeout', p.id], queryFn: () => get(`projects/${p.id}/closeout`) })
  const [choices, setChoices] = useState<Record<string, 'cancel' | 'leave'>>({ tasks: 'leave', deliverables: 'leave', decisions: 'leave', registers: 'leave' })
  const needsReason = ['On Hold', 'Cancelled', 'Complete'].includes(to) || (p.status === 'Complete' && to === 'Active')
  const d = preview.data
  const consequence = to === 'On Hold' ? t('settings.consequence.hold', { n: d?.openTasks ?? '…' })
    : to === 'Cancelled' ? t('settings.consequence.cancel') : to === 'Archived' ? t('settings.consequence.archive')
    : to === 'Active' && p.status === 'Setup' ? t('settings.consequence.activate') : to === 'Complete' ? t('settings.consequence.complete') : null
  const groups = d ? [['tasks', d.tasks], ['deliverables', d.deliverables], ['decisions', d.decisions]] as const : []
  const choice = (k: string, label: string) => (
    <select className={cn(selectCls, 'h-(--control-row-h) w-auto')} value={choices[k]} onChange={(e) => setChoices({ ...choices, [k]: e.target.value as 'cancel' | 'leave' })} aria-label={label}>
      <option value="leave">{t('settings.leaveOpen')}</option><option value="cancel">{t('settings.cancelAll')}</option>
    </select>
  )
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose()} title={t('settings.transitionTitle', { from: t(`value.${p.status}`), to: t(`value.${to}`) })}
      body={consequence} reason={needsReason ? true : 'optional'} destructive={to === 'Cancelled'} confirmLabel={t(`settings.to.${to}`, { from: '' })}
      onConfirm={(reason) => post(`projects/${p.id}/transition`, { toStatus: to, reason, rowVersion: p.rowVersion, closeout: to === 'Complete' ? choices : undefined })}>
      {to === 'Complete' && d && (
        <div className="space-y-2 text-sm">
          <p className="font-semibold">{t('settings.closeout')}</p>
          {groups.map(([k, items]) => (
            <div key={k} className="rounded-md border p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span>{t(`settings.closeout.${k}`, { n: items.length })}</span>
                {items.length > 0 && choice(k, t(`settings.closeout.${k}`, { n: items.length }))}
              </div>
              {items.length > 0 && <ul className="mt-2 max-h-24 space-y-0.5 overflow-y-auto text-xs/[18px] text-muted-foreground">{items.map((i: any) => <li key={i.id}><span className="key">{i.key}</span> {i.name} · {tv(i.status)}</li>)}</ul>}
            </div>
          ))}
          {d.openRegisterItems > 0 && (
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3">
              <span>{t('settings.closeout.registers', { n: d.openRegisterItems })}</span>
              {choice('registers', t('settings.closeout.registers', { n: d.openRegisterItems }))}
            </div>
          )}
        </div>
      )}
    </ConfirmDialog>
  )
}
