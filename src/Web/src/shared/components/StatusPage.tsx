import { Button, Result, Typography, type ResultProps } from 'antd';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

interface StatusPageProps {
  status: NonNullable<ResultProps['status']>;
  title: string;
  message: string;
  children?: ReactNode;
  actions?: ReactNode;
}

/**
 * A full-width message page (error, no access, not found) with a way back home. "Home" reloads the page on
 * purpose: after an error a fresh start is the safest. Works inside and outside the router.
 */
export function StatusPage({ status, title, message, children, actions }: StatusPageProps) {
  const { t } = useTranslation();
  return (
    <Result
      status={status}
      title={<Typography.Title level={1}>{title}</Typography.Title>}
      subTitle={message}
      extra={
        <>
          {actions}
          <Button type="primary" href="/">
            {t('errors.page.home')}
          </Button>
        </>
      }
    >
      {children}
    </Result>
  );
}
