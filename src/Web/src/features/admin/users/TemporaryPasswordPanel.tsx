import { CopyOutlined } from '@ant-design/icons';
import { Alert, App, Button, Form, Input, Space } from 'antd';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import styles from './users.module.css';

/**
 * A temporary password, shown once (IAM-04-AC2, K6). It lives only in the screen's state: it is not cached,
 * not stored in the browser and gone when the screen closes. The admin copies it with the button or by hand.
 */
export function TemporaryPasswordPanel({ password }: { password: string }) {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const id = useId();

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(password);
      void message.success(t('users.temporaryPassword.copied'));
    } catch {
      void message.error(t('users.temporaryPassword.copyFailed'));
    }
  };

  return (
    <>
      <Alert
        type="warning"
        showIcon
        title={t('users.temporaryPassword.onceWarning')}
        style={{ marginBottom: 16 }}
      />
      <Form layout="vertical" component={false}>
        <Form.Item label={t('users.temporaryPassword.label')} htmlFor={id}>
          <Space.Compact block>
            <Input id={id} readOnly value={password} className={styles.password} autoComplete="off" />
            <Button icon={<CopyOutlined aria-hidden />} onClick={() => void copy()}>
              {t('users.temporaryPassword.copy')}
            </Button>
          </Space.Compact>
        </Form.Item>
      </Form>
    </>
  );
}
