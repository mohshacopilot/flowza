import { SystemRole } from '../../shared/auth/systemRoles';
import {
  navigationItems,
  selectedNavigationKey,
  visibleNavigationItems,
  type NavigationItem,
} from './navigation';

const item = (key: string, path: string, requiredRole?: SystemRole): NavigationItem => ({
  key,
  path,
  labelKey: 'nav.home',
  icon: null,
  ...(requiredRole ? { requiredRole } : {}),
});

describe('Navigation by role', () => {
  it('IAM_02_AC1_items_without_a_role_are_visible_to_every_user', () => {
    expect(visibleNavigationItems([item('a', '/a')], []).map((i) => i.key)).toEqual(['a']);
  });

  it('IAM_02_AC1_role_items_are_visible_only_with_that_role', () => {
    const items = [item('a', '/a'), item('admin', '/admin', SystemRole.PlatformAdmin)];

    expect(visibleNavigationItems(items, [SystemRole.User]).map((i) => i.key)).toEqual(['a']);
    expect(
      visibleNavigationItems(items, [SystemRole.User, SystemRole.PlatformAdmin]).map((i) => i.key),
    ).toEqual(['a', 'admin']);
  });

  it('empty_registry_gives_no_items', () => {
    expect(visibleNavigationItems([], [SystemRole.PlatformAdmin])).toEqual([]);
  });

  it('the_users_screen_needs_platform_admin', () => {
    expect(navigationItems.find((i) => i.key === 'users')?.requiredRole).toBe(SystemRole.PlatformAdmin);
  });

  it.each([
    ['/', 'home'],
    ['/admin', 'admin'],
    ['/admin/users', 'users'],
    ['/admin/users/42', 'users'],
    ['/administrator', undefined],
    ['/other', undefined],
  ])('selects_the_longest_matching_path_for_%s', (pathname, expected) => {
    const items = [item('home', '/'), item('admin', '/admin'), item('users', '/admin/users')];
    expect(selectedNavigationKey(items, pathname)).toBe(expected);
  });
});
