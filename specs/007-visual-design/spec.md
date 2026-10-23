# Feature 007: Visual design (UI)

| | |
|---|---|
| **Status** | Approved by the owner, 2026-10-23 (clarifications C1–C7 answered, all "a"). **Amendment 1 (login page option B, teal; section 12): draft, waiting for the owner's approval** |
| **Date** | 2026-10-23 |
| **Module** | UI (visual design of the web application and the login pages) |
| **Scope IDs** | UI-01 to UI-10 (new; added to the [v0.1 scope](../v0.1-scope.md) section 3.10) |
| **Build order** | **Before 002** (owner, U11): every screen of 002–006 is built in this look from the start |
| **Sources** | [UI design discussion](../../docs/discussions/ui-design.md) (U1–U12, all decided 2026-10-23), [constitution](../../docs/constitution.md) Article 12, [spec 001](../001-platform-foundation/spec.md) (PLT-08 branding in M3) |
| **Next documents** | `plan.md` (how), `tasks.md` (steps); both only after this spec is approved |

> **This spec says what and why, never how.** The styling tool (decided in the discussion, U2) and every other
> technology choice belong in `plan.md`. The only "technical" words here are measurable constraints the owner gave
> (screen widths, contrast ratios, the brand colour), because they are requirements.

## 1. Summary
The platform works, but its pages look plain, and the login page does not even look like the rest of the
application. This feature gives the whole product one calm, professional look, the **Classic enterprise** direction
chosen by the owner (U1): a split login page with a deep teal brand panel (the installation's name, a small
workflow picture and what the product does) beside a form on a card, and an application frame with a dark side
navigation in the same deep teal, a clear header and content on light cards (amendment 1).

**Why it matters:** the login page is the first thing an organization sees when it evaluates the product, and staff
use the application every working day. Doing it now, before features 002–006, means the many new screens (org
chart, designers, inbox) are built in the right look once instead of being restyled later.

## 2. Users
| Persona | What they need from this feature |
|---|---|
| **Every user** | A clear, readable application that works with the keyboard, on a desktop and on a phone |
| **Platform Admin** | The organization's own name (and later logo) on the login page and in the application |
| **An organization evaluating the product** | A first impression of a serious, trustworthy product |
| **Developers of features 002–006** | One agreed look and one set of design values, so new screens fit without design work each time |

## 3. User stories and acceptance scenarios
All stories are **P1** (built before 002, counted in Milestone 1).

### US1: A branded login page (P1, UI-01, UI-08)
As a user, I sign in on a page that shows my organization's name, so that I know I am in the right place.
1. **Given** the login page on a desktop, **then** the left half is a deep teal brand panel with the installation's
   initials mark (or logo, from M3) and its name, a small picture of a request going through its steps
   (form → approve → done), the product line as a headline and three short highlights of what the product does
   (C1, C8); the right half shows the sign-in form on a white card with a short line under its title (C8).
2. **Then** the page shows a small "Powered by Flowza" (U3).
3. **Then** the password field has a button to show or hide the typed password.
4. **Given** a wrong username or password, **then** the error appears above the form in the same look, and the
   message stays the general one (IAM-01).
5. **Given** the change-password page (also the forced change at first login), **then** it has the same look as
   the login page (C5).

### US2: A familiar frame for daily work (P1, UI-03, UI-04)
As a user, I find my way through the application the same way on every screen.
1. **Then** every screen has a dark side navigation, in the same deep teal as the login panel (amendment 1), with the
   installation's mark and name at the top, the items the user may use, and an "Administration" group that only
   Platform Admins see.
2. **Then** the current item is highlighted in the brand colour.
3. **Then** the header shows the page title and the user menu (initials avatar and display name; profile and log out).
4. **When** the user collapses the side navigation, **then** it shows icons only, each icon still has its name
   (shown on hover and read by screen readers), and the choice is remembered in that browser (U9).

### US3: Usable on a phone (P1, UI-05)
As a user away from my desk, I can open the application on a phone and use it.
1. **Given** a screen 375 px wide, **then** no page scrolls sideways.
2. **Then** the side navigation is replaced by a menu button that opens it; it closes when an item is chosen.
3. **Then** the login page shows the brand panel as a short band (mark and name) above the form; the picture, the
   headline and the highlights are left out at this width.

### US4: Readable and usable by everyone (P1, UI-06, UI-07)
As a user with low vision or who works with the keyboard, I can read and use every screen.
1. **Then** text and controls meet WCAG 2.1 AA contrast on every screen (constitution 12.2).
2. **Then** the keyboard focus is always clearly visible, also on the dark side navigation and the brand panel.
3. **Given** any brand colour (the admin may change it from M3, PLT-08), **then** text on brand-coloured surfaces
   is still readable (at least 4.5 : 1), because the light or dark text colour is chosen automatically.

### US5: One consistent look (P1, UI-02, UI-09)
As the owner, I want every screen to look like it belongs to the same product.
1. **Then** colours, font, sizes, spacing, corners and shadows all come from one set of design values; no screen
   sets its own.
2. **Then** all existing screens have the new look (U8): login, change password, the frame, home, users list,
   user details, create user, my profile, and the error, loading and empty states.

### US6: The look stays right (P1, UI-10)
As the owner, I want an unintended change to the look to be caught automatically, like any other defect (U12).
1. **When** a change alters how a main screen looks, at desktop or phone width, **then** the automatic check fails
   and shows the difference, until the new look is accepted on purpose.

## 4. Requirements
| ID | Requirement | Milestone |
|---|---|---|
| UI-01 | **Login and change-password pages** in the Classic enterprise look, as in mockup B (amendment 1): a deep teal brand panel (initials mark or logo, installation name, a small workflow picture, the product line as a headline, three short highlights) beside the form on a white card; "Powered by Flowza"; show/hide password; errors in the same look; same design values as the application | M1 |
| UI-02 | **One set of design values** (brand colour teal `#0F6E56` (amendment 1; was `#1f5fbf`), neutral and status colours, font, text sizes, spacing, corners, shadows) is the only source of styling for the application **and** the login pages; system fonts (U5) | M1 |
| UI-03 | **Application frame:** dark side navigation in the deep teal of the login panel (mark and name on top, items by role, "Administration" group for Platform Admins, current item highlighted), header with page title and user menu, content on a light background with cards | M1 |
| UI-04 | The side navigation **collapses to icons**; every icon keeps its name for hover and screen readers; the choice is remembered per browser. Without a remembered choice it starts open on a desktop (C3) and collapsed at tablet width, 768–1024 px (C4) | M1 |
| UI-05 | **Phone width:** at 375 px no page scrolls sideways; the side navigation becomes a menu button; the login page stacks the brand panel above the form. Amends NFR-06 ("usable on a tablet") to include phones for these screens | M1 |
| UI-06 | **Accessibility:** WCAG 2.1 AA contrast and an always visible keyboard focus on every screen, including the dark navigation and the brand panel | M1 |
| UI-07 | **Any brand colour stays readable:** text on brand-coloured surfaces automatically uses light or dark text to reach at least 4.5 : 1 | M1 |
| UI-08 | **Installation name and mark:** the name is set at installation, default "Flowza" (C7; the Settings screen takes over in M3, PLT-08); until a logo can be uploaded, a mark made of the name's initials is shown (C6); long names are shortened with "…" and the full name stays available | M1 |
| UI-09 | **All existing screens** use the new look, including error, loading and empty states; home keeps its present content (C2) | M1 |
| UI-10 | The main screens' look is **checked automatically** at desktop and phone width; an unintended change fails the check | M1 |

## 5. Acceptance criteria
Every criterion gets at least one automated test whose name contains its ID (constitution 1.4), unless marked *manual*.

| ID | Criterion |
|---|---|
| UI-01-AC1 | The login page shows the brand panel (mark, installation name, workflow picture, product line, three highlights), the form on a card and "Powered by Flowza"; the picture is decorative and not read out by screen readers; a screenshot at desktop width matches the approved look |
| UI-01-AC2 | The show/hide button switches the password between hidden and visible and is usable with the keyboard |
| UI-01-AC3 | A failed login shows the general error message in the page's look; the change-password page has the same look as the login page |
| UI-02-AC1 | An automatic check finds no colour, font or size values in screens or pages outside the one set of design values |
| UI-02-AC2 | The login pages and the application use the same brand colour and font (the values are read from the same source) |
| UI-03-AC1 | A Platform Admin sees the "Administration" group; a user without the role does not; the current item is highlighted |
| UI-03-AC2 | The header shows the page title and the user menu with initials and display name; the menu offers profile and log out |
| UI-04-AC1 | Collapsing the navigation shows icons only; each icon has an accessible name; after reloading the page the navigation is still collapsed |
| UI-04-AC2 | Without a remembered choice, the navigation starts open at desktop width and collapsed at tablet width (C3, C4) |
| UI-05-AC1 | At 375 px width, the login page and every application screen have no sideways scrolling |
| UI-05-AC2 | At 375 px width, the menu button opens the navigation, and choosing an item opens that screen and closes the navigation |
| UI-06-AC1 | Accessibility checks report no contrast or focus problems on every screen, at desktop and phone width |
| UI-06-AC2 | Moving through the frame and the login page with the Tab key shows a visible focus on every control |
| UI-07-AC1 | For a range of brand colours (light, mid and dark), the text colour chosen for brand surfaces reaches at least 4.5 : 1 |
| UI-08-AC1 | The initials mark is built from the installation name as agreed (C6); a very long name is shortened with "…" and its full text is available on hover and to screen readers |
| UI-08-AC2 | The installation name set at installation appears on the login page and in the frame; without a value, "Flowza" is shown (C7) |
| UI-09-AC1 | Screenshots of home, users list, user details, create user, my profile, and the error, loading and empty states match the approved look |
| UI-10-AC1 | The automatic look check runs on every pushed change and fails when a main screen differs from its approved screenshot |

## 6. Edge cases
- Long installation names and long display names: shortened with "…", full text on hover and for screen readers.
- Translated text is usually longer than English (Hindi, Tamil): buttons and menu items grow or wrap; text is
  never cut off.
- Browser zoom at 200 %: every screen stays usable (WCAG AA).
- A very light or very dark brand colour (from M3): text on it stays readable (UI-07); the highlighted navigation
  item stays visible on the dark navigation.
- No logo uploaded (always the case before M3): the initials mark is used.
- Users who ask their system for reduced motion: no animations beyond simple fades.
- The login page must work even when the application part of the product is unavailable, because it belongs to
  the sign-in service.
- Printing a screen: the navigation and header are left out; the content prints in black on white.

## 7. Non-functional requirements that apply
From the [v0.1 scope](../v0.1-scope.md) section 6: NFR-04 (works on-premises without internet access: nothing is
loaded from the internet, fonts and images included), NFR-06 (browsers; amended by UI-05 for phone width),
NFR-10 (all text from translation files, including the product line and "Powered by Flowza"), NFR-11 (keyboard and
labels).

## 8. Key entities (business view, not database design)
| Entity | Meaning |
|---|---|
| **Design values** | The one agreed set of colours, font, text sizes, spacing, corners and shadows that every screen uses |
| **Installation brand** | The installation's name, its initials mark and (from M3) its logo and brand colour |
| **Navigation preference** | Whether a user's side navigation is open or collapsed, remembered in that browser only |

## 9. Out of scope
| Item | When |
|---|---|
| Dark mode | Later (U6); the design values are prepared for it |
| Uploading a logo and changing the brand colour on a screen | M3, PLT-08 |
| A bundled web font | Not planned (U5: system fonts) |
| The look of new screens of 002–006 | Each feature follows this look; its screens are specified there |
| The email layout | Feature 006 (NTF N1: fixed clean layout); it uses the same brand values |
| Right-to-left languages | Later (v0.1 is English only) |
| Illustrations beyond the one small workflow picture on the login panel (amendment 1), animations, per-user themes | Not planned |
| Highlights or a picture chosen per installation | Not planned; the highlights are fixed texts (C8) and the picture is the same for everyone |

## 10. Clarifications (answered by the owner, 2026-10-23)
| # | Question | Answer |
|---|---|---|
| C1 | The product line on the login brand panel | **A fixed text from the translation files** ("Requests and approvals in one place"); not editable in v0.1 |
| C2 | Home page content | **Today's content, only restyled**; the counters seen in the mockup arrive with INB-07 in M2 |
| C3 | Side navigation on the first visit | **Open** |
| C4 | Tablet width (768–1024 px) | The side navigation **starts collapsed to icons** (the user can open it; their choice is remembered, UI-04) |
| C5 | Change-password page (also the forced change at first login) | **Same split layout as the login page** |
| C6 | How the initials mark is made | **First letters of the first two words** of the installation name, in capitals; a one-word name gives its first two letters |
| C7 | Where the installation name comes from before M3 | **A value set at installation** (default "Flowza"); M3 moves it to the Settings screen (PLT-08) |
| C8 | (Amendment 1) The highlights, the picture's labels and the line under the form title | **Fixed texts from the translation files**, like C1: "Forms built without code", "Approvals that route themselves", "Every step tracked"; picture labels "Form", "Approve", "Done"; under the title "Use your work username". Not editable in v0.1 |

## 11. Spec quality checklist (Claude checks before asking for approval)
- [x] Every requirement has an ID, a milestone and at least one acceptance criterion
- [x] No technology choices in the spec (only measurable constraints from the owner)
- [x] Every acceptance criterion is testable; none is manual
- [x] Out-of-scope items are listed so nobody builds them by mistake
- [x] All clarifications answered (C1–C7)
- [x] Approved by the owner (2026-10-23)
- [x] Amendment 1: requirements, criteria, out of scope and C8 updated; colours checked for contrast (section 12)
- [ ] Amendment 1 approved by the owner

## 12. Amendments
| # | Date | What changed | Why |
|---|---|---|---|
| 1 | 2026-10-23 | **Login page as mockup B** (owner's choice from three options): deep teal brand panel with the mark and name, a small workflow picture, the product line as a headline and three highlights; the form on a white card with a short line under its title. **Brand colour teal `#0F6E56`** (was `#1f5fbf`); the side navigation uses the same deep teal as the login panel. Changed: summary, US1, US2, US3, UI-01, UI-02, UI-03, UI-01-AC1, out of scope, C8. Discussion: [ui-design.md 4b](../../docs/discussions/ui-design.md) | After T005 the owner saw the built login page and found it too plain and empty, the form too small, and the colours and layout not right. Contrast was checked first: white on the teal 6.2 : 1, light text on the deep teal 9–12 : 1 |
