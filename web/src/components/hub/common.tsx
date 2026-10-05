import { AlertTriangle, Info, Loader2, X, type LucideIcon } from 'lucide-react'
import { createContext, useContext, useEffect, useState, useSyncExternalStore, type ComponentProps, type ReactNode } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api'
import { useMe } from '@/lib/auth'
import { ago } from '@/lib/format'
import { t } from '@/lib/i18n'
import { accentOf, cn, type Accent } from '@/lib/utils'

/** True inside a project, where the project name is the page's h1 and a tab's title is a section heading under it. */
export const InProject = createContext(false)

/** Page header (design §6): optional eyebrow, a readable title, supporting text and the page's actions on the right. */
export function Page({ title, subtitle, eyebrow, actions, children, className }: { title: ReactNode; subtitle?: ReactNode; eyebrow?: ReactNode; actions?: ReactNode; children: ReactNode; className?: string }) {
  const nested = useContext(InProject)
  const Heading = nested ? 'h2' : 'h1'
  return (
    <div className={cn('mx-auto flex w-full flex-col gap-6 p-4 md:p-6 xl:p-8', className)}>
      <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-4">
        <div className="min-w-0 max-w-3xl">
          {eyebrow && <p className="mb-1 text-xs/[18px] font-semibold uppercase tracking-[1px] text-muted-foreground">{eyebrow}</p>}
          <Heading className={cn('break-words font-semibold', nested ? 'text-lg/[26px] tracking-[-0.2px]' : 'text-2xl/8 tracking-[-0.4px] md:text-[28px]/9 md:tracking-[-0.6px]')}>{title}</Heading>
          {subtitle && <p className="mt-1 text-base/6 text-muted-foreground">{subtitle}</p>}
        </div>
        {actions && <div className="no-print flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {children}
    </div>
  )
}

/** A card with a titled header; `accent` tints the header for identity or grouping, never for status. */
export function Section({ title, count, actions, children, className, id, headingId, accent }: { title: ReactNode; count?: number; actions?: ReactNode; children: ReactNode; className?: string; id?: string; headingId?: string; accent?: Accent }) {
  const Heading = useContext(InProject) ? 'h3' : 'h2' // one level below the page title
  return (
    <section id={id} className={cn('overflow-hidden rounded-lg border bg-card', className)} aria-labelledby={headingId ?? (id ? `${id}-h` : undefined)}>
      <div data-accent={accent} className={cn('flex flex-wrap items-center justify-between gap-2 border-b px-5 py-3', accent && 'bg-(--acc-bg)')}>
        <Heading id={headingId ?? (id ? `${id}-h` : undefined)} className="text-base/6 font-semibold">
          {title}{count != null && <span className="ml-2 rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-muted-foreground tabular-nums">{count}</span>}
        </Heading>
        {actions}
      </div>
      {children}
    </section>
  )
}

export function Empty({ children, action, title }: { children: ReactNode; action?: ReactNode; title?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-3 px-4 py-10 text-center text-sm text-muted-foreground">
      {title && <p className="text-base/6 font-semibold text-foreground">{title}</p>}
      <p className="max-w-prose">{children}</p>
      {action}
    </div>
  )
}

export function Loading({ rows = 5, className }: { rows?: number; className?: string }) {
  return (
    <div className={cn('space-y-2 p-4', className)} role="status" aria-label={t('app.loading')}>
      {Array.from({ length: rows }, (_, i) => <Skeleton key={i} className="h-(--row-min) w-full" />)}
    </div>
  )
}

export function ErrorBanner({ error, retry, retryLabel, details }: { error: unknown; retry?: () => void; retryLabel?: string; details?: ReactNode }) {
  const e = error as ApiError
  const conflict = e?.code === 'concurrency_conflict'
  const msg = conflict && e.body.changedBy ? t('error.conflict', { who: e.body.changedBy, when: ago(e.body.changedAt) }) : e?.message ?? t('app.error')
  return (
    <div role="alert" className="flex flex-wrap items-center gap-3 rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-sm text-bad">
      <AlertTriangle className="size-4 shrink-0" aria-hidden />
      <span className="min-w-0 flex-1">{e?.status === 404 ? t('app.notFound') : msg}</span>
      {retry && <Button size="sm" variant="outline" onClick={retry}>{retryLabel ?? (conflict ? t('error.reload') : t('app.retry'))}</Button>}
      {details && <div className="w-full">{details}</div>}
    </div>
  )
}

/** A calm explanation with the useful next step: access, device or scope limits (design §8, read-only and denied). */
export function Notice({ title, children, action, icon: Icon = Info, className }: { title: ReactNode; children?: ReactNode; action?: ReactNode; icon?: LucideIcon; className?: string }) {
  return (
    <div role="status" className={cn('flex gap-3 rounded-lg border bg-muted px-5 py-4 text-sm', className)}>
      <Icon className="mt-0.5 size-5 shrink-0 text-muted-foreground" aria-hidden />
      <div className="min-w-0 space-y-1">
        <p className="font-semibold">{title}</p>
        {children && <div className="text-muted-foreground">{children}</div>}
        {action && <div className="pt-2">{action}</div>}
      </div>
    </div>
  )
}

const phone = '(max-width: 767.98px)'
const subscribePhone = (l: () => void) => { const m = matchMedia(phone); m.addEventListener('change', l); return () => m.removeEventListener('change', l) }
/** True below the tablet breakpoint (§13.0 Responsive); follows the viewport as it changes. */
export const useIsPhone = () => useSyncExternalStore(subscribePhone, () => matchMedia(phone).matches)
/** Phones get a notice instead of desktop/tablet-only screens such as resources (§13.0 Responsive). */
export function DesktopOnly({ children, notice }: { children: ReactNode; notice: ReactNode }) {
  return <>{useIsPhone() ? notice : children}</>
}

/** A value that is unknown or not provided, distinct from zero and from an empty result. */
export function Missing() {
  return <span className="text-muted-foreground"><span aria-hidden>{t('common.dash')}</span><span className="sr-only">{t('common.unavailable')}</span></span>
}

// Row density (§13.0): compact by default (or the account preference); the viewer's choice persists in this browser.
export type Density = 'compact' | 'comfortable'
const DENSITY_KEY = 'hub.density'
const densityListeners = new Set<() => void>()
let chosenDensity: Density | null = (() => { try { const v = localStorage.getItem(DENSITY_KEY); return v === 'compact' || v === 'comfortable' ? v : null } catch { return null } })()
const subscribeDensity = (l: () => void) => { densityListeners.add(l); return () => { densityListeners.delete(l) } }

export function useDensity(): [Density, (d: Density) => void] {
  const me = useMe()
  const density = useSyncExternalStore(subscribeDensity, () => chosenDensity) ?? (me.preferences.denseRows === false ? 'comfortable' : 'compact')
  useEffect(() => { document.documentElement.dataset.density = density }, [density])
  const set = (d: Density) => {
    chosenDensity = d
    try { localStorage.setItem(DENSITY_KEY, d) } catch { /* private window: kept for this visit only */ }
    densityListeners.forEach((l) => l())
  }
  return [density, set]
}

export function DensityToggle() {
  const [density, setDensity] = useDensity()
  return <Segmented label={t('density.label')} value={density} onChange={setDensity}
    options={(['compact', 'comfortable'] as const).map((d) => ({ value: d, label: t(`density.${d}`) }))} />
}

/** One choice among a few (view switches, scopes, density): each option is a button with its own pressed state. */
export function Segmented<T extends string>({ label, value, options, onChange }: { label: string; value: T; options: { value: T; label: ReactNode }[]; onChange: (v: T) => void }) {
  return (
    <div role="group" aria-label={label} className="inline-flex rounded-md border border-input bg-muted p-0.5">
      {options.map((o) => (
        <button key={o.value} type="button" aria-pressed={value === o.value} onClick={() => onChange(o.value)}
          className={cn('min-h-[calc(var(--control-row-h)-4px)] rounded-[4px] px-3 text-sm', value === o.value ? 'bg-card font-semibold text-foreground shadow-[0_1px_2px_rgba(25,27,32,0.12)]' : 'text-muted-foreground hover:text-foreground')}>
          {o.label}
        </button>
      ))}
    </div>
  )
}

/** The card that holds a list's filters (§13.0 Filters); labelled fields, quick chips and active tokens go inside. */
export function FilterBar({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn('flex flex-col gap-4 rounded-lg border bg-card p-4 md:p-5', className)}>{children}</div>
}

/** A quick-filter chip: selected shows an outline, a tint and a check, never the tint alone. */
export function ChipToggle({ on, onClick, children }: { on: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button type="button" aria-pressed={on} onClick={onClick}
      className={cn('min-h-(--control-row-h) rounded-md border px-3 text-sm', on ? 'border-primary bg-accent font-semibold text-accent-foreground' : 'border-input bg-card hover:bg-muted')}>
      {on && <span aria-hidden className="mr-1">✓</span>}{children}
    </button>
  )
}

/** Active filters as removable tokens, with Clear (§13.0 Filters). */
export function ActiveFilters({ tokens, onRemove, onClear }: { tokens: { key: string; label: string; value: ReactNode }[]; onRemove: (key: string) => void; onClear: () => void }) {
  if (!tokens.length) return null
  return (
    <div className="flex flex-wrap items-center gap-2 border-t pt-3" role="group" aria-label={t('filters.active')}>
      {tokens.map((tk) => (
        <span key={tk.key} className="inline-flex min-h-(--control-row-h) items-center gap-1 rounded-md bg-accent pl-2.5 text-sm text-accent-foreground">
          {tk.label}: <strong className="font-semibold">{tk.value}</strong>
          <button type="button" onClick={() => onRemove(tk.key)} aria-label={t('filters.remove', { name: tk.label })} className="grid size-(--control-row-h) place-items-center rounded-md hover:bg-accent-foreground/10"><X className="size-4" /></button>
        </span>
      ))}
      <Button variant="link" size="sm" onClick={onClear}>{t('filters.clear')}</Button>
    </div>
  )
}

/** Summary tile (design §3, §6): tinted, dark text, a metric; a link or a filter when it has one destination. Home uses
 *  blue, mint, lavender, peach in that order. A warning travels as a separate labelled chip, never as the tint. */
export function SummaryTile({ label, value, accent, hint, to, onClick, selected, children }: {
  label: ReactNode; value: ReactNode; accent: Accent; hint?: ReactNode; to?: string; onClick?: () => void; selected?: boolean; children?: ReactNode
}) {
  const cls = cn('block rounded-lg border bg-(--acc-bg) px-4 py-3 text-left md:px-5 md:py-4', (to || onClick) && 'hover:brightness-[0.98]', selected && 'border-primary ring-2 ring-primary')
  const body = <>
    <span className="flex items-center justify-between gap-2 text-sm text-(--acc-fg)">{label}{selected && <span aria-hidden>✓</span>}</span>
    <span className="mt-1 block text-[28px]/9 font-semibold tracking-[-0.6px] tabular-nums">{value}</span>
    {hint && <span className="mt-0.5 block text-xs/[18px] text-muted-foreground">{hint}</span>}
    {children}
  </>
  if (to) return <Link to={to} data-accent={accent} className={cls}>{body}</Link>
  if (onClick) return <button type="button" data-accent={accent} aria-pressed={selected} onClick={onClick} className={cls}>{body}</button>
  return <div data-accent={accent} className={cls}>{body}</div>
}

/** A project's or person's stable identity mark (accent derived from the id). */
export function AccentDot({ id, className }: { id?: string | null; className?: string }) {
  return <span aria-hidden data-accent={accentOf(id)} className={cn('inline-block size-2.5 shrink-0 rounded-full bg-(--acc-stripe)', className)} />
}

/** Hand-built register tables (design §6): 14 px text, header on the subtle surface, density-driven cells. Callers add
 *  text-right to numeric columns and wrap the table in TableRegion. */
export const thCls = 'whitespace-nowrap px-(--cell-px) py-3 text-left font-medium text-muted-foreground'
export const tdCls = 'h-(--row-min) px-(--cell-px) py-(--cell-py) align-top'
export function TableRegion({ children, className, ...props }: ComponentProps<'div'>) {
  // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- the region must allow keyboard scrolling when its table is read-only
  return <div tabIndex={0} className={cn('scroll-region overflow-x-auto rounded-lg border bg-card', className)} {...props}>{children}</div>
}

export function Spinner({ className }: { className?: string }) {
  return <Loader2 className={cn('size-4 animate-spin', className)} aria-hidden />
}

/** A consequential action confirmed with its consequence stated, optionally with a reason (§13.0, G-09). */
export function ConfirmDialog({ open, onOpenChange, title, body, confirmLabel, reason, onConfirm, destructive, children, busy, className }: {
  open: boolean; onOpenChange: (o: boolean) => void; title: ReactNode; body?: ReactNode; confirmLabel?: string
  reason?: boolean | 'optional'; onConfirm: (reason: string) => Promise<unknown> | void; destructive?: boolean; children?: ReactNode; busy?: boolean; className?: string
}) {
  const [text, setText] = useState('')
  const [err, setErr] = useState<unknown>(null)
  const [working, setWorking] = useState(false)
  const needs = reason === true
  const valid = !needs || text.trim().length >= 5
  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) { setText(''); setErr(null) } onOpenChange(o) }}>
      <DialogContent className={className}>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {body && <DialogDescription asChild><div className="space-y-2 text-sm">{body}</div></DialogDescription>}
        </DialogHeader>
        {children}
        {reason && (
          <div className="space-y-1.5">
            <Label htmlFor="confirm-reason">{t('common.reason')}{reason === 'optional' && <span className="font-normal text-muted-foreground"> ({t('common.optional')})</span>}</Label>
            <Textarea id="confirm-reason" value={text} onChange={(e) => setText(e.target.value)} rows={3} aria-describedby="confirm-reason-hint" />
            <p id="confirm-reason-hint" className="text-xs/[18px] text-muted-foreground">{t('common.reasonHint')}</p>
          </div>
        )}
        {err != null && <ErrorBanner error={err} />}
        {err instanceof ApiError && Object.keys(err.fieldErrors).length > 0 && (
          <ul className="list-disc space-y-1 pl-6 text-sm text-bad">{Object.values(err.fieldErrors).flat().map((m, i) => <li key={i}>{m}</li>)}</ul>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button variant={destructive ? 'destructive' : 'default'} disabled={!valid || working || busy}
            onClick={async () => {
              setWorking(true); setErr(null)
              try { await onConfirm(text.trim()); setText(''); onOpenChange(false) } catch (e) { setErr(e) } finally { setWorking(false) }
            }}>
            {working && <Spinner />}{working ? t('common.saving') : confirmLabel ?? t('common.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function Field({ label, htmlFor, hint, error, children, className, optional }: { label: ReactNode; htmlFor?: string; hint?: ReactNode; error?: string[] | string; children: ReactNode; className?: string; optional?: boolean }) {
  const errs = typeof error === 'string' ? [error] : error
  return (
    <div className={cn('space-y-1.5', className)}>
      <Label htmlFor={htmlFor}>{label}{optional && <span className="font-normal text-muted-foreground"> ({t('common.optional')})</span>}</Label>
      {children}
      {hint && !errs?.length && <p className="text-xs/[18px] text-muted-foreground">{hint}</p>}
      {errs?.map((e) => <p key={e} className="text-xs/[18px] text-bad" role="alert">{e}</p>)}
    </div>
  )
}

export const selectCls = 'h-(--control-h) w-full rounded-md border border-input bg-card px-3 text-sm outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:border-border disabled:bg-disabled disabled:text-disabled-foreground'
