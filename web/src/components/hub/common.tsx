import { AlertTriangle, Loader2 } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api'
import { ago } from '@/lib/format'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'

export function Page({ title, subtitle, actions, children, className }: { title: ReactNode; subtitle?: ReactNode; actions?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <div className={cn('mx-auto flex w-full flex-col gap-4 p-4 lg:p-6', className)}>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <h1 className="truncate text-xl font-semibold tracking-tight">{title}</h1>
          {subtitle && <p className="text-sm text-muted-foreground">{subtitle}</p>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {children}
    </div>
  )
}

export function Section({ title, count, actions, children, className, id }: { title: ReactNode; count?: number; actions?: ReactNode; children: ReactNode; className?: string; id?: string }) {
  return (
    <section id={id} className={cn('rounded-lg border bg-card', className)} aria-labelledby={id ? `${id}-h` : undefined}>
      <div className="flex items-center justify-between gap-2 border-b px-4 py-2.5">
        <h2 id={id ? `${id}-h` : undefined} className="text-sm font-semibold">
          {title}{count != null && <span className="ml-2 rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">{count}</span>}
        </h2>
        {actions}
      </div>
      {children}
    </section>
  )
}

export function Empty({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-3 px-4 py-8 text-center text-sm text-muted-foreground">
      <p className="max-w-prose">{children}</p>
      {action}
    </div>
  )
}

export function Loading({ rows = 5 }: { rows?: number }) {
  return (
    <div className="space-y-2 p-4" role="status" aria-label={t('app.loading')}>
      {Array.from({ length: rows }, (_, i) => <Skeleton key={i} className="h-7 w-full" />)}
    </div>
  )
}

export function ErrorBanner({ error, retry }: { error: unknown; retry?: () => void }) {
  const e = error as ApiError
  const conflict = e?.code === 'concurrency_conflict'
  const msg = conflict && e.body.changedBy ? t('error.conflict', { who: e.body.changedBy, when: ago(e.body.changedAt) }) : e?.message ?? t('app.error')
  return (
    <div role="alert" className="flex items-center gap-3 rounded-md border border-bad/30 bg-bad-bg px-3 py-2 text-sm text-bad">
      <AlertTriangle className="size-4 shrink-0" aria-hidden />
      <span className="flex-1">{e?.status === 404 ? t('app.notFound') : msg}</span>
      {retry && <Button size="sm" variant="outline" onClick={retry}>{conflict ? t('error.reload') : t('app.retry')}</Button>}
    </div>
  )
}

export function Spinner({ className }: { className?: string }) {
  return <Loader2 className={cn('size-4 animate-spin', className)} aria-hidden />
}

/** A consequential action confirmed with its consequence stated, optionally with a reason (§13.0, G-09). */
export function ConfirmDialog({ open, onOpenChange, title, body, confirmLabel, reason, onConfirm, destructive, children, busy }: {
  open: boolean; onOpenChange: (o: boolean) => void; title: ReactNode; body?: ReactNode; confirmLabel?: string
  reason?: boolean | 'optional'; onConfirm: (reason: string) => Promise<unknown> | void; destructive?: boolean; children?: ReactNode; busy?: boolean
}) {
  const [text, setText] = useState('')
  const [err, setErr] = useState<unknown>(null)
  const [working, setWorking] = useState(false)
  const needs = reason === true
  const valid = !needs || text.trim().length >= 5
  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) { setText(''); setErr(null) } onOpenChange(o) }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {body && <DialogDescription asChild><div className="space-y-2 text-sm">{body}</div></DialogDescription>}
        </DialogHeader>
        {children}
        {reason && (
          <div className="space-y-1">
            <Label htmlFor="confirm-reason">{t('common.reason')}{reason === 'optional' && <span className="text-muted-foreground"> ({t('common.optional')})</span>}</Label>
            <Textarea id="confirm-reason" value={text} onChange={(e) => setText(e.target.value)} rows={3} aria-describedby="confirm-reason-hint" />
            <p id="confirm-reason-hint" className="text-xs text-muted-foreground">{t('common.reasonHint')}</p>
          </div>
        )}
        {err != null && <ErrorBanner error={err} />}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button variant={destructive ? 'destructive' : 'default'} disabled={!valid || working || busy}
            onClick={async () => {
              setWorking(true); setErr(null)
              try { await onConfirm(text.trim()); setText(''); onOpenChange(false) } catch (e) { setErr(e) } finally { setWorking(false) }
            }}>
            {(working || busy) && <Spinner />}{confirmLabel ?? t('common.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function Field({ label, htmlFor, hint, error, children, className }: { label: ReactNode; htmlFor?: string; hint?: ReactNode; error?: string[] | string; children: ReactNode; className?: string }) {
  const errs = typeof error === 'string' ? [error] : error
  return (
    <div className={cn('space-y-1', className)}>
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {hint && !errs?.length && <p className="text-xs text-muted-foreground">{hint}</p>}
      {errs?.map((e) => <p key={e} className="text-xs text-bad" role="alert">{e}</p>)}
    </div>
  )
}

export const selectCls = 'h-9 w-full rounded-md border border-input bg-card px-2 text-sm focus-visible:outline-2'
