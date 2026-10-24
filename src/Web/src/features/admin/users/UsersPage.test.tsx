import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { routes } from '../../../app/routes';
import { currentUserIs, platformAdmin, problem } from '../../../test/handlers';
import { renderRoutes } from '../../../test/render';
import { server } from '../../../test/server';
import { asha, listItem, page, ravi, usersUrl } from './usersTestData';

/** Answers the list with these users and records every query string the screen asked for. */
function listAnswers(...pages: ReturnType<typeof page>[]) {
  const queries: URLSearchParams[] = [];
  server.use(
    http.get(usersUrl, ({ request }) => {
      const query = new URL(request.url).searchParams;
      queries.push(query);
      const pageNumber = Number(query.get('page') ?? '1');
      return HttpResponse.json(pages[Math.min(pageNumber, pages.length) - 1]);
    }),
  );
  return queries;
}

const usersTable = async () => within(await screen.findByRole('table'));

describe('Users list', () => {
  beforeEach(() => {
    server.use(currentUserIs(platformAdmin));
  });

  it('IAM_04_AC1_users_list_shows_username_name_email_roles_and_status', async () => {
    listAnswers(page([listItem(asha), { ...listItem(ravi), status: 'Locked' }]));
    renderRoutes(routes, '/admin/users');

    const table = await usersTable();
    const ashaRow = within((await table.findByRole('link', { name: 'asha' })).closest('tr') as HTMLElement);
    expect(ashaRow.getByText('Asha Admin')).toBeInTheDocument();
    expect(ashaRow.getByText('asha@example.test')).toBeInTheDocument();
    expect(ashaRow.getByText('Platform Admin')).toBeInTheDocument();
    expect(ashaRow.getByText('Active')).toBeInTheDocument();
    expect(table.getByRole('link', { name: 'asha' })).toHaveAttribute('href', `/admin/users/${asha.id}`);

    const raviRow = within(table.getByRole('link', { name: 'ravi.k' }).closest('tr') as HTMLElement);
    expect(raviRow.getByText('Locked')).toBeInTheDocument();
  });

  it('UI_05_AC1_a_list_with_rows_scrolls_sideways_inside_its_box', async () => {
    listAnswers(page([listItem(asha)]));
    renderRoutes(routes, '/admin/users');

    await (await usersTable()).findByRole('link', { name: 'asha' });
    expect(screen.getByRole('table')).toHaveStyle({ width: 'max-content' });
  });

  it('UI_06_AC1_an_empty_list_fits_the_width_so_no_scroll_area_without_focusable_content_remains', async () => {
    listAnswers(page([]));
    renderRoutes(routes, '/admin/users?search=nobody');

    expect(await screen.findByText('No users match your search.')).toBeInTheDocument();
    // The empty text shows while loading too; the table changes once the empty answer has arrived.
    await vi.waitFor(() => {
      expect(screen.getByRole('table')).not.toHaveStyle({ width: 'max-content' });
    });
  });

  it('UI_06_AC1_a_failed_list_fits_the_width_too', async () => {
    server.use(http.get(usersUrl, () => problem(500)));
    renderRoutes(routes, '/admin/users');

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('table')).not.toHaveStyle({ width: 'max-content' });
  });

  it('IAM_04_search_and_status_filter_are_sent_to_the_server_and_start_at_page_1', async () => {
    const queries = listAnswers(page([listItem(asha)]));
    renderRoutes(routes, '/admin/users?page=2');
    const user = userEvent.setup();
    await usersTable();

    await user.type(
      screen.getByRole('searchbox', { name: 'Search by username, name or email' }),
      'ravi{Enter}',
    );
    await vi.waitFor(() => {
      expect(queries.at(-1)?.get('search')).toBe('ravi');
    });
    expect(queries.at(-1)?.get('page')).toBe('1');

    await user.click(screen.getByRole('combobox', { name: 'Status' }));
    // Ant Design draws the visible options as plain items with a title (its role="option" list is for screen readers).
    await user.click(await screen.findByTitle('Inactive'));
    await vi.waitFor(() => {
      expect(queries.at(-1)?.get('status')).toBe('Inactive');
    });
    expect(queries.at(-1)?.get('search')).toBe('ravi');
    expect(queries.at(-1)?.get('size')).toBe('25');
  });

  it('IAM_04_back_and_forward_keep_the_search_box_in_step_with_the_list', async () => {
    const queries = listAnswers(page([listItem(asha)]));
    const { router } = renderRoutes(routes, '/admin/users?search=asha');
    const user = userEvent.setup();
    const box = () => screen.getByRole('searchbox', { name: 'Search by username, name or email' });
    await usersTable();
    expect(box()).toHaveValue('asha');

    await user.clear(box());
    await user.type(box(), 'ravi{Enter}');
    await vi.waitFor(() => {
      expect(queries.at(-1)?.get('search')).toBe('ravi');
    });

    await act(() => router.navigate(-1)); // the browser's Back button

    // The list for "asha" comes from the cache (no new request); the box must show what the list is filtered by.
    await vi.waitFor(() => {
      expect(box()).toHaveValue('asha');
    });
    expect(router.state.location.search).toBe('?search=asha');
  });

  it('IAM_04_list_pages_through_users_25_at_a_time_with_the_total', async () => {
    const many = Array.from({ length: 25 }, (_, i) => ({
      ...listItem(ravi),
      id: `id-${i}`,
      username: `user${i}`,
    }));
    const queries = listAnswers(page(many, 30, 1), page([listItem(asha)], 30, 2));
    renderRoutes(routes, '/admin/users');
    const user = userEvent.setup();

    expect(await screen.findByText('1–25 of 30 users')).toBeInTheDocument();
    await user.click(screen.getByRole('listitem', { name: '2' }));

    expect(await screen.findByRole('link', { name: 'asha' })).toBeInTheDocument();
    expect(queries.at(-1)?.get('page')).toBe('2');
    expect(screen.getByText('26–30 of 30 users')).toBeInTheDocument();
  });

  it('IAM_04_AC1_new_user_button_opens_the_create_screen', async () => {
    listAnswers(page([]));
    renderRoutes(routes, '/admin/users');
    const user = userEvent.setup();

    expect(await screen.findByText('No users match your search.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'New user' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'New user' })).toBeInTheDocument();
  });

  it('PLT_05_AC2_a_failed_list_shows_the_message_and_reference_code', async () => {
    server.use(http.get(usersUrl, () => problem(500, { code: 'unexpected', reference: '7c1d2e3f' })));
    renderRoutes(routes, '/admin/users');

    expect(
      await screen.findByText('Something went wrong on the server. Please try again.'),
    ).toBeInTheDocument();
    expect(screen.getByText('Reference code: 7c1d2e3f')).toBeInTheDocument();
  });
});
