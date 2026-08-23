# Development process

This document records how the project was built, in the traditional order a Blazor front-end
would be constructed on top of an existing API: infrastructure first, then services, then UI
foundations, then pages, then tests, then documentation. Each entry matches a commit in the
repository's history (`git log --oneline`), so the history itself tells the same story.

## Phase 1 - Repository and backend

| # | Commit | What / why |
|---|--------|------------|
| 1 | `chore: import LedgerLite API backend` | The REST API (github.com/Alex5350/ledgerlite) is imported unchanged so this repository is self-contained: clone once, run the API, run the UI. Its own history and docs stay upstream. |
| 2 | `feat(web): scaffold .NET 10 Blazor Web App with Interactive Auto render mode` | `dotnet new blazor --interactivity Auto --all-interactive --empty`: a server project (SSR + Interactive Server + WASM host) and a Razor class library client that downloads as WebAssembly. Solution wires both to the backend projects. |

## Phase 2 - Design system and plumbing

| # | Commit | What / why |
|---|--------|------------|
| 3 | `build(web): integrate Tailwind CSS v4 with a custom design-token theme` | npm toolchain with `build:css` / `watch:css` scripts; the compiled stylesheet is committed so contributors don't need Node. Design tokens define the ink surface scale, emerald accent, Sora / IBM Plex type stack, card utility and entrance animations. |
| 4 | `feat(web): add typed API client and JWT authentication shared by both render modes` | `ILedgerLiteApiClient` covering every endpoint with camelCase DTOs; `ApiException` parses RFC 9457 ProblemDetails into display-ready errors; JWT claims parsed without a JWT package; localStorage token store safe during prerender; one `AddLedgerLiteClientServices()` call wires both the server and WASM hosts. |

## Phase 3 - UI foundations

| # | Commit | What / why |
|---|--------|------------|
| 5 | `feat(web): add UI component library and app-wide period state` | The hand-built kit: Button/Card/StatCard/Badge, validated Field + SelectField, generic `DataTable<TItem>`, Modal, Skeleton/EmptyState/ErrorPanel, threshold-aware BudgetBar, toast service. `PeriodState` tracks the selected fiscal period across pages. |
| 6 | `feat(web): build app shell with sidebar navigation and period selector` | Fixed sidebar with active-link glow, JS-free mobile hamburger, topbar with the period selector and sign-out; router-level `AuthorizeView` gates protected routes. |

## Phase 4 - Pages, in dependency order

| # | Commit | What / why |
|---|--------|------------|
| 7 | `feat(web): add split-screen login and registration page` | Must exist before any protected page can be reached: login/register toggle, demo-credential chip, friendly error rendering. |
| 8 | `feat(web): add overview dashboard` | The landing page after login: stat cards, trial-balance excerpt, budget bars, recent entries. |
| 9 | `feat(web): add fiscal period and account management pages` | Period lifecycle (create/close with immutability warning) and the chart of accounts - prerequisites for posting entries and setting budgets. |
| 10 | `feat(web): add journal page with live-balance entry editor` | The core feature: paged entries plus the modal editor whose running totals gate the submit button on debits-equal-credits. |
| 11 | `feat(web): add budget tracking, trial balance report and branded 404` | Remaining read/report surfaces. |

## Phase 5 - Testing

| # | Commit | What / why |
|---|--------|------------|
| 12 | `fix(web): retry period loads after failed attempts` | Writing `PeriodState` unit tests exposed a cached faulted task that permanently disabled retry on synchronous failures. Fixed before the tests landed. |
| 13 | `test(web): add 119 bUnit component and service tests` | Component tests for the whole UI kit and key page behaviors; service tests for JWT parsing, error precedence, period selection and toasts. Backend suites (262 tests) keep running in the same `dotnet test`. |

## Phase 6 - Live verification

| # | Commit | What / why |
|---|--------|------------|
| 14 | `fix(web): scope the HTTP handler chain to each Blazor circuit` | Found by driving the real app in a browser: after the first static SSR request disposed its scope, every interactive circuit crashed with `ObjectDisposedException` because a scoped `DelegatingHandler` disposed the *shared* primary handler. |
| 15 | `fix(api): allow the Blazor UI origin for WebAssembly API calls` | Interactive Auto serves WebAssembly from the second visit on, and the browser then calls the API directly, which CORS blocked until the UI origin was whitelisted. |
| 16 | `docs: add screenshots of the running application` | Captured from the live, logged-in app while verifying both render modes end-to-end (login → post a balanced entry → budgets → trial balance). |

## Phase 7 - Documentation

| # | Commit | What / why |
|---|--------|------------|
| 17 | `docs: write Blazor-focused README` | Quick start (two `dotnet run` commands), feature tour, architecture map, testing guide. Backend setup kept minimal by design; the focus is the Blazor application. |
| 18 | `ci: add GitHub Actions build-and-test workflow` | Release build with warnings-as-errors plus the full 381-test suite on every push and pull request. |
| 19 | `docs: document the development process` | This file. |

## Conventions used throughout

- **Conventional Commits** (`feat`, `fix`, `test`, `docs`, `build`, `chore`) with bodies that
  explain *why*, especially for fixes discovered by testing.
- Every commit builds: components, pages, tests and fixes land in dependency order, so checking
  out any intermediate commit still compiles.
- Bugs found by tests or live verification are committed as separate `fix` commits with the
  discovery story in the message: the history shows the debugging process, not just the result.
