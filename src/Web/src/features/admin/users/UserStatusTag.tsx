import { Tag } from 'antd';
import { useTranslation } from 'react-i18next';
import type { UserStatus } from '../../../shared/api/generated/identity/model';

const statusColors: Record<UserStatus, string> = {
  Active: 'success',
  Inactive: 'default',
  Locked: 'warning',
};

/** A user's status as a tag (preset Ant Design status colours, so the theme decides the actual colour). */
export function UserStatusTag({ status }: { status: UserStatus }) {
  const { t } = useTranslation();
  return <Tag color={statusColors[status]}>{t(`users.status.${status}`)}</Tag>;
}
