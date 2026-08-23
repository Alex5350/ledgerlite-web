# Contributing

Thanks for your interest in LedgerLite. This project doubles as a demonstration of professional
engineering practices, so contributions are expected to follow the same standards.

## Getting set up

```bash
git clone https://github.com/Alex5350/ledgerlite-web.git
cd ledgerlite-web
dotnet build          # must succeed with zero warnings
dotnet test           # must pass (262 tests, no setup required)
```

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0); the repository
pins the SDK version with `global.json`.

## Ground rules

1. **Zero warnings, zero warnings-as-errors games.** `TreatWarningsAsErrors` is on in test
   projects; keep the whole solution building clean.
2. **Business rules belong in the domain.** If a rule can be expressed on an aggregate or value
   object, it goes there - not in a handler, endpoint, or database constraint alone. If you find
   yourself writing an `if` about money in the Application layer, stop.
3. **Expected failures are values.** Use `ErrorOr<T>` and add a typed error to the relevant
   `DomainErrors`/`ValidationErrors` catalog. Exceptions are for the unexpected only.
4. **Tests are part of the change.** New domain invariant → `Domain.UnitTests` coverage of every
   branch. New/changed handler → happy path *and* every error path in `Application.UnitTests`.
   New/changed endpoint → an integration test proving the HTTP contract (status codes,
   ProblemDetails shape, auth requirements).
5. **Follow the existing patterns.** Feature folders under `Features/`, `TryCreate` factories on
   aggregates, FluentValidation validators per command, `TypedResults` in endpoints, DI via each
   layer's `DependencyInjection.cs`.

## Workflow

- Branch from `main` using a descriptive name (`feat/budget-rollover`, `fix/trial-balance-totals`).
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/)
  (`feat:`, `fix:`, `test:`, `docs:`, `refactor:`, `chore:`) with a body explaining *why* when
  the change isn't obvious.
- Keep commits focused; a reviewer should be able to describe each one from its diff alone.
- CI (`.github/workflows/ci.yml`) must be green before merge.

## Committing database changes

If you change an entity or configuration, generate a real EF migration rather than editing the
snapshot by hand:

```bash
dotnet ef migrations add <Name> \
  --project src/LedgerLite.Infrastructure \
  --startup-project src/LedgerLite.Api
```

## Reporting issues

Open an issue with: what you expected, what happened, the smallest reproduction (curl script or
failing test), and the log output if the API was running.
