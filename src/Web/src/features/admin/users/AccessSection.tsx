import { Alert, App, Button, Modal, Space, Typography } from 'antd';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { describeApiError } from '../../../shared/api/errorMessages';
import type { UserDetails } from '../../../shared/api/generated/identity/model';
import {
  useActivateUser,
  useDeactivateUser,
  useResetUserPassword,
  useUnlockUser,
} from '../../../shared/api/generated/identity/users/users';
import { ApiErrorAlert, type ShownApiError } from '../../../shared/components/ApiErrorAlert';
import { TemporaryPasswordPanel } from './TemporaryPasswordPanel';
import { useRefreshUsers } from './useRefreshUsers';
import styles from './users.module.css';

/**
 * What decides whether a user can log in: deactivate (ends their session at once, IAM-04-AC3) and activate
 * again (IAM-04-AC5), unlock after wrong passwords (IAM-07-AC1), reset the password to a temporary one shown
 * once (IAM-04-AC2). Each action is a command on the server; the buttons shown follow the user's status.
 */
export function AccessSection({ user }: { user: UserDetails }) {
  const { t, i18n } = useTranslation();
  const { message, modal } = App.useApp();
  const headingId = useId();
  const refreshUsers = useRefreshUsers();
  const [failure, setFailure] = useState<ShownApiError>();
  const [temporaryPassword, setTemporaryPassword] = useState<string>();

  const deactivate = useDeactivateUser();
  const activate = useActivateUser();
  const unlock = useUnlockUser();
  // gcTime 0: the answer (with the password) leaves the query cache as soon as it was read.
  const resetPassword = useResetUserPassword({ mutation: { gcTime: 0 } });
  const busy = deactivate.isPending || activate.isPending || unlock.isPending || resetPassword.isPending;
  const username = user.username;

  /** Runs one action; success shows a short message, failure (e.g. the last Platform Admin) an alert. */
  const run = async (action: () => Promise<unknown>, done?: string) => {
    setFailure(undefined);
    try {
      await action();
      if (done) {
        void message.success(done);
      }
      await refreshUsers();
    } catch (error) {
      setFailure(describeApiError(error, i18n));
    }
  };

  const confirm = (title: string, content: string, onOk: () => Promise<void>) => {
    modal.confirm({
      title,
      content,
      okText: t('users.details.confirm'),
      cancelText: t('users.details.cancel'),
      onOk,
    });
  };

  const onDeactivate = () => {
    confirm(
      t('users.details.deactivateConfirmTitle', { username }),
      t('users.details.deactivateConfirmMessage'),
      () => run(() => deactivate.mutateAsync({ id: user.id }), t('users.details.deactivated', { username })),
    );
  };

  const onResetPassword = () => {
    confirm(t('users.details.resetConfirmTitle', { username }), t('users.details.resetConfirmMessage'), () =>
      run(async () => {
        const result = await resetPassword.mutateAsync({ id: user.id });
        resetPassword.reset();
        setTemporaryPassword(result.password);
      }),
    );
  };

  return (
    <section className={styles.section} aria-labelledby={headingId}>
      <Typography.Title level={2} id={headingId}>
        {t('users.details.accessSection')}
      </Typography.Title>
      <ApiErrorAlert error={failure} />
      {user.status === 'Locked' && (
        <Alert
          type="warning"
          showIcon
          title={t('users.details.lockedMessage')}
          style={{ marginBottom: 16 }}
        />
      )}
      <Space wrap>
        {user.status === 'Inactive' ? (
          <Button
            disabled={busy}
            onClick={() =>
              void run(
                () => activate.mutateAsync({ id: user.id }),
                t('users.details.activated', { username }),
              )
            }
          >
            {t('users.details.activate')}
          </Button>
        ) : (
          <Button danger disabled={busy} onClick={onDeactivate}>
            {t('users.details.deactivate')}
          </Button>
        )}
        {user.status === 'Locked' && (
          <Button
            disabled={busy}
            onClick={() =>
              void run(() => unlock.mutateAsync({ id: user.id }), t('users.details.unlocked', { username }))
            }
          >
            {t('users.details.unlock')}
          </Button>
        )}
        <Button disabled={busy} onClick={onResetPassword}>
          {t('users.details.resetPassword')}
        </Button>
      </Space>

      <Modal
        open={temporaryPassword !== undefined}
        title={t('users.details.resetTitle', { username })}
        onCancel={() => {
          setTemporaryPassword(undefined);
        }}
        footer={
          <Button
            type="primary"
            onClick={() => {
              setTemporaryPassword(undefined);
            }}
          >
            {t('users.temporaryPassword.done')}
          </Button>
        }
        destroyOnHidden
      >
        {temporaryPassword !== undefined && <TemporaryPasswordPanel password={temporaryPassword} />}
      </Modal>
    </section>
  );
}
