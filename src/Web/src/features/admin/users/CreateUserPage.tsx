import { Button, Card, Form, Space, Typography } from 'antd';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { useCreateUser } from '../../../shared/api/generated/identity/users/users';
import { ApiErrorAlert, type ShownApiError } from '../../../shared/components/ApiErrorAlert';
import { applyServerErrors } from '../../../shared/forms/serverErrors';
import { TextField } from '../../../shared/forms/TextField';
import { useZodResolver } from '../../../shared/forms/useZodResolver';
import { RolesField } from './RolesField';
import { TemporaryPasswordPanel } from './TemporaryPasswordPanel';
import { useRefreshUsers } from './useRefreshUsers';
import { emptyToNull, newUserSchema, type NewUserValues } from './userSchema';
import styles from './users.module.css';

const fields = ['username', 'displayName', 'email', 'mobile', 'roles'] as const;

interface CreatedUserView {
  id: string;
  username: string;
  temporaryPassword: string;
}

/** An admin creates a user (IAM-04-AC1; no self sign-up) and gets the temporary password once (IAM-04-AC2). */
export function CreateUserPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const refreshUsers = useRefreshUsers();
  const [failure, setFailure] = useState<ShownApiError>();
  const [created, setCreated] = useState<CreatedUserView>();
  // gcTime 0: the answer (with the password) leaves the query cache as soon as it was read.
  const createUser = useCreateUser({ mutation: { gcTime: 0 } });
  const form = useForm<NewUserValues>({
    resolver: useZodResolver(newUserSchema),
    defaultValues: { username: '', displayName: '', email: '', mobile: '', roles: [] },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFailure(undefined);
    try {
      const result = await createUser.mutateAsync({
        data: { ...values, mobile: emptyToNull(values.mobile) },
      });
      createUser.reset();
      setCreated({ id: result.id, username: values.username, temporaryPassword: result.temporaryPassword });
      void refreshUsers();
    } catch (error) {
      setFailure(applyServerErrors(error, i18n, fields, form.setError));
    }
  });

  if (created) {
    return (
      <>
        <Typography.Title level={1}>{t('users.create.createdTitle')}</Typography.Title>
        <Typography.Paragraph>
          {t('users.create.createdMessage', { username: created.username })}
        </Typography.Paragraph>
        <Card variant="borderless" className={styles.card ?? ''}>
          <div className={styles.form}>
            <TemporaryPasswordPanel password={created.temporaryPassword} />
            <Space>
              <Button
                type="primary"
                onClick={() => {
                  void navigate(`/admin/users/${created.id}`, { replace: true });
                }}
              >
                {t('users.temporaryPassword.openUser')}
              </Button>
              <Link to="/admin/users">{t('users.backToList')}</Link>
            </Space>
          </div>
        </Card>
      </>
    );
  }

  return (
    <>
      <Typography.Title level={1}>{t('users.create.title')}</Typography.Title>
      <ApiErrorAlert error={failure} />
      <Card variant="borderless" className={styles.card ?? ''}>
        <Form layout="vertical" component={false}>
          <form className={styles.form} noValidate onSubmit={(event) => void onSubmit(event)}>
            <TextField
              control={form.control}
              name="username"
              label={t('users.fields.username')}
              extra={t('users.fields.usernameHelp')}
              required
            />
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
            <Controller
              control={form.control}
              name="roles"
              render={({ field }) => <RolesField value={field.value} onChange={field.onChange} />}
            />
            <Space>
              <Button type="primary" htmlType="submit" loading={createUser.isPending}>
                {t('users.create.submit')}
              </Button>
              <Link to="/admin/users">{t('users.create.cancel')}</Link>
            </Space>
          </form>
        </Form>
      </Card>
    </>
  );
}
