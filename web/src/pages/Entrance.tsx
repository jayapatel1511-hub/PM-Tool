import { useState, type FormEvent } from 'react'
import { ArrowRight, LockKeyhole, ShieldCheck } from 'lucide-react'
import './entrance.css'
import { ApprovedHero } from './ApprovedHero'

type DevUser = { email: string; displayName: string; jobTitle?: string; roles: string[] }

function Brand() {
  return <a className="entrance-brand" href="/" aria-label="Tuesday home">tuesday<span aria-hidden="true">.</span></a>
}

function Header() {
  const login = window.location.pathname === '/login'
  return <header className="entrance-header"><div className="entrance-wrap entrance-header-inner">
    <Brand />
    <nav aria-label="Public navigation">
      {!login && <a className="entrance-nav-link" href="/#approach">The approach</a>}
      <a className="entrance-header-cta" href={login ? '/' : '/login'}>{login ? 'Home' : 'Sign in'} <ArrowRight size={15} aria-hidden="true" /></a>
    </nav>
  </div></header>
}

function Footer() {
  return <footer className="entrance-footer"><div className="entrance-wrap entrance-footer-inner"><Brand /><span>Project work, in one place.</span><span>For authorized team members</span></div></footer>
}

export function LandingPage() {
  return <ApprovedHero />
}

export function LoginPage({ mode, onSignIn, onLocalSignIn, users, onPick, error, idle }: { mode: 'Development' | 'LocalPassword' | 'Entra'; onSignIn: () => void; onLocalSignIn: (userName: string, password: string) => Promise<void>; users?: DevUser[]; onPick: (email: string) => void; error?: string; idle?: boolean }) {
  const [userName, setUserName] = useState(''), [password, setPassword] = useState(''), [busy, setBusy] = useState(false)
  async function submit(e: FormEvent) { e.preventDefault(); setBusy(true); try { await onLocalSignIn(userName, password); setPassword('') } finally { setBusy(false) } }
  return <div className="entrance entrance-login">
    <a className="entrance-skip" href="#main">Skip to content</a><Header />
    <main id="main" className="entrance-login-main">
      <aside className="entrance-login-story" aria-label="About Tuesday">
        <span className="entrance-login-story-label">TUESDAY / PROJECT WORK</span>
        <h2>Pick up where<br />the work left off.</h2>
        <p>The next action, the source behind it, and the people involved. In one shared workspace.</p>
        <span className="entrance-login-story-foot">BUILT FOR MULTIDISCIPLINARY TEAMS</span>
      </aside>
      <section className="entrance-login-card" aria-labelledby="login-title">
        <span className="entrance-login-card-icon"><LockKeyhole size={19} /></span>
        <h1 id="login-title">Welcome back</h1>
        <p>{mode === 'Entra' ? 'Sign in with your corporate Microsoft account to continue to Tuesday.' : mode === 'LocalPassword' ? 'Use your individual review account to open the synthetic preview.' : 'Choose a development test user to preview Tuesday.'}</p>
        {idle && <p className="entrance-login-alert">Your previous session ended after inactivity. Sign in again to continue.</p>}
        {error && <p className="entrance-login-alert" role="alert">{error}</p>}
        {mode === 'Entra' ? <button className="entrance-button entrance-login-button" onClick={onSignIn}><MicrosoftMark /> Continue with Microsoft <ArrowRight size={18} /></button> : mode === 'LocalPassword' ?
          <form className="entrance-local-form" onSubmit={submit}><label htmlFor="review-user">User ID</label><input id="review-user" autoComplete="username" required maxLength={64} value={userName} onChange={e => setUserName(e.target.value)} /><label htmlFor="review-password">Password</label><input id="review-password" type="password" autoComplete="current-password" required value={password} onChange={e => setPassword(e.target.value)} /><button className="entrance-button entrance-login-button" type="submit" disabled={busy}>Sign in <ArrowRight size={18} aria-hidden="true" /></button></form> :
          <div className="entrance-dev-users"><p className="entrance-dev-label">Development preview</p>{users === undefined ? <p>Loading test users…</p> : users.length === 0 ? <p>No test users are available.</p> : <div className="entrance-dev-list">{users.map(user => <button key={user.email} onClick={() => onPick(user.email)}><span className="entrance-dev-avatar">{user.displayName.split(' ').map(part => part[0]).slice(0, 2).join('')}</span><span><strong>{user.displayName}</strong><small>{user.jobTitle || user.email}</small></span><ArrowRight size={16} /></button>)}</div>}</div>}
        <div className="entrance-login-assurance"><ShieldCheck size={14} /> Only authorised team members can access project work.</div>
      </section>
    </main><Footer />
  </div>
}

function MicrosoftMark() { return <span className="entrance-ms" aria-hidden="true"><i /><i /><i /><i /></span> }
