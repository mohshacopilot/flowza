import { Layout } from 'antd';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useMatches } from 'react-router';
import type { CurrentUser } from '../../shared/auth/currentUser';
import { useInstallation } from '../../shared/installation/useInstallation';
import { pageTitleKey } from './pageTitle';
import { UserMenu } from './UserMenu';
import styles from './HeaderBar.module.css';

/**
 * The header (spec 007 UI-03): the page's title on the left, the user menu on the right. The title also names the
 * browser tab, "<page> – <installation>", like the login pages. It is not a heading: each page keeps its own <h1>.
 */
export function HeaderBar({ user }: { user: CurrentUser }) {
  const { t } = useTranslation();
  const titleKey = pageTitleKey(useMatches());
  const title = titleKey ? t(titleKey) : undefined;
  const installation = useInstallation().data?.name ?? t('app.name');

  useEffect(() => {
    document.title = title ? t('pageTitles.documentTitle', { page: title, installation }) : installation;
  }, [t, title, installation]);

  return (
    <Layout.Header className={styles.header}>
      <span className={styles.title}>{title}</span>
      <UserMenu user={user} />
    </Layout.Header>
  );
}
