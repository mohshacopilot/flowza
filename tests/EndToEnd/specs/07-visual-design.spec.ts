import { expect, test, type Locator, type Page, type TestInfo } from '@playwright/test';
import {
  createUser,
  demoAdmin,
  demoDesigner,
  demoPassword,
  expectNoAccessibilityViolations,
  logIn,
  logInToApp,
  newUserPage,
  uniqueUsername,
} from '../support/flowza.ts';

// Feature 007, the visual design. Runs in three projects (playwright.config.ts): desktop 1280×800, tablet 900×1100
// and phone 375×812. Every screen is checked for sideways scrolling (UI-05-AC1) and with axe (UI-06-AC1) at every
// width; screenshots are compared only with FLOWZA_E2E_SCREENSHOTS=1 (baselines from CI's Linux runner, PA5).

const compareScreenshots = process.env.FLOWZA_E2E_SCREENSHOTS === '1';

type Width = 'desktop' | 'tablet' | 'phone';
const widthOf = (testInfo: TestInfo): Width => testInfo.project.name as Width;

/** The page fits the window: nothing scrolls sideways (UI-05-AC1). */
async function expectNoSidewaysScrolling(page: Page): Promise<void> {
  const widths = await page.evaluate(() => ({
    content: document.documentElement.scrollWidth,
    window: window.innerWidth,
  }));
  expect(widths.content, `page width on ${page.url()}`).toBeLessThanOrEqual(widths.window);
}

/**
 * The checks every screen gets: no sideways scrolling, no accessibility problem, and (when switched on) the same
 * look as the accepted screenshot. `mask` hides parts that change from run to run.
 */
async function expectScreen(page: Page, name: string, mask: Locator[] = []): Promise<void> {
  await page.waitForLoadState('networkidle');
  await expectNoSidewaysScrolling(page);
  await expectNoAccessibilityViolations(page);
  if (compareScreenshots) {
    await expect(page).toHaveScreenshot(`${name}.png`, { fullPage: true, mask });
  }
}

/** What the walk below keeps in the page: each control's look without focus, and how to read a look. */
interface FocusWalk {
  look: (node: Element) => string;
  rest: Map<Element, string>;
}

/**
 * Presses Tab through the page and checks that every control that gets the focus shows it (UI-06-AC2): its outline
 * or shadow, or (side navigation) its menu item's, must differ from how it looked before the walk, when nothing had
 * the focus. A shadow that is always there (the selected item's bar, a primary button) is not counted as a focus
 * sign. Stops when the focus leaves the page or comes back to the first control.
 */
async function expectVisibleFocusOnEveryTabStop(page: Page): Promise<string[]> {
  // Start from the top of the page: blur() alone keeps the browser's Tab starting point (e.g. an autofocused field).
  // Then remember every control's look without focus.
  // (Functions are written inside evaluate, not passed as text: the page's Content-Security-Policy forbids eval.)
  await page.evaluate(() => {
    document.body.tabIndex = -1;
    document.body.focus();
    document.body.removeAttribute('tabindex');
    const look = (node: Element) => {
      const style = getComputedStyle(node);
      const outline =
        style.outlineStyle !== 'none' && parseFloat(style.outlineWidth) > 0
          ? `${style.outlineStyle} ${style.outlineWidth} ${style.outlineColor}`
          : 'none';
      return `${outline} | ${style.boxShadow}`;
    };
    const rest = new Map<Element, string>();
    for (const node of document.querySelectorAll('a[href], button, input, select, textarea, [tabindex], .ant-menu-item')) {
      rest.set(node, look(node));
    }
    (window as unknown as { flowzaFocusWalk: FocusWalk }).flowzaFocusWalk = { look, rest };
  });
  const stops: string[] = [];
  for (let step = 0; step < 25; step++) {
    await page.keyboard.press('Tab');
    const label = await page.evaluate(() => {
      const element = document.activeElement as HTMLElement | null;
      if (!element || element === document.body) {
        return null;
      }
      // Fields have no text of their own: name them by id, so two fields never look like the same stop.
      const text = (
        element.getAttribute('aria-label') ||
        (element instanceof HTMLInputElement ? `#${element.id || element.name}` : '') ||
        element.textContent?.trim() ||
        element.id
      ).slice(0, 40);
      return `${element.tagName.toLowerCase()} "${text}"`;
    });
    if (!label || stops.includes(label)) {
      break;
    }
    stops.push(label);
    // Polled: Ant Design moves into its focus look with a short transition.
    await expect
      .poll(
        () =>
          page.evaluate(() => {
            const { look, rest } = (window as unknown as { flowzaFocusWalk: FocusWalk }).flowzaFocusWalk;
            const element = document.activeElement as HTMLElement;
            // A control drawn after the walk began has no look to compare with: then any outline or shadow counts.
            const changed = (node: Element | null) =>
              node !== null && (rest.has(node) ? rest.get(node) !== look(node) : look(node) !== 'none | none');
            return changed(element) || changed(element.closest('.ant-menu-item'));
          }),
        { message: `visible focus on ${label}`, timeout: 2000 },
      )
      .toBe(true);
  }
  return stops;
}

const sidePanel = (page: Page) => page.getByRole('complementary');

// ---- Login pages (UI-01) ------------------------------------------------------------------------------------------

async function openLogin(page: Page) {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible();
  return {
    password: page.getByLabel('Password', { exact: true }),
    toggle: page.getByRole('button', { name: 'Show password' }),
  };
}

test('UI_01_AC1_UI_05_AC1_UI_06_AC1_the_login_page_has_the_brand_panel_and_the_form_card', async ({
  page,
}, testInfo) => {
  await openLogin(page);
  const panel = page.locator('.brand-panel');
  const card = page.locator('.form-card');
  await expect(panel.getByText('Flowza')).toBeVisible();
  await expect(card.getByRole('heading', { name: 'Log in' })).toBeVisible();
  await expect(page.getByText('Powered by Flowza')).toBeVisible();

  const panelBox = await panel.boundingBox();
  const cardBox = await card.boundingBox();
  expect(panelBox).not.toBeNull();
  expect(cardBox).not.toBeNull();
  if (panelBox && cardBox) {
    if (widthOf(testInfo) === 'phone') {
      // UI-05: on a phone the brand band sits above the form; the picture and highlights are left out.
      expect(panelBox.y + panelBox.height).toBeLessThanOrEqual(cardBox.y);
      await expect(page.getByText('Requests and approvals in one place')).toBeHidden();
    } else {
      // Side by side: the deep teal panel on the left, the form on the right.
      expect(panelBox.x + panelBox.width).toBeLessThanOrEqual(cardBox.x);
      await expect(page.getByText('Requests and approvals in one place')).toBeVisible();
    }
  }
  await expectScreen(page, 'login');
});

test('UI_01_AC3_UI_06_AC1_a_failed_login_shows_the_general_message_in_the_page_look', async ({ page }) => {
  await openLogin(page);
  await page.getByLabel('Username').fill('nobody.at.all');
  await page.getByLabel('Password', { exact: true }).fill('not-the-password-1');
  await page.getByRole('button', { name: 'Log in' }).click();

  const alert = page.locator('.form-card').getByRole('alert');
  await expect(alert).toHaveText('Username or password is incorrect.');
  await expect(page.locator('.brand-panel')).toBeVisible();
  await expectScreen(page, 'login-failed');
});

test('UI_01_AC3_UI_06_AC1_the_forced_password_change_has_the_login_look', async ({ page, browser }) => {
  // An admin creates a fresh user (in a browser of their own); the new user's first login leads to the change.
  const admin = await newUserPage(browser);
  await logInToApp(admin, demoAdmin.username, demoPassword, demoAdmin.displayName);
  const username = uniqueUsername('look');
  const temporaryPassword = await createUser(admin, { username, displayName: 'Look Person' });
  await admin.context().close();

  await logIn(page, username, temporaryPassword);

  await expect(page.getByRole('heading', { name: 'Choose a new password' })).toBeVisible();
  await expect(page.locator('.brand-panel')).toBeVisible();
  await expect(page.locator('.form-card')).toBeVisible();
  await expectScreen(page, 'forced-password-change');
});

test('UI_01_AC2_each_show_password_button_on_the_change_page_says_which_field_it_shows', async ({ page, browser }) => {
  const admin = await newUserPage(browser);
  await logInToApp(admin, demoAdmin.username, demoPassword, demoAdmin.displayName);
  const username = uniqueUsername('toggles');
  const temporaryPassword = await createUser(admin, { username, displayName: 'Toggle Person' });
  await admin.context().close();

  await logIn(page, username, temporaryPassword);
  await expect(page.getByRole('heading', { name: 'Choose a new password' })).toBeVisible();

  // Three buttons with the same name: each is described by its field's label, so screen readers can tell them apart.
  const toggles = page.getByRole('button', { name: 'Show password' });
  await expect(toggles).toHaveCount(3);
  await expect(toggles.nth(0)).toHaveAccessibleDescription('Current (temporary) password');
  await expect(toggles.nth(1)).toHaveAccessibleDescription('New password');
  await expect(toggles.nth(2)).toHaveAccessibleDescription('Repeat the new password');
});

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

test('UI_06_AC2_tabbing_through_the_login_page_shows_the_focus_on_every_control', async ({ page }) => {
  await openLogin(page);

  const stops = await expectVisibleFocusOnEveryTabStop(page);

  // Username, password, show password and the Log in button at least.
  expect(stops.length, stops.join(', ')).toBeGreaterThanOrEqual(4);
});

// ---- Application screens (UI-09, UI-05-AC1, UI-06-AC1) -------------------------------------------------------------

interface AppScreen {
  name: string;
  path: string;
  heading: string;
}

// Demo data only, so every run shows the same content: the users list is searched for the demo users (their e-mails
// end in demo.example), which hides the users that earlier tests create with random names.
const appScreens: AppScreen[] = [
  { name: 'home', path: '/', heading: 'Welcome' },
  { name: 'users-list', path: '/admin/users?search=demo.example', heading: 'Users' },
  { name: 'create-user', path: '/admin/users/new', heading: 'New user' },
  { name: 'my-profile', path: '/me', heading: 'My profile' },
  { name: 'not-found', path: '/no/such/page', heading: 'Page not found' },
  { name: 'empty-list', path: '/admin/users?search=nobody-has-this-name', heading: 'Users' },
];

for (const screen of appScreens) {
  test(`UI_09_AC1_UI_05_AC1_UI_06_AC1_the_${screen.name.replaceAll('-', '_')}_screen_in_the_new_look`, async ({
    page,
  }) => {
    await logInToApp(page, demoAdmin.username, demoPassword, demoAdmin.displayName, screen.path);
    await expect(page.getByRole('heading', { level: 1, name: screen.heading })).toBeVisible();

    await expectScreen(page, screen.name);
  });
}

test('UI_09_AC1_UI_05_AC1_UI_06_AC1_the_user_details_screen_in_the_new_look', async ({ page }) => {
  await logInToApp(page, demoAdmin.username, demoPassword, demoAdmin.displayName, '/admin/users?search=demo.designer');
  await page.getByRole('link', { name: demoDesigner.username, exact: true }).click();
  await expect(page.getByRole('heading', { level: 1, name: `User ${demoDesigner.username}` })).toBeVisible();
  for (const section of ['Details', 'System roles', 'Access']) {
    await expect(page.getByRole('region', { name: section })).toBeVisible();
  }

  await expectScreen(page, 'user-details');
});

test('UI_09_AC1_UI_05_AC1_UI_06_AC1_the_empty_list_says_so', async ({ page }) => {
  await logInToApp(page, demoAdmin.username, demoPassword, demoAdmin.displayName, '/admin/users?search=nobody-has-this-name');

  await expect(page.getByText('No users match your search.')).toBeVisible();
});

test('UI_09_AC1_UI_05_AC1_UI_06_AC1_the_error_screen_shows_the_reference_code', async ({ page }) => {
  // The frame cannot find out who is logged in: the server answers with an error and a reference code (PLT-05).
  await page.route('**/bff/user', (route) =>
    route.fulfill({
      status: 500,
      contentType: 'application/problem+json',
      body: JSON.stringify({ status: 500, title: 'Server error', code: 'unexpected', reference: 'e2e00500' }),
    }),
  );
  await page.goto('/');

  await expect(page.getByRole('heading', { level: 1, name: 'Something went wrong' })).toBeVisible();
  await expect(page.getByText('Reference code: e2e00500')).toBeVisible();
  await expectScreen(page, 'error');
});

test('UI_09_AC1_UI_05_AC1_UI_06_AC1_the_loading_screen_while_the_server_is_slow', async ({ page }) => {
  // The answer to "who is logged in" never comes: the frame keeps its loading screen, announced to screen readers.
  await page.route('**/bff/user', () => {
    // No answer on purpose.
  });
  await page.goto('/');

  await expect(page.getByRole('status')).toContainText('Loading…');
  await expectNoSidewaysScrolling(page);
  await expectNoAccessibilityViolations(page);
  if (compareScreenshots) {
    await expect(page).toHaveScreenshot('loading.png', { fullPage: true });
  }
});

test('UI_06_AC2_tabbing_through_the_frame_shows_the_focus_on_every_control', async ({ page }, testInfo) => {
  await logInToApp(page, demoAdmin.username, demoPassword, demoAdmin.displayName);
  await expect(page.getByRole('heading', { level: 1, name: 'Welcome' })).toBeVisible();

  const stops = await expectVisibleFocusOnEveryTabStop(page);

  const expected =
    widthOf(testInfo) === 'phone'
      ? ['Open navigation', 'User menu for Demo Admin']
      : ['Flowza home page', 'User menu for Demo Admin'];
  for (const label of expected) {
    expect(stops.some((stop) => stop.includes(label)), stops.join(', ')).toBe(true);
  }
});

// ---- Navigation by width (UI-03, UI-04, UI-05-AC2) -----------------------------------------------------------------

test('UI_04_AC2_without_a_remembered_choice_the_navigation_is_open_on_a_desktop_and_collapsed_on_a_tablet', async ({
  page,
}, testInfo) => {
  test.skip(widthOf(testInfo) === 'phone', 'A phone has the menu button instead of the side panel (UI-05).');
  await logInToApp(page, demoAdmin.username, demoPassword, demoAdmin.displayName);

  const box = await sidePanel(page).boundingBox();
  expect(box?.width).toBe(widthOf(testInfo) === 'desktop' ? 232 : 72);
  await expect(
    page.getByRole('button', { name: widthOf(testInfo) === 'desktop' ? 'Collapse navigation' : 'Expand navigation' }),
  ).toBeVisible();
});

test('UI_04_AC1_UI_03_AC1_collapsed_the_navigation_shows_icons_with_names_and_stays_collapsed_after_a_reload', async ({
  page,
}, testInfo) => {
  test.skip(widthOf(testInfo) !== 'desktop', 'Starts from the open panel of a desktop (C3).');
  // Demo users only, so the screenshot shows the same list in every run (earlier tests add users with random names).
  await logInToApp(page, demoAdmin.username, demoPassword, demoAdmin.displayName, '/admin/users?search=demo.example');
  const navigation = page.getByRole('navigation', { name: 'Main navigation' });
  await expect(navigation.getByText('Administration')).toBeVisible();
  await expect(navigation.getByRole('link', { name: 'Users' })).toHaveAttribute('aria-current', 'page');

  await page.getByRole('button', { name: 'Collapse navigation' }).click();

  await expect.poll(async () => (await sidePanel(page).boundingBox())?.width).toBe(72);
  await expect(navigation.getByRole('link', { name: 'Home' })).toBeAttached();
  await expect(navigation.getByRole('link', { name: 'Users' })).toBeAttached();
  await navigation.locator('.ant-menu-item').nth(1).hover();
  // The collapse button just clicked may still show its own tooltip ("Expand navigation"): look for the item's.
  await expect(page.getByRole('tooltip').filter({ hasText: 'Users' })).toBeVisible();
  await expectNoAccessibilityViolations(page);
  if (compareScreenshots) {
    // Away from the menu and the table, so no tooltip and no highlighted row is in the picture.
    await page.mouse.move(700, 100);
    await expect(page.getByRole('tooltip').filter({ hasText: 'Users' })).toBeHidden();
    await expect(page).toHaveScreenshot('navigation-collapsed.png');
  }

  await page.reload();

  await expect(page.getByRole('button', { name: 'Expand navigation' })).toBeVisible();
  expect((await sidePanel(page).boundingBox())?.width).toBe(72);
});

test('UI_03_AC1_a_user_without_the_role_sees_no_administration_group', async ({ page }, testInfo) => {
  test.skip(widthOf(testInfo) !== 'desktop', 'The groups are the same at every width; checked once.');
  await logInToApp(page, demoDesigner.username, demoPassword, demoDesigner.displayName);
  const navigation = page.getByRole('navigation', { name: 'Main navigation' });

  await expect(navigation.getByRole('link', { name: 'Home' })).toHaveAttribute('aria-current', 'page');
  await expect(navigation.getByText('Administration')).toHaveCount(0);
  await expect(navigation.getByRole('link', { name: 'Users' })).toHaveCount(0);
});

test('UI_05_AC2_UI_06_AC1_on_a_phone_the_menu_button_opens_the_navigation_and_choosing_an_item_closes_it', async ({
  page,
}, testInfo) => {
  test.skip(widthOf(testInfo) !== 'phone', 'Only a phone has the menu button (UI-05).');
  await logInToApp(page, demoAdmin.username, demoPassword, demoAdmin.displayName);
  await expect(sidePanel(page)).toHaveCount(0);
  const menuButton = page.getByRole('button', { name: 'Open navigation' });
  await expect(menuButton).toHaveAttribute('aria-expanded', 'false');

  await menuButton.click();

  const drawer = page.getByRole('dialog', { name: 'Navigation' });
  await expect(drawer).toBeVisible();
  await expect(menuButton).toHaveAttribute('aria-expanded', 'true');
  await expectNoAccessibilityViolations(page, { only: '.ant-drawer' });
  if (compareScreenshots) {
    await expect(page).toHaveScreenshot('navigation-drawer.png');
  }

  await drawer.getByRole('link', { name: 'Users', exact: true }).click();

  await expect(page.getByRole('heading', { level: 1, name: 'Users' })).toBeVisible();
  await expect(drawer).toBeHidden();
  await expect(menuButton).toHaveAttribute('aria-expanded', 'false');
  await expect(menuButton).toBeFocused();
});
