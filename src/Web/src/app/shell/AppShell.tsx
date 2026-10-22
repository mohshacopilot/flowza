import { Layout, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { Link, Outlet } from 'react-router';
import { ApiError } from '../../shared/api/ApiError';
import { useCurrentUser } from '../../shared/auth/currentUser';
import { PageForError } from '../../shared/components/ErrorPages';
import { AppLoading } from './AppLoading';
import { SideNavigation } from './SideNavigation';
import { UserMenu } from './UserMenu';
import styles from './AppShell.module.css';

const isUnauthenticated = (error: unknown): boolean => error instanceof ApiError && error.status === 401;

/**
 * The frame of every screen: header (installation name, user menu), side navigation by role and the page.
 * Pages render in <main>; a "skip to main content" link comes first for keyboard users (constitution 12.2).
 */
export function AppShell() {
  const { t } = useTranslation();
  const user = useCurrentUser();
  // A 401 sends the browser to the login page (query client, app/queryClient.ts); keep the spinner meanwhile.
  const unauthenticated = user.isError && isUnauthenticated(user.error);

  if (user.isPending || unauthenticated) {
    return <AppLoading />;
  }
  if (user.isError) {
    return (
      <main id="main-content">
        <PageForError error={user.error} />
      </main>
    );
  }

  // The installation name is a placeholder until the settings screen (PLT-08, M3).
  const installationName = t('shell.installationName');

  return (
    <Layout className={styles.shell}>
      <a className={styles.skipLink} href="#main-content">
        {t('shell.skipToContent')}
      </a>
      <Layout.Header className={styles.header}>
        <Link to="/" className={styles.brand} aria-label={t('shell.homeLink', { name: installationName })}>
          <Typography.Text strong>{installationName}</Typography.Text>
        </Link>
        <UserMenu user={user.data} />
      </Layout.Header>
      <Layout>
        <Layout.Sider width={240} className={styles.sider}>
          <SideNavigation roles={user.data.roles} />
        </Layout.Sider>
        <Layout.Content id="main-content" tabIndex={-1} className={styles.content}>
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
