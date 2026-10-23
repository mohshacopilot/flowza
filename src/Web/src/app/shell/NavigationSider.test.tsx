import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { currentUserIs, plainUser, platformAdmin } from '../../test/handlers';
import { renderRoutes } from '../../test/render';
import { server } from '../../test/server';
import { viewportWidth } from '../../test/viewport';
import { routes } from '../routes';
import { navigationPreferenceKey } from './useNavigationPreference';

const mainNavigation = () => screen.findByRole('navigation', { name: 'Main navigation' });

describe('Side navigation', () => {
  let viewport: { restore: () => void };
  beforeEach(() => {
    viewport = viewportWidth(1280);
  });
  afterEach(() => {
    viewport.restore();
  });

  it('UI_03_AC1_a_platform_admin_sees_the_administration_group', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');

    const nav = within(await mainNavigation());
    expect(nav.getByText('Administration')).toBeInTheDocument();
    expect(nav.getByRole('link', { name: 'Users' })).toBeInTheDocument();
  });

  it('UI_03_AC1_a_user_without_the_role_sees_no_administration_group', async () => {
    server.use(currentUserIs(plainUser));
    renderRoutes(routes, '/');

    const nav = within(await mainNavigation());
    expect(nav.getByRole('link', { name: 'Home' })).toBeInTheDocument();
    expect(nav.queryByText('Administration')).not.toBeInTheDocument();
  });

  it('UI_03_AC1_the_current_item_is_highlighted', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/admin/users');

    const users = within(await mainNavigation()).getByRole('link', { name: 'Users' });
    expect(users).toHaveAttribute('aria-current', 'page');
    expect(users.closest('li')).toHaveClass('ant-menu-item-selected');
    expect(within(await mainNavigation()).getByRole('link', { name: 'Home' })).not.toHaveAttribute(
      'aria-current',
    );
  });

  it('UI_03_AC1_the_installation_mark_and_name_sit_at_the_top_of_the_navigation_panel', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');

    const panel = within(await screen.findByRole('complementary'));
    expect(await panel.findByRole('link', { name: 'Flowza home page' })).toHaveAttribute('href', '/');
    expect(panel.getByText('Flowza')).toBeInTheDocument();
  });

  it('UI_04_AC1_collapsed_the_items_keep_their_names_and_show_them_as_tooltips', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Collapse navigation' }));

    const nav = within(await mainNavigation());
    expect(screen.getByRole('button', { name: 'Expand navigation' })).toBeInTheDocument();
    expect(nav.getByRole('menu')).toHaveClass('ant-menu-inline-collapsed');
    // The group heading would be cut to one letter; a line separates the group instead.
    expect(nav.queryByText('Administration')).not.toBeInTheDocument();
    expect(nav.getByRole('link', { name: 'Home' })).toBeInTheDocument();
    const users = nav.getByRole('link', { name: 'Users' });

    await user.hover(users.closest('li') ?? users);

    await vi.waitFor(() => {
      const tooltips = screen.getAllByRole('tooltip').map((tooltip) => tooltip.textContent);
      expect(tooltips).toContain('Users');
    });
    // Collapsed, the mark stays and the name is in the link's accessible name only.
    expect(screen.getByRole('link', { name: 'Flowza home page' })).not.toHaveTextContent('Flowza');
  });

  it('UI_04_AC1_after_reloading_the_navigation_is_still_collapsed', async () => {
    server.use(currentUserIs(platformAdmin));
    const first = renderRoutes(routes, '/');
    await userEvent.click(await screen.findByRole('button', { name: 'Collapse navigation' }));
    expect(window.localStorage.getItem(navigationPreferenceKey)).toBe('true');
    first.unmount();

    renderRoutes(routes, '/');

    expect(await screen.findByRole('button', { name: 'Expand navigation' })).toBeInTheDocument();
    expect(within(await mainNavigation()).getByRole('menu')).toHaveClass('ant-menu-inline-collapsed');
  });

  it('UI_04_AC1_the_collapse_button_works_with_the_keyboard', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');
    const user = userEvent.setup();

    (await screen.findByRole('button', { name: 'Collapse navigation' })).focus();
    await user.keyboard('{Enter}');
    expect(screen.getByRole('button', { name: 'Expand navigation' })).toHaveFocus();

    await user.keyboard(' ');
    expect(screen.getByRole('button', { name: 'Collapse navigation' })).toHaveFocus();
    expect(window.localStorage.getItem(navigationPreferenceKey)).toBe('false');
  });
});
