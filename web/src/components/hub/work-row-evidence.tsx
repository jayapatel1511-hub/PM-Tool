import { useFlag } from '@/lib/auth'
import { WorkActivityComments, WorkNewBadge, type WorkSeenFields } from './work-seen'

/** Explicit canonical row identity plus the server's evidence; labels/UUID-looking text never infer identity. */
export function WorkRowEvidence({ row, itemType, itemId, itemKey, projectNumber, sourceKey }: {
  row: object; itemType: string; itemId: string; itemKey: string; projectNumber: string; sourceKey: string
}) {
  const enabled = useFlag('work_screens'), facts = row as WorkSeenFields
  if (!enabled || !facts.seen && !facts.workActivity) return null
  return <div className="flex min-w-0 flex-wrap items-center gap-2">
    <WorkNewBadge seen={facts.seen} />
    {facts.workActivity && <WorkActivityComments itemType={itemType} itemId={itemId} itemKey={itemKey}
      projectNumber={projectNumber} fact={facts.workActivity} focusKey={sourceKey} />}
  </div>
}
