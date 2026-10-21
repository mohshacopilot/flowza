# Flowza web app

One React app for every screen (tech discussion T1): React 19 + TypeScript (strict) + Vite, Ant Design,
React Router, TanStack Query, react-i18next. Tests: Vitest + React Testing Library + MSW.

## Run it
- **With everything else (normal):** F5 on the AppHost in Visual Studio (or `dotnet run --project src/AppHost/Flowza.AppHost`).
  The AppHost runs `npm install` and then the Vite dev server as a program (no container) on
  **http://localhost:5105**; it shows up as `web` in the Aspire dashboard. Browse through the Gateway
  (http://localhost:5100) once its routes exist (T021), so the login cookie and the API share one address.
- **On its own (quick UI work):** `npm install` once, then `npm run dev` in this folder. Calls to `/api/…` and
  `/bff/…` are forwarded to the Gateway (`FLOWZA_GATEWAY_URL`, default http://localhost:5100).

Node.js 24 LTS and npm are required (`engines` in package.json).

## Scripts
| Script | What it does |
|---|---|
| `npm run dev` | Vite dev server (port from `PORT`, default 5105) |
| `npm run build` | Type-check, then production build into `dist/` (served by the Gateway in production) |
| `npm run lint` | ESLint (TypeScript strict, React hooks, jsx-a11y strict, no hard-coded screen text), no warnings allowed |
| `npm run typecheck` | `tsc -b` |
| `npm test` | Vitest once (`npm run test:watch` while coding) |
| `npm run format` / `format:check` | Prettier |

## Folders (tech discussion T10, FE2)
```
src/
  app/          start-up: providers, theme tokens (theme.ts, the only place for colours), routes
  features/     one folder per feature area, loaded lazily (home, admin/users, …)
  shared/       code used by several features (i18n, api, auth, components)
  locales/en/   translation files: ALL screen text lives here (constitution 12.1)
  test/         test set-up: MSW server, render helpers
tooling/        tests for the tooling itself (e.g. the lint rule against hard-coded text)
```

## Rules
- **No text in components.** Every visible string, `aria-label`, `title`, `placeholder`, … comes from `t('…')`;
  the keys are typed, so a missing key fails the type check. The lint rule `no-restricted-syntax` enforces it
  in `src/**/*.tsx` (tests are exempt).
- **Keyboard and labels** (constitution 12.2): jsx-a11y runs in strict mode; every screen must work without a mouse.
- **No `any`**; API types come from the generated client.
- **Exact versions** (`.npmrc` `save-exact=true`); only libraries approved by the owner (constitution 1.6).
