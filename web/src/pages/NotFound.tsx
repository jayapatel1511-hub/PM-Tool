import { Empty, Page } from '@/components/hub/common'
import { t } from '@/lib/i18n'

export function NotFound() {
  return <Page title={t('common.dash')}><Empty>{t('app.notFound')}</Empty></Page>
}
