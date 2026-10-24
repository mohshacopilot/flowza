# Tasks 007: Visual design (UI)

| | |
|---|---|
| **Status** | **Written and analyzed 2026-10-23**; build starts with T001 (feature 007 is built **before 002**, U11) |
| **Spec / Plan** | [spec.md](spec.md) (approved 2026-10-23; amendment 1 approved the same day) · [plan.md](plan.md) (approved 2026-10-23, PA1–PA6; updated for amendment 1, section 17) |
| **Rules** | One task at a time, in order. Each task ends with: builds without warnings, lint clean, its tests pass (and all earlier tests still pass), a local commit (`feat|test|docs(ui): … (IDs)`). Nothing is pushed without the owner's OK. Every task is Milestone 1 |

## Before coding: owner's steps
| # | Step | Needed from |
|---|---|---|
| O1 | Allow a **push** to GitHub, so CI can make the first screenshot baselines (PA5) and run the look check | T012 |
| O2 | **Look at the first baseline images** (and later at any new look) before they are committed (PA5) | T012 |

Nothing else: no new database, secret, library or program.

## Progress
| Task | Status | Commit |
|---|---|---|
| T001 Design values and Ant Design adapter | ✅ | 8c9af68 |
| T002 Login-page CSS generator and the two checks | ✅ | 610bd49 |
| T003 Installation name and initials (Identity) | ✅ | 4e2f387 |
| T004 Login and change-password pages, split layout | ✅ | 8c71511 |
| T005 Show/hide password | ✅ | 2a4dcd3, fix f5fa51c |
| T005b Login page as mockup B, teal values (spec amendment 1) | ✅ | 30d29b9 |
| T006 Installation in the web app, brand mark | ✅ | e7d11dd |
| T007 Dark side navigation and its preference | ✅ | 991aa07 |
| T008 Header and page titles | ✅ | cdbbab2 |
| T009 Phone frame | ✅ | 9dfbe33 |
| T010 Existing screens in the new look | ✅ | 4c83aae |
| T011 Visual and accessibility end-to-end tests | ✅ | 1e7e510 |
| T012 CI look check and screenshot baselines | ✅ | e06c1be, b7b45ce, 85107ea |
| T013 Setup guide and CLAUDE.md | ✅ | 20b7680 (owner to read §5 of the guide) |
| T014 Verify 007 | ⬜ | |

**Progress notes**
- **T001 (2026-10-23):** the dark candidate for text on coloured surfaces is **black** (`onColour.dark` `#000000`),
  not the body text colour `#1b2333`: with `#1b2333` a mid grey only reaches about 4 : 1, while white or black
  always reach at least 4.58 : 1 (test `a_dark_candidate_lighter_than_black_cannot_promise_4_5_to_1`). The brand
  `#1f5fbf` carries white text (6.1 : 1). Reduced motion is a hook (`usePrefersReducedMotion`) that follows the
  system setting while the app is open. Until T007 the old light `Menu` sits in the now dark `Sider` (only the empty
  area below the items is navy); not pushed in between. For T002: the stray-value scan must leave out test files
  (`*.test.ts(x)`), which use example colours on purpose.
- **T002 (2026-10-23):** the generator and the checks live in `src/Web/tooling` (`accountCss.ts`,
  `generate-account-css.ts`, `designValues.test.ts`). Every design value becomes one variable named by its path
  (`--flowza-brand`, `--flowza-neutral-text-secondary`, `--flowza-space-xl` …, numbers in px except font weights),
  plus `--flowza-on-brand` from `readableTextOn`; breakpoints are left out (CSS variables do not work in media
  queries). The drift test ignores line endings (git checks text out with CRLF on Windows). The scan follows the
  spec's wording "colour, font or size": colour literals, font size, weight and family, and written spacing and
  corner sizes (`padding`, `margin`, `gap`, `border-radius`); layout sizes (widths, positions, 1 px lines) stay
  allowed. It covers `src/**/*.{tsx,css}` (not tests) and Identity's `wwwroot/**/*.css` and `Pages/**/*.cshtml`.
  Allow list today: `account.css` (T004), `AppShell.module.css` (T007–T009), and for T010 `users.module.css`,
  `profile.module.css`, `AccessSection.tsx`, `TemporaryPasswordPanel.tsx`, `UserDetailsPage.tsx`,
  `ApiErrorAlert.tsx`. A test fails when a listed file is already clean, so the list only shrinks. The login pages
  do not load `design-values.css` yet; T004 links it.
- **T003 (2026-10-23):** `InstallationBrand` (Identity.Application) holds the C6 rule and the name check; the length
  counts text elements (100 Hindi syllables pass although they are 200 UTF-16 characters). The application reads the
  brand through a port, `IInstallationBrandSource`; v0.1's adapter (`Identity.Infrastructure/Installation`) reads
  `Installation:Name`, and M3 (PLT-08) can swap in the Settings screen without touching the endpoint. A missing key
  falls back to "Flowza"; a key set to blank or longer than 100 stops the start (`ValidateOnStart`, checked by
  removing it and watching the test fail). It is registered in Identity's web host only, not in the `flowza` tool.
  The endpoint is in Identity's OpenAPI document (`GetInstallation`); the web client is regenerated in T006. The
  AppHost left running for the owner was stopped to free Identity's build output; it is started again for the T005
  look.
- **T004 (2026-10-23):** the layout reads the brand through `IInstallationBrandSource` itself, so the login pages do
  not depend on the web app or the Gateway's API routes. The brand panel is the page's `header` (banner landmark),
  the form column holds `main` and the "Powered by Flowza" `footer`. The text key `Product.Name` was replaced by
  `Layout.ProductLine` (C1) and `Layout.PoweredBy`; the page title is now "Log in – <installation name>". The
  mark is an outlined square in the on-brand colour (no tinted fill, so no extra colour value is needed). Field
  borders use the secondary text colour, not the soft neutral border, so a field's edge reaches 3 : 1 (WCAG 1.4.11).
  The phone breakpoint (768 px) is written in the media query, because CSS variables cannot be used there; a comment
  ties it to `breakpoints.tablet`. `account.css` left the T002 allow list (7 files remain, all for T007–T010).
  The T029 end-to-end suite (10 tests, with axe on the login pages) passes unchanged. Not yet checked by eye: the
  owner looks at the login pages after T005.
- **T005 (2026-10-23):** the task named two texts, "Show password" / "Hide password". The button is a toggle with
  `aria-pressed`, and the WAI-ARIA rule for toggles is that the name must **not** change with the state (otherwise a
  screen reader says "Hide password, pressed", which means the opposite). So the name stays "Show password"
  (`Password.Show`) and only `aria-pressed` changes; a pressed button also gets a light background, so the state is
  visible. "Hide password" is therefore not used. The translated name reaches the script through
  `data-show-password-label` on `<body>`. The field goes back to hidden when the form is sent. The end-to-end tests
  live in `specs/07-visual-design.spec.ts`, which T011 extends with the screenshots.
- **T005 fix (f5fa51c):** when the platform was started for the owner's look, the browser still showed the old login
  page: the CSS and script had fixed addresses and no caching rules, so the browser reused its old copy. Every real
  installation would see the same after an upgrade. `asp-append-version` now adds a hash of each file to its address
  (`?v=…`); `UI_01_after_an_upgrade_browsers_fetch_the_changed_look_at_once` checks all three assets. Checked by
  eye afterwards at desktop width and at 375 px (brand band, no sideways scrolling).
- **Owner's look after T005 (2026-10-23):** not happy (too plain, form too small, colours and layout); picked
  mockup B from three options → spec amendment 1, plan §17, task T005b.
- **T005b (2026-10-23):** the login panel reuses the navigation's dark values (plus the new `raised`, `line`,
  `accent`), so the panel and the app's navigation are one deep teal. The workflow picture is HTML/CSS with three
  tiny inline SVG icons drawn in `currentColor`; no image file. The eye button sits inside the field: `account.js`
  wraps the field and the button in one positioned box and gives focus back if the field had it (autofocus on the
  change-password page). The room for the icon is `calc(space-xxl + space-md)`, because the stray-value scan
  flagged a written `44px`. Two findings of the axe checks in the app (not the login pages), both fixed here:
  (1) the old light menu (until T007) showed the selected item in a tint Ant Design derives from the brand, only
  2.7 : 1 with teal → the selected item is now brand with the readable text colour in the light **and** dark menu
  (`UI_06_the_selected_menu_item_is_readable_*`); (2) axe once caught a menu item halfway through its 0.3 s colour
  fade → `expectNoAccessibilityViolations` now waits until no transition is running, so it checks the colours
  people actually read.
- **T006 (2026-10-23):** `npm run api:update` changed only Identity's document (additive: `GET /api/v1/installation`,
  `InstallationView`); the other services answered 401 for their documents, as before, and were skipped.
  `BrandMark` sits in the header for now (where the name was); T007 moves it to the top of the dark navigation.
  While the name loads, **and** when it cannot be loaded, the mark shows its outline only and the link is called
  "Home page" (`shell.homeLinkWithoutName`): the frame keeps working without Identity's answer. The placeholder text
  `shell.installationName` was removed. MSW now answers the installation by default (`server.ts`), so every
  component test that renders the frame works without extra set-up. The "…" itself is drawn by CSS, which jsdom
  does not draw, so the component test checks the full name in `title` and the accessible name; the screenshot
  tests (T011) show the shortened name. End-to-end suite 14/14 with axe on every app page that shows the mark.
- **T007 (2026-10-23):** decisions inside plan §5.5:
  - **One more component, `NavigationSider`** (Sider + `BrandMark` on top + navigation + collapse button), so
    `AppShell` only arranges the frame; T009 puts the same `SideNavigation` into the phone drawer.
  - **Breakpoints:** the adapter sets Ant Design's `screenMD*`/`screenLG*` tokens from `breakpoints.tablet` (768) and
    `breakpoints.desktop` (1025), so `Grid.useBreakpoint().lg` means "desktop" in our terms (Ant Design's own `lg`
    starts at 992 px; the test at 1024 px proves the difference).
  - **Sizes in the design values:** `layout.navigationWidth` 232, `layout.navigationCollapsedWidth` 72 and
    `layout.headerHeight` 64 (also in the regenerated `design-values.css`, harmless for the login pages).
  - **Navigation colours as CSS variables:** Ant Design has no token for the white focus ring on dark, the selected
    item's bar or the panel's lines; `shellCssVariables()` puts `--flowza-navigation-*` on the frame with the same
    names and values as the login pages' `design-values.css` (checked by a test).
  - **Collapse button is our own `<button>`** with "Collapse navigation" / "Expand navigation" (and a tooltip when
    collapsed): Ant Design's built-in Sider trigger is a `div` that the keyboard cannot reach.
  - **Selected item:** brand background with a 3 px accent bar on the left (accent reaches 3.1 : 1 on the brand and
    6.8 : 1 on the deep teal). **Focus:** the link inside an item has the focus; the whole item shows a white ring
    (`:has(a:focus-visible)`), also when the label is hidden.
  - **Collapsed groups:** the "Administration" heading would be cut to one letter in 72 px, so collapsed it becomes
    a divider line; each item keeps its label as accessible name and Ant Design's tooltip.
  - **`app/global.css`** (`body { margin: 0 }`): the browser's 8 px body margin showed as a white strip around
    the dark panel (seen in a local look check at 1280 and 900 px; the old white header had hidden it).
  - **Fixed in T008:** `Grid.useBreakpoint()` knows the width only after its first effect, so at desktop width the
    panel rendered collapsed once and then slid open; an axe check caught the half-faded menu text (1.04 : 1) in one
    end-to-end run. The hook now asks `matchMedia` for the desktop width on the first render (test
    `UI_04_AC2_at_desktop_width_the_very_first_render_is_already_open`, which fails without the fix).
  - Tests set the window width with `test/viewport.ts` (a `matchMedia` that answers width queries); `setup.ts`
    clears `localStorage` after each test. Until T009 the phone width shows the collapsed Sider.
- **T008 (2026-10-23):** decisions inside plan §5.5:
  - **Titles have their own keys** (`pageTitles.*`: Home, Users, New user, User details, My profile, Page not found)
    and a route names its title with `handle: titled(…)`; `pageTitleKey()` takes the deepest matched route's key
    (O(route depth)), so `/admin/users/42` shows "User details" and an error inside the users area still shows
    "Users". The 403 page inside the users area also shows "Users" in the header (its URL says the same).
  - **The header title is text, not a heading:** every page keeps its own `<h1>` (the end-to-end helpers and screen
    readers rely on it), so a page has exactly one `<h1>`. On the users list this means "Users" appears in the header
    and as the page heading; the owner sees it at the T009 look (T010 may make the page headings smaller).
  - **Browser tab:** "<page> – <installation>", the same pattern as the login pages; "Flowza" while the name loads.
  - **Avatar:** initials by the C6 rule in TypeScript (`initialsOf`, letters counted with `Intl.Segmenter` like
    C#'s `StringInfo`; the same test cases as Identity's, Hindi and Tamil included), on the brand colour through the
    adapter's `Avatar.colorTextPlaceholder` (Ant Design's grey gave white text only 2.3 : 1). The avatar is
    `aria-hidden`; the button keeps its name "User menu for …" (A8).
- **T009 (2026-10-23):** decisions inside plan §5.5:
  - **`useWidthClass()`** ("phone" | "tablet" | "desktop") is the one place that reads the width; `AppShell` (Sider
    or drawer) and `useNavigationPreference` (collapsed default) use it. Like the T008 fix it asks `matchMedia` on
    the first render, so a phone never shows the Sider for a moment.
  - **`NavigationDrawer`:** menu button ("Open navigation", `aria-expanded`, `aria-controls`) in the header before
    the title; Ant Design `Drawer` from the left, 232 px, named "Navigation", close button "Close navigation", with
    the same `BrandMark` and `SideNavigation`. It is open **only on the page where it was opened** (state = the
    path at opening), so any link that changes the page closes it; choosing the current page's item closes it
    through the menu's click. Ant Design traps focus inside and returns it to the menu button (default
    `focusTriggerAfterClose`); tests prove both.
  - **Drawer colours** go in through the Drawer's semantic `styles` (CSS variables only): Ant Design's own drawer
    rules are more specific than a CSS module class (the first look showed a white drawer). The drawer is rendered
    outside the frame, so it gets the `--flowza-navigation-*` variables through `rootStyle`.
  - **Phone header:** less padding, smaller title, the user's name hidden (the avatar stays; the button's name still
    says who is logged in). **Content padding** 24 px, 16 px on phones; the column beside the navigation may shrink
    (`min-width: 0`), so the users table scrolls inside its box: at 375 px the page is exactly 375 px wide (look
    check). **Print** hides the Sider and the header. **Reduced motion:** a test proves `AppProviders` switches
    Ant Design's motion off when the system asks for it (the hook and adapter already did, T001).
  - `AppShell.module.css` is clean (variables only) and left the T002 allow list (6 files remain, all for T010).
  - **Tests:** every component test now runs in a 1280 px window unless it sets another width (`setup.ts`), because
    the frame depends on the width. Two existing whole-screen tests (create user, user details) became flaky when all
    26 test files run in parallel on this PC: one passed 5 s (Vitest's limit; it takes 2.4 s alone), one waited
    longer than `findBy…`'s 1 s for a lazily loaded screen. `testTimeout` is now 15 s and `asyncUtilTimeout` 5 s;
    no expectation changed. Four full runs in a row then passed.
- **T010 (2026-10-23):** decisions inside plan §5.6:
  - **`SectionCard`** (shared): a part of a screen on a borderless card (the card shadow from the design values)
    with an `<h2>` in the card's title, still a `<section>` named by that heading, so the existing tests that find
    sections as regions ("Details", "System roles", "Access", "Change password") pass unchanged. User details and
    my profile use it; the users list puts the filters and the table on two cards; create user, the created-user
    panel, home and the status pages sit on a card. Page headings (`<h1>`) stay as they were (the owner accepted
    the frame with "Users" in the header and as the heading).
  - **Spacing scale and code font from the design values:** the adapter now sets Ant Design's `margin*`/`padding*`
    tokens from `space` (same numbers as Ant Design's defaults, so nothing moved) and `fontFamilyCode` from the new
    `font.familyCode`; the screens' CSS reads `--flowza-margin-*`, `--flowza-padding-*` and
    `--flowza-font-family-code` (temporary password). `design-values.css` regenerated (one new line).
  - **403/404 pages:** Ant Design's pictures carry their own colours (a bright blue question mark seen in the look
    check), which the design values cannot reach; they now show a large icon (lock, page search) with status
    "info", whose colour is the brand. The error page keeps the error icon. `StatusPage` takes a `kind`.
  - **Empty list:** Ant Design `Empty` (simple picture) with today's translated text.
  - **Frame error page** (who-is-logged-in failed) sits in a `Layout`, so it has the page background and padding.
  - **The T002 allow list is gone:** the scan has no exceptions left; a new test checks that it covers the screens,
    the frame and the login pages.
  - `className` on Ant Design components needs a definite string (`exactOptionalPropertyTypes`), so module classes
    are passed as `styles.x ?? ''`, as in `NavigationDrawer`.
  - Look check (end-to-end environment, 1280 px): home, users list, user details, create user, my profile, 404 and
    the empty search, each with axe: no findings.
- **2026-10-23 push:** the owner asked for a push; `main` went to GitHub (`2428420..40c2e9b`, everything up to T010)
  after a scan for secrets. **CI run #32 on `40c2e9b` is green** (4 min 57 s; seen by the owner on GitHub), so
  T001–T010 count as done under DV4. This was a one-off OK, not O1 for T012.
- **T011 (2026-10-23/24):** decisions inside plan §10:
  - **Projects:** `chromium` runs the feature 001 specs once (as before); `desktop` 1280×800, `tablet` 900×1100 and
    `phone` 375×812 (mobile, touch) run only `07-visual-design.spec.ts`. Running the 001 specs at three widths would
    triple the run for behaviour that does not depend on the width. `toHaveScreenshot` defaults: 1 % of pixels,
    animations off, caret hidden; comparisons only with `FLOWZA_E2E_SCREENSHOTS=1` (PA5), so a local run never
    needs baselines.
  - **Every screen gets the same three checks** (`expectScreen`): no sideways scrolling (`scrollWidth ≤ innerWidth`,
    UI-05-AC1), axe (UI-06-AC1) and, when switched on, the full-page screenshot (UI-09-AC1, UI-01-AC1/AC3). Screens:
    login, failed login, the forced change for a user created in the test, home, users list, user details, create
    user, my profile, 404, the error page (a mocked 500 with reference `e2e00500` on `/bff/user`), the loading page
    (`/bff/user` never answers) and the empty list. Plus the collapsed panel (desktop) and the open drawer (phone).
  - **Same content in every run, so no masks are needed yet:** the users list is searched for `demo.example` (the
    demo users only; earlier tests add users with random names), user details shows `demo.designer`, my profile
    `demo.admin`, the error page a fixed reference. `expectScreen` takes masks for later screens with dates or
    numbers.
  - **Tab walks (UI-06-AC2):** from the top of the page, Tab until the focus leaves the page; every stop must show an
    outline or focus shadow on the control or (side navigation) on its menu item. Login: username, password, show
    password, Log in (and the skip link); frame: mark, items, collapse button, user menu (phone: menu button, user
    menu).
  - **Fixed after the first run:** two test mistakes (the Tab walk named both fields `input ""` and started after the
    autofocused username, because `blur()` keeps the browser's starting point; two tooltips at once after the
    collapse click) and **one real finding: at 375 px the empty users table still scrolled sideways, a scroll area
    with nothing a keyboard can reach** (axe `scrollable-region-focusable`). An answered empty list now fits the
    width; a list with rows keeps scrolling inside its box (component tests `UI_05_AC1_a_list_with_rows_…`,
    `UI_06_AC1_an_empty_list_fits_the_width_…`, which fails without the fix). The switch waits for the answer,
    because changing the scroll mode rebuilds the table.
  - **Run:** 72 passed, 7 skipped (tests for one width only: drawer on the phone, collapse and groups on the
    desktop, defaults not on the phone), 0 failed, 4.0 minutes. Screenshot comparisons were not run (no baselines
    yet; they come from CI in T012).
- **T012 (2026-10-24):** the owner gave O1 (push) for this task. Steps and decisions inside plan §§10 and 12:
  - **`update-screenshots.yml`** (manual): runs the **whole** end-to-end suite like CI (so the screens show the same
    data as when CI compares them) with `FLOWZA_E2E_SCREENSHOTS=1` and `--update-snapshots=all`, and uploads
    `screenshot-baselines`; it never commits or pushes. The owner started it on GitHub and downloaded the zip (this PC
    has no GitHub CLI); Claude checked the zip (only the 38 expected `.png` files) before unpacking it.
  - **Run #1 showed one unstable picture:** `navigation-collapsed` listed users that earlier tests create with
    random names. Fixed in the test (`b7b45ce`: search `demo.example`, mouse away from the table), run #2 made a
    stable set. **O2: the owner looked at all 38 pictures (a review page by screen and width) and accepted them.**
  - **Baselines committed** (`85107ea`, `test(ui): first screenshot baselines`): 12 screens × desktop/tablet/phone,
    the collapsed panel (desktop) and the drawer (phone). CI's end-to-end step now has `FLOWZA_E2E_SCREENSHOTS=1`.
  - **CI #35 on `main` with the baselines: green** (its first attempt was cancelled by the CI concurrency rule when
    another run for `main` started; the re-run passed all three jobs).
  - **Proof (UI-10-AC1):** branch `ui10-proof-one-pixel` made the header 1 px taller (`layout.headerHeight` 65);
    CI #41 on it: **End-to-end red** with diff images in the `playwright-report` artifact (9 screens: users list,
    user details, my profile, create user at the widths where the page grew by one row). The branch was deleted from
    GitHub and this PC; nothing of it reached `main`.
  - **Observed, for later:** (1) sparse screens (home, 404, error) moved by the same 1 px stayed **under the 1 %
    tolerance** and passed; the plan's tolerance is kept (PA5), a tighter one is a decision for the owner if needed.
    (2) CI #41's first "Build, test and coverage" failed once (exit code 2, no test named in the annotations); all
    463 .NET tests passed locally on the same commit and the re-run was green: watch for a flaky .NET test.
    (3) Dependabot opened pull requests that move the GitHub actions to newer versions (the "Node.js 20 is
    deprecated" warning); they pass CI and wait for the owner.
- **T013 (2026-10-24):** [setup guide](../../docs/setup/local-setup.md) §5 (the old §5 "What to tell Claude" is now §6;
  nothing linked to it by number): 5.1 the installation name (`appsettings.Production.json` or `Installation__Name`
  on a real installation; on the dev PC a temporary change of Identity's `appsettings.json`, put back before a
  commit, because the repository is public), its rules and start-up message, the web app's 5-minute cache; 5.2
  `designValues.json` → `npm run design:css`, commit both, the drift test; 5.3 what a red look check means and the
  five steps to accept a new look (PA5, O2). CLAUDE.md: a "Visual design" row in the decisions table. All relative
  links in both files were checked to point to existing files.

## Phase 1: Design values (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T001 | **Design values and adapter:** `src/Web/src/app/theme/designValues.json` with the values of plan §5.1 (brand, navigation, neutrals, the AA-tuned status colours moved unchanged from `theme.ts`, type, space, shape, breakpoints); `antdTheme.ts` builds the Ant Design `ThemeConfig` from it (`cssVar` prefix `flowza` kept; `Layout`, `Menu` dark, `Card`, `Button`, `Input` component tokens); `contrast.ts` (relative luminance, contrast ratio, `readableTextOn`); `reducedMotion.ts`; `app/theme.ts` removed and its users switched | UI-02, UI-07 (PA4) | `UI_07_AC1_*` (light, mid, dark, near-navy brand colours reach ≥ 4.5 : 1); adapter unit tests (brand → `colorPrimary`, on-brand text from `readableTextOn`); all existing web tests pass |
| T002 | **Login-page CSS generator and the two checks:** `tooling/generate-account-css.ts` + `npm run design:css` writes `wwwroot/account/assets/design-values.css` in Identity (header "generated, do not edit"); file committed; drift test; stray-value scan over `src/**/*.{tsx,css}` and Identity's `account.css` with an **allow list of files still to clean** (today's `account.css` and any CSS module with literal values) that must be **empty by T010** | UI-02 (PA4) | `UI_02_AC2_*` (regenerated file equals the committed one; adapter brand = generated `--flowza-brand`); `UI_02_AC1_*` passes with the allow list |

## Phase 2: Identity (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T003 | **Installation name and initials:** `Installation:Name` options (default "Flowza" in `appsettings.json`; validated at start-up, 1–100 characters, not blank); `InstallationBrand` (name + initials by the C6 rule, text elements via `StringInfo`, upper case invariant); vertical slice `Installation/GetInstallation`; endpoint `GET /api/v1/installation` (any logged-in user, `Cache-Control: private, max-age=300`) in Identity's OpenAPI | UI-08 (C6, C7; PA1, PA2) | Unit `UI_08_AC1_*` (two words, many words, one word, one letter, extra spaces, Hindi and Tamil names); integration on `flowza_test_identity` `UI_08_AC2_*` (configured name and initials; default "Flowza"/"FL"; anonymous → 401); start-up refuses a blank or 101-character name |
| T004 | **Login and change-password pages in the split layout:** `_Layout.cshtml` with the brand panel (initials mark, installation name from `InstallationBrand`, product line from resources, C1) and the form column ("Powered by Flowza"); one column below 768 px with the brand band; `account.css` rewritten as layout only, reading `design-values.css`; focus outlines (brand on the form side, white on the panel); error block in the new look with today's general text; both pages use the layout (C5). Page texts stay as today ("Log in"), so the end-to-end helpers keep working | UI-01, UI-06, UI-08 (C1, C5, C7) | Integration (Identity, `WebApplicationFactory`) `UI_01_*` and `UI_08_AC2_*` for the login page: name, initials, product line and "Powered by Flowza" rendered; a 100-character name carries the full text in `title`; `account.css` leaves the T002 allow list; existing `IAM_01_AC2_*` and the T029 end-to-end suite still pass |
| T005 | **Show/hide password:** `account.js` (served from `/account/assets`, no inline script, CSP unchanged) adds a `<button type="button" aria-pressed>` with translated "Show password" / "Hide password" next to each password field; without JavaScript the field just stays hidden | UI-01 (PA6) | End-to-end `UI_01_AC2_*`: mouse and keyboard (Space, Enter) switch the field between `password` and `text`, `aria-pressed` follows; the login still submits; axe clean on the login page |
| T005b | **Login page as mockup B (spec amendment 1):** `designValues.json` to the teal values of plan §5.1 (brand `#0F6E56`; navigation deep teal with `raised`, `line`, `accent`; light page, border, teal-tinted card shadow) and `npm run design:css`; `_Layout.cshtml` with the deep teal panel (mark, name, decorative circle, workflow picture `aria-hidden`, product line as headline, highlights as a list) and the form on a card; "Use your work username" on the login page; C8 texts in the resources; `account.css` rewritten for it; `account.js` shows the toggle as an eye icon inside the field (name "Show password" kept, icon follows the state) | UI-01, UI-02, UI-06 (C1, C5, C8) | Integration `UI_01_AC1_*` (panel with picture hidden from screen readers, headline, three highlights, form card), `UI_01_AC3_*`, `UI_08_*` updated; web theme and contrast tests pass with the new values; the stray-value scan stays clean for the login pages; end-to-end `UI_01_AC2_*` and the T029 suite pass; the owner looks at the page |

## Phase 3: Web frame (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T006 | **Installation in the web app and the brand mark:** `npm run api:update` (orval) for the new endpoint; `useInstallation()` (TanStack Query, `staleTime` 5 minutes); MSW handler; `BrandMark` (initials square + name, ellipsis, full name in `title` and the link's accessible name; outline only while loading) | UI-08 | Component tests `UI_08_AC1_*` (a 100-character name is shortened, full text in `title` and accessible name), `UI_08_AC2_*` (name and initials from the API appear in the frame) |
| T007 | **Dark side navigation and its preference:** `group` field in the navigation registry ("main" \| "administration"); `SideNavigation` as Ant Design `Menu` `theme="dark"` in a dark `Layout.Sider` (232 px open, 72 px collapsed, collapse button at the bottom), Administration group only when it has visible items, selected item with the brand background and a 3 px left bar; `useNavigationPreference` (`localStorage` `flowza.navigation.collapsed` inside `try/catch`; default open on desktop, collapsed at tablet width, C3/C4; width from `Grid.useBreakpoint()`) | UI-03, UI-04 (C3, C4) | Component tests `UI_03_AC1_*` (admin sees Administration, a User does not, current item selected), `UI_04_AC1_*` (collapsed: icons with accessible names and tooltips), hook tests `UI_04_AC2_*` (defaults by width; stored value wins; broken storage falls back to the default) |
| T008 | **Header and page titles:** each route gets a `titleKey` handle; `HeaderBar` shows the deepest matched title and sets the document title; user menu with an `Avatar` (initials from the display name, same rule as C6), display name, "My profile" and "Log out" | UI-03 | Component tests `UI_03_AC2_*` (title per route, document title, initials, menu items); the end-to-end helpers' "User menu for …" button keeps its accessible name |
| T009 | **Phone frame:** below 768 px no Sider; a menu button in the header (`aria-label`, `aria-expanded`) opens a left `Drawer` with the same `SideNavigation`; choosing an item navigates and closes it; focus returns to the button; content padding 16 px; tables scroll inside their card (`scroll.x`), never the page; `@media print` hides navigation and header; reduced motion switches Ant Design motion off | UI-05, UI-06 | Component tests `UI_05_AC2_*` (button opens the drawer, choosing an item closes it and navigates, `aria-expanded` follows, focus returns) and a reduced-motion test |
| T010 | **Existing screens in the new look:** home (today's content, C2), users list (filters and table in cards), user details (sections as cards), create user, my profile, 403/404/error (reference code kept), `AppLoading`, empty states (`Empty` with translated texts); remove every literal value so the **T002 allow list is empty** | UI-09, UI-02 (C2) | `UI_02_AC1_*` passes with an **empty** allow list; all existing Vitest tests pass; translations only from `locales` (lint) |

## Phase 4: Verification (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T011 | **Visual and accessibility end-to-end tests:** Playwright projects desktop 1280×800, tablet 900×1100, phone 375×812; `07-visual-design.spec.ts`: screenshots (login, failed login, forced change for a fresh user, home, users list, user details, create user, my profile, 404, error via a forced 500, loading via a delayed API, empty list via a search with no result) with masks for dynamic parts; no sideways scrolling at 375 px on every screen; drawer behaviour at 375 px; collapse + reload; defaults at 1280 and 900 px; Tab walk with a visible outline on each focused control; axe on every screen at desktop and phone width. Screenshot assertions only when `FLOWZA_E2E_SCREENSHOTS=1` (PA5); the rest always runs | UI-01, UI-04, UI-05, UI-06, UI-09 | `UI_01_AC1_*`, `UI_01_AC3_*`, `UI_04_AC1_*`, `UI_04_AC2_*`, `UI_05_AC1_*`, `UI_05_AC2_*`, `UI_06_AC1_*`, `UI_06_AC2_*`, `UI_09_AC1_*` written; the non-screenshot tests pass locally through `run-e2e.ps1`; the full earlier end-to-end suite still passes |
| T012 | **CI look check and baselines:** the CI end-to-end job runs with `FLOWZA_E2E_SCREENSHOTS=1` and keeps the Playwright report (diff images) on failure; new manual workflow `update-screenshots.yml` (`workflow_dispatch`, `--update-snapshots`, baselines uploaded as an artifact, never commits or pushes). After the owner's push OK (O1): run it, download the images, show them to the owner (O2), commit them (`test(ui): first screenshot baselines (UI-10)`) | UI-10 (PA5) | `UI_10_AC1_*`: CI green with the baselines; a deliberate one-pixel-row change on a branch turns the job red with a diff image (then reverted) |
| T013 | **Setup guide and CLAUDE.md:** `docs/setup/local-setup.md`: setting `Installation:Name` (C7), `npm run design:css`, how a new look is accepted (PA5); CLAUDE.md decisions table: visual design row (Classic enterprise, Ant Design themed from one design-values file, no Tailwind) | UI-02, UI-08, UI-10 | Owner reads the guide section; links work |
| T014 | **Verify 007:** `run-and-verify` (platform ready, all .NET, web and end-to-end tests), traceability table all ✅, code review by the `code-reviewer` agent and its findings handled; hand checks of the non-tested edge cases (200 % zoom, printing, a long translated label) noted here | All UI IDs | Traceability below all ✅; review findings closed; status of this file "done" |

## Carried to later features
| Item | Where |
|---|---|
| `readableTextOn` in C# for the login page, when the admin can change the brand colour at run time (plan §5.2) | Spec 001 PLT-08, task T042 (M3) |
| Logo instead of the initials mark (login page and frame) | Spec 001 PLT-08, task T042 (M3); `GET /api/v1/installation` keeps its contract and may add a logo address then (a contract addition for the owner to approve) |
| New screens of 002–006 | Each feature builds them in this frame and adds them to the screenshot and axe tests of T011 |

## Analysis (spec ↔ plan ↔ tasks)
| # | Finding | Resolution |
|---|---|---|
| A1 | **UI-07-AC1** asks for readability with "any brand colour"; in M1 the colour is fixed, so it can only be shown with a list of colours | T001 tests `readableTextOn` with light, mid, dark and near-navy colours; the run-time C# part moves to PLT-08 (carried above) |
| A2 | **UI-10-AC1** needs CI and baselines made on Linux, which needs the owner's push OK | T012 waits for O1/O2; until then screenshot assertions are skipped locally (PA5) and the other end-to-end checks still run |
| A3 | **UI-08-AC1** ("a very long name is shortened") cannot be shown in the end-to-end run, which uses the default name (changing it needs a restart of Identity) | Covered by the web component test (T006) and the Identity page integration test with a configured 100-character name (T004) |
| A4 | Plan §5.4's sketch says "Sign in"; the spec does not decide the wording and the end-to-end helpers look for "Log in" | Keep "Log in" (T004); no spec change |
| A5 | **UI-01-AC3** needs the forced change-password page, which only appears for a user with a temporary password | T011 creates a fresh user as the demo admin (existing helpers) and screenshots the forced change |
| A6 | **UI-09-AC1** includes loading and error states, which are hard to reach on a healthy platform | T011 delays or fails the API with Playwright route interception |
| A7 | Spec edge cases without an acceptance criterion (200 % zoom, printing, longer translated text, reduced motion) | Reduced motion has a component test (T009); the others are hand checks recorded in T014 |
| A8 | Restyling may break the 001 end-to-end selectors | T004, T008 keep the accessible names the helpers use ("Log in", "User menu for …"); T011 runs the whole earlier suite |
| A9 | Every M1 acceptance criterion of spec 007 maps to at least one task (table below) | — |

### Traceability (acceptance criteria → tasks)
| Criterion | Task(s) | Status |
|---|---|---|
| UI-01-AC1 | T004, T005b, T011, T012 | ✅ brand panel (mark, name, product line), form card and "Powered by Flowza" (`UI_01_AC1_*`, T004/T005b); picture hidden from screen readers and the three highlights (T005b); beside the form at desktop and tablet, the brand band above it at 375 px, no sideways scrolling, axe clean (`UI_01_AC1_UI_05_AC1_UI_06_AC1_*`, T011); the login page matches its approved screenshot at three widths in CI (T012) |
| UI-01-AC2 | T005 | ✅ mouse and keyboard (Tab, Space, Enter) switch the field, `aria-pressed` follows, the login still works with the password shown, axe clean (`UI_01_AC2_*`, end-to-end); the script is a file with its translated name (`UI_01_AC2_*`, integration) |
| UI-01-AC3 | T004, T011, T012 | ✅ the general error inside the form card and the forced change for a freshly created user in the login look, at three widths, axe clean (`UI_01_AC3_*`, T004 and T011); both match their approved screenshots in CI (T012) |
| UI-02-AC1 | T002, T010 | ✅ no screen, frame file or login page holds a colour, font, spacing or corner value of its own; the scan runs with **no exceptions** and covers the screens, the frame and the login pages (`UI_02_AC1_*`, T010); spacing and the code font come from the design values through the adapter (`UI_02_spacing_and_the_code_font_*`) |
| UI-02-AC2 | T001, T002, T004 | ✅ the theme and `design-values.css` come from the same values and agree on brand, on-brand text and font (`UI_02_AC2_*`, T002); the login pages load `design-values.css` before their layout (`UI_02_AC2_*`, T004) |
| UI-03-AC1 | T007, T011 | ✅ admin sees "Administration", a User does not, the current item is selected with `aria-current` (`UI_03_AC1_*`, component and registry tests, T007; end-to-end with the demo admin and the demo designer, T011) |
| UI-03-AC2 | T008 | ✅ the header shows the title of each page, the browser tab "<page> – <installation>" and follows navigation, the title is not a second heading, the user menu shows initials and display name and offers "My profile" and "Log out" (`UI_03_AC2_*`, component tests); initials by the C6 rule (`UI_03_AC2_initials_*`); the end-to-end helpers still find "User menu for …" |
| UI-04-AC1 | T007, T011 | ✅ collapsed: icons only (72 px), every item keeps its accessible name and its tooltip, axe clean; after a real reload still collapsed (`UI_04_AC1_*`, component tests T007, end-to-end T011) |
| UI-04-AC2 | T007, T011 | ✅ without a remembered choice open (232 px) at 1280 px and collapsed (72 px) at 900 px (`UI_04_AC2_*`, hook tests T007, end-to-end T011) |
| UI-05-AC1 | T009, T011 | ✅ no sideways scrolling on every screen (login, failed login, forced change, home, users list, user details, create user, my profile, 404, error, loading, empty list) at 375, 900 and 1280 px (`UI_*_UI_05_AC1_*`, end-to-end T011); wide tables scroll inside their box (`UI_05_AC1_a_list_with_rows_*`) |
| UI-05-AC2 | T009, T011 | ✅ at 375 px the menu button opens the navigation (`aria-expanded`), choosing "Users" opens the list, closes the drawer and returns the focus to the button (`UI_05_AC2_*`, component tests T009, end-to-end T011) |
| UI-06-AC1 | T004, T009, T011 | ✅ axe finds nothing on every screen, the collapsed panel and the open drawer, at desktop, tablet and phone width (`UI_*_UI_06_AC1_*`, end-to-end T011); the one finding (the empty table's scroll area at 375 px) was fixed (`UI_06_AC1_an_empty_list_fits_the_width_*`) |
| UI-06-AC2 | T004, T011 | ✅ Tab through the login page and the frame at three widths: every control shows an outline or focus shadow (`UI_06_AC2_*`, end-to-end T011) |
| UI-07-AC1 | T001 (A1) | ✅ 22 brand colours and all 256 greys reach ≥ 4.5 : 1; the theme picks the text colour (`UI_07_AC1_*`) |
| UI-08-AC1 | T003, T004, T006 (A3) | 🟡 initials rule proven (`UI_08_AC1_*`: two, many, one word, one letter, spaces, Hindi, Tamil, accents; T003); on the login page a 100-character name is shortened by CSS and kept whole in `title` and the text (`UI_08_AC1_*`, T004); the same in the frame, full name in `title` and the link's accessible name (`UI_08_AC1_*`, T006). The visible "…" cannot appear in the end-to-end run, which uses the default name (A3): a hand check with a long configured name in T014 |
| UI-08-AC2 | T003, T004, T006 | ✅ configured name and initials, default "Flowza"/"FL", anonymous → 401 (`UI_08_AC2_*`, T003); on the login page (`UI_08_AC2_*`, T004); in the frame, from the API (`UI_08_AC2_*`, T006) |
| UI-09-AC1 | T010, T011, T012 | ✅ every screen on cards in the new look; 403/404 with a brand-coloured icon (`UI_09_AC1_*`, T010); home, users list, user details, create user, my profile, 404, error (mocked 500 with its reference code), loading and empty list at three widths with the scroll and axe checks (T011), each matching its approved screenshot in CI (T012) |
| UI-10-AC1 | T012 (A2) | ✅ CI's end-to-end job compares the 14 main screens with the 38 approved screenshots on every push (`FLOWZA_E2E_SCREENSHOTS=1`); CI #35 on `main` green with them; a 1-pixel-taller header on a branch turned the job red with diff images (CI #41), then the branch was deleted (T012) |
