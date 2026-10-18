import { Notice } from '@/components/hub/common'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'
export interface PlannedTarget { date: string; itemKey: string; targetDate: string; rule: string }
export function ScheduleNote({ value }: { value?: PlannedTarget | null }) {
  return value ? <Notice title={t('interaction.latestTitle')}><span>{t('interaction.latestNote', { key: value.itemKey, date: fmtDate(value.date), target: fmtDate(value.targetDate) })} <abbr title={t('interaction.latestWhy')}>{t('interaction.why')}</abbr> · {value.rule}</span></Notice> : null
}
