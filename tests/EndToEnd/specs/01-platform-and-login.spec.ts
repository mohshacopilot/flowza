import { expect, test } from '@playwright/test';
import {
  demoDesigner,
  demoPassword,
  expectNoAccessibilityViolations,
  logIn,
  logInToApp,
  logOut,
} from '../support/flowza.ts';

test('PLT_02_AC1_the_started_platform_serves_the_app_and_its_login_at_one_address', async ({ page }) => {
  await page.goto('/');

  // Not logged in: the Gateway sends the browser to Identity's login page, on the same address.
  await expect(page).toHaveURL(/\/account\/login/);
  expect(new URL(page.url()).origin).toBe(new URL(test.info().project.use.baseURL ?? '').origin);
  await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
  await expectNoAccessibilityViolations(page);
});

test('IAM_01_AC1_a_user_logs_in_and_out_and_after_logout_pages_and_APIs_are_refused', async ({ page }) => {
  await logInToApp(page, demoDesigner.username, demoPassword, demoDesigner.displayName);
  await expect(page.getByRole('heading', { level: 1, name: 'Welcome' })).toBeVisible();
  await expectNoAccessibilityViolations(page);

  await logOut(page, demoDesigner.displayName);

  await page.goto('/me');
  await expect(page).toHaveURL(/\/account\/login/);
  const api = await page.request.get('/api/identity/v1/me');
  expect(api.status()).toBe(401);
});

test('IAM_01_AC2_a_wrong_password_and_an_unknown_user_get_the_same_general_message', async ({ page }) => {
  await logIn(page, demoDesigner.username, 'not-the-password-1');
  const message = page.getByText('Username or password is incorrect.');
  await expect(message).toBeVisible();

  await logIn(page, 'nobody.here', demoPassword);
  await expect(message).toBeVisible();
  await expectNoAccessibilityViolations(page);
});

test('NFR_11_the_user_menu_works_with_the_keyboard_arrow_keys_and_Enter', async ({ page }) => {
  await logInToApp(page, demoDesigner.username, demoPassword, demoDesigner.displayName);

  await page.getByRole('button', { name: `User menu for ${demoDesigner.displayName}` }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('menuitem', { name: 'My profile' })).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('menuitem', { name: 'Log out' })).toBeFocused();
  await page.keyboard.press('ArrowUp');
  await expect(page.getByRole('menuitem', { name: 'My profile' })).toBeFocused();
  await page.keyboard.press('Enter');

  await expect(page.getByRole('heading', { level: 1, name: 'My profile' })).toBeVisible();
  await expectNoAccessibilityViolations(page);
});

test('NFR_11_the_skip_link_is_the_first_tab_stop_and_jumps_to_the_main_content', async ({ page }) => {
  await logInToApp(page, demoDesigner.username, demoPassword, demoDesigner.displayName);

  await page.keyboard.press('Tab');
  const skip = page.getByRole('link', { name: 'Skip to main content' });
  await expect(skip).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/#main-content$/);
});
