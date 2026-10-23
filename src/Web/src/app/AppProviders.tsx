import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { App as AntApp, ConfigProvider } from 'antd';
import enUS from 'antd/locale/en_US';
import { useMemo, useState, type ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import { initI18n } from '../shared/i18n/i18n';
import { createQueryClient } from './queryClient';
import { createAntdTheme } from './theme/antdTheme';
import designValues from './theme/designValues.json';
import { usePrefersReducedMotion } from './theme/reducedMotion';

interface AppProvidersProps {
  children: ReactNode;
  queryClient?: QueryClient;
}

/** Everything every screen needs: translations, theme, Ant Design's message/modal context and server state. */
export function AppProviders({ children, queryClient }: AppProvidersProps) {
  const [client] = useState(() => queryClient ?? createQueryClient());
  const reducedMotion = usePrefersReducedMotion();
  const theme = useMemo(() => createAntdTheme(designValues, { reducedMotion }), [reducedMotion]);
  return (
    <I18nextProvider i18n={initI18n()}>
      <ConfigProvider theme={theme} locale={enUS}>
        <AntApp>
          <QueryClientProvider client={client}>{children}</QueryClientProvider>
        </AntApp>
      </ConfigProvider>
    </I18nextProvider>
  );
}
