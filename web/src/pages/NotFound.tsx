import { Link } from 'react-router'
import { Empty, Page } from '@/components/hub/common'
import { Button } from '@/components/ui/button'
import { t } from '@/lib/i18n'

export function NotFound() {
  return (
    <Page title={t('app.notFoundTitle')}>
      <div className="rounded-lg border bg-card">
        <Empty action={<Button asChild variant="outline"><Link to="/my-work">{t('nav.myWork')}</Link></Button>}>{t('app.notFound')}</Empty>
      </div>
    </Page>
  )
}
