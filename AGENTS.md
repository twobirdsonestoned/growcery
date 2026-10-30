# AGENTS.md — Rules for working in Growcery

This is the canonical rules file for every AI agent and developer working in this repo. Feature specs in
`docs/specs/` define **what** to build. This file defines **how**. `README.md` explains what the app is and how it's
structured.

## 1. Project overview
- Growcery is an ASP.NET Core **.NET 10** JSON API for capturing grocery lists, layered as **Api → Core ← Data**
  (spec 002):

  | Project | Layer | Contains |
  |---|---|---|
  | `src/Growcery.Api` | Presentation | Controllers, HTTP contracts, auth (JWT bearer validation of Auth0 tokens, spec 003), DI composition root (`Program.cs`) |
  | `src/Growcery.Core` | Business | Domain entities and rules, services, and the interfaces other layers implement |
  | `src/Growcery.Data` | Data | EF Core + Npgsql (PostgreSQL), repositories, migrations |
  | `tests/Growcery.Core.Tests` | — | Unit tests for Core, using in-memory fakes |
  | `tests/Growcery.Api.Tests` | — | Integration tests through HTTP (`WebApplicationFactory` + EF InMemory) |
- There is no frontend yet.
- **Identity is external (Auth0, spec 003).** The API is a resource server only. It validates access tokens and never
  stores passwords, issues tokens or sets auth cookies. Don't add account, login or token endpoints.
- The hosting target is **Azure Container Apps**, running the existing `Dockerfile` image. HTTPS ends at the ingress,
  so the app receives plain HTTP. Don't propose Azure Functions (rejected: it would mean rewriting the controllers and
  the auth).

## 2. Commands
| Task | Command |
|---|---|
| Restore / build / test (same as CI) | `dotnet restore` · `dotnet build` · `dotnet test` |
| Add a migration | `dotnet tool restore`, then `dotnet ef migrations add <Name> --project src/Growcery.Data --startup-project src/Growcery.Api` |
| Run locally | `docker compose up` → API on `http://localhost:3030`, Swagger at `/swagger` (Development) |

CI (`.github/workflows/tests.yml`) runs build + test on every PR.

## 3. How to work on a feature
- Features are specified in `docs/specs/NNN-<slug>.md`. **Read the whole spec before writing code.** The spec is the
  source of truth. A later spec can supersede parts of an earlier one, and the earlier spec says so at the top.
- Follow the spec's implementation order. Don't add anything the spec lists as out of scope.
- **Open questions:** never resolve a spec's open question yourself, and never assume a default. Stop and ask the user.
  Then record the answer in the spec's **Decisions** list, in the same commit as the code it affects.
- If the spec conflicts with the code, or is ambiguous, **stop and ask**. Don't guess.
- Keep changes focused. Don't refactor unrelated code while doing feature work.
- Keep `README.md` up to date when a change affects the architecture, the data model (ERD) or how to run the app.

## 4. Architecture rules (layers)
- **Dependencies point into Core.** Api references Core and Data. Data references Core. Core references **no** other
  project.
- **Core is technology-free.** It may only reference `Microsoft.Extensions.*.Abstractions` packages. **Never** add
  ASP.NET Core, EF Core, Npgsql or Identity to Core, and never add `using Microsoft.AspNetCore…` or
  `using Microsoft.EntityFrameworkCore…` there.
- **Core owns the interfaces** (`Abstractions/`: repositories, `IUnitOfWork`, `IClock`, `ICurrentUser`). Data and Api
  implement them, and DI wires them up in `Program.cs` through `AddGrowceryCore()` and `AddGrowceryData()`.
- **Business logic and validation live in Core**, in domain entities and services. Controllers only translate HTTP to
  service calls and results back to HTTP. They don't hold business rules or DataAnnotations validation.
- **Controllers inject Core service interfaces only.** Never inject `ApplicationDbContext` or repositories into
  controllers.
- **Services return `Result`/`Result<T>`** (`Ok`, `NotFound`, `Invalid`), never HTTP types. Api maps them with
  `ToActionResult()`.
- **Repositories return domain entities**, never EF types or `IQueryable`. Read-only queries use `AsNoTracking()`.
- **Database-specific behaviour stays in Data.** That includes provider exceptions (e.g. Postgres unique violations),
  column types, and timestamp precision (`PostgresClock`).
- Delete behaviour in the database must match the app's behaviour (no cascades the app doesn't intend).

## 5. Code conventions
- **Namespaces:** file-scoped, matching the project and folder (e.g. `Growcery.Core.Domain`, `Growcery.Data.Repositories`).
- **Nullable** is enabled. The build must not add new warnings.
- **DI:** constructor injection into `private readonly _field`s.
- **Response contracts** are `sealed record`s in `Growcery.Api/Contracts`. **Never return domain or EF entities from
  endpoints.**
- **Errors:** every non-2xx response from the domain endpoints is **ProblemDetails** (`application/problem+json`).
  `Invalid` results become `ValidationProblemDetails`. Authentication failures (`401`) are ProblemDetails too.
- **OpenAPI:** every action declares its responses with `[ProducesResponseType]`, success and error codes. Don't add
  `[Produces("application/json")]`, because it turns ProblemDetails into plain `application/json`.
- **Timestamps:** use `IClock.UtcNow`. Never call `DateTimeOffset.UtcNow` for persisted values.
- **Culture:** parse and format with `CultureInfo.InvariantCulture`. Dev machines use `en-ZA`, where the decimal
  separator is `,`.
- **Logging:** inject `ILogger<T>`, log writes with structured IDs, and never log user free text (names).
- Use `async`/`await` for all I/O.

## 6. Security rules
- Every endpoint uses `[Authorize]`, and authenticates with `Authorization: Bearer <Auth0 access token>`.
- Token validation always checks the signature (RS256 only), issuer, audience and lifetime. Never disable any of these.
- Identify the user only through `ICurrentUser` (the token's `sub`), and resolve them to a domain `Person`. Access to a
  list is through `ListMember` membership.
- **Never** accept owner, person or user IDs from the request body or query string.
- A resource the caller can't access returns **`404`**, not `403`, so the API doesn't reveal which IDs exist.
- Unauthenticated API calls must return **`401`**, never a `302` redirect.
- Never log tokens or the `Authorization` header.
- Never commit secrets. Connection strings come from environment variables (see `.env.example`,
  `docker-compose.yml`). The `Auth0:*` settings aren't secrets, but never commit an Auth0 client secret or
  Management API token.

## 7. Database and migrations
- Any change to an entity configuration or `ApplicationDbContext` needs a **new migration** in
  `src/Growcery.Data/Migrations`, committed together with the model change.
- Map domain entities with `IEntityTypeConfiguration<T>` in `src/Growcery.Data/Configurations`. Domain classes stay
  free of persistence attributes.
- **Never edit or delete a migration that's already on `main`.** Add a new one instead. A mechanical namespace or file
  move is allowed only if the generated SQL is shown to be identical.
- Before committing, review each generated migration's foreign keys, cascade rules, indexes and column types.
- Development applies migrations automatically on startup (`Program.cs`). Don't add other startup DB logic.

## 8. Testing rules (test-first)
- **Write the failing test first**, then implement until it passes.
- **Business rules:** unit-test them in `tests/Growcery.Core.Tests` with the in-memory fakes (no database, no HTTP).
- **Endpoints:** integration-test them in `tests/Growcery.Api.Tests` with `GrowceryWebApplicationFactory` via
  `IClassFixture<>`.
  - Use a unique user per test (`CreateAuthenticatedClientAsync()` sends a Bearer token for a new `sub`), because the
    InMemory database is shared by all tests in a class.
  - Tests sign their own JWTs (`TestTokens`) and **never call Auth0** or any other network service.
- Name tests `Method_Condition_ExpectedResult` (e.g. `Create_WithEmptyName_ReturnsBadRequest`).
- Check status codes **and** response bodies. For security rules, add a test that a non-member gets `404`.
- `dotnet test` must pass before **every** commit.
- **Never delete, skip or weaken an existing test** to make a change pass. A test may be removed only when a spec
  decision removes the behaviour it covers, and the commit message must say which decision.

## 9. Git and delivery
- Work on a feature branch `feature/<slug>` created from `main`. **Never commit directly to `main`.**
- Make **one small commit per spec implementation step**, with an imperative subject line
  (e.g. `Add GroceryList entities and DbContext config`).
- Open **one PR per spec** to `main`. The PR description links the spec and includes its Definition-of-Done
  checklist. CI must be green before merge.
- **Ask before** pushing, opening a PR, force-pushing, or rewriting history.

## 10. Definition of done (every feature)
- [ ] Everything in the spec's own Definition of Done is met.
- [ ] The layer rules in §4 hold (Core has no technology references, and controllers use services only).
- [ ] `dotnet build` has no new warnings, and `dotnet test` passes locally and in CI.
- [ ] New endpoints are visible and callable in Swagger UI.
- [ ] Migrations are generated, reviewed, committed, and apply to a fresh database.
- [ ] The spec and `README.md` are updated with every decision and structural change.
