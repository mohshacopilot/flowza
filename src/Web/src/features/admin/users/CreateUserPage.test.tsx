import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { routes } from '../../../app/routes';
import { currentUserIs, platformAdmin, problem } from '../../../test/handlers';
import { renderRoutes } from '../../../test/render';
import { server } from '../../../test/server';
import { ravi, userIs, usersUrl } from './usersTestData';

const field = (name: string) => screen.getByRole('textbox', { name });

async function fillValidUser(user: ReturnType<typeof userEvent.setup>) {
  await user.type(await screen.findByRole('textbox', { name: 'Username' }), 'ravi.k');
  await user.type(field('Display name'), 'Ravi Kumar');
  await user.type(field('Email'), 'ravi@example.test');
}

describe('Create user', () => {
  beforeEach(() => {
    server.use(currentUserIs(platformAdmin));
  });

  it('IAM_04_AC1_admin_creates_a_user_with_several_roles', async () => {
    let body: unknown;
    server.use(
      http.post(usersUrl, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ id: ravi.id, temporaryPassword: 'Tmp-7hQ2-xK9w' }, { status: 201 });
      }),
    );
    renderRoutes(routes, '/admin/users/new');
    const user = userEvent.setup();

    await fillValidUser(user);
    await user.click(screen.getByRole('checkbox', { name: 'App Designer' }));
    await user.click(screen.getByRole('checkbox', { name: 'Platform Admin' }));
    expect(screen.getByRole('checkbox', { name: 'User' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'User' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Create user' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'User created' })).toBeInTheDocument();
    expect(body).toEqual({
      username: 'ravi.k',
      displayName: 'Ravi Kumar',
      email: 'ravi@example.test',
      mobile: null,
      roles: ['PlatformAdmin', 'AppDesigner'],
    });
  });

  it('IAM_04_AC2_the_temporary_password_is_shown_once_with_a_copy_button', async () => {
    server.use(
      http.post(usersUrl, () =>
        HttpResponse.json({ id: ravi.id, temporaryPassword: 'Tmp-7hQ2-xK9w' }, { status: 201 }),
      ),
      userIs(ravi),
    );
    renderRoutes(routes, '/admin/users/new');
    const user = userEvent.setup();

    await fillValidUser(user);
    await user.click(screen.getByRole('button', { name: 'Create user' }));

    expect(await screen.findByRole('textbox', { name: 'Temporary password' })).toHaveValue('Tmp-7hQ2-xK9w');
    expect(
      screen.getByText(
        'This password is shown only once. Copy it now and give it to the user in a safe way.',
      ),
    ).toBeInTheDocument();
    expect(screen.getByText(/They must change it at their first login/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Copy password' }));
    expect(await navigator.clipboard.readText()).toBe('Tmp-7hQ2-xK9w');
    expect(await screen.findByText('The password was copied.')).toBeInTheDocument();

    // Leaving the screen drops the password: the user's page does not show it.
    await user.click(screen.getByRole('button', { name: 'Open the user' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'User ravi.k' })).toBeInTheDocument();
    expect(screen.queryByDisplayValue('Tmp-7hQ2-xK9w')).not.toBeInTheDocument();
  });

  it('IAM_04_AC4_a_username_outside_the_format_is_rejected_before_sending', async () => {
    const post = vi.fn();
    server.use(
      http.post(usersUrl, () => {
        post();
        return HttpResponse.json({}, { status: 201 });
      }),
    );
    renderRoutes(routes, '/admin/users/new');
    const user = userEvent.setup();

    await user.type(await screen.findByRole('textbox', { name: 'Username' }), 'ab');
    await user.type(field('Display name'), 'Ravi Kumar');
    await user.type(field('Email'), 'not-an-email');
    await user.type(screen.getByRole('textbox', { name: 'Mobile (optional)' }), 'call me');
    await user.click(screen.getByRole('button', { name: 'Create user' }));

    const username = field('Username');
    expect(username).toHaveAttribute('aria-invalid', 'true');
    expect(username).toHaveAccessibleDescription(
      expect.stringContaining("A username has 3–50 characters: letters, digits, '.', '_' or '-'."),
    );
    expect(field('Email')).toHaveAccessibleDescription('Enter a valid email address.');
    expect(screen.getByRole('textbox', { name: 'Mobile (optional)' })).toHaveAttribute(
      'aria-invalid',
      'true',
    );
    expect(post).not.toHaveBeenCalled();
  });

  it('IAM_04_AC4_a_username_or_email_already_used_is_shown_next_to_the_field', async () => {
    server.use(
      http.post(usersUrl, () =>
        problem(400, {
          code: 'users.username_taken',
          detail: 'This username is already used.',
          errors: { username: ['users.username_taken'] },
          reference: '0a1b2c3d',
        }),
      ),
    );
    renderRoutes(routes, '/admin/users/new');
    const user = userEvent.setup();

    await fillValidUser(user);
    await user.click(screen.getByRole('button', { name: 'Create user' }));

    await vi.waitFor(() => {
      expect(field('Username')).toHaveAttribute('aria-invalid', 'true');
    });
    expect(field('Username')).toHaveAccessibleDescription(
      expect.stringContaining('This username is already used.'),
    );
    expect(screen.getByRole('alert')).toHaveTextContent('Reference code: 0a1b2c3d');
    expect(screen.queryByRole('heading', { name: 'User created' })).not.toBeInTheDocument();
  });
});
