import { SystemRole } from '../../shared/auth/systemRoles';
import {
  groupNavigationItems,
  navigationGroups,
  navigationItems,
  selectedNavigationKey,
  visibleNavigationItems,
  type NavigationGroupKey,
  type NavigationItem,
} from './navigation';

const item = (
  key: string,
  path: string,
  requiredRole?: SystemRole,
  group: NavigationGroupKey = 'main',
): NavigationItem => ({
  key,
  path,
  labelKey: 'nav.home',
  icon: null,
  group,
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

  it('UI_03_AC1_the_users_screen_is_in_the_administration_group', () => {
    expect(navigationItems.find((i) => i.key === 'users')?.group).toBe('administration');
    expect(navigationItems.find((i) => i.key === 'home')?.group).toBe('main');
  });

  it('UI_03_AC1_items_stand_under_their_groups_in_group_order', () => {
    const items = [item('admin', '/admin', undefined, 'administration'), item('a', '/a'), item('b', '/b')];

    const grouped = groupNavigationItems(navigationGroups, items);

    expect(grouped.map((g) => [g.group.key, g.items.map((i) => i.key)])).toEqual([
      ['main', ['a', 'b']],
      ['administration', ['admin']],
    ]);
  });

  it('UI_03_AC1_a_group_without_visible_items_is_left_out', () => {
    const items = visibleNavigationItems(
      [item('a', '/a'), item('admin', '/admin', SystemRole.PlatformAdmin, 'administration')],
      [SystemRole.User],
    );

    expect(groupNavigationItems(navigationGroups, items).map((g) => g.group.key)).toEqual(['main']);
    expect(groupNavigationItems(navigationGroups, [])).toEqual([]);
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
