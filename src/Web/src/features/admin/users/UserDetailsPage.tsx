import { Alert, Space, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { describeApiError } from '../../../shared/api/errorMessages';
import { useGetUser } from '../../../shared/api/generated/identity/users/users';
import { ApiErrorAlert } from '../../../shared/components/ApiErrorAlert';
import { LoadingIndicator } from '../../../shared/components/LoadingIndicator';
import { AccessSection } from './AccessSection';
import { ProfileSection } from './ProfileSection';
import { RolesSection } from './RolesSection';
import { UserStatusTag } from './UserStatusTag';
import styles from './users.module.css';

/** One user for a Platform Admin: details, system roles and access (IAM-02, IAM-04, IAM-07). */
export function UserDetailsPage() {
  const { t, i18n } = useTranslation();
  const { userId = '' } = useParams();
  const user = useGetUser(userId);

  const backLink = <Link to="/admin/users">{t('users.backToList')}</Link>;

  if (user.isPending) {
    return <LoadingIndicator label={t('users.details.loading')} />;
  }
  if (user.isError) {
    return (
      <>
        <Typography.Title level={1}>{t('users.title')}</Typography.Title>
        <ApiErrorAlert error={describeApiError(user.error, i18n)} />
        {backLink}
      </>
    );
  }

  const details = user.data;
  return (
    <>
      <Typography.Paragraph>{backLink}</Typography.Paragraph>
      <Space align="baseline">
        <Typography.Title level={1}>
          {t('users.details.title', { username: details.username })}
        </Typography.Title>
        <UserStatusTag status={details.status} />
      </Space>
      {details.mustChangePassword && (
        <Alert
          type="info"
          showIcon
          title={t('users.details.mustChangePassword')}
          className={styles.alert ?? ''}
        />
      )}
      {/* Keyed by the row version: after any change the sections start again from the saved values. */}
      <ProfileSection
        key={`profile-${details.version}`}
        user={details}
        onReload={() => void user.refetch()}
      />
      <RolesSection key={`roles-${details.version}`} user={details} />
      <AccessSection user={details} />
    </>
  );
}
