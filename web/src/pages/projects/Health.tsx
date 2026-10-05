import { useState } from 'react'
import { toast } from 'sonner'
import { ConfirmDialog, Field, Spinner, selectCls } from '@/components/hub/common'
import { HealthPill } from '@/components/hub/pills'
import { Why } from '@/components/hub/why'
import { Button } from '@/components/ui/button'
import { useProjectRefresh } from '@/hooks/data'
import { del, post } from '@/lib/api'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
import type { ProjectDetail } from '@/lib/types'
import { ProjectSlots } from './slots'

// Health in the project header (§16.3, §16.4): the reported colour, with the computed colour beside it when they differ,
// each labelled; "Why?" lists the indicators behind the computed colour, and the PM's override note opens from the
// "reported by" line (FR-HLT-02, AC-HLT-03).
function HealthSlot({ p }: { p: ProjectDetail }) {
  const h = p.health
  const [overriding, setOverriding] = useState(false)
  const refresh = useProjectRefresh()
  const differs = h.overrideActive && h.reported !== h.computed
  const by = t('health.overrideBy', { name: h.overrideBy ?? '', date: fmtDate(h.healthOverrideExpiresAt) })
  return (
    <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1">
      {differs && <HealthPill health={h.reported} label={t('health.reported')} />}
      <Why reasons={h.reasons} title={t('health.whyTitle', { health: t(`health.${h.computed}`) })}>
        <HealthPill health={differs ? h.computed : h.reported} label={differs ? t('health.computed') : undefined} />
      </Why>
      {h.overrideActive && (h.healthOverrideNote
        ? <Why reasons={[{ text: h.healthOverrideNote }]} title={by}><span className="text-xs/[18px] text-muted-foreground">{by}</span></Why>
        : <span className="text-xs/[18px] text-muted-foreground">{by}</span>)}
      {p.permissions.healthOverride.ok && p.status === 'Active' && (
        <Button size="xs" variant="link" className="min-h-6 px-1" onClick={() => setOverriding(true)}>{h.overrideActive ? t('health.changeOverride') : t('health.override')}</Button>
      )}
      {overriding && <OverrideDialog p={p} onClose={(ok) => { setOverriding(false); if (ok) refresh(p.id) }} />}
    </span>
  )
}

function OverrideDialog({ p, onClose }: { p: ProjectDetail; onClose: (ok: boolean) => void }) {
  const [health, setHealth] = useState(p.health.healthOverride ?? (p.health.computed === 'Red' ? 'Yellow' : 'Green'))
  const [clearing, setClearing] = useState(false)
  return (
    <ConfirmDialog open onOpenChange={(o) => !o && onClose(false)} title={t('health.overrideTitle')} reason confirmLabel={t('health.override')}
      body={t('health.overrideBody', { computed: t(`health.${p.health.computed}`) })}
      onConfirm={async (note) => { await post(`projects/${p.id}/health-override`, { health, note, rowVersion: p.rowVersion }); toast.success(t('health.overrideSaved')); onClose(true) }}>
      <Field label={t('health.reported')} htmlFor="ho-health">
        <select id="ho-health" className={selectCls} value={health} onChange={(e) => setHealth(e.target.value)}>
          {['Green', 'Yellow', 'Red'].map((x) => <option key={x} value={x}>{t(`health.${x}`)}</option>)}
        </select>
      </Field>
      {p.health.overrideActive && (
        <div><Button variant="outline" disabled={clearing} onClick={async () => {
          setClearing(true)
          try { await del(`projects/${p.id}/health-override`, undefined, p.rowVersion); toast.success(t('health.overrideCleared')); onClose(true) }
          catch (e) { toast.error((e as Error).message) } finally { setClearing(false) }
        }}>{clearing && <Spinner />}{t('health.clearOverride')}</Button></div>
      )}
    </ConfirmDialog>
  )
}

ProjectSlots.Health = HealthSlot
