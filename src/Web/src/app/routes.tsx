import type { RouteObject } from 'react-router';
import { SystemRole } from '../shared/auth/systemRoles';
import { NotFoundPage, RouteErrorPage } from '../shared/components/ErrorPages';
import { AppLoading } from './shell/AppLoading';
import { AppShell } from './shell/AppShell';
import { titled } from './shell/pageTitle';
import { RequireRole } from './shell/RequireRole';

/**
 * The route table. Feature areas are loaded lazily, so each one is its own bundle. Errors inside a page are
 * caught by the inner boundary, so the header and navigation stay usable; the outer one catches shell errors.
 * Each page's `handle` names its title, which the header and the browser tab show (UI-03).
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
            handle: titled('pageTitles.home'),
            lazy: async () => ({ Component: (await import('../features/home/HomePage')).HomePage }),
          },
          {
            path: 'admin/users',
            handle: titled('pageTitles.users'),
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
                handle: titled('pageTitles.newUser'),
                lazy: async () => ({
                  Component: (await import('../features/admin/users/CreateUserPage')).CreateUserPage,
                }),
              },
              {
                path: ':userId',
                handle: titled('pageTitles.userDetails'),
                lazy: async () => ({
                  Component: (await import('../features/admin/users/UserDetailsPage')).UserDetailsPage,
                }),
              },
            ],
          },
          {
            path: 'me',
            handle: titled('pageTitles.myProfile'),
            lazy: async () => ({ Component: (await import('../features/profile/ProfilePage')).ProfilePage }),
          },
          { path: '*', handle: titled('pageTitles.notFound'), Component: NotFoundPage },
        ],
      },
    ],
  },
];
