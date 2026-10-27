import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, ChevronRight } from 'lucide-react'
import { Fragment, useState } from 'react'
import { Link } from 'react-router'
import { toast } from 'sonner'
import { ConfirmDialog, Empty, ErrorBanner, Field, Loading, Section, TableRegion, tdCls, thCls } from '@/components/hub/common'
import { ExportMenu } from '@/components/hub/export'
import { Pill } from '@/components/hub/pills'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { typedRequest } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { invalidateChange } from '@/lib/keys'
import { t, type TextKey } from '@/lib/i18n'

/** The checks whose detail is a code, not text: the reference list a deactivated record belongs to, and the role a grant gives. */
const DETAIL: Record<string, Record<string, TextKey>> = {
  inactiveReference: { disciplines: 'admin.disciplines', clients: 'admin.clients', offices: 'admin.offices', deliverableTypes: 'admin.deliverableTypes', phases: 'admin.phases' },
  inAppRoles: { Admin: 'role.Admin', Executive: 'role.Executive', Supervisor: 'role.Supervisor', ProjectManager: 'role.ProjectManager', ReadOnly: 'role.ReadOnly', StandardUser: 'role.StandardUser' },
}
const detailText = (check: string, detail?: string | null) => (detail && DETAIL[check]?.[detail] ? t(DETAIL[check][detail]) : detail ?? '')

/** The records behind one count: the same list the count was taken from (FR-ADR-02). Each row links to the tool that fixes it (FR-ADR-05). */
function CheckList({ id, check }: { id: string; check: string }) {
  const list = useQuery({ queryKey: ['admin', 'review', 'check', check], queryFn: () => typedRequest('get', '/api/v1/admin/review/checks/{key}', { params: { key: check } }) })
  if (list.isPending) return <Loading rows={2} />
  if (list.error) return <ErrorBanner error={list.error} retry={() => list.refetch()} />
  return (
    <div id={id} className="overflow-x-auto rounded-md border bg-card">
      <table className="w-full text-sm">
        <caption className="sr-only">{list.data.title}</caption>
        <thead className="bg-muted"><tr><th className={thCls}>{t('common.name')}</th><th className={thCls}>{t('common.details')}</th><th className={thCls}>{t('common.date')}</th><th className={thCls}><span className="sr-only">{t('common.actions')}</span></th></tr></thead>
        <tbody>
          {list.data.rows.map((r, i) => (
            <tr key={`${r.id ?? r.name}-${i}`} className="border-t">
              <td className={tdCls}>{r.name}</td>
              <td className={`${tdCls} text-muted-foreground`}>{detailText(check, r.detail)}</td>
              <td className={`${tdCls} tabular-nums`}>{fmtDate(r.at)}</td>
              <td className={`${tdCls} text-right`}><Link className="text-primary underline-offset-2 hover:underline" to={r.link} aria-label={t('adminReview.openRow', { name: r.name })}>{t('common.open')}</Link></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/** Admin review (§55.4, FR-ADR-01..07): computed checks that open their lists, the last recorded review beside now, and the record form.
 *  It adds no edit path of its own: every fix is the existing tool a row links to (FR-ADR-05). */
export function AdminReview() {
  const qc = useQueryClient()
  const [open, setOpen] = useState<string | null>(null)
  const [notes, setNotes] = useState<Record<string, string>>({})
  const [note, setNote] = useState('')
  const [confirm, setConfirm] = useState(false)
  const review = useQuery({ queryKey: ['admin', 'review'], queryFn: () => typedRequest('get', '/api/v1/admin/review', {}) })
  const record = useMutation({
    mutationFn: () => typedRequest('post', '/api/v1/admin/review', { body: { note: note.trim() || null, checks: Object.entries(notes).filter(([, v]) => v.trim()).map(([key, v]) => ({ key, note: v.trim() })) } }),
    onSuccess: () => { setNotes({}); setNote(''); setOpen(null); void invalidateChange(qc, 'AdminReview'); toast.success(t('adminReview.recorded')) },
  })
  if (review.isPending) return <Loading rows={8} />
  if (review.error) return <ErrorBanner error={review.error} retry={() => review.refetch()} />
  const { checks, last, dueOn, overdue } = review.data
  const then = (key: string) => last?.checks.find((c) => c.key === key)
  return (
    <div className="space-y-6">
      <Section title={t('adminReview.title')} actions={last && <ExportMenu path={`admin/review/${last.id}/export`} params={{}} name="admin-review" label={t('adminReview.export')} />}>
        <div className="space-y-2 px-5 py-4 text-sm">
          <p className="text-muted-foreground">{t('adminReview.intro')}</p>
          {last ? <p>{t('adminReview.last', { date: fmtDate(last.reviewedAt), who: last.reviewer })}{last.note && <span className="text-muted-foreground"> · {last.note}</span>}</p> : <p>{t('adminReview.never')}</p>}
          {overdue ? <Pill tone="warn">{dueOn ? t('adminReview.overdue', { date: fmtDate(dueOn) }) : t('adminReview.due')}</Pill> : <p className="text-muted-foreground">{t('adminReview.nextDue', { date: fmtDate(dueOn) })}</p>}
        </div>
      </Section>
      <TableRegion>
        <table className="w-full text-sm">
          <caption className="sr-only">{t('adminReview.title')}</caption>
          <thead className="bg-muted"><tr><th className={thCls}>{t('adminReview.check')}</th><th className={`${thCls} text-right`}>{t('adminReview.now')}</th><th className={`${thCls} text-right`}>{t('adminReview.then')}</th><th className={thCls}>{t('adminReview.noteHeader')}</th></tr></thead>
          <tbody>
            {checks.map((c) => {
              const shown = open === c.key, was = then(c.key)
              return (
                <Fragment key={c.key}>
                  <tr className="border-t align-top">
                    <td className={tdCls}><div className="font-medium">{c.title}</div><div className="max-w-prose text-xs/[18px] text-muted-foreground">{c.rule}</div></td>
                    <td className={`${tdCls} text-right tabular-nums`}>
                      {c.count === 0 ? <span>0</span> : (
                        <Button size="sm" variant="outline" className="tabular-nums" aria-expanded={shown} aria-controls={`review-${c.key}`} aria-label={t('adminReview.show', { title: c.title, n: c.count })} onClick={() => setOpen(shown ? null : c.key)}>
                          {shown ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}{c.count}
                        </Button>
                      )}
                    </td>
                    <td className={`${tdCls} text-right tabular-nums`}>{was ? was.count : t('common.dash')}{was?.note && <div className="max-w-48 text-left text-xs/[18px] text-muted-foreground">{was.note}</div>}</td>
                    <td className={tdCls}><Input aria-label={t('adminReview.noteFor', { title: c.title })} maxLength={2000} value={notes[c.key] ?? ''} onChange={(e) => setNotes({ ...notes, [c.key]: e.target.value })} /></td>
                  </tr>
                  {shown && <tr><td colSpan={4} className="bg-muted/40 px-4 py-3"><CheckList id={`review-${c.key}`} check={c.key} /></td></tr>}
                </Fragment>
              )
            })}
            {checks.length === 0 && <tr><td colSpan={4}><Empty>{t('adminReview.none')}</Empty></td></tr>}
          </tbody>
        </table>
      </TableRegion>
      <Section title={t('adminReview.record')}>
        <div className="space-y-3 px-5 py-4">
          <Field label={t('adminReview.reviewNote')} htmlFor="review-note" optional><Textarea id="review-note" rows={3} maxLength={2000} value={note} onChange={(e) => setNote(e.target.value)} /></Field>
          <Button onClick={() => setConfirm(true)}>{t('adminReview.record')}</Button>
        </div>
      </Section>
      <ConfirmDialog open={confirm} onOpenChange={setConfirm} title={t('adminReview.recordTitle')} body={<p>{t('adminReview.recordBody')}</p>} confirmLabel={t('adminReview.record')} onConfirm={() => record.mutateAsync()} />
    </div>
  )
}
