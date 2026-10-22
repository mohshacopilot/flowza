import { DownOutlined, LogoutOutlined, UserOutlined } from '@ant-design/icons';
import { Button, Dropdown, type MenuProps } from 'antd';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import type { CurrentUser } from '../../shared/auth/currentUser';
import { logout } from '../../shared/auth/session';

/**
 * The user menu in the header: my profile and log out. Follows the menu button pattern: Enter or Space opens it
 * with focus on the first item, the arrow keys move, Enter chooses, Escape closes and returns to the button.
 */
export function UserMenu({ user }: { user: CurrentUser }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const button = useRef<HTMLButtonElement>(null);

  const items: MenuProps['items'] = [
    { key: 'profile', icon: <UserOutlined aria-hidden />, label: t('shell.myProfile') },
    { type: 'divider' },
    { key: 'logout', icon: <LogoutOutlined aria-hidden />, label: t('shell.logOut') },
  ];

  const onClick: MenuProps['onClick'] = ({ key }) => {
    setOpen(false);
    if (key === 'profile') {
      void navigate('/me');
    } else if (key === 'logout') {
      logout();
    }
  };

  const onKeyDown: MenuProps['onKeyDown'] = (event) => {
    if (event.key === 'Escape') {
      setOpen(false);
      button.current?.focus();
    }
  };

  return (
    <Dropdown
      menu={{ items, onClick, onKeyDown }}
      trigger={['click']}
      open={open}
      onOpenChange={setOpen}
      autoFocus
    >
      <Button ref={button} type="text" aria-label={t('shell.userMenu', { name: user.displayName })}>
        <UserOutlined aria-hidden />
        <span>{user.displayName}</span>
        <DownOutlined aria-hidden />
      </Button>
    </Dropdown>
  );
}
