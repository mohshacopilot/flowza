import { Menu, type MenuProps } from 'antd';
import { useTranslation } from 'react-i18next';
import { Link, useLocation } from 'react-router';
import {
  groupNavigationItems,
  navigationGroups,
  navigationItems,
  selectedNavigationKey,
  visibleNavigationItems,
  type NavigationItem,
} from './navigation';
import styles from './SideNavigation.module.css';

type MenuItem = Required<MenuProps>['items'][number];

interface SideNavigationProps {
  roles: readonly string[];
  /** Collapsed to icons (UI-04): each label stays the link's accessible name and shows as a tooltip. */
  collapsed?: boolean;
}

/**
 * The dark side navigation by role and group (spec 007 UI-03). Items are ordinary links, so Tab, Enter and
 * "open in new tab" all work. A group heading appears only when the user has an item in that group.
 */
export function SideNavigation({ roles, collapsed = false }: SideNavigationProps) {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const visible = visibleNavigationItems(navigationItems, roles);
  const selected = selectedNavigationKey(visible, pathname);

  const toMenuItem = (item: NavigationItem): MenuItem => ({
    key: item.key,
    icon: item.icon,
    label: (
      <Link to={item.path} aria-current={item.key === selected ? 'page' : undefined}>
        {t(item.labelKey)}
      </Link>
    ),
  });

  const items = groupNavigationItems(navigationGroups, visible).flatMap(
    ({ group, items: groupItems }): MenuItem[] => {
      const entries = groupItems.map(toMenuItem);
      if (group.labelKey === undefined) {
        return entries;
      }
      // Collapsed to 72 px a heading would be cut to one letter; a line separates the group instead.
      return collapsed
        ? [{ type: 'divider', key: `${group.key}-divider` }, ...entries]
        : [{ type: 'group', key: group.key, label: t(group.labelKey), children: entries }];
    },
  );

  return (
    <nav aria-label={t('shell.mainNavigation')} className={styles.navigation}>
      <Menu mode="inline" theme="dark" selectedKeys={selected ? [selected] : []} items={items} />
    </nav>
  );
}
