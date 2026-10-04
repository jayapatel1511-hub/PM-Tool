import { useState, type FormEvent, type ReactNode } from 'react'
import { AlertTriangle, ArrowRight, Check, ChevronRight, Folder, Info, LockKeyhole, ShieldCheck, Users } from 'lucide-react'
import { ApprovedHero } from './ApprovedHero'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { t } from '@/lib/i18n'
import { accentOf, cn } from '@/lib/utils'
import './entrance.css'

// The public landing is the approved Paper hero; sign-in (design §10) uses the app's type scale, the shell's mint brand mark
// and restrained pastel marks on a white canvas. AuthGate renders these outside the router, so navigation stays on plain links.

type DevUser = { email: string; displayName: string; jobTitle?: string; roles: string[] }

const wrap = 'mx-auto w-full max-w-6xl px-4 md:px-6 xl:px-8'
const eyebrow = 'text-xs/[18px] font-semibold uppercase tracking-[1px] text-muted-foreground'
const heading = 'text-2xl/8 font-semibold tracking-[-0.4px] text-balance md:text-[28px]/9 md:tracking-[-0.6px] lg:text-[32px]/10 lg:tracking-[-0.8px]'
/** A pastel square (set data-accent): the brand "t", a step number, an avatar or an icon tile. */
const mark = 'grid shrink-0 place-items-center bg-(--acc-bg) font-semibold tabular-nums text-(--acc-fg)'
const TILES = [['blue', Folder, '-rotate-4'], ['mint', Users, 'rotate-5'], ['lavender', Check, '-rotate-3']] as const

function Brand() {
  return (
    <a href="/" aria-label={t('entrance.home')} className="inline-flex min-h-11 items-center gap-2.5 rounded-md text-foreground">
      <span aria-hidden data-accent="mint" className={cn(mark, 'size-8 rounded-[10px] text-lg')}>t</span>
      <span className="text-xl font-semibold tracking-[-0.4px]">{t('app.name')}</span>
    </a>
  )
}

/** Skip link, public header and footer around the sign-in screen. */
function PublicShell({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col bg-background text-base/6 text-foreground">
      <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded-md focus:bg-card focus:px-3 focus:py-2 focus:shadow-popover">{t('app.skip')}</a>
      <header className="border-b">
        <div className={cn(wrap, 'flex min-h-16 items-center justify-between gap-4')}>
          <Brand />
          <nav aria-label={t('entrance.publicNav')} className="flex items-center gap-6">
            <Button asChild variant="outline"><a href="/">{t('nav.home')}<ArrowRight aria-hidden /></a></Button>
          </nav>
        </div>
      </header>
      {children}
      <footer className="border-t">
        <div className={cn(wrap, 'flex flex-col items-start gap-2 py-6 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between sm:gap-6')}>
          <Brand /><span>{t('entrance.tagline')}</span><span>{t('entrance.audience')}</span>
        </div>
      </footer>
    </div>
  )
}

/** The approved Paper hero (Jay, 2026-10-03; PR #36), unchanged. */
export function LandingPage() {
  return <ApprovedHero />
}

export function LoginPage({ mode, onSignIn, onLocalSignIn, users, onPick, error, idle }: { mode: 'Development' | 'LocalPassword' | 'Entra'; onSignIn: () => void; onLocalSignIn: (userName: string, password: string) => Promise<void>; users?: DevUser[]; onPick: (email: string) => void; error?: string; idle?: boolean }) {
  const [userName, setUserName] = useState(''), [password, setPassword] = useState(''), [busy, setBusy] = useState(false)
  async function submit(e: FormEvent) { e.preventDefault(); setBusy(true); try { await onLocalSignIn(userName, password); setPassword('') } finally { setBusy(false) } }
  return <PublicShell>
    <main id="main" className="mx-auto grid w-full max-w-5xl flex-1 content-center px-4 py-8 md:grid-cols-2 md:px-6 md:py-16">
      <aside aria-label={t('entrance.about')} className="hidden flex-col rounded-l-xl border border-r-0 bg-sidebar p-8 md:flex lg:p-10">
        <p className={eyebrow}>{t('entrance.storyEyebrow')}</p>
        <h2 className={cn(heading, 'mt-2')}>{t('entrance.storyTitle')}</h2>
        <p className="mt-4 max-w-sm text-muted-foreground">{t('entrance.storyBody')}</p>
        <div aria-hidden className="mt-8 flex gap-3">
          {TILES.map(([accent, Icon, turn]) => <span key={accent} data-accent={accent} className={cn(mark, 'h-14 w-[74px] rounded-[10px]', turn)}><Icon className="size-5" /></span>)}
        </div>
        <p className={cn(eyebrow, 'mt-auto pt-12')}>{t('entrance.storyFoot')}</p>
      </aside>
      <section aria-labelledby="login-title" className="w-full max-w-md justify-self-center rounded-xl border bg-card p-5 sm:p-8 md:max-w-none md:rounded-l-none lg:p-10">
        <span aria-hidden data-accent="blue" className={cn(mark, 'size-10 rounded-[10px]')}><LockKeyhole className="size-5" /></span>
        <h1 id="login-title" className="mt-6 text-2xl/8 font-semibold tracking-[-0.4px] md:text-[28px]/9 md:tracking-[-0.6px]">{t('entrance.welcome')}</h1>
        <p className="mt-2 text-muted-foreground">{t(`entrance.intro.${mode}`)}</p>
        {idle && <p className="mt-4 flex gap-2 rounded-md border bg-muted px-4 py-3 text-sm"><Info aria-hidden className="mt-0.5 size-4 shrink-0 text-muted-foreground" />{t('entrance.idle')}</p>}
        {error && <p role="alert" className="mt-4 flex gap-2 rounded-md border border-bad/30 bg-bad-bg px-4 py-3 text-sm text-bad"><AlertTriangle aria-hidden className="mt-0.5 size-4 shrink-0" />{error}</p>}
        {mode === 'Entra' ? <Button size="lg" className="mt-6 w-full whitespace-normal" onClick={onSignIn}><MicrosoftMark />{t('entrance.microsoft')}</Button> : mode === 'LocalPassword' ?
          <form className="mt-6 space-y-4" onSubmit={submit}>
            <div className="space-y-1.5"><Label htmlFor="review-user">{t('auth.userId')}</Label><Input id="review-user" autoComplete="username" required maxLength={64} value={userName} onChange={e => setUserName(e.target.value)} /></div>
            <div className="space-y-1.5"><Label htmlFor="review-password">{t('auth.password')}</Label><Input id="review-password" type="password" autoComplete="current-password" required value={password} onChange={e => setPassword(e.target.value)} /></div>
            <Button type="submit" size="lg" className="w-full" disabled={busy}>{t('auth.signIn')}<ArrowRight aria-hidden /></Button>
          </form> :
          <div className="mt-6">
            <p className={eyebrow}>{t('entrance.devLabel')}</p>
            {users === undefined ? <p className="mt-2 text-sm text-muted-foreground">{t('entrance.devLoading')}</p> : users.length === 0 ? <p className="mt-2 text-sm text-muted-foreground">{t('entrance.devEmpty')}</p> :
              // The picker has no person id, so avatars key their accent by email (the in-app avatar keys by id).
              <div className="mt-3 max-h-72 divide-y overflow-y-auto rounded-lg border">{users.map(user => (
                <button key={user.email} onClick={() => onPick(user.email)} className="flex min-h-14 w-full items-center gap-3 px-4 py-2 text-left hover:bg-muted focus-visible:-outline-offset-2">
                  <span data-accent={accentOf(user.email)} className={cn(mark, 'size-8 rounded-full text-xs')}>{user.displayName.split(' ').map(part => part[0]).slice(0, 2).join('')}</span>
                  <span className="flex min-w-0 flex-1 flex-col"><strong className="truncate text-sm font-semibold">{user.displayName}</strong><small className="truncate text-xs/[18px] text-muted-foreground">{user.jobTitle || user.email}</small></span>
                  <ChevronRight aria-hidden className="size-4 shrink-0 text-muted-foreground" />
                </button>
              ))}</div>}
          </div>}
        <div className="mt-8 flex items-center justify-center gap-2 border-t pt-4 text-center text-xs/[18px] text-muted-foreground"><ShieldCheck aria-hidden className="size-4 shrink-0" />{t('entrance.assurance')}</div>
      </section>
    </main>
  </PublicShell>
}

function MicrosoftMark() { return <span className="entrance-ms" aria-hidden="true"><i /><i /><i /><i /></span> }
