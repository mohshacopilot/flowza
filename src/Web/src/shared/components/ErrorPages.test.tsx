import { screen } from '@testing-library/react';
import { AppShell } from '../../app/shell/AppShell';
import { ApiError } from '../api/ApiError';
import { currentUserIs, plainUser } from '../../test/handlers';
import { renderRoutes, renderWithProviders as renderInProviders } from '../../test/render';
import { server } from '../../test/server';
import { AppErrorBoundary } from './AppErrorBoundary';
import { PageForError, RouteErrorPage } from './ErrorPages';

function Thrower({ error }: { error: unknown }): never {
  throw error;
}

describe('Error pages', () => {
  it('PLT_05_AC2_error_page_shows_the_reference_code', () => {
    renderInProviders(<PageForError error={new ApiError({ status: 500, reference: 'a1b2c3d4' })} />);

    expect(screen.getByRole('heading', { level: 1, name: 'Something went wrong' })).toBeInTheDocument();
    expect(screen.getByText('Reference code: a1b2c3d4')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Go to the home page' })).toHaveAttribute('href', '/');
  });

  it('PLT_05_AC2_error_without_a_server_reference_shows_no_reference_line', () => {
    renderInProviders(<PageForError error={new Error('render failed')} />);

    expect(screen.getByRole('heading', { level: 1, name: 'Something went wrong' })).toBeInTheDocument();
    expect(screen.queryByText(/Reference code/)).not.toBeInTheDocument();
  });

  it('IAM_02_AC1_a_403_from_the_server_shows_the_no_access_page', () => {
    renderInProviders(<PageForError error={new ApiError({ status: 403, reference: 'ffff0000' })} />);

    expect(screen.getByRole('heading', { level: 1, name: 'No access' })).toBeInTheDocument();
  });

  it.each([
    [403, 'No access'],
    [404, 'Page not found'],
  ])('UI_09_AC1_the_%i_page_shows_an_icon_in_the_brand_colour_not_a_stock_picture', (status, title) => {
    const { container } = renderInProviders(<PageForError error={new ApiError({ status })} />);

    expect(screen.getByRole('heading', { level: 1, name: title })).toBeInTheDocument();
    // Status "info" takes the brand colour (colorInfo); Ant Design's 403/404 pictures have colours of their own.
    expect(container.querySelector('.ant-result-info')).not.toBeNull();
    expect(container.querySelector('.ant-result-image')).toBeNull();
  });

  it('a_404_from_the_server_shows_the_not_found_page', () => {
    renderInProviders(<PageForError error={new ApiError({ status: 404 })} />);

    expect(screen.getByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument();
  });

  it('PLT_05_AC2_app_error_boundary_catches_errors_outside_the_router', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    renderInProviders(
      <AppErrorBoundary>
        <Thrower error={new ApiError({ status: 500, reference: '0badc0de' })} />
      </AppErrorBoundary>,
    );

    expect(screen.getByText('Reference code: 0badc0de')).toBeInTheDocument();
  });

  it('PLT_05_AC2_a_page_that_fails_keeps_the_shell_and_shows_the_error_page', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    server.use(currentUserIs(plainUser));
    renderRoutes(
      [
        {
          path: '/',
          Component: AppShell,
          children: [
            {
              ErrorBoundary: RouteErrorPage,
              children: [
                {
                  index: true,
                  Component: () => <Thrower error={new ApiError({ status: 500, reference: 'deadbeef' })} />,
                },
              ],
            },
          ],
        },
      ],
      '/',
    );

    expect(await screen.findByText('Reference code: deadbeef')).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Main navigation' })).toBeInTheDocument();
  });
});
