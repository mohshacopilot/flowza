import { Typography } from 'antd';
import { useTranslation } from 'react-i18next';

export function HomePage() {
  const { t } = useTranslation();
  return (
    <>
      <Typography.Title level={1}>{t('home.title')}</Typography.Title>
      <Typography.Paragraph>{t('home.intro')}</Typography.Paragraph>
    </>
  );
}
