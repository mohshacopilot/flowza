import { Typography } from 'antd';
import { useTranslation } from 'react-i18next';

/** Placeholder for the user administration screens (T026). */
export function UsersPage() {
  const { t } = useTranslation();
  return (
    <>
      <Typography.Title level={1}>{t('users.title')}</Typography.Title>
      <Typography.Paragraph>{t('users.intro')}</Typography.Paragraph>
    </>
  );
}
