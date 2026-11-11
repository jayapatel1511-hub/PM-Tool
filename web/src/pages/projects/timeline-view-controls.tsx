import { Checkbox } from '@/components/ui/checkbox'
import { t } from '@/lib/i18n'
import type { TimelineLayer } from './timeline-view-state'

/** These switches mask existing presentation only; they never alter the canonical Timeline query. */
export function TimelineLayerControls({ choices, selected, onChange }: { choices: readonly TimelineLayer[]; selected: Set<TimelineLayer>; onChange: (layers: TimelineLayer[]) => void }) {
  return <fieldset data-timeline-layer-controls className="space-y-2">
    <legend className="text-sm font-medium">{t('timelineViews.layers')}</legend>
    <div className="flex flex-wrap gap-x-5 gap-y-2">{choices.map(layer => <label key={layer} className="flex min-h-(--control-h) items-center gap-2 text-sm">
      <Checkbox checked={selected.has(layer)} onCheckedChange={on => onChange(choices.filter(value => value === layer ? on === true : selected.has(value)))} />{t(`timelineViews.layer.${layer}`)}
    </label>)}</div>
  </fieldset>
}
