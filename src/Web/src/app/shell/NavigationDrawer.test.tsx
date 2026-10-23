import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { currentUserIs, defaultInstallation, installationIs, platformAdmin } from '../../test/handlers';
import { renderRoutes } from '../../test/render';
import { server } from '../../test/server';
import { viewportWidth } from '../../test/viewport';
import { routes } from '../routes';

/** The pages' own data never arrives: these tests look only at the frame. */
const pagesKeepLoading = http.get('*/api/*', async () => {
  await delay('infinite');
  return HttpResponse.json({});
});

const menuButton = () => screen.findByRole('button', { name: 'Open navigation' });

describe('Phone frame (UI-05)', () => {
  let viewport: { restore: () => void };
  beforeEach(() => {
    viewport = viewportWidth(375);
    server.use(currentUserIs(platformAdmin), installationIs(defaultInstallation), pagesKeepLoading);
  });
  afterEach(() => {
    viewport.restore();
  });

  it('UI_05_AC2_a_phone_has_a_menu_button_instead_of_the_side_panel', async () => {
    renderRoutes(routes, '/');

    const button = await menuButton();
    expect(button).toHaveAttribute('aria-expanded', 'false');
    expect(within(screen.getByRole('banner')).getByText('Home')).toBeInTheDocument();
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Main navigation' })).not.toBeInTheDocument();
  });

  it('UI_05_AC2_the_menu_button_opens_the_navigation', async () => {
    renderRoutes(routes, '/');
    const user = userEvent.setup();

    await user.click(await menuButton());

    const drawer = await screen.findByRole('dialog', { name: 'Navigation' });
    expect(screen.getByRole('button', { name: 'Open navigation' })).toHaveAttribute('aria-expanded', 'true');
    const nav = within(within(drawer).getByRole('navigation', { name: 'Main navigation' }));
    expect(nav.getByRole('link', { name: 'Home' })).toHaveAttribute('aria-current', 'page');
    expect(nav.getByText('Administration')).toBeInTheDocument();
    expect(within(drawer).getByRole('link', { name: 'Flowza home page' })).toBeInTheDocument();
  });

  it('UI_05_AC2_choosing_an_item_opens_that_screen_closes_the_navigation_and_returns_focus', async () => {
    renderRoutes(routes, '/');
    const user = userEvent.setup();
    await user.click(await menuButton());
    const drawer = await screen.findByRole('dialog', { name: 'Navigation' });

    await user.click(within(drawer).getByRole('link', { name: 'Users' }));

    expect(await within(screen.getByRole('banner')).findByText('Users')).toBeInTheDocument();
    await vi.waitFor(() => {
      expect(screen.queryByRole('dialog', { name: 'Navigation' })).not.toBeInTheDocument();
    });
    const button = screen.getByRole('button', { name: 'Open navigation' });
    expect(button).toHaveAttribute('aria-expanded', 'false');
    await vi.waitFor(() => {
      expect(button).toHaveFocus();
    });
  });

  it('UI_05_AC2_choosing_the_current_page_closes_the_navigation_too', async () => {
    renderRoutes(routes, '/');
    const user = userEvent.setup();
    await user.click(await menuButton());
    const drawer = await screen.findByRole('dialog', { name: 'Navigation' });

    await user.click(within(drawer).getByRole('link', { name: 'Home' }));

    await vi.waitFor(() => {
      expect(screen.queryByRole('dialog', { name: 'Navigation' })).not.toBeInTheDocument();
    });
  });

  it('UI_05_AC2_Escape_closes_the_navigation_and_returns_focus_to_the_menu_button', async () => {
    renderRoutes(routes, '/');
    const user = userEvent.setup();
    const button = await menuButton();
    await user.click(button);
    await screen.findByRole('dialog', { name: 'Navigation' });

    await user.keyboard('{Escape}');

    await vi.waitFor(() => {
      expect(screen.queryByRole('dialog', { name: 'Navigation' })).not.toBeInTheDocument();
    });
    expect(button).toHaveAttribute('aria-expanded', 'false');
    await vi.waitFor(() => {
      expect(button).toHaveFocus();
    });
  });

  it('UI_05_AC2_the_close_button_has_a_name', async () => {
    renderRoutes(routes, '/');
    const user = userEvent.setup();
    await user.click(await menuButton());

    await user.click(await screen.findByRole('button', { name: 'Close navigation' }));

    await vi.waitFor(() => {
      expect(screen.queryByRole('dialog', { name: 'Navigation' })).not.toBeInTheDocument();
    });
  });

  it('UI_05_the_user_menu_keeps_its_name_on_a_phone', async () => {
    renderRoutes(routes, '/');

    expect(await screen.findByRole('button', { name: 'User menu for Asha Admin' })).toBeInTheDocument();
  });
});

describe('Tablet frame', () => {
  it('UI_05_from_768_px_the_side_panel_returns_and_the_menu_button_goes', async () => {
    const viewport = viewportWidth(768);
    server.use(currentUserIs(platformAdmin), pagesKeepLoading);
    renderRoutes(routes, '/');

    expect(await screen.findByRole('complementary')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Open navigation' })).not.toBeInTheDocument();
    viewport.restore();
  });
});
