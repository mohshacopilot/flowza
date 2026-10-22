# Checklist: set up a fresh development PC (PLT-06-AC1)

**Acceptance criterion PLT-06-AC1** *(manual)*: a fresh machine runs the product in under 30 minutes by
following the guide ([README.md](../../../README.md), "Set up a development PC").

How to use: someone who did not build Flowza follows the README on a PC without Flowza, ticks each step, notes the
time, and signs at the end. Anything unclear goes to "Findings" and is fixed in the README before signing.

| | |
|---|---|
| **Tester** | |
| **Date** | |
| **PC** (Windows version, SQL Server edition and version) | |
| **Start time** (after the tools in step 1 are installed) | |

## Steps
| # | Step (README) | Done | Minutes | Notes |
|---|---|---|---|---|
| 1 | Tools installed: .NET 10 SDK, Node.js 24, SQL Server 2019+ with SSMS, git, smtp4dev | ☐ | | Installing tools is not counted in the 30 minutes |
| 2 | Code cloned | ☐ | | |
| 3 | SQL logins allowed, login `flowza_app` created, `create-databases.sql` run: ten databases exist | ☐ | | |
| 4 | SQL password stored in the `flowza-dev` user secrets (not in any file in the repository) | ☐ | | |
| 5 | AppHost started (F5 or `dotnet run`): the Aspire dashboard shows every part running and healthy | ☐ | | |
| 6 | `init-admin` (or `demo-data`) run; the one-time password was shown | ☐ | | |
| 7 | http://localhost:5100 opens the login page; login works; with `init-admin` the password change is asked | ☐ | | |
| 8 | Users screen opens for the admin; a test user can be created | ☐ | | |
| 9 | An email sent by the platform shows in smtp4dev at http://localhost:5000 *(from feature 006; skip until then)* | ☐ | | |
| 10 | `dotnet test --solution Flowza.slnx` passes; `npm test` in `src/Web` passes | ☐ | | Not counted in the 30 minutes |

**End time** (after step 8): ______ **Total minutes** (steps 2–8): ______ **Under 30 minutes:** ☐ yes ☐ no

## Back-up list confirmed (PLT-06)
The README's "Back up" table names: ☐ the four service databases ☐ `flowza_messaging` ☐ the Gateway's key folder
☐ the certificates ☐ settings and secrets.

## Findings
| # | What was unclear or went wrong | Fixed in the README (commit) |
|---|---|---|
| | | |

## Sign-off
PLT-06-AC1 met: ☐ yes **Owner:** __________________ **Date:** __________
