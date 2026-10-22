import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { routes } from '../../../app/routes';
import type { UserDetails } from '../../../shared/api/generated/identity/model';
import { currentUserIs, platformAdmin, problem } from '../../../test/handlers';
import { renderRoutes } from '../../../test/render';
import { server } from '../../../test/server';
import { asha, noContent, ravi, userIs, userUrl } from './usersTestData';

const openUser = async (user: UserDetails) => {
  renderRoutes(routes, `/admin/users/${user.id}`);
  await screen.findByRole('heading', { level: 1, name: `User ${user.username}` });
};

const section = (name: string) => within(screen.getByRole('region', { name }));

/** Records each POST to /users/{id}/<action> and answers it with the given response. */
function actionAnswers(action: string, answer: () => Response = noContent) {
  const calls: string[] = [];
  server.use(
    http.post(`${userUrl}/${action}`, ({ params }) => {
      calls.push(String(params.id));
      return answer();
    }),
  );
  return calls;
}

describe('User details', () => {
  beforeEach(() => {
    server.use(currentUserIs(platformAdmin));
  });

  it('IAM_04_AC1_admin_edits_name_email_and_mobile_and_the_row_version_goes_along', async () => {
    let body: unknown;
    server.use(
      userIs(ravi),
      http.put(userUrl, async ({ request }) => {
        body = await request.json();
        return noContent();
      }),
    );
    await openUser(ravi);
    const user = userEvent.setup();
    const details = section('Details');

    expect(details.getByRole('textbox', { name: 'Username' })).toHaveAttribute('readonly');
    const name = details.getByRole('textbox', { name: 'Display name' });
    await user.clear(name);
    await user.type(name, 'Ravi K.');
    await user.clear(details.getByRole('textbox', { name: 'Mobile (optional)' }));
    await user.click(details.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('The changes were saved.')).toBeInTheDocument();
    expect(body).toEqual({
      displayName: 'Ravi K.',
      email: 'ravi@example.test',
      mobile: null,
      version: ravi.version,
    });
  });

  it('IAM_04_a_change_made_meanwhile_by_someone_else_is_refused_and_can_be_reloaded', async () => {
    let gets = 0;
    server.use(
      http.get(userUrl, () => {
        gets++;
        return HttpResponse.json(ravi);
      }),
      http.put(userUrl, () => problem(409, { code: 'common.changed_by_someone_else' })),
    );
    await openUser(ravi);
    const user = userEvent.setup();

    await user.click(section('Details').getByRole('button', { name: 'Save changes' }));

    expect(
      await screen.findByText(
        'Someone else changed this user in the meantime. Reload it and make your change again.',
      ),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Reload' }));
    await vi.waitFor(() => {
      expect(gets).toBe(2);
    });
  });

  it('IAM_02_AC2_admin_gives_a_user_several_system_roles', async () => {
    let body: unknown;
    server.use(
      userIs(ravi),
      http.put(`${userUrl}/roles`, async ({ request }) => {
        body = await request.json();
        return noContent();
      }),
    );
    await openUser(ravi);
    const user = userEvent.setup();
    const roles = section('System roles');

    expect(roles.getByRole('checkbox', { name: 'User' })).toBeChecked();
    await user.click(roles.getByRole('checkbox', { name: 'Platform Admin' }));
    await user.click(roles.getByRole('checkbox', { name: 'App Designer' }));
    await user.click(roles.getByRole('button', { name: 'Save roles' }));

    expect(await screen.findByText('The roles were saved.')).toBeInTheDocument();
    expect(body).toEqual({ roles: ['PlatformAdmin', 'AppDesigner'] });
  });

  it('IAM_02_AC2_removing_platform_admin_from_the_last_admin_shows_why_it_is_refused', async () => {
    server.use(
      userIs(asha),
      http.put(`${userUrl}/roles`, () =>
        problem(409, {
          code: 'users.last_active_platform_admin',
          detail: 'The last active Platform Admin cannot be deactivated or lose the Platform Admin role.',
        }),
      ),
    );
    await openUser(asha);
    const user = userEvent.setup();
    const roles = section('System roles');

    await user.click(roles.getByRole('checkbox', { name: 'Platform Admin' }));
    await user.click(roles.getByRole('button', { name: 'Save roles' }));

    expect(
      await roles.findByText(
        'This is the last active Platform Admin. Make another user Platform Admin first.',
      ),
    ).toBeInTheDocument();
  });

  it('IAM_04_AC3_admin_deactivates_a_user_after_confirming', async () => {
    server.use(userIs(ravi));
    const calls = actionAnswers('deactivate');
    await openUser(ravi);
    const user = userEvent.setup();

    await user.click(section('Access').getByRole('button', { name: 'Deactivate' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getAllByText('Deactivate ravi.k?').length).toBeGreaterThan(0);
    expect(calls).toEqual([]);
    await user.click(within(dialog).getByRole('button', { name: 'Yes, continue' }));

    expect(await screen.findByText('ravi.k was deactivated.')).toBeInTheDocument();
    expect(calls).toEqual([ravi.id]);
  });

  it('IAM_04_AC3_cancelling_the_confirmation_deactivates_nobody', async () => {
    server.use(userIs(ravi));
    const calls = actionAnswers('deactivate');
    await openUser(ravi);
    const user = userEvent.setup();

    await user.click(section('Access').getByRole('button', { name: 'Deactivate' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancel' }));

    await vi.waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
    expect(calls).toEqual([]);
  });

  it('IAM_02_AC2_deactivating_the_last_platform_admin_shows_why_it_is_refused', async () => {
    server.use(userIs(asha));
    actionAnswers('deactivate', () => problem(409, { code: 'users.last_active_platform_admin' }));
    await openUser(asha);
    const user = userEvent.setup();

    await user.click(section('Access').getByRole('button', { name: 'Deactivate' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Yes, continue' }),
    );

    expect(
      await section('Access').findByText(
        'This is the last active Platform Admin. Make another user Platform Admin first.',
      ),
    ).toBeInTheDocument();
  });

  it('IAM_04_AC5_an_inactive_user_can_be_activated_again', async () => {
    server.use(userIs({ ...ravi, status: 'Inactive' }));
    const calls = actionAnswers('activate');
    await openUser(ravi);
    const user = userEvent.setup();
    const access = section('Access');

    expect(access.queryByRole('button', { name: 'Deactivate' })).not.toBeInTheDocument();
    await user.click(access.getByRole('button', { name: 'Activate' }));

    expect(await screen.findByText('ravi.k was activated.')).toBeInTheDocument();
    expect(calls).toEqual([ravi.id]);
  });

  it('IAM_07_AC1_admin_unlocks_a_locked_user', async () => {
    server.use(userIs({ ...ravi, status: 'Locked' }));
    const calls = actionAnswers('unlock');
    await openUser(ravi);
    const user = userEvent.setup();
    const access = section('Access');

    expect(
      access.getByText('This account is locked for a while after too many wrong passwords.'),
    ).toBeInTheDocument();
    await user.click(access.getByRole('button', { name: 'Unlock' }));

    expect(await screen.findByText('ravi.k was unlocked.')).toBeInTheDocument();
    expect(calls).toEqual([ravi.id]);
  });

  it('IAM_07_AC1_unlock_is_offered_only_for_a_locked_user', async () => {
    server.use(userIs(ravi));
    await openUser(ravi);

    expect(section('Access').queryByRole('button', { name: 'Unlock' })).not.toBeInTheDocument();
  });

  it('IAM_04_AC2_reset_shows_the_temporary_password_once', async () => {
    server.use(userIs({ ...ravi, mustChangePassword: true }));
    const calls = actionAnswers('reset-password', () => HttpResponse.json({ password: 'Tmp-Rz4p-81Lm' }));
    await openUser(ravi);
    const user = userEvent.setup();

    expect(screen.getByText('Must change the password at the next login.')).toBeInTheDocument();
    await user.click(section('Access').getByRole('button', { name: 'Reset password' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Yes, continue' }),
    );

    const shown = await screen.findByRole('dialog', { name: 'Temporary password for ravi.k' });
    expect(within(shown).getByRole('textbox', { name: 'Temporary password' })).toHaveValue('Tmp-Rz4p-81Lm');
    expect(calls).toEqual([ravi.id]);

    await user.click(within(shown).getByRole('button', { name: 'Done' }));
    await vi.waitFor(() => {
      expect(screen.queryByDisplayValue('Tmp-Rz4p-81Lm')).not.toBeInTheDocument();
    });
  });

  it('IAM_04_an_unknown_user_shows_a_message_and_the_way_back', async () => {
    server.use(http.get(userUrl, () => problem(404, { code: 'users.not_found' })));
    renderRoutes(routes, `/admin/users/${ravi.id}`);

    expect(await screen.findByText('This user does not exist.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to users' })).toHaveAttribute('href', '/admin/users');
  });
});
