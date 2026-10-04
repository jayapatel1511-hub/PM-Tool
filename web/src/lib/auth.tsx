import { createStandardPublicClientApplication, type IPublicClientApplication } from '@azure/msal-browser'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { api, configureApi, get } from '@/lib/api'
import { setDateContext } from '@/lib/format'
import { t } from '@/lib/i18n'
import { LandingPage, LoginPage } from '@/pages/Entrance'

// Entra uses PKCE through MSAL. The isolated review deployment uses individual
// password accounts and an HttpOnly cookie until company identity is ready.

export interface Me {
  id: string
  displayName: string
  email: string
  jobTitle?: string
  officeId?: string
  supervisorId?: string
  isTemplateEditor: boolean
  roles: { role: string; source: string }[]
  systemRoles: string[]
  capabilities: {
    createProject: boolean; createTask: boolean; portfolio: boolean; workload: boolean; staff: boolean; allStaff: boolean
    admin: boolean; templates: boolean; readOnly: boolean; directReports: number
  }
  settings: {
    dateFormat: string; orgTimeZone: string; today: string; idleTimeoutHours: number; restrictedProjectsEnabled: boolean
    allowSelfReview: boolean; taskDueSoonDays: number; milestoneApproachingDays: number; chainDepthLimit: number
    defaultWeeklyCapacityHours: number; coordinationLookaheadWeeks: number; workingDaysEnabled: boolean
  }
  preferences: { digestEnabled: boolean; digestTimeLocal?: string; denseRows: boolean; weeklySummaryEnabled: boolean }
}

interface AuthState { me: Me; signOut: () => void }
const Ctx = createContext<AuthState | null>(null)
export const useMe = () => useContext(Ctx)!.me
export const useAuth = () => useContext(Ctx)!

const DEV_KEY = 'hub.devUser'
const ACTIVITY_KEY = 'hub.lastActivity'
const SIGNIN_KEY = 'hub.signedIn'

interface Config { authMode: 'Development' | 'LocalPassword' | 'Entra'; entra: { clientId?: string; tenantId?: string; apiScope?: string } }

let msal: IPublicClientApplication | null = null

export function AuthGate({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState<'loading' | 'public' | 'ready' | 'error'>('loading')
  const [idleNotice, setIdleNotice] = useState(false)
  const [signInError, setSignInError] = useState('')
  const [authMode, setAuthMode] = useState<Config['authMode']>('Entra')
  const cfg = useRef<Config | null>(null)
  const qc = useQueryClient()

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const c: Config = await (await fetch('/api/v1/config')).json()
        cfg.current = c
        setAuthMode(c.authMode)
        if (idleExpired(8)) { sessionStorage.removeItem(DEV_KEY); setIdleNotice(true) }
        if (c.authMode === 'Development') {
          configureApi(async (): Promise<Record<string, string>> => { const u = sessionStorage.getItem(DEV_KEY); return u ? { 'X-Dev-User': u } : {} }, () => { sessionStorage.removeItem(DEV_KEY); preparePublicRoute(); setReady('public') })
          if (!cancelled) { const signedIn = !!sessionStorage.getItem(DEV_KEY); if (signedIn) finishSignIn(); else preparePublicRoute(); setReady(signedIn ? 'ready' : 'public') }
          return
        }
        if (c.authMode === 'LocalPassword') {
          configureApi(async () => ({}), () => { qc.removeQueries({ queryKey: ['me'] }); preparePublicRoute(); setReady('public') })
          try { await get<Me>('me'); if (!cancelled) { finishSignIn(); setReady('ready') } }
          catch { if (!cancelled) { preparePublicRoute(); setReady('public') } }
          return
        }
        if (!c.entra.clientId || !c.entra.tenantId || !c.entra.apiScope) throw new Error('Entra sign-in is not configured')
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
        }, () => msal?.loginRedirect({ scopes: [c.entra.apiScope!] }))
        if (!cancelled) { finishSignIn(); setReady('ready') }
      } catch {
        if (!cancelled) setReady('error')
      }
    })()
    return () => { cancelled = true }
  }, [qc])

  if (ready === 'loading') return window.location.pathname === '/' ? <LandingPage /> : <Splash text={t('auth.signingIn')} />
  if (ready === 'error') return window.location.pathname === '/' ? <LandingPage /> : <Splash text={t('app.error')} />
  if (ready === 'public') return window.location.pathname === '/' ? <LandingPage /> : <PublicLogin mode={authMode} idle={idleNotice} error={signInError} onSignIn={() => {
    setSignInError('')
    msal?.loginRedirect({ scopes: [cfg.current!.entra.apiScope!] }).catch(() => setSignInError('Microsoft sign-in could not start. Please try again.'))
  }} onLocalSignIn={async (userName, password) => {
    try { await api('auth/local/sign-in', { method: 'POST', body: { userName, password } }); qc.removeQueries({ queryKey: ['me'] }); sessionStorage.removeItem(SIGNIN_KEY); touch(); finishSignIn(); setReady('ready') }
    catch { setSignInError(t('auth.localFailed')) }
  }} onPick={email => { sessionStorage.setItem(DEV_KEY, email); touch(); finishSignIn(); setReady('ready') }} />
  return <Signed onSignOut={() => {
    sessionStorage.removeItem(SIGNIN_KEY)
    if (cfg.current?.authMode === 'Development') { sessionStorage.removeItem(DEV_KEY); window.location.replace('/') }
    else if (cfg.current?.authMode === 'LocalPassword') { api('auth/local/sign-out', { method: 'POST' }).catch(() => {}).finally(() => { qc.removeQueries({ queryKey: ['me'] }); window.location.replace('/') }) }
    else msal?.logoutRedirect()
  }}>{children}</Signed>
}

const RETURN_TO = 'hub.returnTo'
function preparePublicRoute() {
  const path = window.location.pathname
  if (path !== '/' && path !== '/login') { sessionStorage.setItem(RETURN_TO, path + window.location.search + window.location.hash); window.history.replaceState(null, '', '/login') }
}
function finishSignIn() {
  const target = sessionStorage.getItem(RETURN_TO)
  sessionStorage.removeItem(RETURN_TO)
  if (window.location.pathname === '/' || window.location.pathname === '/login')
    window.location.replace(target?.startsWith('/') && !target.startsWith('//') && target !== '/login' ? target : '/my-work')
}

function PublicLogin({ mode, onSignIn, onLocalSignIn, onPick, idle, error }: { mode: Config['authMode']; onSignIn: () => void; onLocalSignIn: (userName: string, password: string) => Promise<void>; onPick: (email: string) => void; idle: boolean; error: string }) {
  const users = useQuery({ queryKey: ['dev-users'], enabled: mode === 'Development', queryFn: async () => {
    const response = await fetch('/api/dev/users')
    if (!response.ok) throw new Error('Could not load test users')
    return response.json() as Promise<{ email: string; displayName: string; jobTitle?: string; roles: string[] }[]>
  } })
  return <LoginPage mode={mode} onSignIn={onSignIn} onLocalSignIn={onLocalSignIn} users={mode === 'Development' ? users.data : undefined} onPick={onPick} idle={idle} error={error || (users.error ? 'Test users could not be loaded. Refresh to try again.' : undefined)} />
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
  return <Ctx.Provider value={{ me: me.data, signOut: onSignOut }}>{children}</Ctx.Provider>
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
