import { Descriptions, Tag, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { describeApiError } from '../../shared/api/errorMessages';
import { useGetMyProfile } from '../../shared/api/generated/identity/me/me';
import { roleLabel } from '../../shared/auth/roleLabel';
import { ApiErrorAlert } from '../../shared/components/ApiErrorAlert';
import { LoadingIndicator } from '../../shared/components/LoadingIndicator';
import { SectionCard } from '../../shared/components/SectionCard';
import { ChangePasswordSection } from './ChangePasswordSection';
import styles from './profile.module.css';

/**
 * "My profile" for every logged-in user (L4): their own details and system roles, read only (an admin changes
 * them, IAM-04), and changing their own password (IAM-06-AC3).
 */
export function ProfilePage() {
  const { t, i18n } = useTranslation();
  const profile = useGetMyProfile();

  return (
    <>
      <Typography.Title level={1}>{t('profile.title')}</Typography.Title>
      <SectionCard title={t('profile.detailsSection')}>
        {profile.isPending && <LoadingIndicator label={t('profile.loading')} />}
        {profile.isError && <ApiErrorAlert error={describeApiError(profile.error, i18n)} />}
        {profile.isSuccess && (
          <>
            <Descriptions
              className={styles.details}
              column={1}
              bordered
              size="small"
              items={[
                { key: 'username', label: t('profile.username'), children: profile.data.username },
                { key: 'displayName', label: t('profile.displayName'), children: profile.data.displayName },
                { key: 'email', label: t('profile.email'), children: profile.data.email },
                {
                  key: 'mobile',
                  label: t('profile.mobile'),
                  children: profile.data.mobile ?? t('profile.noMobile'),
                },
                {
                  key: 'roles',
                  label: t('profile.roles'),
                  children: profile.data.roles.map((role) => (
                    <Tag key={role} variant="filled">
                      {roleLabel(t, role)}
                    </Tag>
                  )),
                },
              ]}
            />
            <Typography.Paragraph type="secondary">{t('profile.detailsHelp')}</Typography.Paragraph>
          </>
        )}
      </SectionCard>
      <ChangePasswordSection />
    </>
  );
}
