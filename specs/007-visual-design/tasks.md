# Tasks 007: Visual design (UI)

| | |
|---|---|
| **Status** | **Written and analyzed 2026-10-23**; build starts with T001 (feature 007 is built **before 002**, U11) |
| **Spec / Plan** | [spec.md](spec.md) (approved 2026-10-23) · [plan.md](plan.md) (approved 2026-10-23, PA1–PA6) |
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
| T002 Login-page CSS generator and the two checks | ⬜ | |
| T003 Installation name and initials (Identity) | ⬜ | |
| T004 Login and change-password pages, split layout | ⬜ | |
| T005 Show/hide password | ⬜ | |
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
| UI-01-AC1 | T004, T011, T012 | ⬜ |
| UI-01-AC2 | T005 | ⬜ |
| UI-01-AC3 | T004, T011, T012 | ⬜ |
| UI-02-AC1 | T002, T010 | ⬜ |
| UI-02-AC2 | T001, T002 | 🟡 the Ant Design theme is built only from the design values (T001, `UI_02_*`); the login-page CSS and the drift check follow in T002 |
| UI-03-AC1 | T007, T011 | ⬜ |
| UI-03-AC2 | T008 | ⬜ |
| UI-04-AC1 | T007, T011 | ⬜ |
| UI-04-AC2 | T007, T011 | ⬜ |
| UI-05-AC1 | T009, T011 | ⬜ |
| UI-05-AC2 | T009, T011 | ⬜ |
| UI-06-AC1 | T004, T009, T011 | ⬜ |
| UI-06-AC2 | T004, T011 | ⬜ |
| UI-07-AC1 | T001 (A1) | ✅ 22 brand colours and all 256 greys reach ≥ 4.5 : 1; the theme picks the text colour (`UI_07_AC1_*`) |
| UI-08-AC1 | T003, T004, T006 (A3) | ⬜ |
| UI-08-AC2 | T003, T004, T006 | ⬜ |
| UI-09-AC1 | T010, T011, T012 | ⬜ |
| UI-10-AC1 | T012 (A2) | ⬜ |
