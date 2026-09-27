import { createStandardPublicClientApplication, type IPublicClientApplication } from '@azure/msal-browser'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { api, configureApi, get } from '@/lib/api'
import { setDateContext } from '@/lib/format'
import { t } from '@/lib/i18n'

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
    defaultWeeklyCapacityHours: number; workingDaysEnabled: boolean
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
  const [ready, setReady] = useState<'loading' | 'dev-pick' | 'local-login' | 'ready' | 'error'>('loading')
  const [idleNotice, setIdleNotice] = useState(false)
  const cfg = useRef<Config | null>(null)
  const qc = useQueryClient()

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const c: Config = await (await fetch('/api/v1/config')).json()
        cfg.current = c
        if (idleExpired(8)) { sessionStorage.removeItem(DEV_KEY); setIdleNotice(true) }
        if (c.authMode === 'Development') {
          configureApi(async (): Promise<Record<string, string>> => { const u = sessionStorage.getItem(DEV_KEY); return u ? { 'X-Dev-User': u } : {} }, () => { sessionStorage.removeItem(DEV_KEY); setReady('dev-pick') })
          if (!cancelled) setReady(sessionStorage.getItem(DEV_KEY) ? 'ready' : 'dev-pick')
          return
        }
        if (c.authMode === 'LocalPassword') {
          configureApi(async () => ({}), () => { qc.removeQueries({ queryKey: ['me'] }); setReady('local-login') })
          try { await get<Me>('me'); if (!cancelled) setReady('ready') }
          catch { if (!cancelled) setReady('local-login') }
          return
        }
        msal = await createStandardPublicClientApplication({
          auth: { clientId: c.entra.clientId!, authority: `https://login.microsoftonline.com/${c.entra.tenantId}`, redirectUri: window.location.origin },
          cache: { cacheLocation: 'sessionStorage' },
        })
        const result = await msal.handleRedirectPromise()
        const account = result?.account ?? msal.getAllAccounts()[0]
        if (!account) { await msal.loginRedirect({ scopes: [c.entra.apiScope!] }); return }
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
        if (!cancelled) setReady('ready')
      } catch {
        if (!cancelled) setReady('error')
      }
    })()
    return () => { cancelled = true }
  }, [qc])

  if (ready === 'loading') return <Splash text={t('auth.signingIn')} />
  if (ready === 'error') return <Splash text={t('app.error')} />
  if (ready === 'dev-pick') return <DevPicker idle={idleNotice} onPick={(email) => { sessionStorage.setItem(DEV_KEY, email); touch(); setReady('ready') }} />
  if (ready === 'local-login') return <LocalLogin idle={idleNotice} onSignedIn={() => { qc.removeQueries({ queryKey: ['me'] }); touch(); setReady('ready') }} />
  return <Signed onSignOut={() => {
    sessionStorage.removeItem(SIGNIN_KEY)
    if (cfg.current?.authMode === 'Development') { sessionStorage.removeItem(DEV_KEY); setReady('dev-pick') }
    else if (cfg.current?.authMode === 'LocalPassword') { api('auth/local/sign-out', { method: 'POST' }).catch(() => {}).finally(() => { qc.removeQueries({ queryKey: ['me'] }); setReady('local-login') }) }
    else msal?.logoutRedirect()
  }}>{children}</Signed>
}

function LocalLogin({ onSignedIn, idle }: { onSignedIn: () => void; idle: boolean }) {
  const [userName, setUserName] = useState(''), [password, setPassword] = useState(''), [busy, setBusy] = useState(false), [error, setError] = useState(false)
  async function submit(e: React.FormEvent) {
    e.preventDefault(); setBusy(true); setError(false)
    try { await api('auth/local/sign-in', { method: 'POST', body: { userName, password } }); setPassword(''); onSignedIn() }
    catch { setError(true); setPassword('') }
    finally { setBusy(false) }
  }
  return <main className="grid min-h-screen place-items-center bg-frame p-4"><form onSubmit={submit} className="w-full max-w-sm space-y-3 rounded-xl bg-card p-6 shadow-xl">
    <h1 className="text-xl font-semibold">{t('auth.localTitle')}</h1>
    <p className="text-sm text-muted-foreground">{t('auth.localHint')}</p>
    {idle && <p className="rounded-md bg-warn-bg p-2 text-sm text-warn">{t('auth.idle', { hours: 8 })}</p>}
    <div className="space-y-1"><label className="block text-sm font-medium" htmlFor="local-user">{t('auth.userId')}</label><Input id="local-user" autoComplete="username" required maxLength={64} value={userName} onChange={e => setUserName(e.target.value)} /></div>
    <div className="space-y-1"><label className="block text-sm font-medium" htmlFor="local-password">{t('auth.password')}</label><Input id="local-password" type="password" autoComplete="current-password" required value={password} onChange={e => setPassword(e.target.value)} /></div>
    {error && <p role="alert" className="text-sm text-destructive">{t('auth.localFailed')}</p>}
    <Button className="w-full" disabled={busy} type="submit">{t('auth.signIn')}</Button>
  </form></main>
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

function Splash({ text, action }: { text: string; action?: ReactNode }) {
  return (
    <div className="grid min-h-screen place-items-center bg-frame text-frame-foreground">
      <div className="space-y-4 text-center" role="status">
        <div className="text-lg font-semibold">{t('app.name')}</div>
        <p className="text-frame-muted">{text}</p>
        {action}
      </div>
    </div>
  )
}

function DevPicker({ onPick, idle }: { onPick: (email: string) => void; idle: boolean }) {
  const users = useQuery({ queryKey: ['dev-users'], queryFn: async () => (await fetch('/api/dev/users')).json() as Promise<{ email: string; displayName: string; jobTitle?: string; roles: string[] }[]> })
  return (
    <main className="grid min-h-screen place-items-center bg-frame p-4">
      <div className="w-full max-w-lg rounded-xl bg-card p-6 shadow-xl">
        <h1 className="text-xl font-semibold">{t('auth.devTitle')}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t('auth.devHint')}</p>
        {idle && <p className="mt-3 rounded-md bg-warn-bg p-2 text-sm text-warn">{t('auth.idle', { hours: 8 })}</p>}
        <ul className="mt-4 divide-y rounded-md border">
          {(users.data ?? []).map((u) => (
            <li key={u.email}>
              <button className="flex w-full items-center justify-between px-3 py-2 text-left hover:bg-muted focus-visible:bg-muted" onClick={() => onPick(u.email)}>
                <span><span className="font-medium">{u.displayName}</span> <span className="text-muted-foreground">· {u.jobTitle}</span></span>
                <span className="text-xs text-muted-foreground">{u.roles.map((r) => t(`role.${r}`)).join(', ') || t('role.StandardUser')}</span>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </main>
  )
}
