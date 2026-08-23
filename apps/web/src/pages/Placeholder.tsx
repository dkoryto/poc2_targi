import { useTranslation } from 'react-i18next';
import { EmptyState } from '@/components/ui';

export function Placeholder({ titleKey }: { titleKey: string }) {
  const { t } = useTranslation();
  return (
    <div className="page">
      <div className="page-header">
        <h1>{t(titleKey)}</h1>
      </div>
      <EmptyState title={t('common.inPreparation')} detail={t('common.inPreparationDetail')} />
    </div>
  );
}
