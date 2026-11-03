import { useId, type ReactNode } from 'react'
import { Link } from 'react-router'
import { Activity, AlertCircle, Briefcase, CalendarClock, CheckSquare, Users } from 'lucide-react'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { t } from '@/lib/i18n'
import { cn } from '@/lib/utils'
import './count-card.css'

const ICONS = { blue: Briefcase, violet: CheckSquare, red: AlertCircle, amber: CalendarClock, green: Activity, teal: Users }

/** One destination for a count; a ratio may supply its explicit numerator/denominator links as children. */
export function CountCard({ label, value, valueText, where, definition, to, tone = 'blue', trend, children }: {
  label: ReactNode; value: ReactNode; valueText?: string; where?: ReactNode; definition?: ReactNode; to?: string
  tone?: keyof typeof ICONS; trend?: ReactNode; children?: ReactNode
}) {
  const id = useId()
  const Icon = ICONS[tone]
  const alert = tone === 'red' && typeof value === 'number' && value > 0
  const labelledBy = `${id}-label ${id}-value${where ? ` ${id}-where` : ''}`
  const tip = (el: React.ReactElement) => definition ? <Tooltip><TooltipTrigger asChild>{el}</TooltipTrigger><TooltipContent className="max-w-72">{definition}</TooltipContent></Tooltip> : el
  const labelContent = <span id={`${id}-label`} className={cn('count-card__label', definition && 'underline decoration-dotted underline-offset-4')}>{label}</span>
  const content = <>
    {trend ? <span className="count-card__top"><span className="count-card__icon" aria-hidden><Icon className="size-4" /></span>{trend}</span>
      : <span className="count-card__icon" aria-hidden><Icon className="size-4" /></span>}
    {to ? labelContent : definition ? tip(<button type="button" className="self-start rounded-sm text-left">{labelContent}</button>) : labelContent}
    <span id={`${id}-value`} aria-label={valueText} className="count-card__value" data-alert={alert || undefined}>{value}</span>
    {where && <span id={`${id}-where`} className="count-card__where">{where}</span>}
    {alert && <span className="count-card__meta"><span aria-hidden>■ </span>{t('dash.attention')}</span>}
    {children}
  </>
  const cls = cn('count-card', `count-card--${tone}`, to && 'count-card--link')
  return to ? tip(<Link data-count-card="" to={to} className={cls} aria-labelledby={labelledBy}>{content}</Link>)
    : <article data-count-card="" className={cls} aria-labelledby={labelledBy}>{content}</article>
}
