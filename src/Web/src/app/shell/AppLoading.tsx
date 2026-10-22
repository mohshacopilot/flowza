import { Flex } from 'antd';
import { useTranslation } from 'react-i18next';
import { LoadingIndicator } from '../../shared/components/LoadingIndicator';
import styles from './AppShell.module.css';

/**
 * The whole-screen spinner: while the app first loads its code (the router's HydrateFallback, so a full page load
 * is never blank) and while the shell asks who is logged in.
 */
export function AppLoading() {
  const { t } = useTranslation();
  return (
    <Flex className={styles.loading} align="center" justify="center">
      <LoadingIndicator label={t('shell.loading')} size="large" />
    </Flex>
  );
}
