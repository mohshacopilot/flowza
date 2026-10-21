import { QueryClient } from '@tanstack/react-query';
import { render, type RenderResult } from '@testing-library/react';
import { ConfigProvider } from 'antd';
import type { ReactNode } from 'react';
import { createMemoryRouter, type RouteObject } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { AppProviders } from '../app/AppProviders';

/** A query client for tests: no retries, so errors show at once. */
export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { retry: false } },
  });
}

/**
 * The real providers (translations, theme, query client), with animations off: jsdom never finishes them,
 * so menus and popups would stay "hidden" to Testing Library.
 */
export function TestProviders({ children }: { children: ReactNode }) {
  return (
    <AppProviders queryClient={createTestQueryClient()}>
      <ConfigProvider theme={{ token: { motion: false } }}>{children}</ConfigProvider>
    </AppProviders>
  );
}

export function renderWithProviders(ui: ReactNode): RenderResult {
  return render(<TestProviders>{ui}</TestProviders>);
}

/** Renders a route table at a URL with the real providers. */
export function renderRoutes(routes: RouteObject[], url = '/'): RenderResult {
  const router = createMemoryRouter(routes, { initialEntries: [url] });
  return renderWithProviders(<RouterProvider router={router} />);
}
