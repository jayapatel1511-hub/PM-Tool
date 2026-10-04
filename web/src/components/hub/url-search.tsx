import { startTransition, useOptimistic, type ComponentProps } from 'react'
import { Input } from '@/components/ui/input'

/** Keep typing synchronous while URL navigation commits in a React transition. External URL changes
 * (Clear, saved views and back/forward) are the source of truth once the transition has settled. */
export function UrlSearchInput({ value, onValueChange, ...props }: Omit<ComponentProps<typeof Input>, 'value' | 'onChange'> & { value: string; onValueChange: (value: string) => void }) {
  const [draft, updateDraft] = useOptimistic(value)
  return <Input {...props} type="search" value={draft} onChange={e => {
    const next = e.target.value
    startTransition(() => { updateDraft(next); onValueChange(next) })
  }} />
}
