import type { ComponentType } from 'react'
import { HealthPill } from '@/components/hub/pills'
import { t } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'

// Header parts filled in by later packets: the health "Why?" popover (005), its override action and the follow control (006).
export const ProjectSlots: { Health: ComponentType<{ p: ProjectDetail }>; Override: ComponentType<{ p: ProjectDetail }>; Follow: ComponentType<{ p: ProjectDetail }> } = {
  Health: ({ p }: { p: ProjectDetail }) => {
    const differs = p.health.overrideActive && p.health.reported !== p.health.computed
    return (
      <span className="inline-flex flex-wrap items-center gap-1">
        <HealthPill health={p.health.reported} label={differs ? t('health.reported') : undefined} />
        {differs && <HealthPill health={p.health.computed} label={t('health.computed')} />}
      </span>
    )
  },
  Override: () => null,
  Follow: () => null,
}
