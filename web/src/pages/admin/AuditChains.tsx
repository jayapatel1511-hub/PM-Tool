import { useMutation, useQuery } from '@tanstack/react-query'
import { CheckCircle2, Download, ShieldCheck } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Empty, ErrorBanner, Loading, Section } from '@/components/hub/common'
import { download, typedRequest } from '@/lib/api'
import { fmtTime } from '@/lib/format'
import { t } from '@/lib/i18n'


/** Packet 048 S04: operators can verify and export the immutable audit-chain records. */
export function AuditChains() {
  const [verifyId, setVerifyId] = useState<string | null>(null)
  const list = useQuery({ queryKey: ['admin', 'audit-chains'], queryFn: () => typedRequest('get', '/api/v1/admin/audit-chains', {}) })
  // Verifying logs an entry and can alert Admins (FR-VER-04), so it runs only on an explicit press, never on focus or remount.
  const verify = useMutation({
    mutationFn: (id: string) => typedRequest('get', '/api/v1/admin/audit-chains/{id}/verify', { params: { id } }),
    onSuccess: () => { void list.refetch() }, // the row's last-verified time changed
  })
  const runVerify = (id: string) => { setVerifyId(id); verify.mutate(id) }
  if (list.isPending) return <Loading rows={5} />
  if (list.error) return <ErrorBanner error={list.error} retry={() => list.refetch()} />
  const chains = list.data
  return <Section title={t('auditChains.title')}>
    {chains.length === 0 ? <Empty>{t('auditChains.empty')}</Empty> : <div className="overflow-x-auto"><table className="w-full text-sm"><caption className="sr-only">{t('auditChains.title')}</caption><thead className="bg-muted"><tr><th className="px-3 py-2 text-left">{t('auditChains.chain')}</th><th className="px-3 py-2 text-left">{t('auditChains.lastRecord')}</th><th className="px-3 py-2 text-right">{t('auditChains.records')}</th><th className="px-3 py-2 text-right">{t('common.actions')}</th></tr></thead><tbody>{chains.map(c => <tr key={c.chainId} className="border-t"><td className="px-3 py-3"><div className="font-medium">{c.chainId}</div><code className="text-xs text-muted-foreground">{c.headHash ?? c.chainId}</code></td><td className="px-3 py-3">{c.lastVerifiedAt ? fmtTime(c.lastVerifiedAt) : '—'}</td><td className="px-3 py-3 text-right tabular-nums">{c.headSeq ?? '—'}</td><td className="px-3 py-3"><div className="flex justify-end gap-2"><Button size="sm" variant="outline" onClick={() => runVerify(c.chainId)} disabled={verify.isPending}><ShieldCheck className="size-4" />{t('auditChains.verify')}</Button><Button size="sm" variant="ghost" onClick={() => download(`admin/audit-chains/${c.chainId}/record?format=json`, `audit-chain-${c.chainId}.json`)}><Download className="size-4" />JSON</Button><Button size="sm" variant="ghost" onClick={() => download(`admin/audit-chains/${c.chainId}/record?format=csv`, `audit-chain-${c.chainId}.csv`)}><Download className="size-4" />CSV</Button></div></td></tr>)}</tbody></table></div>}
    {verifyId && <div className="border-t px-5 py-4">{verify.isPending ? <Loading rows={1} /> : verify.error ? <ErrorBanner error={verify.error} retry={() => runVerify(verifyId)} /> : verify.data && <p role="status" className="flex items-center gap-2 text-sm"><CheckCircle2 className={verify.data.valid ? 'size-4 text-ok' : 'size-4 text-bad'} />{verify.data.valid ? t('auditChains.valid') : t('auditChains.invalid', { seq: verify.data.firstBrokenSeq ?? '—' })}{verify.data.verifiedAt && <span className="text-muted-foreground">· {fmtTime(verify.data.verifiedAt)}</span>}</p>}</div>}
  </Section>
}
