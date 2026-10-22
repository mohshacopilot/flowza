import { Menu } from 'antd';
import { useTranslation } from 'react-i18next';
import { Link, useLocation } from 'react-router';
import { navigationItems, selectedNavigationKey, visibleNavigationItems } from './navigation';

/** Side navigation by role. Items are ordinary links, so Tab, Enter and "open in new tab" all work. */
export function SideNavigation({ roles }: { roles: readonly string[] }) {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const items = visibleNavigationItems(navigationItems, roles);
  const selected = selectedNavigationKey(items, pathname);

  return (
    <nav aria-label={t('shell.mainNavigation')}>
      <Menu
        mode="inline"
        selectedKeys={selected ? [selected] : []}
        items={items.map((item) => ({
          key: item.key,
          icon: item.icon,
          label: (
            <Link to={item.path} aria-current={item.key === selected ? 'page' : undefined}>
              {t(item.labelKey)}
            </Link>
          ),
        }))}
      />
    </nav>
  );
}
