import { Plus, Trash2 } from 'lucide-react'
import { Field, Section, selectCls } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import type { components } from '@/gen/api'
import { t } from '@/lib/i18n'

type RiskSeed = components['schemas']['TemplateRiskIn']

export function TemplateRiskSeedEditor({ rows, editable, busy, onChange }: {
  rows: RiskSeed[]; editable: boolean; busy: boolean; onChange: (rows: RiskSeed[]) => void
}) {
  return <Section title={t('tpl.risks')} count={rows.length} actions={editable && <Button size="sm" variant="outline" disabled={busy}
    onClick={() => onChange([...rows, { ref: `new-${crypto.randomUUID()}`, title: '', probability: null, impact: null, mitigation: null, triggerIndicator: null }])}>
    <Plus className="size-4" />{t('tpl.riskAdd')}</Button>}>
    <div className="space-y-4 p-5">
      <p className="text-sm text-muted-foreground">{t('tpl.riskHint')}</p>
      {rows.length === 0 && <p className="text-sm">{t('tpl.riskNone')}</p>}
      {rows.map((risk, index) => {
        const prefix = `template-risk-${index}`
        const update = (patch: Partial<RiskSeed>) => onChange(rows.map(row => row.ref === risk.ref ? { ...row, ...patch } : row))
        return <fieldset key={risk.ref} className="grid gap-3 rounded-md border p-4 sm:grid-cols-2 lg:grid-cols-4" disabled={!editable || busy}>
          <legend className="px-1 text-xs text-muted-foreground">{t('tpl.risks')} {index + 1}</legend>
          <Field label={t('tpl.riskTitle')} htmlFor={`${prefix}-title`} className="sm:col-span-2">
            <Input id={`${prefix}-title`} value={risk.title} maxLength={300} onChange={event => update({ title: event.target.value })} />
          </Field>
          {(['probability', 'impact'] as const).map(field => <Field key={field} label={t(field === 'probability' ? 'tpl.riskProbability' : 'tpl.riskImpact')} htmlFor={`${prefix}-${field}`}>
            <select id={`${prefix}-${field}`} className={selectCls} value={risk[field] ?? ''} onChange={event => update({ [field]: event.target.value ? Number(event.target.value) : null })}>
              <option value="">{t('tpl.riskChoose')}</option>{[1, 2, 3].map(value => <option key={value} value={value}>{value}</option>)}
            </select>
          </Field>)}
          <Field label={t('tpl.riskMitigation')} htmlFor={`${prefix}-mitigation`} optional className="sm:col-span-2">
            <Textarea id={`${prefix}-mitigation`} value={risk.mitigation ?? ''} maxLength={4000} onChange={event => update({ mitigation: event.target.value || null })} />
          </Field>
          <Field label={t('tpl.riskTrigger')} htmlFor={`${prefix}-trigger`} optional className="sm:col-span-2">
            <Textarea id={`${prefix}-trigger`} value={risk.triggerIndicator ?? ''} maxLength={2000} onChange={event => update({ triggerIndicator: event.target.value || null })} />
          </Field>
          {editable && <Button size="sm" variant="ghost" className="justify-self-start" onClick={() => onChange(rows.filter(row => row.ref !== risk.ref))}><Trash2 className="size-4" />{t('tpl.riskRemove')}</Button>}
        </fieldset>
      })}
    </div>
  </Section>
}

export function TemplateRiskPreview({ rows }: { rows: readonly RiskSeed[] }) {
  return <div className="space-y-1 text-sm" data-template-risk-preview>
    <p>{t('tpl.riskPreview', { count: rows.length })}</p>
    {rows.length > 0 && <ul className="list-inside list-disc text-muted-foreground">{rows.map(row => <li key={row.ref}>{row.title}</li>)}</ul>}
  </div>
}
