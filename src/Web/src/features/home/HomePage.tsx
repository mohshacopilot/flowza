import { Card, Typography } from 'antd';
import { useTranslation } from 'react-i18next';

/** The home page: today's welcome text (spec 007 C2), on a card like every other screen. */
export function HomePage() {
  const { t } = useTranslation();
  return (
    <>
      <Typography.Title level={1}>{t('home.title')}</Typography.Title>
      <Card variant="borderless">
        <Typography.Paragraph>{t('home.intro')}</Typography.Paragraph>
      </Card>
    </>
  );
}
