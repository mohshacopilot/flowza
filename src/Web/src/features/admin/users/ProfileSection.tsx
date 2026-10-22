import { App, Button, Form, Input, Typography } from 'antd';
import { useId, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../../shared/api/ApiError';
import type { UserDetails } from '../../../shared/api/generated/identity/model';
import { useUpdateUser } from '../../../shared/api/generated/identity/users/users';
import { ApiErrorAlert, type ShownApiError } from '../../../shared/components/ApiErrorAlert';
import { applyServerErrors } from '../../../shared/forms/serverErrors';
import { TextField } from '../../../shared/forms/TextField';
import { useZodResolver } from '../../../shared/forms/useZodResolver';
import { useRefreshUsers } from './useRefreshUsers';
import { emptyToNull, userProfileSchema, type UserProfileValues } from './userSchema';
import styles from './users.module.css';

const fields = ['displayName', 'email', 'mobile'] as const;

/**
 * Edit display name, email and mobile (IAM-04-AC1). The username is shown but cannot change. The row version
 * read with the user goes back with the change, so a change made meanwhile by someone else is refused (409)
 * instead of overwritten (constitution 8.5).
 */
export function ProfileSection({ user, onReload }: { user: UserDetails; onReload: () => void }) {
  const { t, i18n } = useTranslation();
  const { message } = App.useApp();
  const headingId = useId();
  const usernameId = useId();
  const refreshUsers = useRefreshUsers();
  const [failure, setFailure] = useState<ShownApiError & { conflict: boolean }>();
  const updateUser = useUpdateUser();
  const form = useForm<UserProfileValues>({
    resolver: useZodResolver(userProfileSchema),
    defaultValues: { displayName: user.displayName, email: user.email, mobile: user.mobile ?? '' },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFailure(undefined);
    try {
      await updateUser.mutateAsync({
        id: user.id,
        data: { ...values, mobile: emptyToNull(values.mobile), version: user.version },
      });
      void message.success(t('users.details.saved'));
      await refreshUsers();
    } catch (error) {
      const conflict = error instanceof ApiError && error.status === 409;
      setFailure({ ...applyServerErrors(error, i18n, fields, form.setError), conflict });
    }
  });

  return (
    <section className={styles.section} aria-labelledby={headingId}>
      <Typography.Title level={2} id={headingId}>
        {t('users.details.profileSection')}
      </Typography.Title>
      <ApiErrorAlert
        error={failure}
        action={
          failure?.conflict ? (
            <Button size="small" onClick={onReload}>
              {t('users.details.reload')}
            </Button>
          ) : undefined
        }
      />
      <Form layout="vertical" component={false}>
        <form className={styles.form} noValidate onSubmit={(event) => void onSubmit(event)}>
          <Form.Item label={t('users.fields.username')} htmlFor={usernameId}>
            {/* Filled look: the username can be read and copied, never changed (IAM-04-AC1). */}
            <Input id={usernameId} value={user.username} readOnly variant="filled" />
          </Form.Item>
          <TextField
            control={form.control}
            name="displayName"
            label={t('users.fields.displayName')}
            required
          />
          <TextField
            control={form.control}
            name="email"
            type="email"
            label={t('users.fields.email')}
            required
          />
          <TextField control={form.control} name="mobile" type="tel" label={t('users.fields.mobile')} />
          <Button type="primary" htmlType="submit" loading={updateUser.isPending}>
            {t('users.details.save')}
          </Button>
        </form>
      </Form>
    </section>
  );
}
