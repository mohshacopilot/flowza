import AxeBuilder from '@axe-core/playwright';
import { expect, type Browser, type Page } from '@playwright/test';

/** The documented demo password of every demo user (C5; docs/setup/local-setup.md §4.1). */
export const demoPassword = 'Flowza-Demo-2026';

export const demoAdmin = { username: 'demo.admin', displayName: 'Demo Admin' } as const;
export const demoDesigner = { username: 'demo.designer', displayName: 'Dana Designer' } as const;

/**
 * A page in a context of its own, so two users (an admin and the person they manage) can be logged in at once.
 * axe needs pages made this way (not browser.newPage()).
 */
export async function newUserPage(browser: Browser): Promise<Page> {
  return (await browser.newContext()).newPage();
}

/** A username nobody used before, so each run works on fresh users (users are never deleted, C6). */
export function uniqueUsername(purpose: string): string {
  const stamp = Date.now().toString(36) + Math.floor(Math.random() * 1296).toString(36);
  return `e2e.${purpose}.${stamp}`;
}

/** Opens a protected page and logs in on Identity's login page (served through the Gateway). */
export async function logIn(page: Page, username: string, password: string, path = '/'): Promise<void> {
  await page.goto(path);
  await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Log in' }).click();
}

/** Logs in and waits until the app shell shows the user's menu. */
export async function logInToApp(page: Page, username: string, password: string, displayName: string, path = '/') {
  await logIn(page, username, password, path);
  await expect(page.getByRole('button', { name: `User menu for ${displayName}` })).toBeVisible();
}

export async function logOut(page: Page, displayName: string): Promise<void> {
  await page.getByRole('button', { name: `User menu for ${displayName}` }).click();
  await page.getByRole('menuitem', { name: 'Log out' }).click();
  await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
}

/** The forced change at the first login (IAM-03, IAM-04-AC2): temporary password, then a new one twice. */
export async function chooseNewPassword(page: Page, temporaryPassword: string, newPassword: string): Promise<void> {
  await expect(page.getByRole('heading', { name: 'Choose a new password' })).toBeVisible();
  await page.getByLabel('Current (temporary) password').fill(temporaryPassword);
  await page.getByLabel('New password', { exact: true }).fill(newPassword);
  await page.getByLabel('Repeat the new password').fill(newPassword);
  await page.getByRole('button', { name: 'Save the new password' }).click();
}

export interface NewUser {
  username: string;
  displayName: string;
  roles?: readonly ('Platform Admin' | 'App Designer')[];
}

/** A Platform Admin creates a user on the users screen (IAM-04-AC1) and reads the temporary password shown once. */
export async function createUser(page: Page, user: NewUser): Promise<string> {
  await page.goto('/admin/users/new');
  await page.getByRole('textbox', { name: 'Username' }).fill(user.username);
  await page.getByRole('textbox', { name: 'Display name' }).fill(user.displayName);
  await page.getByRole('textbox', { name: 'Email' }).fill(`${user.username}@e2e.example`);
  for (const role of user.roles ?? []) {
    await page.getByRole('checkbox', { name: role }).check();
  }
  await page.getByRole('button', { name: 'Create user' }).click();
  await expect(page.getByRole('heading', { name: 'User created' })).toBeVisible();
  return readTemporaryPassword(page);
}

export async function readTemporaryPassword(page: Page): Promise<string> {
  const field = page.getByRole('textbox', { name: 'Temporary password' });
  await expect(field).not.toHaveValue('');
  return field.inputValue();
}

/** Opens a user's page from the users list, searching by username. */
export async function openUser(page: Page, username: string): Promise<void> {
  await page.goto(`/admin/users?search=${encodeURIComponent(username)}`);
  await page.getByRole('link', { name: username, exact: true }).click();
  await expect(page.getByRole('heading', { name: `User ${username}` })).toBeVisible();
}

/** Clicks an access action and confirms it in the dialog (deactivate, reset password). */
export async function confirmAction(page: Page, button: string): Promise<void> {
  await page.getByRole('region', { name: 'Access' }).getByRole('button', { name: button }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Yes, continue' }).click();
}

/**
 * Automatic accessibility check (NFR-11, FE8): no WCAG 2.1 A/AA violations on the page as it is now. The
 * failure message lists each rule and the elements that break it.
 */
export async function expectNoAccessibilityViolations(page: Page, options: { only?: string } = {}): Promise<void> {
  // With a dialog open, check only the dialog: the page behind it is covered by the dark mask and not in use.
  let axe = new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']);
  if (options.only) {
    axe = axe.include(options.only);
  }
  const results = await axe.analyze();
  const summary = results.violations.map(
    (v) =>
      `${v.id} (${v.impact ?? 'unknown'}): ${v.help} — ` +
      v.nodes.map((n) => `${n.target.join(' ')} [${n.failureSummary?.replaceAll('\n', ' ') ?? ''}]`).join(', '),
  );
  expect(summary, `accessibility violations on ${page.url()}`).toEqual([]);
}
