import { t, tv } from '@/lib/i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown } from 'lucide-react'
import { Link } from 'react-router'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { typedRequest } from '@/lib/api'
import { invalidateChange } from '@/lib/keys'
import { useFlag, useMe } from '@/lib/auth'


/** Collapsed getting-started card for the current person's Today view (§51.2). */
export function OnboardingCard() {
  const enabled = useFlag('onboarding_card')
  const me = useMe()
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const q = useQuery({ queryKey: ['onboarding', me.id], enabled, queryFn: () => typedRequest('get', '/api/v1/me/onboarding', {}) })
  if (!enabled || me.capabilities.readOnly || q.isPending || q.isError || !q.data || q.data.hidden || q.data.done) return null
  const card = q.data
  const hide = async () => { await typedRequest('post', '/api/v1/me/onboarding/hide', { body: {} }); await invalidateChange(qc, 'UserPreferences') }
  return (
    <section aria-label={t('onboarding.title')} className="mb-5 rounded-lg border bg-card">
      <div className="flex min-h-12 items-center gap-3 px-4">
        <button type="button" className="flex min-w-0 flex-1 items-center gap-2 text-left text-sm font-medium" aria-expanded={open} onClick={() => setOpen(v => !v)}>
          <ChevronDown className={open ? 'size-4' : 'size-4 -rotate-90'} aria-hidden />
          <span>{t('onboarding.summary', { done: card.completed, total: card.total })}</span><span className="text-xs text-muted-foreground">{t(open ? 'onboarding.hideSteps' : 'onboarding.showSteps')}</span>
        </button>
        <Button variant="ghost" size="sm" onClick={() => void hide()}>{t('onboarding.hide')}</Button>
      </div>
      {open && <div className="divide-y border-t px-4">
        {card.groups.map(group => <div key={group.role} className="py-3">
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{group.role === 'Everyone' ? t('onboarding.group.Everyone') : tv(group.role)}</h3>
          <ul className="space-y-2">{group.steps.map(step => <li key={group.role + '-' + step.key} className="flex items-center gap-2 text-sm">
            <Checkbox checked={step.done} disabled aria-label={step.label} />
            {step.route && !step.done ? <Link className="hover:underline" to={step.route}>{step.label}</Link> : <span>{step.label}</span>}
          </li>)}</ul>
        </div>)}
      </div>}
    </section>
  )
}
