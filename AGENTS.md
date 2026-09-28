# AGENTS.md — Rules for working in Growcery

This is the canonical rules file for every AI agent and developer working in this repo. Feature specs in
`docs/specs/` define **what** to build. This file defines **how**.

## 1. Project overview
- Growcery is an ASP.NET Core **.NET 10** JSON API (`growcery.csproj`).
- It uses PostgreSQL through EF Core + Npgsql (`Data/ApplicationDbContext.cs`).
- Auth is ASP.NET Core Identity with cookies (`/api/account/register`, `/api/account/login`).
- Tests use xUnit + `WebApplicationFactory` in `growcery.Tests/`.
- There is no frontend yet.

## 2. Commands
| Task | Command |
|---|---|
| Restore / build / test (same as CI) | `dotnet restore` · `dotnet build` · `dotnet test` |
| EF tooling (local `dotnet-ef`) | `dotnet tool restore`, then `dotnet ef migrations add <Name>` |
| Run locally | `docker compose up` → API on `http://localhost:3030`, Swagger at `/swagger` (Development) |

CI (`.github/workflows/tests.yml`) runs build + test on every PR.

## 3. How to work on a feature
- Features are specified in `docs/specs/NNN-<slug>.md`. **Read the whole spec before writing code.** The spec is the
  source of truth.
- Follow the spec's implementation order. Don't add anything the spec lists as out of scope.
- **Open questions:** never resolve a spec's open question yourself, and never assume a default. Stop and ask the user.
  Then record the answer in the spec's **Decisions** list, in the same commit as the code it affects.
- If the spec conflicts with the code, or is ambiguous, **stop and ask**. Don't guess.
- Keep changes focused. Don't refactor unrelated code while doing feature work.

## 4. Code conventions
Match the existing code:
- **Controllers:** `[ApiController]` + `ControllerBase`, attribute routes under `api/...`, and constructor injection into
  `private readonly _field`s. Reference: `Controllers/AccountController.cs`.
- **Namespaces:** file-scoped `growcery.Controllers`, `growcery.Models`, `growcery.Data`.
- **Nullable** is enabled. The build must not add new warnings.
- **Request models** are classes with DataAnnotations. Reference: `Models/AccountViewModels.cs`.
- **Response models** are `sealed record`s. Reference: `Models/ApiResponseModels.cs`. Reuse `ErrorResponse` and
  `ValidationErrorResponse` where they fit.
- **Never return EF entities from endpoints.** Map them to response records.
- **Validation:** rely on `[ApiController]`'s automatic `400 ValidationProblemDetails`. For checks beyond
  DataAnnotations, use `ModelState.AddModelError(...)` + `return ValidationProblem();`.
- **Data access** goes straight through `ApplicationDbContext` in controllers. Don't add repository or service layers
  unless a spec asks for them.
- **Timestamps:** use `DateTimeOffset.UtcNow` (Npgsql `timestamptz` rejects non-UTC offsets).
- Use `async`/`await` for all EF calls (`ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`).

## 5. Security rules
- Every endpoint outside `api/account` uses `[Authorize]`.
- Scope every data query to the current user (`_userManager.GetUserId(User)`).
- **Never** accept owner or user IDs from the request body or query string.
- A resource owned by another user returns **`404`**, not `403`, so the API doesn't reveal which IDs exist.
- Unauthenticated API calls must return **`401`**, never a `302` redirect.
- Never commit secrets. Connection strings come from environment variables (see `.env.example`,
  `docker-compose.yml`).

## 6. Database and migrations
- Any change to an entity or `ApplicationDbContext` needs a **new migration** in `Migrations/`, committed together
  with the model change.
- In `OnModelCreating`, call `base.OnModelCreating(builder)` **first** (Identity needs it).
- **Never edit or delete a migration that's already on `main`.** Add a new one instead.
- Before committing, review each generated migration's foreign keys, cascade rules, indexes and column types.
- Development applies migrations automatically on startup (`Program.cs`). Don't add other startup DB logic.

## 7. Testing rules (test-first)
- **For each endpoint, write the failing integration test first**, then implement until it passes.
- Use `GrowceryWebApplicationFactory` (EF InMemory) via `IClassFixture<>`. Reference:
  `growcery.Tests/AccountControllerLoginTests.cs`.
- **Use a unique username per test.** The InMemory database is shared by all tests in a class.
- Authenticate through the real `/api/account/register` or `/api/account/login` endpoints. The factory's default
  `HttpClient` keeps the auth cookie for later requests.
- Name tests `Method_Condition_ExpectedResult` (e.g. `Create_WithEmptyName_ReturnsBadRequest`).
- Check status codes **and** response bodies. For security rules, add a test that another user gets `404`.
- `dotnet test` must pass before **every** commit.
- **Never delete, skip or weaken an existing test** to make a change pass.

## 8. Git and delivery
- Work on a feature branch `feature/<slug>` created from `main`. **Never commit directly to `main`.**
- Make **one small commit per spec implementation step**, with an imperative subject line
  (e.g. `Add GroceryList entities and DbContext config`).
- Open **one PR per spec** to `main`. The PR description links the spec and includes its Definition-of-Done
  checklist. CI must be green before merge.
- **Ask before** pushing, opening a PR, force-pushing, or rewriting history.

## 9. Definition of done (every feature)
- [ ] Everything in the spec's own Definition of Done is met.
- [ ] `dotnet build` has no new warnings, and `dotnet test` passes locally and in CI.
- [ ] New endpoints are visible and callable in Swagger UI.
- [ ] Migrations are generated, reviewed, committed, and apply to a fresh database.
- [ ] The spec is updated with every decision made during the work.
