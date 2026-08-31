# LedgerLite Web: the engineering view

The companion to the [README's product story](README.md): architecture, the request path, and
every major engineering decision traced back to the bookkeeping problem it exists to solve.
This repository is the Blazor web client for the
[LedgerLite API](https://github.com/Alex5350/ledgerlite), imported here unchanged so one clone
runs the full stack; the backend's own deep dive lives upstream.

## Architecture

![Request flow - the browser loads the Blazor host (static SSR, then Interactive Auto); one component library runs on both the SignalR server circuit and WebAssembly; login exchanges credentials for a JWT sent as a bearer header to the LedgerLite API, which runs CQRS use cases over the double-entry domain and persists through EF Core to SQLite](docs/diagrams/request-flow.svg)

```
src/
├── LedgerLite.Web/            # Blazor server host: SSR + Interactive Server + WASM boot
├── LedgerLite.Web.Client/     # Components, pages, UI kit, API client, auth  (runs in BOTH
│   ├── Pages/                 #   render modes - this is the Interactive Auto payload)
│   ├── Ui/                    #   hand-built component library + PeriodState + toasts
│   ├── Services/Api/          #   typed ILedgerLiteApiClient, DTOs, ApiException
│   ├── Services/Auth/         #   JWT state provider, token store, bearer handler
│   └── wwwroot/               #   appsettings + compiled Tailwind stylesheet
├── LedgerLite.Api/            # ── the backend (imported unchanged) ──
├── LedgerLite.Application/    #   DDD/Clean Architecture, CQRS + ErrorOr
├── LedgerLite.Domain/         #   double-entry ledger domain model
└── LedgerLite.Infrastructure/ #   EF Core + SQLite, JWT, channels
tests/
├── LedgerLite.Web.Tests/              # 119 tests: bUnit components + service units
├── LedgerLite.Domain.UnitTests/       # ── backend suites (262 tests) ──
├── LedgerLite.Application.UnitTests/
└── LedgerLite.Api.IntegrationTests/
```

**Layering rule:** the client renders and validates for feedback; the API decides. Every
mutation goes through the typed `ILedgerLiteApiClient`, whose handler chain attaches the JWT
bearer token. Domain errors come back as RFC 9457 ProblemDetails, are parsed into
`ApiException` once at this boundary, and are rendered inline or as toasts. The rules
themselves live once, in the LedgerLite API's domain model; this client never re-decides them.

## How the tech solves the business problem

| Business problem | Engineering decision | Why this tech | What it buys | Where documented |
|---|---|---|---|---|
| Unbalanced entries must be impossible, yet the check must be visible while typing | Live-balance editor in the Journal page: running debit/credit totals gate the Post button client-side; the API re-validates through its domain before persisting, and rejections return as RFC 9457 ProblemDetails parsed into `ApiException` | Rules live once behind the API boundary; the client renders outcomes rather than re-deciding | Instant feedback where the user types, and a UI that cannot bypass the rulebook | [process.md](docs/process.md) Phase 4, item 10 |
| First paint must be fast, but the editor must react to every keystroke | Blazor Web App with Interactive Auto: pages render as static SSR, go interactive over SignalR (Server), and switch to WebAssembly on subsequent visits | The user gets a fast first load and rich interactivity after, without a full app download up front; one component library runs in both hosts | The site feels instant on first visit and app-like afterwards, from a single codebase | [process.md](docs/process.md) Phase 1, item 2 |
| One user's session must not break another's | The API client's handler chain is scoped per Blazor circuit: a scoped `BearerTokenHandler` over a scoped primary handler, so each circuit owns and tears down exactly its own chain | A `DelegatingHandler` disposes its inner handler; sharing a primary handler across scopes let the first scope's teardown kill every other circuit's connection pool | Sessions cannot leak into each other; token attach works on both the Server and WebAssembly hosts | [process.md](docs/process.md) Phase 6, item 14; `src/LedgerLite.Web.Client/LedgerLiteClientServices.cs` |
| On later visits the browser calls the API directly as WebAssembly | The API whitelists the UI origin for CORS | Interactive Auto serves WebAssembly from the second visit on; the browser then calls the API directly, which the same-origin assumption blocked | The faster second-hosting path works, not just the first | [process.md](docs/process.md) Phase 6, item 15 |
| The interface must encode behavior, not just colors | Hand-built UI kit on Tailwind CSS v4 design tokens: threshold-aware `BudgetBar`, validated `Field`/`SelectField`, generic `DataTable<TItem>`, `StatCard`, `Modal`, toasts, skeleton loaders, empty states; no stock component kit | Budget bars own the amber-at-80 / red-at-100 semantics; fields own validation display; nothing depends on a third-party kit's internals | The design language means the same thing on every page, and can change in one place | [process.md](docs/process.md) Phase 2 item 3, Phase 3 item 5 |
| Sign-in state must survive prerender and work in both hosting models | JWT with a custom `AuthenticationStateProvider` that parses token claims without a JWT library, a localStorage-backed token store safe during prerender, a `DelegatingHandler` that attaches bearer tokens, and router-level authorization with redirect-to-login | One auth story registered by a single `AddLedgerLiteClientServices()` call wires both the Server and WebAssembly hosts | Login works identically before and after the hosting switch, and protected routes are gated at the router | [process.md](docs/process.md) Phase 2, item 4 |
| Contributors should need only the .NET SDK to run everything | The compiled Tailwind stylesheet is committed; Node is needed only to change styles (`npm run build:css`) | The stylesheet is a build artifact; committing it removes a toolchain from the happy path | Two `dotnet run` commands stand up API plus UI; no npm install to try the app | [process.md](docs/process.md) Phase 2, item 3 |

The row that shaped the product most is the first: the live-balance editor is the feature, not
decoration. The editor's running totals exist so the user sees the constraint while typing,
and the gated Post button is the same rule wearing a UI hat. But the editor is deliberately
not the enforcement: the identical debits-equal-credits rule, the open-period check and the
account rules all live in the API's domain, so a stale tab, a hand-crafted HTTP request or a
future second client cannot post what the rulebook refuses.

The row with the best war story is per-circuit scoping. Validate-then-ship looked correct: the
scoped handler chain passed every test. Driving the real app in a browser exposed it. The
first static SSR request disposed its DI scope, which disposed the scoped `DelegatingHandler`,
which disposed the shared primary handler, and every interactive circuit then crashed with
`ObjectDisposedException`. The fix gives each circuit its complete chain, so one session's
teardown touches exactly its own connection pool ([process.md](docs/process.md) Phase 6).

## Decisions

No ADR files exist in this repository; the decision record is
[docs/process.md](docs/process.md), whose entries match the commit history one for one. The
decisions that shaped this client, each cited to the section that records it:

- **Import the backend unchanged** rather than re-hosting its code: the repository is
  self-contained, clone once and run, while history and docs stay upstream
  ([process.md](docs/process.md), Phase 1).
- **Scaffold Interactive Auto from day one** (`dotnet new blazor --interactivity Auto`): both
  hosting models exercise the same component library, which is why per-circuit lifetimes and
  CORS were discovered rather than assumed ([process.md](docs/process.md), Phase 1).
- **One typed API client and one JWT story registered for both hosts**: a single
  `AddLedgerLiteClientServices()` wires the Server host and the WebAssembly host identically
  ([process.md](docs/process.md), Phase 2).
- **Commit the compiled stylesheet**: the .NET SDK is the only prerequisite; the npm toolchain
  exists solely for style changes ([process.md](docs/process.md), Phase 2).
- **Hand-built component kit over a third-party one**: behavior worth owning (threshold tones,
  validation display, generic tables) lives in components this repository controls
  ([process.md](docs/process.md), Phase 3).
- **Build pages in dependency order** (login, then periods and accounts, then journal and
  budgets) so every commit compiles and each page lands with what it needs
  ([process.md](docs/process.md), Phase 4).
- **Tests are part of the change**: the `PeriodState` unit tests caught a cached faulted task
  that permanently disabled retry, fixed before the tests landed ([process.md](docs/process.md),
  Phase 5).
- **Live verification is a phase, not an afterthought**: the two Phase 6 fixes (per-circuit
  scoping, WebAssembly CORS) came from driving the real app in a browser, and the screenshots
  were captured during that same end-to-end pass ([process.md](docs/process.md), Phase 6).

## Request and data flow

One representative path: posting a journal entry.

1. The browser requests `/journal`; the Blazor host renders the page as static HTML (fast
   first paint, prerendered content visible immediately).
2. The page goes interactive: Interactive Auto serves it over a SignalR circuit first, and on
   later visits runs the WebAssembly build in the browser. Same components, both hosts.
3. The user types lines in the editor. The component computes running debit/credit totals and
   keeps the Post button disabled while the sides differ: validation where the user can see
   it.
4. On submit, `ILedgerLiteApiClient` sends the entry to the API. The circuit-scoped
   `BearerTokenHandler` attaches the JWT from the token store as a bearer header.
5. The API validates through its domain (debits equal credits, at least two lines, the period
   open, account numbers unique) and persists via EF Core; on rejection it returns RFC 9457
   ProblemDetails.
6. The client parses the response once into `ApiException`; failures render inline next to the
   field or as toasts, and the journal list and period state refresh.

## Stack, and why

| Area | Choice and why |
|---|---|
| **Blazor Web App (.NET 10)** | Interactive Auto (Server + WebAssembly), static SSR, router-level authorization: fast first paint and app-like interactivity from one component library ([process.md](docs/process.md) Phase 1) |
| **Tailwind CSS v4 + design tokens** | `@theme` tokens define the ink/emerald palette and Sora / IBM Plex type stack; the compiled stylesheet is committed, so running the app needs no Node.js ([process.md](docs/process.md) Phase 2) |
| **Hand-built UI kit** | `Button`, `Modal`, validated `Field`/`SelectField`, generic `DataTable<TItem>`, `StatCard`, toasts, skeleton loaders, empty states, threshold-aware `BudgetBar`: no UI kit dependency, behavior owned here ([process.md](docs/process.md) Phase 3) |
| **JWT authentication** | Custom `AuthenticationStateProvider` (claims parsed without a JWT library), prerender-safe localStorage token store, bearer `DelegatingHandler`, redirect-to-login ([process.md](docs/process.md) Phase 2) |
| **bUnit 1.40, xUnit v3, NSubstitute** | Component tests that render real components over a shared `AppTestContext` with cascading authentication state |
| **Backend (imported)** | ASP.NET Core minimal APIs, EF Core 10 + SQLite, Serilog, 262 backend tests; see the [API repository](https://github.com/Alex5350/ledgerlite) |

## Testing

```bash
dotnet test          # 381 tests total, no setup required
```

The **119 Web tests** cover both layers of the front-end:

- **bUnit component tests** - every UI kit component (button variants, loading states, budget
  threshold tones, field validation, generic table, modal visibility, toast lifecycle) plus
  page behavior: the login flow (calls `IAuthService` with the entered values, renders
  `AuthResult` errors), and the journal editor's live-balance gating (post disabled until
  debits equal credits).
- **Service unit tests** - JWT claims/expiry parsing in `JwtAuthenticationStateProvider`,
  `ApiException` error precedence, `PeriodState` selection/retry semantics (including the
  faulted-task retry bug the tests caught), and toast queuing.

The **262 backend suites** (domain, application, API integration) keep running in the same
`dotnet test`; their story is the [API repository's](https://github.com/Alex5350/ledgerlite).

CI (`.github/workflows/ci.yml`) runs a Release build with warnings-as-errors plus the full
381-test suite on every push and pull request.

## Security and operations

- **Auth:** JWT bearer tokens; claims parsed by a custom `AuthenticationStateProvider`, token
  store safe during prerender, protected routes gated at the router with redirect-to-login.
  Login itself is rate-limited (5 per minute per IP) on the API side.
- **Session isolation:** the HTTP handler chain is scoped per circuit, so one session's
  disposal cannot tear down another's connections (see the table above).
- **Operations:** the compiled Tailwind stylesheet is committed; `npm run build:css` (or
  `watch:css`) only when styles change. The root `Dockerfile` builds the backend API
  container; the UI is intended for `dotnet run` during development. The API base URL is
  `Api:BaseUrl` (default `http://localhost:5080`); ports 5080/5010 can be overridden with
  `ASPNETCORE_URLS`.

## Jargon

Terms used across this repo, from [Interactive Auto](docs/GLOSSARY.md) to
[trial balance](docs/GLOSSARY.md), are defined in the [glossary](docs/GLOSSARY.md), plain
English first. The bookkeeping core (journal entries, debits and credits, period close) has
its fuller reference in the
[API repository's glossary](https://github.com/Alex5350/ledgerlite/blob/main/docs/GLOSSARY.md).
