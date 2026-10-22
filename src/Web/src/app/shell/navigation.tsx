import { HomeOutlined, TeamOutlined } from '@ant-design/icons';
import type { ParseKeys } from 'i18next';
import type { ReactNode } from 'react';
import { SystemRole } from '../../shared/auth/systemRoles';

export interface NavigationItem {
  key: string;
  path: string;
  labelKey: ParseKeys;
  icon: ReactNode;
  /** Shown only to users with this role. Without it, everyone who is logged in sees the item. */
  requiredRole?: SystemRole;
}

/**
 * The side navigation (a registry: a new feature adds its entry here, nothing else changes). Hiding an item
 * is a convenience only; the route guard and, above all, the server refuse users without the role (IAM-02).
 */
export const navigationItems: readonly NavigationItem[] = [
  { key: 'home', path: '/', labelKey: 'nav.home', icon: <HomeOutlined aria-hidden /> },
  {
    key: 'users',
    path: '/admin/users',
    labelKey: 'nav.users',
    icon: <TeamOutlined aria-hidden />,
    requiredRole: SystemRole.PlatformAdmin,
  },
];

/** The items a user with these roles may see, in registry order. O(items × roles), both tiny. */
export function visibleNavigationItems(
  items: readonly NavigationItem[],
  roles: readonly string[],
): NavigationItem[] {
  return items.filter((item) => item.requiredRole === undefined || roles.includes(item.requiredRole));
}

/** The item whose path is the longest prefix of the current path (so /admin/users/42 selects "Users"). */
export function selectedNavigationKey(
  items: readonly NavigationItem[],
  pathname: string,
): string | undefined {
  let best: NavigationItem | undefined;
  for (const item of items) {
    const matches =
      item.path === '/' ? pathname === '/' : pathname === item.path || pathname.startsWith(`${item.path}/`);
    if (matches && (best === undefined || item.path.length > best.path.length)) {
      best = item;
    }
  }
  return best?.key;
}
