import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { routes } from '../../app/routes';
import type { MyProfile } from '../../shared/api/generated/identity/model';
import { currentUserIs, plainUser, problem } from '../../test/handlers';
import { renderRoutes } from '../../test/render';
import { server } from '../../test/server';

const meUrl = '*/api/identity/v1/me';
const passwordUrl = '*/api/identity/v1/me/password';

const uma: MyProfile = {
  id: '0195f0c2-0000-7000-8000-000000000003',
  username: 'uma',
  displayName: 'Uma User',
  email: 'uma@example.test',
  mobile: null,
  roles: ['AppDesigner', 'User'],
};

const openProfile = async () => {
  renderRoutes(routes, '/me');
  await screen.findByRole('heading', { level: 1, name: 'My profile' });
};

const passwordSection = () => within(screen.getByRole('region', { name: 'Change password' }));
const passwordField = (name: string) => passwordSection().getByLabelText(name, { selector: 'input' });

async function fillPasswords(
  user: ReturnType<typeof userEvent.setup>,
  current: string,
  next: string,
  repeat = next,
) {
  const typeInto = async (label: string, text: string) => {
    if (text !== '') {
      await user.type(passwordField(label), text);
    }
  };
  await typeInto('Current password', current);
  await typeInto('New password', next);
  await typeInto('Repeat the new password', repeat);
}

describe('My profile', () => {
  beforeEach(() => {
    server.use(
      currentUserIs(plainUser),
      http.get(meUrl, () => HttpResponse.json(uma)),
    );
  });

  it('shows_the_users_own_details_and_system_roles_read_only', async () => {
    await openProfile();
    const details = within(screen.getByRole('region', { name: 'Details' }));

    expect(await details.findByText('uma')).toBeInTheDocument();
    expect(details.getByText('Uma User')).toBeInTheDocument();
    expect(details.getByText('uma@example.test')).toBeInTheDocument();
    expect(details.getByText('Not given')).toBeInTheDocument();
    expect(details.getByText('App Designer')).toBeInTheDocument();
    expect(details.queryByRole('textbox')).not.toBeInTheDocument();
  });

  it('IAM_06_AC3_user_changes_their_own_password_after_entering_the_current_one', async () => {
    let body: unknown;
    server.use(
      http.post(passwordUrl, async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await openProfile();
    const user = userEvent.setup();

    await fillPasswords(user, 'old-secret-1', 'a-much-longer-passphrase');
    await user.click(passwordSection().getByRole('button', { name: 'Change password' }));

    expect(
      await screen.findByText('Your password was changed. Use the new one from your next login.'),
    ).toBeInTheDocument();
    // Only the current and the new password are sent; the repeat stays in the browser.
    expect(body).toEqual({ currentPassword: 'old-secret-1', newPassword: 'a-much-longer-passphrase' });
    // The form is emptied, so the passwords do not stay on the screen.
    expect(passwordField('Current password')).toHaveValue('');
    expect(passwordField('New password')).toHaveValue('');
    expect(passwordField('Repeat the new password')).toHaveValue('');
  });

  it('IAM_06_AC3_a_wrong_current_password_is_shown_at_its_field', async () => {
    server.use(
      http.post(passwordUrl, () =>
        problem(400, {
          code: 'passwords.current_wrong',
          errors: { currentPassword: ['passwords.current_wrong'] },
        }),
      ),
    );
    await openProfile();
    const user = userEvent.setup();

    await fillPasswords(user, 'not-my-password', 'a-much-longer-passphrase');
    await user.click(passwordSection().getByRole('button', { name: 'Change password' }));

    await vi.waitFor(() => {
      expect(passwordField('Current password')).toHaveAttribute('aria-invalid', 'true');
    });
    expect(passwordField('Current password')).toHaveAccessibleDescription(
      'The current password is not correct.',
    );
    expect(screen.queryByText(/Your password was changed/)).not.toBeInTheDocument();
  });

  it('IAM_06_AC1_a_new_password_the_server_refuses_shows_every_broken_rule', async () => {
    server.use(
      http.post(passwordUrl, () =>
        problem(400, {
          code: 'passwords.common',
          errors: { newPassword: ['passwords.common', 'passwords.same_as_username'] },
        }),
      ),
    );
    await openProfile();
    const user = userEvent.setup();

    await fillPasswords(user, 'old-secret-1', 'password1234');
    await user.click(passwordSection().getByRole('button', { name: 'Change password' }));

    await vi.waitFor(() => {
      expect(passwordField('New password')).toHaveAttribute('aria-invalid', 'true');
    });
    const description = passwordField('New password').getAttribute('aria-describedby') ?? '';
    const shown = description
      .split(' ')
      .map((id) => document.getElementById(id)?.textContent)
      .join(' ');
    expect(shown).toContain('This password is too common. Choose another one.');
    expect(shown).toContain('The password must not be your username.');
  });

  it('IAM_06_AC1_too_short_or_different_repeat_is_caught_before_sending', async () => {
    const post = vi.fn();
    server.use(
      http.post(passwordUrl, () => {
        post();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await openProfile();
    const user = userEvent.setup();

    await fillPasswords(user, '', 'short', 'other');
    await user.click(passwordSection().getByRole('button', { name: 'Change password' }));

    expect(await screen.findByText('Enter your current password.')).toBeInTheDocument();
    expect(screen.getByText('A password has at least 10 characters.')).toBeInTheDocument();
    expect(screen.getByText('The two new passwords are not the same.')).toBeInTheDocument();
    expect(post).not.toHaveBeenCalled();
  });

  it('IAM_06_the_new_password_must_differ_from_the_current_one', async () => {
    await openProfile();
    const user = userEvent.setup();

    await fillPasswords(user, 'same-password-1', 'same-password-1');
    await user.click(passwordSection().getByRole('button', { name: 'Change password' }));

    expect(
      await screen.findByText('The new password must be different from the current one.'),
    ).toBeInTheDocument();
  });

  it('PLT_05_AC2_a_failed_profile_load_shows_the_reference_code', async () => {
    server.use(http.get(meUrl, () => problem(500, { reference: '9e8d7c6b' })));
    await openProfile();

    expect(await screen.findByText('Reference code: 9e8d7c6b')).toBeInTheDocument();
    // Changing the password does not depend on the details being shown.
    expect(passwordSection().getByRole('button', { name: 'Change password' })).toBeInTheDocument();
  });
});
