import { MenuOutlined } from '@ant-design/icons';
import { Button, Drawer, type DrawerProps } from 'antd';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';
import designValues from '../theme/designValues.json';
import { shellCssVariables } from '../theme/shellCssVariables';
import { BrandMark } from './BrandMark';
import { SideNavigation } from './SideNavigation';
import styles from './NavigationDrawer.module.css';

// The drawer is rendered at the end of <body>, outside the frame, so it needs the navigation colours itself.
const drawerVariables = shellCssVariables(designValues);
const drawerClass = styles.drawer ?? '';

// Ant Design's own drawer rules are more specific than a CSS module class, so the deep teal goes in through the
// Drawer's semantic styles (CSS variables only, UI-02).
const drawerStyles: DrawerProps['styles'] = {
  section: { background: 'var(--flowza-navigation-background)' },
  header: {
    paddingBlock: 'var(--flowza-padding-xs)',
    paddingInline: 'var(--flowza-padding-xs)',
    borderBottom: 'none',
  },
  body: { display: 'flex', flexDirection: 'column', padding: 0 },
};

/**
 * The navigation on a phone (spec 007 UI-05): a menu button in the header opens a drawer from the left with the
 * same mark and navigation as the side panel. Choosing an item (or the mark) opens that page and closes the
 * drawer; Escape, the close button and a tap outside close it too. Ant Design keeps focus inside while it is open
 * and returns it to the menu button afterwards.
 */
export function NavigationDrawer({ roles }: { roles: readonly string[] }) {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const drawerId = useId();
  // Open only on the page where it was opened: going to another page closes it, whatever link was used.
  const [openOn, setOpenOn] = useState<string | null>(null);
  const open = openOn === pathname;
  const close = () => {
    setOpenOn(null);
  };

  return (
    <>
      <Button
        type="text"
        icon={<MenuOutlined aria-hidden />}
        aria-label={t('shell.openNavigation')}
        aria-expanded={open}
        aria-controls={open ? drawerId : undefined}
        onClick={() => {
          setOpenOn(pathname);
        }}
      />
      <Drawer
        id={drawerId}
        open={open}
        onClose={close}
        placement="left"
        size={designValues.layout.navigationWidth}
        aria-label={t('shell.navigationDialog')}
        closable={{ 'aria-label': t('shell.closeNavigation') }}
        rootStyle={drawerVariables}
        rootClassName={drawerClass}
        styles={drawerStyles}
      >
        <div className={styles.brand}>
          <BrandMark />
        </div>
        <SideNavigation roles={roles} onNavigate={close} />
      </Drawer>
    </>
  );
}
