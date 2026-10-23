import { FileSearchOutlined, LockOutlined } from '@ant-design/icons';
import { Button, Card, Result, Typography } from 'antd';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

export type StatusKind = 'forbidden' | 'notFound' | 'error';

interface StatusPageProps {
  kind: StatusKind;
  title: string;
  message: string;
  children?: ReactNode;
  actions?: ReactNode;
}

/**
 * Ant Design's 403/404 pictures carry their own colours (a bright blue question mark), which the design values
 * cannot reach (UI-02); an icon in the brand colour (status "info", whose colour is the brand) says the same.
 * The error page keeps Ant Design's error icon in the error colour.
 */
const looks: Record<StatusKind, { status: 'info' | 'error'; icon?: ReactNode }> = {
  forbidden: { status: 'info', icon: <LockOutlined aria-hidden /> },
  notFound: { status: 'info', icon: <FileSearchOutlined aria-hidden /> },
  error: { status: 'error' },
};

/**
 * A full-width message page (error, no access, not found) on a card, with a way back home. "Home" reloads the
 * page on purpose: after an error a fresh start is the safest. Works inside and outside the router.
 */
export function StatusPage({ kind, title, message, children, actions }: StatusPageProps) {
  const { t } = useTranslation();
  const look = looks[kind];
  return (
    <Card variant="borderless">
      <Result
        status={look.status}
        {...(look.icon ? { icon: look.icon } : {})}
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
    </Card>
  );
}
