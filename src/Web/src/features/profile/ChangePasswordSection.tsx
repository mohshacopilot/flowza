import { App, Button, Form, Typography } from 'antd';
import { useId, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useChangeMyPassword } from '../../shared/api/generated/identity/me/me';
import { ApiErrorAlert, type ShownApiError } from '../../shared/components/ApiErrorAlert';
import { applyServerErrors } from '../../shared/forms/serverErrors';
import { TextField } from '../../shared/forms/TextField';
import { useZodResolver } from '../../shared/forms/useZodResolver';
import { changePasswordSchema, type ChangePasswordValues } from './passwordSchema';
import styles from './profile.module.css';

const fields = ['currentPassword', 'newPassword'] as const;
const empty: ChangePasswordValues = { currentPassword: '', newPassword: '', confirmPassword: '' };

/**
 * A user changes their own password after entering the current one (IAM-06-AC3). The rules of IAM-06-AC1 are
 * the server's; a wrong current password or a refused new one is shown at its field. Passwords never leave
 * this form's state and are cleared after a successful change.
 */
export function ChangePasswordSection() {
  const { t, i18n } = useTranslation();
  const { message } = App.useApp();
  const headingId = useId();
  const [failure, setFailure] = useState<ShownApiError>();
  const changePassword = useChangeMyPassword();
  const form = useForm<ChangePasswordValues>({
    resolver: useZodResolver(changePasswordSchema),
    defaultValues: empty,
  });

  const onSubmit = form.handleSubmit(async ({ currentPassword, newPassword }) => {
    setFailure(undefined);
    try {
      await changePassword.mutateAsync({ data: { currentPassword, newPassword } });
      form.reset(empty);
      void message.success(t('profile.passwordChanged'));
    } catch (error) {
      setFailure(applyServerErrors(error, i18n, fields, form.setError));
    }
  });

  return (
    <section className={styles.section} aria-labelledby={headingId}>
      <Typography.Title level={2} id={headingId}>
        {t('profile.passwordSection')}
      </Typography.Title>
      <ApiErrorAlert error={failure} />
      <Form layout="vertical" component={false}>
        <form className={styles.form} noValidate onSubmit={(event) => void onSubmit(event)}>
          <TextField
            control={form.control}
            name="currentPassword"
            type="password"
            autoComplete="current-password"
            label={t('profile.currentPassword')}
            required
          />
          <TextField
            control={form.control}
            name="newPassword"
            type="password"
            autoComplete="new-password"
            label={t('profile.newPassword')}
            extra={t('profile.newPasswordHelp')}
            required
          />
          <TextField
            control={form.control}
            name="confirmPassword"
            type="password"
            autoComplete="new-password"
            label={t('profile.confirmPassword')}
            required
          />
          <Button type="primary" htmlType="submit" loading={changePassword.isPending}>
            {t('profile.changePassword')}
          </Button>
        </form>
      </Form>
    </section>
  );
}
