import type { PersonDateNotices } from './person-facts'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'

/** Server-derived recorded notices only; dates never determine selection or eligibility here. */
export function personNoticeWords(notices?: PersonDateNotices | null): string[] {
  if (!notices) return []
  return [notices.personType,
    notices.starts && notices.startDate ? t('personFacts.starts', { date: fmtDate(notices.startDate) }) : null,
    notices.ends && notices.endDate ? t('personFacts.ends', { date: fmtDate(notices.endDate) }) : null,
  ].filter((word): word is string => typeof word === 'string' && word.length > 0)
}

/** Explicit owner flag protects old labels even when enabled data remains cached. */
export function personOptionLabel(name: string, notices: PersonDateNotices | null | undefined, enabled: boolean): string {
  if (!enabled) return name
  const words = personNoticeWords(notices)
  return words.length ? `${name} · ${words.join(' · ')}` : name
}
