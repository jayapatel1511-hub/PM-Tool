import { useEffect, useSyncExternalStore } from 'react'
import { useBlocker } from 'react-router'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import type { SafeForm } from '@/hooks/drafts'
import { fmtTime } from '@/lib/format'
import { discardUnsaved, hasUnsaved, leaveQuestion } from '@/lib/drafts'
import { t } from '@/lib/i18n'

/** FR-SAV-03: the one-line notice of a restored draft; Discard puts the form back to empty. */
export function DraftNotice({ form }: { form: Pick<SafeForm, 'restoredAt' | 'discard'> }) {
  if (!form.restoredAt) return null
  return (
    <p role="status" className="rounded-md bg-muted px-3 py-2 text-sm">
      {t('draft.restored', { time: fmtTime(form.restoredAt) })} · <button type="button" className="font-medium text-primary underline underline-offset-4 hover:text-foreground" onClick={form.discard}>{t('draft.discard')}</button>
    </p>
  )
}

/** FR-SAV-04: "Keep editing or discard?" for every way out of a form with unsaved input: closing a dialog, going to another page
 *  and the Reload action of the build-update notice (FR-SAV-06). Mounted once, in the page frame. */
export function LeaveHost() {
  const ask = useSyncExternalStore(leaveQuestion.subscribe, leaveQuestion.get)
  const blocker = useBlocker(hasUnsaved)
  useEffect(() => { if (blocker.state === 'blocked') leaveQuestion.show({ proceed: () => { discardUnsaved(); blocker.proceed() }, cancel: blocker.reset }) }, [blocker])
  return (
    <Dialog open={!!ask} onOpenChange={(open) => !open && leaveQuestion.answer(false)}>
      <DialogContent showCloseButton={false} className="sm:max-w-md">
        <DialogHeader><DialogTitle>{t('draft.leaveTitle')}</DialogTitle><DialogDescription>{t('draft.leaveBody')}</DialogDescription></DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => leaveQuestion.answer(true)}>{t('draft.leave')}</Button>
          <Button onClick={() => leaveQuestion.answer(false)}>{t('draft.keep')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
