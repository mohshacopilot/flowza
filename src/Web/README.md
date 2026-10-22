# Flowza web app

One React app for every screen (tech discussion T1): React 19 + TypeScript (strict) + Vite, Ant Design,
React Router, TanStack Query, React Hook Form + Zod (forms), react-i18next. Tests: Vitest + React Testing Library + MSW.

## Run it

- **With everything else (normal):** F5 on the AppHost in Visual Studio (or `dotnet run --project src/AppHost/Flowza.AppHost`).
  The AppHost runs `npm install` and then the Vite dev server as a program (no container) on
  **http://localhost:5105**; it shows up as `web` in the Aspire dashboard. Browse through the Gateway
  (http://localhost:5100) once its routes exist (T021), so the login cookie and the API share one address.
- **On its own (quick UI work):** `npm install` once, then `npm run dev` in this folder. Calls to `/api/…` and
  `/bff/…` are forwarded to the Gateway (`FLOWZA_GATEWAY_URL`, default http://localhost:5100).

Node.js 24 LTS and npm are required (`engines` in package.json).

## Scripts

| Script                            | What it does                                                                                             |
| --------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `npm run dev`                     | Vite dev server (port from `PORT`, default 5105)                                                         |
| `npm run build`                   | Type-check, then production build into `dist/` (served by the Gateway in production)                     |
| `npm run lint`                    | ESLint (TypeScript strict, React hooks, jsx-a11y strict, no hard-coded screen text), no warnings allowed |
| `npm run typecheck`               | `tsc -b`                                                                                                 |
| `npm test`                        | Vitest once (`npm run test:watch` while coding)                                                          |
| `npm run format` / `format:check` | Prettier                                                                                                 |
| `npm run api:fetch`               | Saves each running service's OpenAPI document to `openapi/<service>.json`                                |
| `npm run api:generate`            | Generates the TanStack Query hooks from `openapi/*.json` into `src/shared/api/generated/<service>/`      |
| `npm run api:update`              | Both of the above                                                                                        |
| `npm run api:check`               | Generates a client from a sample document and type-checks it (proves the generator set-up)               |

## Calling the API (tech discussion T10, FE5)

- **Generated hooks only.** orval turns each service's OpenAPI document into typed hooks (`useListUsers`, …).
  Paths are rewritten to the Gateway's routes (`/api/v1/users` of Identity → `/api/identity/v1/users`).
  The documents in `openapi/` and the generated code are committed, so CI and other PCs need no running services.
  Regenerate after an API change: start the AppHost, then `npm run api:update`. Never edit generated files.
- **One fetch function** (`src/shared/api/apiFetch.ts`) does every call: same-origin cookie, the anti-forgery
  header `X-CSRF: 1` on POST/PUT/PATCH/DELETE, and every failure as an `ApiError` (status, `code`, `reference`,
  field errors from ProblemDetails; status 0 when the network failed).
- **Messages:** `describeApiError(error, i18n)` gives the text to show: the translation of the server's error
  code (`errors.codes.<code>` in `locales/en/common.json`; add one per code a service defines), else the
  server's English text for business errors, else a message for the status; plus the reference code and
  field errors keyed by the form's field names.
- **Session end:** a 401 from any call sends the browser to the login page (`app/queryClient.ts`).
- A service publishes its document at `/openapi/v1.json` in Development (anonymous access there is needed
  for `api:fetch`).

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
