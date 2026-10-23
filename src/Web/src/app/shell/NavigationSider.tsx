import { MenuFoldOutlined, MenuUnfoldOutlined } from '@ant-design/icons';
import { Layout, Tooltip } from 'antd';
import { useTranslation } from 'react-i18next';
import designValues from '../theme/designValues.json';
import { BrandMark } from './BrandMark';
import { SideNavigation } from './SideNavigation';
import { useNavigationPreference } from './useNavigationPreference';
import styles from './NavigationSider.module.css';

/**
 * The deep teal side panel on desktop and tablet (spec 007 UI-03, UI-04): the installation's mark and name on top,
 * the navigation, and a button at the bottom that collapses it to icons. The choice is remembered per browser.
 */
export function NavigationSider({ roles }: { roles: readonly string[] }) {
  const { t } = useTranslation();
  const { collapsed, setCollapsed } = useNavigationPreference();
  const toggleLabel = collapsed ? t('shell.expandNavigation') : t('shell.collapseNavigation');

  return (
    <Layout.Sider
      className={styles.sider}
      width={designValues.layout.navigationWidth}
      collapsedWidth={designValues.layout.navigationCollapsedWidth}
      collapsed={collapsed}
      trigger={null}
    >
      <div className={styles.panel}>
        <div className={collapsed ? styles.brandCollapsed : styles.brand}>
          <BrandMark collapsed={collapsed} />
        </div>
        <SideNavigation roles={roles} collapsed={collapsed} />
        {/* Ant Design's own trigger is not a button; this one works with the keyboard. */}
        <Tooltip title={collapsed ? toggleLabel : undefined} placement="right">
          <button
            type="button"
            className={collapsed ? styles.toggleCollapsed : styles.toggle}
            aria-label={toggleLabel}
            onClick={() => {
              setCollapsed(!collapsed);
            }}
          >
            {collapsed ? <MenuUnfoldOutlined aria-hidden /> : <MenuFoldOutlined aria-hidden />}
            {!collapsed && <span>{toggleLabel}</span>}
          </button>
        </Tooltip>
      </div>
    </Layout.Sider>
  );
}
