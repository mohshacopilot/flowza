import { Alert } from 'antd';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import styles from './ApiErrorAlert.module.css';

export interface ShownApiError {
  message: string;
  reference: string | undefined;
}

/** A failed call shown above a form or list: the message and, when the server gave one, the reference code. */
export function ApiErrorAlert({ error, action }: { error: ShownApiError | undefined; action?: ReactNode }) {
  const { t } = useTranslation();
  if (!error) {
    return null;
  }
  return (
    <Alert
      type="error"
      showIcon
      title={error.message}
      {...(error.reference
        ? { description: t('errors.page.reference', { reference: error.reference }) }
        : {})}
      {...(action ? { action } : {})}
      className={styles.alert ?? ''}
    />
  );
}
