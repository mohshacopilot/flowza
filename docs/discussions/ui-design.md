# Discussion: UI (visual design)

| | |
|---|---|
| **Status** | Decided by the owner (U1–U12, all "a"; direction **A, Classic enterprise**), 2026-10-23. **Revised the same day after the owner saw the built login page (section 4b): login option B, teal** |
| **Feeds** | A small feature spec `specs/007-visual-design/spec.md` (sections 1–6) and its `plan.md` (section 7) |
| **Scope IDs** | New, proposed: UI-01 to UI-07 (section 5); added to [specs/v0.1-scope.md](../../specs/v0.1-scope.md) once approved |
| **Related** | [PLT](platform.md) (PLT-08 Settings screen: installation name, logo, from M3), [IAM](identity-and-access.md) (login page), constitution Article 12 (usable by everyone), tech discussion T10 (Ant Design theme tokens) |
| **Owning parts** | Web app (React) and Identity's own pages (login, change password) |

## What this file is
The owner looked at the running platform on 2026-10-23 and found the pages plain ("the page looks bad") and asked
for a richer look, for example with Tailwind. This file is the design discussion for that, done the same way as the
module discussions: sections 1–6 are **what and why** (input for `spec.md`); section 7 is **how** (input for
`plan.md`) and must **not** be copied into the spec. The tool choice (Tailwind or not) is a *how*, so it is decided
here but written down only in the plan.

## 1. Purpose
- People use the platform every working day: requesters, approvers, admins. A clear, calm look makes the daily work
  faster and builds trust.
- The login page is the **first thing** an organization sees when it evaluates the platform.
- One consistent look, set **before** features 002–006 add many new screens (org chart, form designer, process
  designer, inbox), so every new screen gets it from the start instead of being restyled later.

## 2. What exists today (facts, 2026-10-23)
| Part | Today |
|---|---|
| Login and change-password pages | Served by the **Identity service** as its own server pages (on purpose: the password is typed only on Identity's page, T5). They have **their own small stylesheet** and do **not** share the React app's look |
| Web app (React) | Ant Design 6 with one theme file: brand blue `#1f5fbf`, radius 6, system fonts (incl. Noto for Devanagari and Tamil), status colours tuned for AA contrast (found by the axe checks, T029) |
| Shell | White header (installation name as text, user menu), white side navigation (Home, Users for admins), grey page |
| Branding | No logo yet; the installation name is a placeholder. From **M3**, PLT-08 lets the admin set the installation name and logo |

## 3. The three directions (mockups shown in the session on 2026-10-23)
Each direction covers the login page and the app shell. The brand colour is the same blue in all three so the
layouts can be compared; the colour itself is question U4.

**A. Classic enterprise:** split login, dark side navigation.
```
┌───────────────────────┬──────────────────────┐   ┌────────┬──────────────────────────────┐
│  ▣ Your organisation  │  Sign in             │   │▣ Org   │ Home                 (AU) ▾ │
│                       │  Username [        ] │   │■ Home  ├──────────────────────────────┤
│  Requests and         │  Password [      👁] │   │  Inbox │ ┌────┐ ┌────┐ ┌────┐          │
│  approvals in one     │  [     Sign in     ] │   │  Users │ │ 3  │ │ 2  │ │ 12 │  cards   │
│  place                │                      │   │ (dark) │ └────┘ └────┘ └────┘          │
│  (brand colour panel) │   Powered by Flowza  │   │        │ table …                      │
└───────────────────────┴──────────────────────┘   └────────┴──────────────────────────────┘
```

**B. Clean and light:** centred card, light side navigation.
```
            ▣ Your organisation                   ┌────────┬──────────────────────────────┐
     ┌──────────────────────────┐                 │▣ Org   │ Home                 (AU) ▾ │
     │ Sign in                  │                 │▐Home   │                              │
     │ Username [             ] │                 │ Inbox  │  white cards on light grey,  │
     │ Password [           👁] │                 │ Users  │  generous spacing,           │
     │ [        Sign in       ] │                 │(white) │  selected item tinted        │
     └──────────────────────────┘                 └────────┴──────────────────────────────┘
          Powered by Flowza
```

**C. Bold brand bar:** brand-coloured login background, navigation along the top.
```
┌──────────────────────────────────────────────┐   ┌──────────────────────────────────────────┐
│ (brand colour)   ┌──────────────────┐        │   │▣ Org   Home  Inbox  Users       (AU) ▾ │ (brand bar)
│                  │ ▣ Sign in        │        │   ├──────────────────────────────────────────┤
│                  │ Username [     ] │        │   │  full-width content, no side navigation  │
│                  │ Password [     ] │        │   │                                          │
│                  │ [   Sign in    ] │        │   │                                          │
└──────────────────┴──────────────────┴────────┘   └──────────────────────────────────────────┘
```
Note for C: a top bar fits 4–6 items. The admin area grows in later features (org chart, apps, settings, email
log), so C would need a "More" menu or a second-level bar later.

## 4. Questions for the owner
Answer like the other discussions ("U1 - a"). Recommended answers are marked ★.

| # | Question | Options |
|---|---|---|
| U1 | Which visual direction? | **Decided by the owner, 2026-10-23: a) A Classic enterprise** (split login with a brand panel; dark side navigation). Other options were: b) B Clean and light (Claude's recommendation) · c) C Bold brand bar · d) a mix |
| U2 | Styling approach (decided here, written in the plan) | a) ★ Keep **Ant Design** and theme it fully (colours, fonts, spacing, radius); the login pages use the **same design values**; no new library · b) Ant Design for components **plus Tailwind** for page layout and the login pages · c) Replace Ant Design with Tailwind and another component kit (not recommended: large rework of done screens and the planned form builder) |
| U3 | Whose brand is shown? | a) ★ The **installation's** name and logo (the customer organization), with a small "Powered by Flowza" on the login page · b) Flowza's name and logo only |
| U4 | Brand colour | a) ★ Keep the current blue `#1f5fbf` · b) a deeper navy/indigo · c) another colour you name. In every case, from M3 the admin may change it (PLT-08), so the design must stay readable with **any** brand colour |
| U5 | Font | a) ★ System fonts as today (no download, works offline on-premises, Indian scripts covered) · b) a bundled web font such as Inter (stored with the app, never from the internet), with the Noto fonts as fallback for Indian scripts |
| U6 | Dark mode | a) ★ Not in v0.1, but all colours come from design values so it can be added later · b) In v0.1 |
| U7 | Phone and tablet width | a) ★ Every screen is usable at phone width (375 px): the side navigation becomes a menu button · b) Desktop only in v0.1 |
| U8 | Which screens now? | a) ★ All existing screens: login, change password, shell, home, users list and details, create user, my profile, error pages · b) Login and shell only, the rest when touched |
| U9 | Side navigation | a) ★ Can be collapsed to icons; the choice is remembered in that browser · b) Always open |
| U10 | A logo before M3 (PLT-08 not built yet) | a) ★ A simple mark from the installation name's first letters until the logo can be uploaded in M3 · b) A fixed Flowza logo until then |
| U11 | When to build | a) ★ As a small feature **before 002** (about 1.5–2 days), so 002–006 inherit the look · b) Later, together with PLT-08 in M3 |
| U12 | How we check the look stays right | a) ★ Automatic screenshot tests of the main screens at desktop and phone width, plus the existing axe accessibility checks · b) Manual checks only |

### 4a. Owner's decisions (2026-10-23)
**U1 - a, U2 to U12 all - a** (the recommended answers).

| # | Decision |
|---|---|
| U1 | Direction **A, Classic enterprise**: split login page with a brand panel; dark navy side navigation |
| U2 | **Ant Design**, fully themed; the login pages use the same design values; **no Tailwind, no new library** |
| U3 | The **installation's** name and logo, with a small "Powered by Flowza" on the login page |
| U4 | Brand colour stays **`#1f5fbf`**; the design must stay readable with any brand colour (from M3, PLT-08) |
| U5 | **System fonts** as today |
| U6 | **No dark mode** in v0.1; all colours come from design values so it can be added later |
| U7 | Every screen usable at **375 px**; the side navigation becomes a menu button |
| U8 | **All existing screens** are restyled now |
| U9 | Side navigation **collapses to icons**; the choice is remembered in that browser |
| U10 | An **initials mark** from the installation name until the logo can be uploaded (M3) |
| U11 | A small feature **`007-visual-design`, built before 002** |
| U12 | **Automatic screenshot tests** at desktop and phone width, plus the existing axe checks |

### 4b. Revision after seeing the built login page (owner, 2026-10-23)
After T005 the owner looked at the running login page (split layout, flat blue panel, small form) and was **not
happy**: too plain and empty, the form too small, the colours and the layout not right. Claude showed three richer
login mockups (A: centred card on an indigo pattern; **B: split page with an illustration and highlights, deep teal
and white**; C: navy banner with an amber accent and an overlapping form card). **The owner picked B.**

| # | Revised decision (replaces the row in 4a) |
|---|---|
| U1 | Still the Classic enterprise family (split login, dark side navigation), but the login page follows **mockup B**: the brand panel is **deep teal** and carries the mark and name, a **small workflow picture** (Form → Approve → Done), the product line as a headline and **three short highlights**; the form sits **on a white card** on a light page, with a short line under the title. The dark side navigation of the app uses the **same deep teal** as the login panel, so both parts look like one product |
| U4 | Brand colour becomes **teal `#0F6E56`** (was blue `#1f5fbf`). Still: from M3 the admin may change it (PLT-08), and any brand colour must stay readable (UI-07) |

Mockup B, desktop:
```
┌──────────── deep teal panel ─────────────┬──────────── light page ────────────────┐
│ [SB] State Bank of Example                │                                        │
│                                           │   ┌──────────────────────────────┐     │
│ [Form] ── [Approve] ── [✓ Done]           │   │ Log in                       │     │
│                                           │   │ Use your work username       │     │
│ Requests and approvals in one place       │   │ Username [                 ] │     │
│ ✓ Forms built without code                │   │ Password [              👁 ] │     │
│ ✓ Approvals that route themselves         │   │ [          Log in          ] │     │
│ ✓ Every step tracked                      │   └──────────────────────────────┘     │
│                                           │          Powered by Flowza             │
└───────────────────────────────────────────┴────────────────────────────────────────┘
phone: a deep teal band with mark and name above the form card; picture, headline and highlights hidden
```

Contrast checked before writing the spec (WCAG 2.1): white on teal `#0F6E56` 6.2 : 1; teal on white 6.2 : 1 and
on the light page 5.6 : 1; light text on the deep teal panel 9–12 : 1. Two points for the plan: the mockup's light
teal field borders reach only 1.5 : 1 (a field's edge needs 3 : 1, so the built borders are darker), and the
selected navigation item (teal on deep teal, 2.2 : 1) keeps its light side bar so it stays visible.

What stays as built: the installation name and initials (T003), the show/hide button and its tests (T005, now
shown as an eye icon inside the field with the same accessible name), the design-values file and its checks
(T001, T002). What changes: the colour values, the login layout and its CSS (T004), the plan's login sketch.

## 5. Draft acceptance criteria (for the spec, after the answers)
| ID | Requirement (what) | How we check it |
|---|---|---|
| UI-01 | The login and change-password pages have the same look as the web app (colours, typography, logo or initials mark, installation name) | Screenshot test of both pages |
| UI-02 | One set of design values (colours, font, spacing, radius, shadows) is the only source of styling; no screen sets its own colours | Lint/review rule + screenshot tests |
| UI-03 | The shell follows the chosen direction: header, navigation by role, page title, content area | Screenshot test of the shell for an admin and a normal user |
| UI-04 | Every screen is usable at 375 px width without sideways scrolling (if U7 = a) | Screenshot test at phone width |
| UI-05 | Text and controls meet WCAG 2.1 AA contrast, and focus is always visible for keyboard users (constitution 12.2) | axe checks on every page; keyboard end-to-end test |
| UI-06 | The look stays readable when the brand colour changes (from M3) | Unit test: for a range of brand colours the text on brand-coloured surfaces reaches 4.5:1 |
| UI-07 | Loading, empty and error states share the same look (no unstyled pages) | Screenshot tests of the empty list, loading and error pages |

## 6. Edge cases
- Long installation names and long user names: shortened with "…" and the full text on hover/focus.
- Translated text is often longer than English (Hindi, Tamil): layouts must not break; buttons grow, never cut text.
- A brand colour with poor contrast (from M3): the platform picks white or dark text automatically (UI-06).
- No logo uploaded: the initials mark is used (U10).
- Browser zoom at 200 %: still usable (AA).
- Users who prefer reduced motion: no animations beyond simple fades.
- The login page must work even when the web app is down (it is served by Identity), so it cannot depend on the
  React bundle.

## 7. Design notes (how; for `plan.md` only)
- **One source of design values.** Today the values live in `src/Web/src/app/theme.ts` (Ant Design tokens, already
  exposed as CSS variables `--flowza-*`). The plan should make a single token file the source and **generate** a
  small CSS file of the same variables for Identity's pages, so the login page and the app can never drift apart.
- **If U2 = a (Ant Design only):** layout CSS uses CSS Modules (already in use) reading the `--flowza-*` variables;
  the login pages use the generated CSS file. No new library.
- **If U2 = b (Ant Design + Tailwind):** Tailwind (MIT) needs owner approval in the plan (constitution 1.6) and an
  ADR. Points to settle: Tailwind's reset ("preflight") must not override Ant Design's styles (use CSS cascade
  layers); a rule for which tool styles what (Ant Design = components, Tailwind = layout only); Tailwind's theme reads
  the same design values; the Identity pages need their own small Tailwind build (no CDN on-premises).
- **Fonts and images** are always bundled with the app, never loaded from the internet (on-premises installations
  may have no internet access).
- **Screenshot tests:** Playwright `toHaveScreenshot` at 1280 px and 375 px. Fonts render differently on Windows
  (dev PC) and Linux (CI), so baselines are kept for CI (Linux) and compared there; locally they are optional.
- **Patterns:** none new. Design values follow the existing "one theme file" rule (T10, FE4).

## 8. How this goes through spec-driven development
1. **Discuss** (this file): the owner answers U1–U12.
2. **Specify:** Claude writes `specs/007-visual-design/spec.md` from sections 1–6 and the answers (IDs UI-01…);
   the owner approves it. The scope file gets the new IDs and the build order puts 007 before 002 (if U11 = a).
3. **Plan:** `plan.md` with the styling approach, the token file, the screenshot tests and, if Tailwind is chosen,
   the library approval and an ADR; CLAUDE.md's decision table is updated after approval.
4. **Tasks → implement → verify**, one task at a time, tests named after the UI IDs.
