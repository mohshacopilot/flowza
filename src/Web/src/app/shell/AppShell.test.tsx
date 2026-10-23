import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { browser } from '../../shared/auth/session';
import { currentUserIs, plainUser, platformAdmin, problem } from '../../test/handlers';
import { renderRoutes } from '../../test/render';
import { server } from '../../test/server';
import { routes } from '../routes';

const mainNavigation = () => screen.findByRole('navigation', { name: 'Main navigation' });

describe('App shell', () => {
  it('IAM_02_AC1_platform_admin_sees_the_users_screen_in_the_navigation', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');

    const nav = within(await mainNavigation());
    expect(nav.getByRole('link', { name: 'Home' })).toHaveAttribute('aria-current', 'page');
    expect(nav.getByRole('link', { name: 'Users' })).toHaveAttribute('href', '/admin/users');
  });

  it('IAM_02_AC1_user_without_platform_admin_does_not_see_the_users_screen', async () => {
    server.use(currentUserIs(plainUser));
    renderRoutes(routes, '/');

    const nav = within(await mainNavigation());
    expect(nav.getByRole('link', { name: 'Home' })).toBeInTheDocument();
    expect(nav.queryByRole('link', { name: 'Users' })).not.toBeInTheDocument();
  });

  it('IAM_02_AC1_opening_an_admin_screen_without_the_role_shows_the_403_page', async () => {
    server.use(currentUserIs(plainUser));
    renderRoutes(routes, '/admin/users');

    expect(await screen.findByRole('heading', { level: 1, name: 'No access' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Users' })).not.toBeInTheDocument();
  });

  it('IAM_02_AC1_platform_admin_opens_the_users_screen', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/admin/users');

    expect(await screen.findByRole('heading', { level: 1, name: 'Users' })).toBeInTheDocument();
    const nav = within(await mainNavigation());
    expect(nav.getByRole('link', { name: 'Users' })).toHaveAttribute('aria-current', 'page');
  });

  it('shows_the_installation_name_and_the_user_in_the_header', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');

    const header = within(await screen.findByRole('banner'));
    // The name comes from Identity (UI-08, T006), a moment after the frame.
    expect(await header.findByRole('link', { name: 'Flowza home page' })).toHaveAttribute('href', '/');
    expect(header.getByRole('button', { name: 'User menu for Asha Admin' })).toBeInTheDocument();
  });

  it('shows_the_404_page_for_an_unknown_address_inside_the_shell', async () => {
    server.use(currentUserIs(plainUser));
    renderRoutes(routes, '/no/such/page');

    expect(await screen.findByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument();
    expect(await mainNavigation()).toBeInTheDocument();
  });

  it('NFR_11_skip_link_is_the_first_tab_stop_and_points_to_the_main_content', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');
    await mainNavigation();

    await userEvent.tab();

    const skip = screen.getByRole('link', { name: 'Skip to main content' });
    expect(skip).toHaveFocus();
    expect(skip).toHaveAttribute('href', '#main-content');
    expect(screen.getByRole('main')).toHaveAttribute('id', 'main-content');
  });

  // Arrow keys and Enter inside the menu need real layout (jsdom has none); the end-to-end tests (T029) cover them.
  it('NFR_11_user_menu_opens_with_Enter_and_focuses_its_first_item', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');
    const user = userEvent.setup();

    (await screen.findByRole('button', { name: 'User menu for Asha Admin' })).focus();
    await user.keyboard('{Enter}');

    const profile = await screen.findByRole('menuitem', { name: 'My profile' });
    await vi.waitFor(() => {
      expect(profile).toHaveFocus();
    });
    expect(screen.getByRole('menuitem', { name: 'Log out' })).toBeInTheDocument();
  });

  it('NFR_11_Escape_closes_the_user_menu_and_returns_focus_to_its_button', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');
    const user = userEvent.setup();
    const menuButton = await screen.findByRole('button', { name: 'User menu for Asha Admin' });

    menuButton.focus();
    await user.keyboard('{Enter}');
    const profile = await screen.findByRole('menuitem', { name: 'My profile' });
    await vi.waitFor(() => {
      expect(profile).toHaveFocus();
    });
    await user.keyboard('{Escape}');

    await vi.waitFor(() => {
      expect(screen.queryByRole('menuitem', { name: 'My profile' })).not.toBeInTheDocument();
    });
    expect(menuButton).toHaveFocus();
  });

  it('opening_my_profile_from_the_user_menu', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'User menu for Asha Admin' }));
    await user.click(await screen.findByRole('menuitem', { name: 'My profile' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'My profile' })).toBeInTheDocument();
  });

  it('IAM_01_log_out_from_the_user_menu', async () => {
    const assign = vi.spyOn(browser, 'assign').mockImplementation(() => undefined);
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'User menu for Asha Admin' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Log out' }));

    expect(assign).toHaveBeenCalledWith('/bff/logout');
  });

  it('IAM_01_not_logged_in_goes_to_the_login_page_and_back_afterwards', async () => {
    const assign = vi.spyOn(browser, 'assign').mockImplementation(() => undefined);
    server.use(http.get('*/bff/user', () => problem(401)));
    renderRoutes(routes, '/');

    await vi.waitFor(() => {
      expect(assign).toHaveBeenCalledWith('/bff/login?returnUrl=%2F');
    });
  });

  it('PLT_05_AC2_shell_error_shows_the_reference_code_from_problem_details', async () => {
    server.use(http.get('*/bff/user', () => problem(500, { code: 'unexpected', reference: '4bf92f35' })));
    renderRoutes(routes, '/');

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Something went wrong' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Reference code: 4bf92f35')).toBeInTheDocument();
  });
});
