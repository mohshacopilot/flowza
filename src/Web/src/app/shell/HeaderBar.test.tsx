import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { currentUserIs, defaultInstallation, installationIs, platformAdmin } from '../../test/handlers';
import { renderRoutes } from '../../test/render';
import { server } from '../../test/server';
import { routes } from '../routes';

const header = async () => within(await screen.findByRole('banner'));

/** The pages' own data never arrives: these tests look only at the frame. */
const pagesKeepLoading = http.get('*/api/*', async () => {
  await delay('infinite');
  return HttpResponse.json({});
});

describe('Header (UI-03-AC2)', () => {
  it.each([
    ['/', 'Home'],
    ['/admin/users', 'Users'],
    ['/admin/users/new', 'New user'],
    ['/admin/users/0190a5c4-0000-7000-8000-000000000042', 'User details'],
    ['/me', 'My profile'],
    ['/no/such/page', 'Page not found'],
  ])('UI_03_AC2_the_header_shows_the_title_of_%s', async (url, title) => {
    server.use(currentUserIs(platformAdmin), installationIs(defaultInstallation), pagesKeepLoading);
    renderRoutes(routes, url);

    expect(await (await header()).findByText(title)).toBeInTheDocument();
  });

  it('UI_03_AC2_the_browser_tab_shows_the_page_and_the_installation', async () => {
    server.use(
      currentUserIs(platformAdmin),
      installationIs({ name: 'State Bank of Example', initials: 'SB' }),
      pagesKeepLoading,
    );
    renderRoutes(routes, '/admin/users');

    await vi.waitFor(() => {
      expect(document.title).toBe('Users – State Bank of Example');
    });
  });

  it('UI_03_AC2_the_browser_tab_follows_the_page', async () => {
    server.use(currentUserIs(platformAdmin), installationIs(defaultInstallation), pagesKeepLoading);
    const { router } = renderRoutes(routes, '/');
    await vi.waitFor(() => {
      expect(document.title).toBe('Home – Flowza');
    });

    await router.navigate('/me');

    expect(await (await header()).findByText('My profile')).toBeInTheDocument();
    await vi.waitFor(() => {
      expect(document.title).toBe('My profile – Flowza');
    });
  });

  it('UI_03_AC2_the_title_is_text_not_a_second_heading', async () => {
    server.use(currentUserIs(platformAdmin), installationIs(defaultInstallation), pagesKeepLoading);
    renderRoutes(routes, '/admin/users');

    const bar = await header();
    await bar.findByText('Users');
    expect(bar.queryByRole('heading')).not.toBeInTheDocument();
  });

  it('UI_03_AC2_the_user_menu_shows_the_initials_and_the_display_name', async () => {
    server.use(currentUserIs(platformAdmin), pagesKeepLoading);
    renderRoutes(routes, '/');

    const button = (await header()).getByRole('button', { name: 'User menu for Asha Admin' });
    expect(within(button).getByText('AA')).toBeInTheDocument();
    expect(within(button).getByText('AA').closest('[aria-hidden="true"]')).not.toBeNull();
    expect(within(button).getByText('Asha Admin')).toBeInTheDocument();
  });

  it('UI_03_AC2_the_user_menu_offers_my_profile_and_log_out', async () => {
    server.use(currentUserIs(platformAdmin), pagesKeepLoading);
    renderRoutes(routes, '/');
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'User menu for Asha Admin' }));

    expect(await screen.findByRole('menuitem', { name: 'My profile' })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'Log out' })).toBeInTheDocument();
  });
});
