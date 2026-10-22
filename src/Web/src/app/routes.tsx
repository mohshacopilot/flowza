import type { RouteObject } from 'react-router';
import { SystemRole } from '../shared/auth/systemRoles';
import { NotFoundPage, RouteErrorPage } from '../shared/components/ErrorPages';
import { AppLoading } from './shell/AppLoading';
import { AppShell } from './shell/AppShell';
import { RequireRole } from './shell/RequireRole';

/**
 * The route table. Feature areas are loaded lazily, so each one is its own bundle. Errors inside a page are
 * caught by the inner boundary, so the header and navigation stay usable; the outer one catches shell errors.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    Component: AppShell,
    HydrateFallback: AppLoading,
    ErrorBoundary: RouteErrorPage,
    children: [
      {
        ErrorBoundary: RouteErrorPage,
        children: [
          {
            index: true,
            lazy: async () => ({ Component: (await import('../features/home/HomePage')).HomePage }),
          },
          {
            path: 'admin/users',
            element: <RequireRole role={SystemRole.PlatformAdmin} />,
            children: [
              {
                index: true,
                lazy: async () => ({
                  Component: (await import('../features/admin/users/UsersPage')).UsersPage,
                }),
              },
              {
                path: 'new',
                lazy: async () => ({
                  Component: (await import('../features/admin/users/CreateUserPage')).CreateUserPage,
                }),
              },
              {
                path: ':userId',
                lazy: async () => ({
                  Component: (await import('../features/admin/users/UserDetailsPage')).UserDetailsPage,
                }),
              },
            ],
          },
          {
            path: 'me',
            lazy: async () => ({ Component: (await import('../features/profile/ProfilePage')).ProfilePage }),
          },
          { path: '*', Component: NotFoundPage },
        ],
      },
    ],
  },
];
