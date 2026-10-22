import { expect, test } from '@playwright/test';
import {
  chooseNewPassword,
  confirmAction,
  createUser,
  demoAdmin,
  demoPassword,
  expectNoAccessibilityViolations,
  logIn,
  logInToApp,
  newUserPage,
  openUser,
  readTemporaryPassword,
  uniqueUsername,
} from '../support/flowza.ts';

test('IAM_04_AC1_IAM_03_admin_creates_a_user_who_must_choose_a_password_at_the_first_login', async ({ browser }) => {
  const username = uniqueUsername('new');
  const admin = await newUserPage(browser);
  await logInToApp(admin, demoAdmin.username, demoPassword, demoAdmin.displayName, '/admin/users');
  await expectNoAccessibilityViolations(admin);

  await admin.goto('/admin/users/new');
  await expectNoAccessibilityViolations(admin);
  const temporaryPassword = await createUser(admin, { username, displayName: 'New Person', roles: ['App Designer'] });
  await expectNoAccessibilityViolations(admin);

  // The new user: the temporary password works once and must be replaced (IAM-03, IAM-04-AC2).
  const user = await newUserPage(browser);
  await logIn(user, username, temporaryPassword);
  await expectNoAccessibilityViolations(user);
  await chooseNewPassword(user, temporaryPassword, 'my-own-passphrase-1');
  await expect(user.getByRole('button', { name: 'User menu for New Person' })).toBeVisible();
  await expect(user.getByRole('heading', { level: 1, name: 'Welcome' })).toBeVisible();
});

test('IAM_04_AC2_a_reset_shows_a_temporary_password_once_and_the_user_must_change_it', async ({ browser }) => {
  const username = uniqueUsername('reset');
  const admin = await newUserPage(browser);
  await logInToApp(admin, demoAdmin.username, demoPassword, demoAdmin.displayName);
  const first = await createUser(admin, { username, displayName: 'Reset Person' });

  const user = await newUserPage(browser);
  await logIn(user, username, first);
  await chooseNewPassword(user, first, 'chosen-passphrase-1');
  await expect(user.getByRole('heading', { level: 1, name: 'Welcome' })).toBeVisible();

  await openUser(admin, username);
  await expectNoAccessibilityViolations(admin);
  await confirmAction(admin, 'Reset password');
  const dialog = admin.getByRole('dialog', { name: `Temporary password for ${username}` });
  await expect(dialog).toBeVisible();
  const second = await readTemporaryPassword(admin);
  await expectNoAccessibilityViolations(admin, { only: '[role="dialog"]' });
  await dialog.getByRole('button', { name: 'Done' }).click();
  await expect(admin.getByRole('textbox', { name: 'Temporary password' })).toHaveCount(0);

  // The old password no longer works; the temporary one leads to the forced change.
  const again = await newUserPage(browser);
  await logIn(again, username, 'chosen-passphrase-1');
  await expect(again.getByText('Username or password is incorrect.')).toBeVisible();
  await logIn(again, username, second);
  await chooseNewPassword(again, second, 'another-passphrase-2');
  await expect(again.getByRole('heading', { level: 1, name: 'Welcome' })).toBeVisible();
});

test('IAM_04_AC3_IAM_04_AC5_deactivation_ends_the_users_session_and_activation_lets_them_back_in', async ({ browser }) => {
  const username = uniqueUsername('deact');
  const admin = await newUserPage(browser);
  await logInToApp(admin, demoAdmin.username, demoPassword, demoAdmin.displayName);
  const temporaryPassword = await createUser(admin, { username, displayName: 'Leaving Person' });

  const user = await newUserPage(browser);
  await logIn(user, username, temporaryPassword, '/me');
  await chooseNewPassword(user, temporaryPassword, 'leaving-passphrase-1');
  await expect(user.getByRole('heading', { level: 1, name: 'My profile' })).toBeVisible();

  await openUser(admin, username);
  await confirmAction(admin, 'Deactivate');
  await expect(admin.getByText(`${username} was deactivated.`)).toBeVisible();

  // Their next action is refused and the session ends (IAM-04-AC3): back at the login page.
  await user.reload();
  await expect(user).toHaveURL(/\/account\/login/);
  await logIn(user, username, 'leaving-passphrase-1');
  await expect(user.getByText('Username or password is incorrect.')).toBeVisible();

  // Activated again, the same user logs in with the same password (IAM-04-AC5).
  await admin.getByRole('region', { name: 'Access' }).getByRole('button', { name: 'Activate' }).click();
  await expect(admin.getByText(`${username} was activated.`)).toBeVisible();
  await logInToApp(user, username, 'leaving-passphrase-1', 'Leaving Person');
});
