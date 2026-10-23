import { screen, within } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { currentUserIs, installationIs, installationUrl, platformAdmin, problem } from '../../test/handlers';
import { renderRoutes } from '../../test/render';
import { server } from '../../test/server';
import { viewportWidth } from '../../test/viewport';
import { routes } from '../routes';

// The mark sits at the top of the side navigation panel (T007); at desktop width the panel is open with the name.
const panel = async () => within(await screen.findByRole('complementary'));

describe('Brand mark in the frame (UI-08)', () => {
  let viewport: { restore: () => void };
  beforeEach(() => {
    viewport = viewportWidth(1280);
  });
  afterEach(() => {
    viewport.restore();
  });

  it('UI_08_AC2_the_name_and_initials_from_the_installation_appear_in_the_frame', async () => {
    server.use(
      currentUserIs(platformAdmin),
      installationIs({ name: 'State Bank of Example', initials: 'SB' }),
    );
    renderRoutes(routes, '/');

    const home = await (await panel()).findByRole('link', { name: 'State Bank of Example home page' });
    expect(home).toHaveAttribute('href', '/');
    expect(within(home).getByText('SB')).toBeInTheDocument();
    expect(within(home).getByText('State Bank of Example')).toBeInTheDocument();
  });

  it('UI_08_AC2_without_a_configured_name_the_frame_shows_Flowza_and_FL', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');

    const home = await (await panel()).findByRole('link', { name: 'Flowza home page' });
    expect(within(home).getByText('FL')).toBeInTheDocument();
  });

  it('UI_08_AC1_a_100_character_name_is_shortened_on_screen_but_whole_for_hover_and_screen_readers', async () => {
    const name = `Directorate of ${'x'.repeat(85)}`;
    expect(name).toHaveLength(100);
    server.use(currentUserIs(platformAdmin), installationIs({ name, initials: 'DO' }));
    renderRoutes(routes, '/');

    const home = await (await panel()).findByRole('link', { name: `${name} home page` });
    expect(home).toHaveAttribute('title', name);
    // The text itself is complete; the "…" comes from the stylesheet (text-overflow: ellipsis), which jsdom does
    // not draw. The screenshot tests (T011) show the shortened name.
    expect(within(home).getByText(name)).toBeInTheDocument();
  });

  it('the_initials_are_not_read_out_twice', async () => {
    server.use(currentUserIs(platformAdmin));
    renderRoutes(routes, '/');

    const home = await (await panel()).findByRole('link', { name: 'Flowza home page' });
    expect(within(home).getByText('FL')).toHaveAttribute('aria-hidden', 'true');
  });

  it('shows_only_the_outline_of_the_mark_while_the_name_loads', async () => {
    server.use(
      currentUserIs(platformAdmin),
      http.get(installationUrl, async () => {
        await delay('infinite');
        return HttpResponse.json({});
      }),
    );
    renderRoutes(routes, '/');

    const home = await (await panel()).findByRole('link', { name: 'Home page' });
    expect(home).not.toHaveAttribute('title');
    expect(home.textContent).toBe('');
  });

  it('keeps_the_frame_working_when_the_name_cannot_be_loaded', async () => {
    server.use(
      currentUserIs(platformAdmin),
      http.get(installationUrl, () => problem(500)),
    );
    renderRoutes(routes, '/');

    expect(await (await panel()).findByRole('link', { name: 'Home page' })).toHaveAttribute('href', '/');
    expect(await screen.findByRole('heading', { level: 1, name: 'Welcome' })).toBeInTheDocument();
  });
});
