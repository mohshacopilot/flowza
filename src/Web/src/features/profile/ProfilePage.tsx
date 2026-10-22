import { Typography } from 'antd';
import { useTranslation } from 'react-i18next';

/** Placeholder for "My profile" and change password (T027). */
export function ProfilePage() {
  const { t } = useTranslation();
  return (
    <>
      <Typography.Title level={1}>{t('profile.title')}</Typography.Title>
      <Typography.Paragraph>{t('profile.intro')}</Typography.Paragraph>
    </>
  );
}
