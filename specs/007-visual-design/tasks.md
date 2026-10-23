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
| T006 Installation in the web app, brand mark | ⬜ | |
| T007 Dark side navigation and its preference | ⬜ | |
| T008 Header and page titles | ⬜ | |
| T009 Phone frame | ⬜ | |
| T010 Existing screens in the new look | ⬜ | |
| T011 Visual and accessibility end-to-end tests | ⬜ | |
| T012 CI look check and screenshot baselines | ⬜ | |
| T013 Setup guide and CLAUDE.md | ⬜ | |
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
| UI-01-AC1 | T004, T005b, T011, T012 | 🟡 brand panel (mark, name, product line), form card and "Powered by Flowza" (`UI_01_AC1_*`, T004/T005b); workflow picture hidden from screen readers and the three highlights (`UI_01_AC1_*`, T005b); the desktop screenshot follows in T011/T012 |
| UI-01-AC2 | T005 | ✅ mouse and keyboard (Tab, Space, Enter) switch the field, `aria-pressed` follows, the login still works with the password shown, axe clean (`UI_01_AC2_*`, end-to-end); the script is a file with its translated name (`UI_01_AC2_*`, integration) |
| UI-01-AC3 | T004, T011, T012 | 🟡 the general error above the form in the page look, and the change-password page in the same layout (`UI_01_AC3_*`, T004); screenshots in T011/T012 |
| UI-02-AC1 | T002, T010 | 🟡 the scan runs (`UI_02_AC1_*`, T002); `account.css` is clean (T004); 7 files remain on the allow list for T007–T010 |
| UI-02-AC2 | T001, T002, T004 | ✅ the theme and `design-values.css` come from the same values and agree on brand, on-brand text and font (`UI_02_AC2_*`, T002); the login pages load `design-values.css` before their layout (`UI_02_AC2_*`, T004) |
| UI-03-AC1 | T007, T011 | ⬜ |
| UI-03-AC2 | T008 | ⬜ |
| UI-04-AC1 | T007, T011 | ⬜ |
| UI-04-AC2 | T007, T011 | ⬜ |
| UI-05-AC1 | T009, T011 | ⬜ |
| UI-05-AC2 | T009, T011 | ⬜ |
| UI-06-AC1 | T004, T009, T011 | ⬜ |
| UI-06-AC2 | T004, T011 | ⬜ |
| UI-07-AC1 | T001 (A1) | ✅ 22 brand colours and all 256 greys reach ≥ 4.5 : 1; the theme picks the text colour (`UI_07_AC1_*`) |
| UI-08-AC1 | T003, T004, T006 (A3) | 🟡 initials rule proven (`UI_08_AC1_*`: two, many, one word, one letter, spaces, Hindi, Tamil, accents; T003); on the login page a 100-character name is shortened by CSS and kept whole in `title` and the text (`UI_08_AC1_*`, T004); the frame follows in T006 |
| UI-08-AC2 | T003, T004, T006 | 🟡 configured name and initials, default "Flowza"/"FL", anonymous → 401 (`UI_08_AC2_*`, T003); the login page shows the configured name and initials (`UI_08_AC2_*`, T004); the frame follows in T006 |
| UI-09-AC1 | T010, T011, T012 | ⬜ |
| UI-10-AC1 | T012 (A2) | ⬜ |
