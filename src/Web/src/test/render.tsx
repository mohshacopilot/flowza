import type { QueryClient } from '@tanstack/react-query';
import { render, type RenderResult } from '@testing-library/react';
import { App as AntApp, ConfigProvider } from 'antd';
import type { ReactNode } from 'react';
import { createMemoryRouter, type RouteObject } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { AppProviders } from '../app/AppProviders';
import { createQueryClient } from '../app/queryClient';

/** The app's query client, without retries, so errors show at once. */
export function createTestQueryClient(): QueryClient {
  return createQueryClient({ queries: { retry: false, gcTime: Infinity }, mutations: { retry: false } });
}

/**
 * The real providers (translations, theme, query client), with animations off: jsdom never finishes them,
 * so menus and popups would stay "hidden" to Testing Library. The inner App makes App.useApp() (messages,
 * confirm dialogs) use the settings without animations too.
 */
export function TestProviders({ children, queryClient }: { children: ReactNode; queryClient?: QueryClient }) {
  return (
    <AppProviders queryClient={queryClient ?? createTestQueryClient()}>
      <ConfigProvider theme={{ token: { motion: false } }}>
        <AntApp>{children}</AntApp>
      </ConfigProvider>
    </AppProviders>
  );
}

export function renderWithProviders(ui: ReactNode, queryClient?: QueryClient): RenderResult {
  return render(<TestProviders {...(queryClient ? { queryClient } : {})}>{ui}</TestProviders>);
}

/**
 * Renders a route table at a URL with the real providers. Returns the router too (to go back in history) and uses
 * the given query client when a test wants to look into the cache.
 */
export function renderRoutes(
  routes: RouteObject[],
  url = '/',
  queryClient?: QueryClient,
): RenderResult & { router: ReturnType<typeof createMemoryRouter> } {
  const router = createMemoryRouter(routes, { initialEntries: [url] });
  return { ...renderWithProviders(<RouterProvider router={router} />, queryClient), router };
}
