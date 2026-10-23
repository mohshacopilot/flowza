# Plan 007: Visual design (UI)

| | |
|---|---|
| **Status** | **Draft for the owner's review** (items to approve: PA1–PA6, section 14) |
| **Date** | 2026-10-23 |
| **Spec** | [spec.md](spec.md) (approved 2026-10-23, C1–C7 all "a") |
| **Inputs** | [UI design discussion](../../docs/discussions/ui-design.md) (U1–U12, design notes in section 7), [tech discussion](../../docs/discussions/tech-discussion.md) T10 (Ant Design theme tokens, FE4), [plan 001](../001-platform-foundation/plan.md) sections 5, 10, 12, [CLAUDE.md](../../CLAUDE.md) |
| **Next** | `tasks.md` after this plan is approved; built **before plan 002** (U11) |

> **This is the "how".** The spec says what and why; this plan chooses files, values, APIs and tests. Anything
> here that changes a contract or adds a library needs the owner's approval (constitution 1.6); section 14 lists
> exactly those items. This feature adds **no library, no database change and no event**.

## 1. Approach in one paragraph
All visual decisions move into **one design-values file** in the web app. From it, an **adapter** builds the Ant
Design theme for the React app, and a small **generator** writes the same values as CSS variables for Identity's
login pages, with a test that fails if the two ever drift (today they already differ: the login page uses
`#1f5eff`, the app `#1f5fbf`). The React frame is rebuilt in the **Classic enterprise** look with Ant Design's own
components (dark `Layout.Sider` + `Menu`, header with page title and user menu, a `Drawer` on phones). Identity's
login and change-password pages get the **split layout** (brand panel + form). The installation name comes from
Identity's configuration (C7) through one small new API, together with its initials mark (C6), so both the login
page and the app show the same brand. Playwright **screenshot tests** at desktop, tablet and phone width, plus the
existing axe checks, protect the result.

## 2. Technical context
| Item | Choice |
|---|---|
| Web | React 19 + TypeScript (strict) + Vite, **Ant Design 6** (themed, U2), CSS Modules for layout (already used), react-i18next |
| Login pages | Identity's Razor Pages (`/account/login`, `/account/change-password`) with their own CSS (generated variables + hand-written layout) and one small script file |
| Identity | One new read-only endpoint and one configuration section (`Installation`) |
| Tests | Vitest + RTL (web), xUnit + Shouldly (Identity), **Playwright `toHaveScreenshot`** (built into the Playwright we already use) + axe |
| Fonts | System fonts (U5), the stack already in `theme.ts`; nothing is downloaded from the internet (NFR-04) |
| Constraints | Works on-premises without internet; latest Chrome, Edge, Firefox; WCAG 2.1 AA (constitution 12.2) |

## 3. Constitution check
| Article | How this plan complies |
|---|---|
| 1 Spec first | Every task names a UI ID; screenshot and unit tests carry the IDs |
| 2 Generic core | No business words; only the installation's own name |
| 3 Service boundaries | The installation name is owned by Identity (where PLT-08 settings live in M3); the web app reads it through the Gateway; no shared database |
| 4 Clean design | One source of design values (single responsibility); adapter for Ant Design; pure functions for contrast and initials, unit-tested |
| 5 Testing | Unit (contrast, initials, preference, adapter, drift), component tests, screenshot + axe end-to-end, all named by ID |
| 6 Portability | No CDN, fonts or images from the internet; screenshots compared on CI's Linux runner |
| 7 Security | New endpoint needs a logged-in user (IAM-05); no inline scripts on the login page (CSP stays strict) |
| 8 Data | No data change |
| 9 Simplicity | **No new library** (U2); Tailwind rejected; only Ant Design components we already ship |
| 10 Observability | Nothing new; error pages keep the reference code (PLT-05) |
| 11 Privacy | The endpoint returns the installation name only; the display name stays where it is today (`/bff/user`) |
| 12 Usable by everyone | AA contrast computed and tested (UI-06, UI-07), visible focus on dark surfaces, keyboard drawer, all text from translation files |

## 4. Structure (changed or new)
```
src/Web/
  src/app/theme/                       (replaces src/app/theme.ts)
    designValues.json                  THE source: colours, font stack, sizes, spacing, radius, shadows, breakpoints
    antdTheme.ts                       adapter: design values → Ant Design ThemeConfig (tokens + Layout/Menu/Card…)
    contrast.ts                        WCAG relative luminance, contrast ratio, readable text colour on a colour (UI-07)
    reducedMotion.ts                   turns Ant Design motion off when the user prefers reduced motion
  src/app/shell/
    AppShell.tsx                       frame: Sider (desktop/tablet) or Drawer (phone), header, content
    SideNavigation.tsx                 dark Menu, groups "main" and "administration", collapsed mode with tooltips
    NavigationDrawer.tsx       (new)   phone: menu button + Drawer holding the same SideNavigation
    HeaderBar.tsx              (new)   page title (from the route) + user menu (initials avatar, display name)
    BrandMark.tsx              (new)   initials mark + installation name, shortened with "…" (UI-08)
    useNavigationPreference.ts (new)   collapsed/open, remembered in localStorage, default by width (C3, C4)
    navigation.tsx                     items get a `group` ("main" | "administration")
  src/app/routes.tsx                   each route carries a `titleKey` handle for the header
  src/shared/installation/     (new)   useInstallation(): name + initials from Identity (TanStack Query, cached)
  src/features/**                      pages wrapped in the frame's cards; users list, details, create, profile restyled
  src/shared/components/               error, loading and empty states in the new look (UI-09)
  src/locales/en/common.json           new keys (navigation group, menu button, collapse, product line, …)
  tooling/generate-account-css.ts (new) writes Identity's design-values CSS from designValues.json
src/Services/Identity/
  Flowza.Identity.Application/Installation/   (new slice) GetInstallation query, InstallationBrand (name + initials)
  Flowza.Identity.Api/
    Endpoints: GET /api/v1/installation (new)
    Pages/Shared/_Layout.cshtml         split layout: brand panel + form column; "Powered by Flowza"
    Pages/Account/Login.cshtml, ChangePassword.cshtml   show/hide password button; error block in the new look
    wwwroot/account/assets/design-values.css  (generated, committed; never edited by hand)
    wwwroot/account/assets/account.css        layout only, reads the variables
    wwwroot/account/assets/account.js  (new)  show/hide password (progressive enhancement)
    Resources: product line, "Powered by Flowza", show/hide labels
    appsettings.json: "Installation": { "Name": "Flowza" }
tests/EndToEnd/
  specs/07-visual-design.spec.ts (new)        screenshots + axe at desktop, tablet, phone; navigation behaviour
  specs/07-visual-design.spec.ts-snapshots/   baseline images (Linux, from CI)
  playwright.config.ts                         projects: desktop 1280×800, tablet 900×1100, phone 375×812
.github/workflows/update-screenshots.yml (new) manual run that produces new baseline images as an artifact
docs/setup/local-setup.md                      how to set the installation name; how to accept a new look
```

## 5. Design per part

### 5.1 Design values (UI-02)
`designValues.json` holds every visual value, by purpose, not by place:

| Group | Values (from the approved mockup A) |
|---|---|
| Brand | `brand` `#1f5fbf`; `onBrand` computed (UI-07), not stored |
| Navigation | `navBackground` `#14294d`, `navText` `#c8d3e6`, `navGroupLabel` `#8193b3`, `navSelectedBackground` = brand, `focusOnDark` `#ffffff` |
| Neutrals | page `#f3f5f9`, surface `#ffffff`, border `#e3e8f0`, text `#1b2333`, secondary text `#5b6577` (AA on page and surface) |
| Status | the AA-tuned success, warning, error colours already in `theme.ts` (T029), moved unchanged |
| Type | system font stack (incl. Noto Devanagari/Tamil), sizes 12 / 14 / 16 / 20 / 24, weights 400 / 600 |
| Space and shape | spacing scale 4 / 8 / 12 / 16 / 24 / 32, radius 6 (controls) and 8 (cards), one card shadow, focus ring 3 px |
| Breakpoints | phone < 768, tablet 768–1024, desktop > 1024 (C4) |

- **Ant Design adapter** (`antdTheme.ts`): maps the values to `ThemeConfig` tokens and component tokens (`Layout.siderBg`,
  `Menu.darkItemBg`, `darkItemSelectedBg`, `darkItemColor`, `Card`, `Button`, `Input`). `cssVar: { prefix: 'flowza' }`
  stays, so layout CSS keeps reading `--flowza-*` variables.
- **Login pages:** `npm run design:css` runs `generate-account-css.ts`, which writes `design-values.css` (CSS variables
  with the same names) into Identity's `wwwroot`. The file is committed, so Identity builds without Node.
  A Vitest test regenerates it in memory and compares with the committed file (**drift check, UI-02-AC2**).
- **No stray values (UI-02-AC1):** a Vitest test scans `src/**/*.{tsx,css}` and Identity's `account.css` for colour
  literals (`#…`, `rgb(…)`, `hsl(…)`) and pixel font sizes; only `designValues.json` and the generated file may
  contain them. Known exceptions are listed in the test with a reason (none expected).

### 5.2 Readable on any brand colour (UI-07)
`contrast.ts`: WCAG 2.1 relative luminance (sRGB linearisation) and contrast ratio
`(L1 + 0.05) / (L2 + 0.05)`; `readableTextOn(colour)` returns white or the dark text colour, whichever has the higher
ratio. Used by the adapter (`colorTextLightSolid` on brand buttons, selected menu item) and by the CSS generator
(`--flowza-on-brand`). The selected navigation item also gets a 3 px left bar in `focusOnDark`, so it stays visible
on the dark navigation even if a future brand colour is close to navy. From M3, when the admin changes the colour
at run time, the same function is needed in C# for the login page; that port (with a shared case list) is a PLT-08
task, not this feature.

### 5.3 Installation name and mark (UI-08, C6, C7)
- **Configuration:** `Installation:Name` in Identity (default `"Flowza"` in `appsettings.json`; an installation sets
  its own in `appsettings.Production.json` or an environment variable). Options are validated at start-up: 1–100
  characters, not blank.
- **`InstallationBrand`** (Identity.Application, small value type): `Name` and `Initials`. Initials rule (C6): split the
  name on whitespace; two or more words → first **text element** (grapheme, `StringInfo`) of each of the first two
  words; one word → its first two text elements; then upper case (invariant). Text elements keep Indian scripts and
  combining marks intact. Only C# computes initials, so the login page and the app can never disagree.
- **API:** `GET /api/v1/installation` → `{ "name": "…", "initials": "…" }`, any logged-in user (IAM-05), cached by the
  browser for 5 minutes. In M3 the same endpoint reads PLT-08's settings instead; the contract does not change.
- **Login pages** read `InstallationBrand` directly (they are in Identity), so they work even when the web app and the
  Gateway's API routes are unavailable (spec edge case).
- **Web:** `useInstallation()` (TanStack Query, `staleTime` 5 minutes). Until it loads, the frame shows the mark's
  outline without text (no flash of "Flowza"). Long names: CSS `text-overflow: ellipsis`, the full name in `title`
  and in the link's accessible name.

### 5.4 Login and change-password pages (UI-01, C1, C5)
```
┌──────────── brand panel (brand colour) ────────────┬──────────── form column (surface) ───────────┐
│ [YO] Your organisation                              │  Sign in                                     │
│                                                     │  Use your work username.                     │
│ Requests and approvals in one place   (C1, resx)    │  Username [                    ]             │
│                                                     │  Password [                 ][Show]          │
│                                                     │  [            Sign in            ]           │
│                                                     │            Powered by Flowza                 │
└─────────────────────────────────────────────────────┴──────────────────────────────────────────────┘
phone (< 768 px): the brand panel becomes a band (mark + name) above the form; the product line is hidden
```
- `_Layout.cshtml` gets the two-column layout (CSS grid; one column below 768 px). Both pages use it (C5).
- **Show/hide password** (UI-01-AC2): a real `<button type="button" aria-pressed>` with a translated label
  ("Show password" / "Hide password"), added by `account.js` (served from `/account/assets`, so the Gateway's CSP
  `script-src 'self'` keeps working). Without JavaScript the field simply stays hidden; the form still works.
- The error block (`role="alert"`) keeps today's general text (IAM-01) in the new look.
- Focus: 3 px brand outline on the form side, white outline on the brand panel.

### 5.5 Application frame (UI-03, UI-04, UI-05, C3, C4)
| Width | Navigation | Header |
|---|---|---|
| Desktop > 1024 | `Layout.Sider` 232 px, dark, open by default (C3); collapse button at the bottom → 72 px icons only | page title left; user menu right |
| Tablet 768–1024 | same Sider, **collapsed by default** (C4) | same |
| Phone < 768 | no Sider; a menu button (`aria-label`, `aria-expanded`) in the header opens a left `Drawer` with the same menu; choosing an item navigates and closes it | menu button, page title, avatar only |

- **Menu:** Ant Design `Menu` in `theme="dark"`, items from the existing registry (`navigation.tsx`), grouped by the new
  `group` field; the "Administration" group appears only if the user has at least one item in it (UI-03-AC1). In
  collapsed mode Ant Design shows each item's label as a tooltip, and the label stays the accessible name (UI-04-AC1).
- **Preference** (`useNavigationPreference`): `localStorage` key `flowza.navigation.collapsed` (`"true"`/`"false"`),
  read and written inside `try/catch` (private windows); without a stored value the default comes from the width
  (C3, C4). Width is read with Ant Design's `Grid.useBreakpoint()` (no new listener code).
- **Header:** the page title comes from the matched route's `titleKey` (and is also set as the document title); the
  user menu shows an `Avatar` with the user's initials (from the display name, same rule as C6, computed in the web
  app for the display name only), the display name (hidden on phones) and the menu items "My profile" and "Log out".
- **Content:** page background from the design values; each page's main content sits in `Card`s; 24 px padding on
  desktop, 16 px on phones. No page may be wider than the viewport (UI-05-AC1): tables get Ant Design's `scroll.x`
  inside their card, so only the table scrolls, never the page.
- **Reduced motion:** `reducedMotion.ts` sets the Ant Design token `motion: false` when
  `prefers-reduced-motion: reduce` matches.
- **Print:** `@media print` hides the Sider, header and drawer button.

### 5.6 Existing screens (UI-09, C2)
Home (today's content, C2), users list (filters in a card, table in a card), user details (sections as cards),
create user, my profile, 403/404/error (with the reference code), loading (`AppLoading`) and empty states (Ant
Design `Empty` with translated text). Only layout and theme change; no behaviour, API or validation changes, so
the existing Vitest and end-to-end tests must still pass unchanged (apart from selectors that follow the new frame).

## 6. Events
None. (No integration event changes; the installation name is read, not published.)

## 7. Design patterns
| Pattern | Problem it solves here |
|---|---|
| **Single source of truth + generated artefact** | One `designValues.json`; the Ant Design theme and the login-page CSS are derived from it, so the two looks cannot drift (UI-02) |
| **Adapter** | `antdTheme.ts` adapts our design values to Ant Design's `ThemeConfig`; replacing or upgrading Ant Design touches only this file |
| **Registry** (existing) | Navigation items stay a registry; the new `group` field adds the Administration group without a `switch` (open/closed) |
| **Progressive enhancement** | The login page works without JavaScript; the show/hide button is added by `account.js` |

No other new pattern (constitution 9.1).

## 8. Data structures and algorithms
| Problem | Choice | Cost |
|---|---|---|
| Readable text on a colour (UI-07) | WCAG relative luminance + contrast ratio, compare two candidates | O(1) |
| Initials (C6) | Split on whitespace, first text element(s) via `StringInfo` | O(n) in the name's length |
| Default navigation state | Breakpoint lookup + one `localStorage` read | O(1) |
| Selected menu item (existing) | Longest matching path prefix | O(items) |
| Page title | The deepest matched route's `titleKey` | O(route depth) |
| Stray-value scan (UI-02-AC1) | One regular-expression pass per source file | O(total source size) |
| Drift check (UI-02-AC2) | Regenerate in memory, compare strings | O(file size) |

Unit tests cover edge cases: black, white, mid-grey, pure primaries and near-navy brand colours; empty-after-trim,
one-letter, one-word, many-word, Hindi and Tamil names; a missing or broken `localStorage`.

## 9. Web app summary
- **Routes:** unchanged paths; each gets a `titleKey`.
- **New components:** `NavigationDrawer`, `HeaderBar`, `BrandMark`; hook `useNavigationPreference`; `useInstallation`.
- **Translations (en):** `nav.groups.administration`, `shell.openNavigation`, `shell.collapseNavigation`,
  `shell.expandNavigation`, page titles, empty-state texts. Identity resources: product line (C1), "Powered by
  Flowza", "Show password", "Hide password", "Use your work username.".
- **API client:** orval regenerates from Identity's OpenAPI (new `installation` endpoint).
- **Accessibility:** focus ring 3 px (brand on light, white on dark); skip link kept; menu button with
  `aria-expanded`; drawer traps focus and returns it to the button; all icons `aria-hidden` with text labels.

## 10. Testing plan
| Spec IDs | Test type | How |
|---|---|---|
| UI-01-AC1 | End-to-end screenshot | Login page at desktop and phone width (`toHaveScreenshot`, username field empty) |
| UI-01-AC2 | End-to-end | Click and keyboard (Space/Enter) on the show/hide button switch `type` between `password` and `text`; `aria-pressed` follows |
| UI-01-AC3 | End-to-end screenshot | Failed login (general message) and the forced change-password page (fresh demo user with temporary password) |
| UI-02-AC1 | Unit (Vitest) | Stray-value scan over web sources and `account.css` |
| UI-02-AC2 | Unit (Vitest) | Regenerated `design-values.css` equals the committed file; the adapter's `colorPrimary` equals the generated `--flowza-brand` |
| UI-03-AC1 | Component (RTL) + end-to-end | Admin sees "Administration"; a User does not; selected item has `aria-current`/selected state |
| UI-03-AC2 | Component (RTL) | Header shows the route's title and the user menu with initials, name, profile and log out |
| UI-04-AC1 | End-to-end | Collapse → icons only, every item has an accessible name; reload → still collapsed |
| UI-04-AC2 | Unit (hook) + end-to-end | No stored value: open at 1280 px, collapsed at 900 px |
| UI-05-AC1 | End-to-end | At 375 px on every screen: `document.documentElement.scrollWidth <= innerWidth` |
| UI-05-AC2 | End-to-end | At 375 px: menu button opens the drawer; choosing "Users" opens the list and closes the drawer |
| UI-06-AC1 | End-to-end (axe) | Every screen at desktop and phone width: no violations (contrast, focus, names) |
| UI-06-AC2 | End-to-end | Tab through login page and frame; each focused element has a visible outline (computed style check) |
| UI-07-AC1 | Unit (Vitest) | For a list of brand colours (light, mid, dark, near-navy), `readableTextOn` reaches ≥ 4.5 : 1 |
| UI-08-AC1 | Unit (xUnit) + end-to-end | Initials rule cases (C6) in `InstallationBrand`; a 100-character name is shortened and its full text is in `title` and the accessible name |
| UI-08-AC2 | Integration (Identity) + end-to-end | `GET /api/v1/installation` returns the configured name and initials; without a value "Flowza"/"FL"; anonymous → 401 (IAM-05) |
| UI-09-AC1 | End-to-end screenshot | Home, users list, user details, create user, my profile, 404, error (forced), loading (delayed API) and empty list |
| UI-10-AC1 | CI | The screenshot tests run in the existing CI end-to-end job on every push; a difference fails the job and the diff images are kept as an artifact |

**Screenshot rules:** baselines are made **on CI's Linux runner** (fonts differ between Windows and Linux); locally
the screenshot assertions are skipped unless `FLOWZA_E2E_SCREENSHOTS=1`. Dynamic parts (temporary passwords, dates,
reference codes) are masked. Tolerance: `maxDiffPixelRatio` 0.01, animations disabled. **Accepting a new look on
purpose** = run the manual `update-screenshots` workflow, download its images, review them with the owner, commit
them (`test(ui): accept new screenshots …`).

## 11. Local development and configuration
- `Installation:Name` defaults to "Flowza"; the setup guide explains how an installation sets its own name (C7).
- `npm run design:css` after changing `designValues.json`; the drift test reminds anyone who forgets.
- No change to the AppHost, databases or secrets.

## 12. CI
- The existing end-to-end job also runs `07-visual-design.spec.ts` with `FLOWZA_E2E_SCREENSHOTS=1` and uploads
  the Playwright report (with diff images) on failure.
- New manual workflow `update-screenshots.yml` (`workflow_dispatch`): runs the same tests with
  `--update-snapshots` and uploads the new baselines as an artifact; it never commits or pushes by itself.

## 13. Identity API addition (for PA1)
| Method and path | Purpose | Who |
|---|---|---|
| `GET /api/v1/installation` | The installation's name and initials mark (C6, C7) | Any logged-in user |

## 14. Items needing the owner's approval (constitution 1.6)
| # | Item | Proposal |
|---|---|---|
| PA1 | **API contract** | `GET /api/v1/installation` → `{ name, initials }` (section 13); unchanged when M3 moves the name to the Settings screen |
| PA2 | **Configuration** | `Installation:Name` in Identity, default "Flowza", validated 1–100 characters (C7) |
| PA3 | **No new library** | Ant Design only (U2); screenshot comparison is built into the Playwright we already use; no Tailwind, no stylelint, no web font |
| PA4 | **Design values file + generated login CSS** | `designValues.json` as the one source; `design-values.css` generated by `npm run design:css` and committed into Identity; drift test in CI (section 5.1) |
| PA5 | **Screenshot baselines from CI** | Baselines made on CI's Linux runner by a manual workflow; local runs skip screenshot assertions by default; a new look is accepted only after the owner sees the images (section 10) |
| PA6 | **Login page script** | One small file `account.js` for show/hide password; no inline script; the page works without it |

No database design change and no event change.

## 15. Risks
| Risk | Mitigation |
|---|---|
| Screenshot tests are flaky (fonts, anti-aliasing, timing) | Linux-only baselines, animations off, masks for dynamic parts, 1 % tolerance, wait for network idle; one retry in CI (already set) |
| Ant Design's dark `Menu` tokens do not reach AA for group labels or disabled items | The axe checks (UI-06-AC1) catch it; component tokens set explicitly in the adapter |
| Restyling breaks existing end-to-end selectors | Tests use roles and labels (not CSS classes); run the full end-to-end suite in the same task |
| The login page and the app drift again later | Drift test (UI-02-AC2) and stray-value scan (UI-02-AC1) in CI |
| M3 changes the brand colour at run time | `readableTextOn` already used everywhere in the web app; the C# port for the login page is listed for PLT-08 (section 5.2) |
| Longer translated texts break the narrow collapsed menu | Collapsed mode shows icons with tooltips only; open mode wraps text; phone drawer is full height |

## 16. Estimate
About **1.5–2 build days** (as agreed in U11): design values, adapter and generator 0.25 · login pages 0.25 ·
installation endpoint 0.25 · frame (Sider, Drawer, header, preference) 0.5 · existing screens 0.25 ·
screenshot and accessibility tests, CI workflow 0.5.
