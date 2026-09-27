import { HelpCircle } from 'lucide-react'
import { Fragment, type ReactNode } from 'react'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { t } from '@/lib/i18n'

export interface Reason { rule?: string; text: string; colour?: string | null; threshold?: string | number | null; values?: Record<string, unknown> | null }

/** "Why?" popover: every computed status, indicator and colour shows the rule, threshold and values that
 *  produced it (constitution II, §4 principle 6, AC-ATT-06). */
export function Why({ reasons, children, title, defaultOpen }: { reasons?: Reason[] | null; children: ReactNode; title?: string; defaultOpen?: boolean }) {
  const list = reasons ?? []
  return (
    <Popover defaultOpen={defaultOpen}>
      <PopoverTrigger asChild>
        <button type="button" className="inline-flex items-center gap-1 rounded-full focus-visible:outline-2" aria-label={`${title ?? ''} ${t('common.why')}`.trim()}>
          {children}
          <HelpCircle className="size-3.5 text-muted-foreground" aria-hidden />
        </button>
      </PopoverTrigger>
      <PopoverContent className="w-96 text-sm" align="start">
        <div className="mb-2 font-semibold">{title ?? t('common.why')}</div>
        {list.length === 0 ? <p className="text-muted-foreground">{t('why.none')}</p> : (
          <ul className="space-y-2">
            {list.map((r, i) => (
              <li key={i} className="border-l-2 pl-2" style={{ borderColor: r.colour === 'Red' ? 'var(--bad)' : r.colour === 'Yellow' ? 'var(--warn)' : 'var(--border)' }}>
                <div>{r.text}</div>
                {(r.rule || r.threshold != null) && (
                  <div className="text-xs text-muted-foreground">{r.rule && <span className="key mr-2">{r.rule}</span>}{r.threshold != null && t('why.threshold', { value: String(r.threshold) })}</div>
                )}
                {r.values && Object.keys(r.values).length > 0 && (
                  <dl className="mt-1 grid grid-cols-[auto_1fr] gap-x-3 text-xs">
                    {Object.entries(r.values).map(([k, v]) => <Fragment key={k}><dt className="text-muted-foreground">{t(`whyv.${k}`)}</dt><dd>{Array.isArray(v) ? v.join(', ') : String(v ?? t('common.dash'))}</dd></Fragment>)}
                  </dl>
                )}
              </li>
            ))}
          </ul>
        )}
      </PopoverContent>
    </Popover>
  )
}
