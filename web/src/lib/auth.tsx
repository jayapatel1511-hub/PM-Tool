import type { IPublicClientApplication } from '@azure/msal-browser'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { lazy, useEffect, useRef, useState, type ReactNode } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { api, configureApi, get } from '@/lib/api'
import { forgetDrafts } from '@/lib/drafts'
import { setDateContext } from '@/lib/format'
import { t , runtimeText } from '@/lib/i18n'
import { lazyImport } from '@/lib/lazy'
const LandingPage = lazy(() => lazyImport(() => import('@/pages/Entrance').then(module => ({ default: module.LandingPage }))))
const LoginPage = lazy(() => lazyImport(() => import('@/pages/Entrance').then(module => ({ default: module.LoginPage }))))
import { configureClientErrorReporter, disableClientErrorReporter } from '@/lib/client-errors'

// Entra uses PKCE through MSAL. The isolated review deployment uses individual
// password accounts and an HttpOnly cookie until company identity is ready.

export type { Me, Offer } from './auth-context'
export { Ctx, useFlag, useMe, useAuth, useOffer } from './auth-context'
import { Ctx, type Me } from './auth-context'


const DEV_KEY = 'hub.devUser'
const ACTIVITY_KEY = 'hub.lastActivity'
const SIGNIN_KEY = 'hub.signedIn'

interface Config { authMode: 'Development' | 'LocalPassword' | 'Entra' | 'Social'; providers?: string[]; entra: { clientId?: string; tenantId?: string; apiScope?: string } }

export function socialSignInError() {
  const reason = new URLSearchParams(window.location.search).get('authError')
  return reason ? runtimeText(`auth.socialError.${['failed', 'profile', 'email_exists', 'connected', 'inactive'].includes(reason) ? reason : 'failed'}`) : ''
}

let msal: IPublicClientApplication | null = null

export function AuthGate({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState<'loading' | 'public' | 'ready' | 'error'>('loading')
  const [idleNotice, setIdleNotice] = useState(false)
  const [signInError, setSignInError] = useState(socialSignInError)
  const [authMode, setAuthMode] = useState<Config['authMode']>('Entra')
  const [providers, setProviders] = useState<string[]>([])
  const cfg = useRef<Config | null>(null)
  const qc = useQueryClient()

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const c: Config = await (await fetch('/api/v1/config')).json()
        cfg.current = c
        setAuthMode(c.authMode)
        setProviders(c.providers ?? [])
        if (idleExpired(8)) { sessionStorage.removeItem(DEV_KEY); setIdleNotice(true) }
        if (c.authMode === 'Development') {
          configureApi(async (): Promise<Record<string, string>> => { const u = sessionStorage.getItem(DEV_KEY); return u ? { 'X-Dev-User': u } : {} }, () => { sessionStorage.removeItem(DEV_KEY); preparePublicRoute(); setReady('public') })
          if (!cancelled) { const signedIn = !!sessionStorage.getItem(DEV_KEY); if (signedIn) { configureErrorReporter(() => ({ 'X-Dev-User': sessionStorage.getItem(DEV_KEY)! })); finishSignIn() } else preparePublicRoute(); setReady(signedIn ? 'ready' : 'public') }
          return
        }
        if (c.authMode === 'LocalPassword' || c.authMode === 'Social') {
          configureApi(async () => ({}), () => { qc.removeQueries({ queryKey: ['me'] }); preparePublicRoute(); setReady('public') })
          try { await get<Me>('me'); if (!cancelled) { configureErrorReporter(() => ({})); finishSignIn(); setReady('ready') } }
          catch { if (!cancelled) { preparePublicRoute(); setReady('public') } }
          return
        }
        if (!c.entra.clientId || !c.entra.tenantId || !c.entra.apiScope) throw new Error('Entra sign-in is not configured')
        const { createStandardPublicClientApplication } = await lazyImport(() => import('@azure/msal-browser'))
        msal = await createStandardPublicClientApplication({
          auth: { clientId: c.entra.clientId!, authority: `https://login.microsoftonline.com/${c.entra.tenantId}`, redirectUri: window.location.origin },
          cache: { cacheLocation: 'sessionStorage' },
        })
        const result = await msal.handleRedirectPromise()
        const account = result?.account ?? msal.getAllAccounts()[0]
        if (!account) { if (!cancelled) { preparePublicRoute(); setReady('public') }; return }
        msal.setActiveAccount(account)
        configureApi(async (): Promise<Record<string, string>> => {
          try {
            const tok = await msal!.acquireTokenSilent({ scopes: [c.entra.apiScope!], account: msal!.getActiveAccount()! })
            return { Authorization: `Bearer ${tok.accessToken}` }
          } catch {
            await msal!.acquireTokenRedirect({ scopes: [c.entra.apiScope!] })
            return {}
          }
        }, () => { disableClientErrorReporter(); void msal?.loginRedirect({ scopes: [c.entra.apiScope!] }) })
        if (!cancelled) { configureErrorReporter(async () => ({ Authorization: `Bearer ${(await msal!.acquireTokenSilent({ scopes: [c.entra.apiScope!], account: msal!.getActiveAccount()! })).accessToken}` })); finishSignIn(); setReady('ready') }
      } catch {
        if (!cancelled) setReady('error')
      }
    })()
    return () => { cancelled = true }
  }, [qc])

  if (ready === 'loading') return window.location.pathname === '/' ? <LandingPage /> : <Splash text={t('auth.signingIn')} />
  if (ready === 'error') return window.location.pathname === '/' ? <LandingPage /> : <Splash text={t('app.error')} />
  if (ready === 'public') return window.location.pathname === '/' ? <LandingPage /> : <PublicLogin mode={authMode} providers={providers} idle={idleNotice} error={signInError} onSignIn={provider => {
    setSignInError('')
    if (authMode === 'Social' && provider && providers.includes(provider)) {
      window.location.assign(`/api/v1/auth/social/sign-in/${encodeURIComponent(provider)}`)
      return
    }
    msal?.loginRedirect({ scopes: [cfg.current!.entra.apiScope!] }).catch(() => setSignInError('Microsoft sign-in could not start. Please try again.'))
  }} onLocalSignIn={async (userName, password) => {
    try { await api('auth/local/sign-in', { method: 'POST', body: { userName, password } }); qc.removeQueries({ queryKey: ['me'] }); sessionStorage.removeItem(SIGNIN_KEY); touch(); configureErrorReporter(() => ({})); finishSignIn(); setReady('ready') }
    catch { setSignInError(t('auth.localFailed')) }
  }} onPick={email => { sessionStorage.setItem(DEV_KEY, email); configureErrorReporter(() => ({ 'X-Dev-User': sessionStorage.getItem(DEV_KEY)! })); touch(); finishSignIn(); setReady('ready') }} />
  return <Signed onSignOut={() => {
    sessionStorage.removeItem(SIGNIN_KEY)
    if (cfg.current?.authMode === 'Development') { disableClientErrorReporter(); sessionStorage.removeItem(DEV_KEY); window.location.replace('/') }
    else if (cfg.current?.authMode === 'LocalPassword') { api('auth/local/sign-out', { method: 'POST' }).then(() => { disableClientErrorReporter(); qc.removeQueries({ queryKey: ['me'] }); window.location.replace('/') }).catch(() => toast.error(t('auth.signOutFailed'))) }
    else if (cfg.current?.authMode === 'Social') { api('auth/social/sign-out', { method: 'POST' }).then(() => { disableClientErrorReporter(); qc.removeQueries({ queryKey: ['me'] }); window.location.replace('/') }).catch(() => toast.error(t('auth.signOutFailed'))) }
    else void msal?.logoutRedirect().then(() => disableClientErrorReporter())
  }}>{children}</Signed>
}

function configureErrorReporter(headers: () => Promise<Record<string, string>> | Record<string, string>) {
  configureClientErrorReporter({
    send: async body => {
      const response = await fetch('/api/v1/client-errors', {
        method: 'POST', credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json', ...(await headers()) },
        body: JSON.stringify(body),
      })
      if (!response.ok) throw new Error('client error report rejected')
    },
  })
}

const RETURN_TO = 'hub.returnTo'
function preparePublicRoute() {
  disableClientErrorReporter()
  const path = window.location.pathname
  if (path !== '/' && path !== '/login') { sessionStorage.setItem(RETURN_TO, path + window.location.search + window.location.hash); window.history.replaceState(null, '', '/login') }
}
function finishSignIn() {
  const target = sessionStorage.getItem(RETURN_TO)
  sessionStorage.removeItem(RETURN_TO)
  if (window.location.pathname === '/' || window.location.pathname === '/login')
    window.location.replace(target?.startsWith('/') && !target.startsWith('//') && target !== '/login' ? target : '/my-work')
}

function PublicLogin({ mode, providers, onSignIn, onLocalSignIn, onPick, idle, error }: { mode: Config['authMode']; providers: string[]; onSignIn: (provider?: string) => void; onLocalSignIn: (userName: string, password: string) => Promise<void>; onPick: (email: string) => void; idle: boolean; error: string }) {
  const users = useQuery({ queryKey: ['dev-users'], enabled: mode === 'Development', queryFn: async () => {
    const response = await fetch('/api/dev/users')
    if (!response.ok) throw new Error('Could not load test users')
    return response.json() as Promise<{ email: string; displayName: string; jobTitle?: string; roles: string[] }[]>
  } })
  return <LoginPage mode={mode} providers={providers} onSignIn={onSignIn} onLocalSignIn={onLocalSignIn} users={mode === 'Development' ? users.data : undefined} onPick={onPick} idle={idle} error={error || (users.error ? 'Test users could not be loaded. Refresh to try again.' : undefined)} />
}

function Signed({ children, onSignOut }: { children: ReactNode; onSignOut: () => void }) {
  const me = useQuery({ queryKey: ['me'], queryFn: () => get<Me>('me'), staleTime: 60_000 })
  useEffect(() => {
    if (me.data && !sessionStorage.getItem(SIGNIN_KEY)) {
      sessionStorage.setItem(SIGNIN_KEY, '1')
      api('me/sign-in', { method: 'POST' }).catch(() => {})
    }
  }, [me.data])
  useIdleSignOut(me.data?.settings.idleTimeoutHours ?? 8, onSignOut)
  if (me.isPending) return <Splash text={t('app.loading')} />
  if (me.error) return <Splash text={(me.error as any)?.status === 403 ? t('auth.inactive') : t('app.error')} action={<Button variant="outline" onClick={onSignOut}>{t('auth.signOut')}</Button>} />
  setDateContext(me.data.settings.today, me.data.settings.dateFormat)
  // An explicit sign-out leaves no drafts or waiting fields for the next person on this browser; the idle sign-out keeps them (051-D2).
  return <Ctx.Provider value={{ me: me.data, signOut: () => { forgetDrafts(me.data.id); onSignOut() } }}>{children}</Ctx.Provider>
}

function touch() { localStorage.setItem(ACTIVITY_KEY, String(Date.now())) }
function idleExpired(hours: number) {
  const last = Number(localStorage.getItem(ACTIVITY_KEY) ?? 0)
  return last > 0 && Date.now() - last > hours * 3_600_000
}

/** FR-AUTH-05: sign out after the configured idle period (default 8 hours, Q17). */
function useIdleSignOut(hours: number, signOut: () => void) {
  useEffect(() => {
    touch()
    let last = 0
    const onAct = () => { const n = Date.now(); if (n - last > 30_000) { last = n; touch() } }
    const events = ['pointerdown', 'keydown', 'scroll'] as const
    events.forEach((e) => window.addEventListener(e, onAct, { passive: true }))
    const timer = setInterval(() => { if (idleExpired(hours)) signOut() }, 60_000)
    return () => { events.forEach((e) => window.removeEventListener(e, onAct)); clearInterval(timer) }
  }, [hours, signOut])
}

/** Sign-in and loading states on the white canvas the entrance and app content share, with the shell's mint brand mark. */
function Splash({ text, action }: { text: string; action?: ReactNode }) {
  return (
    <div className="grid min-h-dvh place-items-center bg-background p-4 text-foreground">
      <div className="space-y-4 text-center" role="status">
        <div className="flex items-center justify-center gap-2.5 text-xl font-semibold tracking-[-0.4px]">
          <span aria-hidden data-accent="mint" className="grid size-8 shrink-0 place-items-center rounded-[10px] bg-(--acc-bg) text-lg text-(--acc-fg)">t</span>
          {t('app.name')}
        </div>
        <p className="max-w-sm text-base/6 text-muted-foreground">{text}</p>
        {action}
      </div>
    </div>
  )
}
