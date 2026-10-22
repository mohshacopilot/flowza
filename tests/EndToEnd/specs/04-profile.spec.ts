import { expect, test } from '@playwright/test';
import {
  chooseNewPassword,
  createUser,
  demoAdmin,
  demoPassword,
  expectNoAccessibilityViolations,
  logIn,
  logInToApp,
  newUserPage,
  logOut,
  uniqueUsername,
} from '../support/flowza.ts';

test('IAM_06_AC3_a_user_changes_their_own_password_on_my_profile', async ({ browser }) => {
  const username = uniqueUsername('own');
  const admin = await newUserPage(browser);
  await logInToApp(admin, demoAdmin.username, demoPassword, demoAdmin.displayName);
  const temporaryPassword = await createUser(admin, { username, displayName: 'Own Person' });

  const page = await newUserPage(browser);
  await logIn(page, username, temporaryPassword, '/me');
  await chooseNewPassword(page, temporaryPassword, 'first-passphrase-1');
  await expect(page.getByRole('heading', { level: 1, name: 'My profile' })).toBeVisible();
  await expectNoAccessibilityViolations(page);

  const form = page.getByRole('region', { name: 'Change password' });
  await form.getByLabel('Current password').fill('first-passphrase-1');
  await form.getByLabel('New password', { exact: true }).fill('second-passphrase-2');
  await form.getByLabel('Repeat the new password').fill('second-passphrase-2');
  await form.getByRole('button', { name: 'Change password' }).click();
  await expect(page.getByText('Your password was changed. Use the new one from your next login.')).toBeVisible();

  await logOut(page, 'Own Person');
  await logIn(page, username, 'first-passphrase-1');
  await expect(page.getByText('Username or password is incorrect.')).toBeVisible();
  await logInToApp(page, username, 'second-passphrase-2', 'Own Person');
});
