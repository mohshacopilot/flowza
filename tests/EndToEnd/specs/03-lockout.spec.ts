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
  openUser,
  uniqueUsername,
} from '../support/flowza.ts';

const lockedMessage =
  'This account is locked after too many wrong passwords. Try again in 15 minutes, or ask an administrator to unlock it.';

test('IAM_07_AC1_five_wrong_passwords_lock_the_account_and_the_admin_unlocks_it', async ({ browser }) => {
  const username = uniqueUsername('lock');
  const admin = await newUserPage(browser);
  await logInToApp(admin, demoAdmin.username, demoPassword, demoAdmin.displayName);
  const temporaryPassword = await createUser(admin, { username, displayName: 'Locked Person' });

  const user = await newUserPage(browser);
  await logIn(user, username, temporaryPassword);
  await chooseNewPassword(user, temporaryPassword, 'right-passphrase-1');
  await expect(user.getByRole('heading', { level: 1, name: 'Welcome' })).toBeVisible();
  await user.context().clearCookies();

  for (let attempt = 1; attempt <= 5; attempt++) {
    await logIn(user, username, `wrong-passphrase-${attempt}`);
  }
  await expect(user.getByText(lockedMessage)).toBeVisible();

  // Locked means locked: even the right password is refused now.
  await logIn(user, username, 'right-passphrase-1');
  await expect(user.getByText(lockedMessage)).toBeVisible();
  await expectNoAccessibilityViolations(user);

  // The admin sees the lock and ends it early (IAM-07-AC1, K3).
  await openUser(admin, username);
  await expect(admin.getByText('This account is locked for a while after too many wrong passwords.')).toBeVisible();
  await admin.getByRole('region', { name: 'Access' }).getByRole('button', { name: 'Unlock' }).click();
  await expect(admin.getByText(`${username} was unlocked.`)).toBeVisible();

  await logInToApp(user, username, 'right-passphrase-1', 'Locked Person');
});
