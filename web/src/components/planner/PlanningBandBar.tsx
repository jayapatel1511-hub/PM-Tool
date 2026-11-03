import { t } from '@/lib/i18n'
import { useFlag } from '@/lib/auth'
import { confidenceLabel, hoursLabel } from './labels'
import './planner-semantic.css'

export function PlanningBandBar({ confirmed, expected, possible, capacity, partial, provisional }: { confirmed: number; expected: number; possible: number; capacity: number; partial?: boolean; provisional?: boolean }) {
  const visual = useFlag('visual_system')
  const scale = Math.max(confirmed + expected + possible, capacity, 1)
  const label = [t('planner.summaryBand', { confirmed: hoursLabel(confirmed), expected: hoursLabel(expected), possible: hoursLabel(possible), capacity: hoursLabel(capacity) }), partial && t('planner.partialHint'), provisional && t('capacity.provisionalAvailability')].filter(Boolean).join('; ')
  if (visual) return <figure className="planner-semantic-band">
    <div role="img" aria-label={label} title={label} data-planning-summary className="relative h-3 rounded-sm border bg-muted">
      <div className="flex h-full overflow-hidden rounded-sm">
        <span data-planning-confidence="Confirmed" className="planner-semantic-confirmed h-full shrink-0" style={{ width: `${confirmed / scale * 100}%` }} />
        <span hidden={expected === 0} data-planning-confidence="Expected" className="planner-semantic-expected h-full shrink-0" style={{ width: `${expected / scale * 100}%` }} />
        <span hidden={possible === 0} data-planning-confidence="Possible" className="planner-semantic-possible-band h-full shrink-0" style={{ width: `${possible / scale * 100}%` }} />
      </div>
      <span aria-hidden className="absolute -top-0.5 -bottom-0.5 w-0.5 bg-foreground" style={{ left: `calc(${capacity / scale * 100}% - 1px)` }} />
    </div>
    <figcaption className="planner-semantic-band-values" aria-label={label}>
      <span title={confidenceLabel('Confirmed')}>{hoursLabel(confirmed)}</span> / <span title={confidenceLabel('Expected')}>{hoursLabel(expected)}</span> / <span title={confidenceLabel('Possible')}>{hoursLabel(possible)}</span>
    </figcaption>
  </figure>
  return <div role="img" aria-label={label} title={label} data-planning-summary className="relative my-1 h-3 min-w-28 rounded-sm border bg-muted">
    <div className="flex h-full overflow-hidden rounded-sm"><span className="h-full shrink-0 bg-ok" style={{ width: `${confirmed / scale * 100}%` }} /><span className="h-full shrink-0 bg-warn" style={{ width: `${expected / scale * 100}%` }} /><span className="h-full shrink-0 bg-(--acc-stripe)" style={{ width: `${possible / scale * 100}%` }} /></div>
    <span aria-hidden className="absolute -top-0.5 -bottom-0.5 w-0.5 bg-foreground" style={{ left: `calc(${capacity / scale * 100}% - 1px)` }} />
  </div>
}
