import { useQuery } from '@tanstack/react-query'
import type { components } from '@/gen/api'
import { Field, selectCls } from '@/components/hub/common'
import { typedRequest } from '@/lib/api'
import { useFlag } from '@/lib/auth'
import { personNoticeWords } from './person-option-label'
import { fmtDate } from '@/lib/format'
import { t } from '@/lib/i18n'

export type RecordedPersonFacts = NonNullable<components['schemas']['MyStaffPerson']['personFacts']>
export type PersonDateNotices = RecordedPersonFacts['notices']
export type RecordedCertification = RecordedPersonFacts['certifications'][number]
const readTags = () => typedRequest('get', '/api/v1/certification-tags', {})
export function useCertificationTags() {
  const enabled = useFlag('capacity_screens')
  return useQuery({ queryKey: ['certification-tags'], enabled, queryFn: readTags })
}
export function PersonDateNotice({ notices }: { notices?: PersonDateNotices | null }) {
  const enabled = useFlag('capacity_screens')
  if (!enabled || !notices) return null
  const words = personNoticeWords(notices)
  return words.length ? <span className="block whitespace-normal text-xs text-muted-foreground" data-person-date-notice>{words.join(' · ')}</span> : null
}
export function CertificationChip({ record }: { record: RecordedCertification }) {
  const expiry = record.expiryState === 'Unknown' || record.expiryState === 'ExpiresIn' && record.daysToExpiry == null ? t('personFacts.expiryUnknown') : record.expiryState === 'Expired' ? t('personFacts.expired') : record.expiryState === 'ExpiresIn'
    ? t('personFacts.expiresIn', { n: record.daysToExpiry! }) : t('personFacts.expiryDate', { date: fmtDate(record.expiresOn) })
  return <span data-person-certification={record.tagId} className="inline-flex max-w-full flex-wrap items-center gap-x-1 rounded-md border px-2 py-0.5 text-xs font-normal"><span className="break-words">{record.tagName}</span><span className={record.expiryState === 'Expired' ? 'text-bad' : record.expiryState === 'ExpiresIn' ? 'text-warn' : 'text-muted-foreground'}>· {expiry}</span>{!record.tagActive && <span className="text-muted-foreground">· {t('personFacts.inactiveTag')}</span>}</span>
}
export function PersonFactsChips({ facts }: { facts?: RecordedPersonFacts | null }) {
  const enabled = useFlag('capacity_screens')
  if (!enabled || !facts) return null
  if (!facts.notices.personType && !facts.notices.starts && !facts.notices.ends && !facts.certifications.length) return null
  return <div className="mt-1 space-y-1" data-person-facts><PersonDateNotice notices={facts.notices} />{facts.certifications.length > 0 && <div className="flex flex-wrap gap-1">{facts.certifications.map(record => <CertificationChip key={record.id} record={record} />)}</div>}</div>
}
export function CertificationFilter({ value, onChange, id }: { value: string; onChange: (value: string) => void; id: string }) {
  const enabled = useFlag('capacity_screens'), tags = useCertificationTags()
  if (!enabled) return null
  return <Field label={t('personFacts.filter')} htmlFor={id} className="w-full sm:w-64"><select id={id} className={selectCls} value={value} disabled={tags.isPending} onChange={event => onChange(event.target.value)}><option value="">{t('personFacts.allTags')}</option>{(tags.data ?? []).map(tag => <option key={tag.id} value={tag.id}>{tag.name}{tag.isActive ? '' : ` · ${t('personFacts.inactiveTag')}`}</option>)}</select>{tags.error && <span role="alert" className="text-xs text-bad">{t('personFacts.tagsUnavailable')}</span>}</Field>
}
