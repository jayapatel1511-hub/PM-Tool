import { Component, useEffect, useState, type ReactNode } from 'react'
import { useRouteError } from 'react-router'
import { Button } from '@/components/ui/button'
import { t } from '@/lib/i18n'
import { clientErrorReference, reportClientError } from '@/lib/client-errors'

export function RouterErrorPage() {
  return <ErrorPage error={useRouteError()} />
}

export function ErrorPage({ error }: { error?: unknown }) {
  const [reference] = useState(() => clientErrorReference())
  useEffect(() => { reportClientError({ reference, kind: 'render', error }) }, [reference, error])
  useEffect(() => { document.title = 'Tuesday' }, [])
  return <main className="grid min-h-dvh place-items-center bg-background p-6 text-foreground"><div className="max-w-md space-y-4 text-center">
    <h1 className="text-2xl font-semibold">{t('error.pageTitle')}</h1><p className="text-muted-foreground">{t('error.pageBody')}</p>
    <p className="font-mono text-xs text-muted-foreground">{t('error.pageReference', { ref: reference })}</p>
    <div className="flex justify-center gap-2"><Button onClick={() => window.location.reload()}>{t('error.reload')}</Button><Button asChild variant="outline"><a href="/my-work">{t('nav.myWork')}</a></Button></div>
  </div></main>
}

export class RootErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean; error?: unknown }> {
  state: { failed: boolean; error?: unknown } = { failed: false }
  static getDerivedStateFromError(error: unknown) { return { failed: true, error } }
  render() { return this.state.failed ? <ErrorPage error={this.state.error} /> : this.props.children }
}
