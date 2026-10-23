import { expect, test, type Page } from '@playwright/test';
import { demoDesigner, demoPassword, expectNoAccessibilityViolations } from '../support/flowza.ts';

// Feature 007, the visual design. T005: show/hide password on the login pages (UI-01-AC2, PA6). The screenshots and
// the axe checks at every width follow in T011.

async function openLogin(page: Page) {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
  return {
    password: page.getByLabel('Password', { exact: true }),
    toggle: page.getByRole('button', { name: 'Show password' }),
  };
}

test('UI_01_AC2_the_show_password_button_switches_the_field_with_the_mouse', async ({ page }) => {
  const { password, toggle } = await openLogin(page);
  await password.fill('typed-secret-1');
  await expect(password).toHaveAttribute('type', 'password');
  await expect(toggle).toHaveAttribute('aria-pressed', 'false');

  await toggle.click();
  await expect(password).toHaveAttribute('type', 'text');
  await expect(toggle).toHaveAttribute('aria-pressed', 'true');
  await expect(password).toHaveValue('typed-secret-1');

  await toggle.click();
  await expect(password).toHaveAttribute('type', 'password');
  await expect(toggle).toHaveAttribute('aria-pressed', 'false');
});

test('UI_01_AC2_the_show_password_button_works_with_the_keyboard', async ({ page }) => {
  const { password, toggle } = await openLogin(page);
  await password.fill('typed-secret-1');

  // The button is the next tab stop after the password field.
  await page.keyboard.press('Tab');
  await expect(toggle).toBeFocused();

  await page.keyboard.press('Space');
  await expect(password).toHaveAttribute('type', 'text');
  await expect(toggle).toHaveAttribute('aria-pressed', 'true');

  await page.keyboard.press('Enter');
  await expect(password).toHaveAttribute('type', 'password');
  await expect(toggle).toHaveAttribute('aria-pressed', 'false');
});

test('UI_01_AC2_the_login_still_works_while_the_password_is_shown', async ({ page }) => {
  const { password, toggle } = await openLogin(page);
  await page.getByLabel('Username').fill(demoDesigner.username);
  await password.fill(demoPassword);
  await toggle.click();
  await expect(password).toHaveAttribute('type', 'text');

  await page.getByRole('button', { name: 'Log in' }).click();

  await expect(page.getByRole('button', { name: `User menu for ${demoDesigner.displayName}` })).toBeVisible();
});

test('UI_01_AC2_the_login_page_with_the_shown_password_has_no_accessibility_problems', async ({ page }) => {
  const { toggle } = await openLogin(page);
  await expectNoAccessibilityViolations(page);

  await toggle.click();
  await expectNoAccessibilityViolations(page);
});
