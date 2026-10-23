import { App, Button } from 'antd';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { describeApiError } from '../../../shared/api/errorMessages';
import type { UserDetails } from '../../../shared/api/generated/identity/model';
import { useSetUserRoles } from '../../../shared/api/generated/identity/users/users';
import { ApiErrorAlert, type ShownApiError } from '../../../shared/components/ApiErrorAlert';
import { SectionCard } from '../../../shared/components/SectionCard';
import { RolesField } from './RolesField';
import { useRefreshUsers } from './useRefreshUsers';

/**
 * A user's system roles (IAM-02-AC2): several at once; the server refuses to take Platform Admin from the last
 * active Platform Admin, and that message is shown here.
 */
export function RolesSection({ user }: { user: UserDetails }) {
  const { t, i18n } = useTranslation();
  const { message } = App.useApp();
  const refreshUsers = useRefreshUsers();
  const [roles, setRoles] = useState<string[]>(user.roles);
  const [failure, setFailure] = useState<ShownApiError>();
  const setUserRoles = useSetUserRoles();

  const save = async () => {
    setFailure(undefined);
    try {
      await setUserRoles.mutateAsync({ id: user.id, data: { roles } });
      void message.success(t('users.details.rolesSaved'));
      await refreshUsers();
    } catch (error) {
      setFailure(describeApiError(error, i18n));
    }
  };

  return (
    <SectionCard title={t('users.details.rolesSection')}>
      <ApiErrorAlert error={failure} />
      <RolesField value={roles} onChange={setRoles} disabled={setUserRoles.isPending} hideLegend />
      <Button type="primary" onClick={() => void save()} loading={setUserRoles.isPending}>
        {t('users.details.saveRoles')}
      </Button>
    </SectionCard>
  );
}
