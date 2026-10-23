import { Layout } from 'antd';
import { useTranslation } from 'react-i18next';
import { Outlet } from 'react-router';
import { ApiError } from '../../shared/api/ApiError';
import { useCurrentUser } from '../../shared/auth/currentUser';
import { PageForError } from '../../shared/components/ErrorPages';
import designValues from '../theme/designValues.json';
import { shellCssVariables } from '../theme/shellCssVariables';
import { AppLoading } from './AppLoading';
import { HeaderBar } from './HeaderBar';
import { NavigationDrawer } from './NavigationDrawer';
import { NavigationSider } from './NavigationSider';
import { useWidthClass } from './useWidthClass';
import styles from './AppShell.module.css';

const isUnauthenticated = (error: unknown): boolean => error instanceof ApiError && error.status === 401;

const shellVariables = shellCssVariables(designValues);

/**
 * The frame of every screen: the deep teal side navigation (installation mark and name, items by role), the header
 * (page title, user menu) and the page. Pages render in <main>; a "skip to main content" link comes first for keyboard users
 * (constitution 12.2).
 */
export function AppShell() {
  const { t } = useTranslation();
  const user = useCurrentUser();
  // A phone gets a menu button and a drawer instead of the side panel (UI-05).
  const phone = useWidthClass() === 'phone';
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

  return (
    <Layout className={styles.shell} style={shellVariables}>
      <a className={styles.skipLink} href="#main-content">
        {t('shell.skipToContent')}
      </a>
      {phone ? null : <NavigationSider roles={user.data.roles} />}
      <Layout className={styles.main}>
        <HeaderBar
          user={user.data}
          navigation={phone ? <NavigationDrawer roles={user.data.roles} /> : undefined}
        />
        <Layout.Content id="main-content" tabIndex={-1} className={styles.content}>
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
