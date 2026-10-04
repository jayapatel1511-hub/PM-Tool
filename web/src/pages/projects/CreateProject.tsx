import { Monitor } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { toast } from 'sonner'
import { DesktopOnly, ErrorBanner, Field, Notice, Spinner, selectCls } from '@/components/hub/common'
import { Avatar, PeoplePicker } from '@/components/hub/people'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { useReference } from '@/hooks/data'
import { ApiError, post } from '@/lib/api'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'

export interface DisciplinePick { disciplineId: string; leadUserId: string | null; leadName?: string }
interface MemberPick { userId: string; name: string; roles: string[]; disciplineId: string | null }

/** Extra first-step sources contributed by later packets (copy a project's structure, start from a template). */
/** What a source can see and change in the dialog: the first step's fields and the chosen disciplines. */
export interface CreateContext { form: Record<string, string>; disciplines: DisciplinePick[]; setDisciplines: (d: DisciplinePick[]) => void }
export const CREATE_SOURCES: {
  id: string; label: string; render: (v: any, set: (v: any) => void, ctx: CreateContext) => React.ReactNode; body: (v: any) => Record<string, unknown>
  /** Shown on the last step, before Create (packet 012: what a template will create). */
  summary?: (v: any, ctx: CreateContext) => React.ReactNode
}[] = []

/** Two-step create dialog: identity, then team and disciplines (§12.1 UI behaviour, Workflow 1). Creating projects is
 *  desktop/tablet only (§13.0), so phones get a notice instead of the form. */
export function CreateProjectDialog({ onClose }: { onClose: () => void }) {
  const ref = useReference()
  const navigate = useNavigate()
  const [step, setStep] = useState<1 | 2>(1)
  const [f, setF] = useState<Record<string, string>>({ priority: 'Medium' })
  const [disciplines, setDisciplines] = useState<DisciplinePick[]>([])
  const [members, setMembers] = useState<MemberPick[]>([])
  const [source, setSource] = useState<Record<string, any>>({})
  const [err, setErr] = useState<ApiError | null>(null)
  const [busy, setBusy] = useState(false)
  const fe = err?.fieldErrors ?? {}
  const ctx: CreateContext = { form: f, disciplines, setDisciplines }
  const v = (k: string) => f[k] ?? ''
  const set = (k: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => setF({ ...f, [k]: e.target.value })
  const valid1 = v('projectNumber').trim() && v('name').trim() && v('clientId') && v('officeId')

  const submit = async () => {
    setBusy(true); setErr(null)
    try {
      const extra = Object.assign({}, ...CREATE_SOURCES.map((s) => s.body(source[s.id])))
      const res = await post<{ id: string; projectNumber: string }>('projects', {
        ...f, startDate: f.startDate || null, targetCompletionDate: f.targetCompletionDate || null, projectTypeId: f.projectTypeId || null,
        coordinationDay: f.coordinationDay || null,
        disciplines: disciplines.map((d) => ({ disciplineId: d.disciplineId, leadUserId: d.leadUserId })),
        members: members.map((m) => ({ userId: m.userId, roles: m.roles, disciplineId: m.disciplineId })), ...extra,
      })
      toast.success(t('projects.created', { number: res.projectNumber }))
      onClose()
      navigate(`/projects/${encodeURIComponent(res.projectNumber)}`)
    } catch (e) {
      setErr(e as ApiError)
      if ((e as ApiError).fieldErrors?.projectNumber || (e as ApiError).code === 'duplicate_project_number') setStep(1)
    } finally { setBusy(false) }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[760px]">
        <DialogHeader>
          <DialogTitle>{t('projects.create')}</DialogTitle>
          <DialogDescription>
            <DesktopOnly notice={t('projects.createPhoneHint')}>{t('projects.step', { n: step })} — {step === 1 ? t('projects.step1') : t('projects.step2')}</DesktopOnly>
          </DialogDescription>
        </DialogHeader>
        <DesktopOnly notice={<>
          <Notice icon={Monitor} title={t('app.phoneNotice')} />
          <DialogFooter><Button variant="outline" onClick={onClose}>{t('common.close')}</Button></DialogFooter>
        </>}>
          {step === 1 ? (
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t('projects.col.number')} htmlFor="p-num" error={fe.projectNumber} hint={t('projects.numberHint')}>
                <Input id="p-num" className="key" value={v('projectNumber')} onChange={set('projectNumber')} autoFocus />
              </Field>
              <Field label={t('common.name')} htmlFor="p-name" error={fe.name}><Input id="p-name" value={v('name')} onChange={set('name')} maxLength={200} /></Field>
              <Field label={t('projects.col.client')} htmlFor="p-client" error={fe.clientId} hint={t('projects.clientHint')}>
                <select id="p-client" className={selectCls} value={v('clientId')} onChange={set('clientId')}>
                  <option value="">{t('common.selectPlaceholder')}</option>
                  {ref.data?.clients.filter((c) => c.isActive).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </Field>
              <Field label={t('projects.clientRef')} htmlFor="p-cref" optional><Input id="p-cref" value={v('clientReference')} onChange={set('clientReference')} /></Field>
              <Field label={t('projects.col.office')} htmlFor="p-office" error={fe.officeId}>
                <select id="p-office" className={selectCls} value={v('officeId')} onChange={set('officeId')}>
                  <option value="">{t('common.selectPlaceholder')}</option>
                  {ref.data?.offices.filter((o) => o.isActive).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
                </select>
              </Field>
              <Field label={t('projects.type')} htmlFor="p-type" optional>
                <select id="p-type" className={selectCls} value={v('projectTypeId')} onChange={set('projectTypeId')}>
                  <option value="">{t('common.none')}</option>
                  {ref.data?.projectTypes.filter((o) => o.isActive).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
                </select>
              </Field>
              <Field label={t('field.StartDate')} htmlFor="p-start" optional><Input id="p-start" type="date" value={v('startDate')} onChange={set('startDate')} /></Field>
              <Field label={t('field.TargetCompletionDate')} htmlFor="p-target" optional><Input id="p-target" type="date" value={v('targetCompletionDate')} onChange={set('targetCompletionDate')} /></Field>
              <Field label={t('common.priority')} htmlFor="p-prio">
                <select id="p-prio" className={selectCls} value={v('priority')} onChange={set('priority')}>{['Low', 'Medium', 'High', 'Critical'].map((p) => <option key={p} value={p}>{t(`value.${p}`)}</option>)}</select>
              </Field>
              <Field label={t('field.CoordinationDay')} htmlFor="p-day">
                <select id="p-day" className={selectCls} value={v('coordinationDay')} onChange={set('coordinationDay')}>
                  <option value="">{t('projects.mondayDefault')}</option>
                  {['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map((d) => <option key={d} value={d}>{t(`day.${d}`)}</option>)}
                </select>
              </Field>
              <Field label={t('field.Location')} htmlFor="p-loc" optional className="sm:col-span-2"><Input id="p-loc" value={v('location')} onChange={set('location')} /></Field>
              <Field label={t('common.description')} htmlFor="p-desc" optional className="sm:col-span-2"><Textarea id="p-desc" rows={3} value={v('description')} onChange={set('description')} /></Field>
              {CREATE_SOURCES.map((s) => <div key={s.id} className="sm:col-span-2">{s.render(source[s.id], (x) => setSource({ ...source, [s.id]: x }), ctx)}</div>)}
            </div>
          ) : (
            <div className="space-y-6">
              <fieldset className="space-y-3">
                <legend className="text-base/6 font-semibold">{t('team.disciplines')}</legend>
                <p className="text-sm text-muted-foreground">{t('projects.leadHint')}</p>
                <ul className="divide-y rounded-lg border">
                  {ref.data?.disciplines.filter((d) => d.isActive).map((d) => {
                    const picked = disciplines.find((x) => x.disciplineId === d.id)
                    return (
                      <li key={d.id} className="flex flex-wrap items-center gap-x-3 gap-y-2 px-4 py-2">
                        <label className="flex min-h-(--control-row-h) w-48 items-center gap-2 text-sm">
                          <Checkbox checked={!!picked} onCheckedChange={(c) => setDisciplines(c ? [...disciplines, { disciplineId: d.id, leadUserId: null }] : disciplines.filter((x) => x.disciplineId !== d.id))} />
                          <span className="size-2.5 shrink-0 rounded-sm" style={{ background: d.colour }} aria-hidden />{d.name}
                        </label>
                        {picked && (
                          <div className="min-w-48 flex-1">
                            <PeoplePicker value={picked.leadUserId} valueName={picked.leadName} placeholder={t('team.chooseLead')}
                              onChange={(id, p) => setDisciplines(disciplines.map((x) => x.disciplineId === d.id ? { ...x, leadUserId: id, leadName: p?.displayName } : x))} />
                          </div>
                        )}
                      </li>
                    )
                  })}
                </ul>
              </fieldset>
              <fieldset className="space-y-3">
                <legend className="text-base/6 font-semibold">{t('team.members')}</legend>
                <div className="max-w-sm">
                  <PeoplePicker value={null} placeholder={t('team.addMember')} allowClear={false} exclude={members.map((m) => m.userId)}
                    onChange={(id, p) => id && p && setMembers([...members, { userId: id, name: p.displayName, roles: ['TeamMember'], disciplineId: null }])} />
                </div>
                {members.length > 0 && (
                  <ul className="divide-y rounded-lg border">
                    {members.map((m) => (
                      <li key={m.userId} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-2 text-sm">
                        <span className="flex w-44 min-w-0 items-center gap-2 font-medium"><Avatar id={m.userId} name={m.name} /><span className="min-w-0 break-words">{m.name}</span></span>
                        <span className="flex flex-wrap items-center gap-x-4 gap-y-1">
                          {['PM', 'TeamMember', 'Reviewer', 'Viewer'].map((r) => (
                            <label key={r} className="flex min-h-6 items-center gap-1.5">
                              <Checkbox aria-label={`${t(`role.${r}`)} — ${m.name}`} checked={m.roles.includes(r)} onCheckedChange={(c) => setMembers(members.map((x) => x.userId === m.userId ? { ...x, roles: c ? [...x.roles, r] : x.roles.filter((y) => y !== r) } : x))} />{t(`role.${r}`)}
                            </label>
                          ))}
                        </span>
                        <select className={cn(selectCls, 'h-(--control-row-h) w-auto')} value={m.disciplineId ?? ''} aria-label={t('field.PrimaryDisciplineId')}
                          onChange={(e) => setMembers(members.map((x) => x.userId === m.userId ? { ...x, disciplineId: e.target.value || null } : x))}>
                          <option value="">{t('team.noPrimary')}</option>
                          {disciplines.map((d) => <option key={d.disciplineId} value={d.disciplineId}>{ref.data?.disciplines.find((x) => x.id === d.disciplineId)?.name}</option>)}
                        </select>
                        <Button size="sm" variant="ghost" className="ml-auto" aria-label={t('team.removeNamed', { name: m.name })} onClick={() => setMembers(members.filter((x) => x.userId !== m.userId))}>{t('common.remove')}</Button>
                      </li>
                    ))}
                  </ul>
                )}
              </fieldset>
              {CREATE_SOURCES.map((s) => s.summary && source[s.id] ? <div key={s.id}>{s.summary(source[s.id], ctx)}</div> : null)}
            </div>
          )}
          {err && <ErrorBanner error={err} />}
          <DialogFooter>
            {step === 2 && <Button variant="outline" onClick={() => setStep(1)}>{t('common.back')}</Button>}
            <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
            {step === 1 ? <Button disabled={!valid1} onClick={() => setStep(2)}>{t('common.next')}</Button>
              : <Button disabled={busy} onClick={submit}>{busy ? <><Spinner />{t('common.saving')}</> : t('projects.createButton')}</Button>}
          </DialogFooter>
        </DesktopOnly>
      </DialogContent>
    </Dialog>
  )
}
