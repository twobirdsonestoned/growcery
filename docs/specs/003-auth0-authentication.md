# Spec 003 — Authentication with Auth0 (JWT bearer)

| Field    | Value                                   |
|----------|-----------------------------------------|
| Status   | **Draft: open questions in §10 must be answered before implementation** |
| Branch   | `feature/auth0-authentication`          |
| Scope    | Replace ASP.NET Core Identity + cookie auth with Auth0-issued JWT access tokens |
| Depends  | Spec 002 (layered architecture, `Person` + `ICurrentUser`) |

> **For AI agents and developers:** Follow the repo rules in [`/AGENTS.md`](../../AGENTS.md). This spec defines
> **what** to change. AGENTS.md defines **how**. Don't start implementing until every open question in
> [§10](#10-open-questions) has an answer recorded in [§9 Decisions](#9-decisions).

---

## 1. Goal

Growcery currently runs its own login. ASP.NET Core Identity stores users and passwords in the `AspNet*` tables, and
`/api/account/login` signs the user in with an encrypted session cookie.

After the PR #6 review, the next focus was set to stateless JWT authentication. The first idea was to issue tokens from
our own API. The review of that idea (D18) pointed out that an issuer we build ourselves also has to handle sign-up,
user management, deactivation, password reset, MFA and token refresh. That's weeks of identity work that isn't
Growcery's product.

So this spec hands **all of identity to an external provider, Auth0** (D19). The API becomes a **resource server
only** (D20):
- It accepts `Authorization: Bearer <access token>`.
- It checks that Auth0 issued the token for this API and that the token is still valid.
- It maps the token's subject to a domain `Person`.

The API holds no passwords, issues no tokens, sets no cookies and has no account endpoints.

```
┌────────┐ 1. login (hosted page, Auth0 SDK)  ┌──────────┐  sign-up, login, password reset,
│ Client │ ─────────────────────────────────► │  Auth0   │  MFA, social login, refresh,
│        │ ◄──────── 2. access token (JWT) ── │  tenant  │  blocking users (dashboard)
│        │                                    └────┬─────┘
│        │ 3. Authorization: Bearer <JWT>          │ public signing keys (JWKS),
│        │ ──────────────────────────► ┌───────────▼───┐  fetched and cached
└────────┘ ◄── 4. JSON / ProblemDetails │ Growcery.Api  │  validates signature, iss, aud, exp;
                                        └───────────────┘  sub → Person (D21)
```

OAuth2/OIDC (the Authorization Code + PKCE flow) only happens between the **client** and **Auth0**, and is handled by
Auth0's SDK and hosted login page. The API has no OAuth/OIDC code. It only uses ASP.NET Core's standard JWT bearer
validation. For now, Swagger UI acts as the client (§4.4). A future frontend spec reuses the same Auth0 application.

### How a user signs up and logs in
1. The user taps **Log in / Sign up** in the app. The app sends them to Auth0's hosted login page (Universal Login),
   which can carry Growcery's logo and colours.
2. On that page they **sign up** (email + password, or e.g. "Continue with Google"), **log in**, or use
   **Forgot password?**. MFA and email verification apply there too if they're turned on. Growcery builds none of
   this.
3. Auth0 sends them back to the app with an **access token** for the Growcery API.
4. The app calls the API with `Authorization: Bearer <token>`.
5. On the first call, the API creates the user's `Person` (spec 002 D17).

Until a frontend exists, Swagger's **Authorize** button plays the app's part (§4.4): it opens the same Auth0 page and
sends the token on every "Try it out" call.

### In scope
- JWT bearer validation against an Auth0 tenant, with `401` ProblemDetails for missing or invalid tokens.
- Removing ASP.NET Core Identity: the code, the packages, the account endpoints and the `AspNet*` tables.
- `ICurrentUser` read from token claims.
- Swagger UI login through Auth0, so the API can be tried and debugged locally.
- Test infrastructure that signs its own tokens. Tests never call Auth0.
- README: the auth flow, the Auth0 tenant setup steps, and the ERD without `AspNetUsers`.

### Out of scope
- **Configured in the Auth0 dashboard, not in code:** sign-up, the login UI, password reset, MFA, social logins,
  refresh tokens, and blocking or deleting users.
- Roles and permissions (scopes, RBAC). Any authenticated user may use the API, as today.
- Auth0 tenant configuration as code (Deploy CLI or Terraform).
- Deleting a user's Growcery data when their Auth0 account is deleted.
- A frontend, CORS, and the production tenant (decided with hosting).

---

## 2. Architecture changes

| Project | Change |
|---|---|
| `Growcery.Core` | **None.** `ICurrentUser` and lazy `Person` creation (spec 002 D17) are already provider-agnostic |
| `Growcery.Data` | `ApplicationDbContext : DbContext` (was `IdentityDbContext<ApplicationUser>`). Delete `Identity/ApplicationUser.cs` and the Identity EF package. New migration (§5) |
| `Growcery.Api` | JWT bearer validation replaces Identity and the cookie. Delete `AccountController` and the account models. `HttpCurrentUser` reads token claims. Swagger gets an OAuth2 security definition |
| `tests/Growcery.Api.Tests` | Test token signing replaces cookie login (§7) |

### 2.1 Api (`Growcery.Api`)
- **Package:** add `Microsoft.AspNetCore.Authentication.JwtBearer`.
- **`Program.cs`:**
  - Remove `AddIdentity<ApplicationUser, IdentityRole>(...)` and `ConfigureApplicationCookie(...)`.
  - Add `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`:

    ```csharp
    options.Authority = $"https://{auth0.Domain}/";   // signing keys fetched from Auth0's JWKS endpoint
    options.Audience = auth0.Audience;                 // the Auth0 API identifier
    options.MapInboundClaims = false;                  // keep "sub" as "sub" (no WS-* claim type renaming)
    options.TokenValidationParameters.NameClaimType = /* see §10 Q1 */;
    options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
    options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
    ```
  - **`401` stays ProblemDetails.** A missing, malformed, expired, wrongly signed, wrong-issuer or wrong-audience token
    returns `401` `application/problem+json`, with the `WWW-Authenticate: Bearer …` header the handler adds. The
    existing `UseStatusCodePages` already writes the body for an empty `401`. If the handler's challenge bypasses it,
    write the body in `JwtBearerEvents.OnChallenge` with `Results.Problem(...)` (the same approach as the current
    cookie events). The JSON fallback for a non-JSON `Accept` must still apply (`401`, never `500`).
  - **Fail fast:** if `Auth0:Domain` or `Auth0:Audience` is missing, startup throws a clear
    `InvalidOperationException`, as it already does for the connection string. The `Testing` environment supplies its
    own validation settings (§7).
- **`Infrastructure/HttpCurrentUser.cs`:**
  - `ExternalId` = the `sub` claim (D21).
  - `DisplayName` = the configured name claim if present, otherwise `sub` (§10 Q1).
- **Delete:** `Controllers/AccountController.cs`, `Models/AccountViewModels.cs`, and `Models/ApiResponseModels.cs`
  (`AccountResponse`, the legacy `ErrorResponse`, and `ValidationErrorResponse`).
- **Swagger:** an OAuth2 security definition (§4.4) and a global security requirement, so Swagger UI sends the token on
  every call.
- **Controllers:** no change. The grocery controllers keep `[Authorize]`.

### 2.2 Data (`Growcery.Data`)
- `ApplicationDbContext` derives from plain `DbContext`. `OnModelCreating` keeps `ApplyConfigurationsFromAssembly`.
  The `base.OnModelCreating(builder)`-first rule was only needed for Identity (AGENTS.md §7 is updated).
- Delete `Identity/ApplicationUser.cs`, and remove the `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package.
- New migration `RemoveAspNetIdentity` (§5).

### 2.3 Configuration
Neither value is a secret. Both identify **which** tenant and API to trust, and the signing keys are public (JWKS).

| Setting | Env var | Example | Where |
|---|---|---|---|
| `Auth0:Domain` | `Auth0__Domain` | `growcery-dev.eu.auth0.com` | `appsettings.Development.json` (dev tenant), `docker-compose.yml`, `.env.example` |
| `Auth0:Audience` | `Auth0__Audience` | `https://api.growcery.local` | same |
| `Auth0:SwaggerClientId` | `Auth0__SwaggerClientId` | the SPA application's Client ID | same (Development only) |

**No signing key or Data Protection keys need managing.** This supersedes spec 001 §7's Data Protection prerequisite,
because no auth cookie is encrypted any more.

---

## 3. Domain model

There are no changes to the domain tables. The only change is what `Person.ExternalId` holds:

| Before (spec 002) | After (D21) |
|---|---|
| `AspNetUsers.Id` (a GUID string) | The Auth0 `sub` claim, e.g. `auth0|6721f0c2…` or `google-oauth2|1048…` |

The `sub` already says which Auth0 connection the user signed in with (database, Google, …), and it's stable for that
user within the tenant. `ExternalId` keeps its unique index, and there's still no foreign key to any identity table.

The README ERD drops `AspNetUsers` and the dotted `ExternalId (no FK)` relation, and documents `ExternalId` as "Auth0
`sub`".

---

## 4. API contract

### 4.1 Removed endpoints
| Method | Route | Replaced by |
|---|---|---|
| `POST` | `/api/account/register` | Auth0 hosted sign-up |
| `POST` | `/api/account/login` | Auth0 hosted login, which returns the access token to the client |

### 4.2 Grocery endpoints
The 10 endpoints in spec 002 §4 are **unchanged**, with the same routes, bodies, responses and `404`-for-non-members
rule. The only differences:
- Every request must send `Authorization: Bearer <Auth0 access token for the Growcery API audience>`.
- An Auth0 **ID token** isn't accepted (wrong audience) and gets a `401`. Clients must request an access token for the
  API audience.

### 4.3 Authentication failures
| Case | Response |
|---|---|
| No `Authorization` header | `401` ProblemDetails + `WWW-Authenticate: Bearer` |
| Malformed token, bad signature, unknown signing key | `401` ProblemDetails + `WWW-Authenticate: Bearer error="invalid_token"` |
| Expired (beyond the 30-second skew) | `401`, as above |
| Wrong issuer (another tenant) or wrong audience | `401`, as above |
| Signed with an algorithm other than RS256 (including `none`) | `401`, as above |
| Valid token, first call by this `sub` | Normal response. A `Person` is created lazily (spec 002 D17) |

Error bodies never echo the token or the reason it failed beyond the standard `WWW-Authenticate` error code.

### 4.4 Swagger UI login (Development only)
- Swagger UI gets an OAuth2 security scheme of type **Authorization Code with PKCE**:
  - authorization URL `https://{Domain}/authorize`
  - token URL `https://{Domain}/oauth/token`
  - the `audience` sent as an additional query parameter
- `UseSwaggerUI` sets `OAuthClientId(SwaggerClientId)` and `OAuthUsePkce()`. No client secret is used.
- **Flow:** **Authorize** → Auth0's hosted login page → back to `/swagger/oauth2-redirect.html` → Swagger sends the
  Bearer token on every call.

Whether to do this at all is open (§10 Q4).

---

## 5. Migrations
- A new migration, **`RemoveAspNetIdentity`**, drops the 7 Identity tables: `AspNetRoleClaims`, `AspNetUserClaims`,
  `AspNetUserLogins`, `AspNetUserRoles`, `AspNetUserTokens`, `AspNetRoles` and `AspNetUsers`.
- Its `Down()` recreates them, as EF generates it.
- `InitialCreate` (on `main`) is **not** edited (AGENTS.md §7). A fresh database applies `InitialCreate`, then
  `AddGroceryDomain`, then `RemoveAspNetIdentity`.
- No domain table changes. Review that the generated migration touches **only** `AspNet*` tables.
- **Existing data:** the `Person` rows in dev databases hold old `AspNetUsers` IDs that no Auth0 user will ever
  match. How to handle this is §10 Q2.
- **Other environments:** use the idempotent script, as in spec 001 §7. The drop destroys the Identity users, so apply
  it only once nobody needs the old logins. Today only dev databases exist.

---

## 6. Auth0 tenant setup (manual; repeated in the README)

The developer does these steps once per tenant in the Auth0 dashboard. Nothing here is committed to the repo apart from
the domain, audience and client ID (§2.3).

1. **Sign up** at auth0.com and create a tenant on the **Free** plan (e.g. `growcery-dev`). Pick a region (EU or US).
2. **Applications → APIs → Create API:**
   - Name `Growcery API`.
   - Identifier = the audience (e.g. `https://api.growcery.local`). It's a name, not a URL that has to resolve, and it
     can't be changed later.
   - Signing algorithm **RS256**.
3. **Applications → Create Application → Single Page Application** (`Growcery Swagger`; a future web app can reuse it or
   get its own):
   - Allowed Callback URLs: `http://localhost:3030/swagger/oauth2-redirect.html`, plus the Visual Studio HTTPS
     profile's `https://localhost:<port>/swagger/oauth2-redirect.html`.
   - Allowed Web Origins: `http://localhost:3030` and `https://localhost:<port>`.
   - Copy its **Client ID** into `Auth0:SwaggerClientId`.
4. **Authentication → Database:** keep `Username-Password-Authentication` enabled for the SPA. Social connections
   (e.g. Google) are optional.
5. **Name claim** (only if §10 Q1 chooses a custom claim): **Actions → Library → Build Custom** (Login / Post Login).
   Add `api.accessToken.setCustomClaim("https://growcery.app/name", event.user.name)`, then add the Action to the
   Post-Login flow.
6. **Check the network:** open `https://<domain>/.well-known/openid-configuration` in a browser on the dev machine. If a
   corporate proxy blocks it, the API can't download signing keys.

---

## 7. Tests

### 7.1 Test infrastructure (`tests/Growcery.Api.Tests`)
- **`TestTokens`** (new) holds an RSA key generated once per test run, a test issuer and a test audience. Its
  `Create(sub, name?, lifetime?, audience?, issuer?, signingKey?)` mints a JWT with
  `Microsoft.IdentityModel.JsonWebTokens`. The parameters exist so tests can build expired, wrong-audience,
  wrong-issuer and wrongly signed tokens.
- **`GrowceryWebApplicationFactory`**:
  - Supplies `Auth0:Domain`/`Auth0:Audience` test values through environment variables, the same way as the connection
    string (they're read before the configuration hooks run).
  - Then `PostConfigure<JwtBearerOptions>`: clear `Authority` and the metadata address, and set
    `TokenValidationParameters` to the test issuer, audience and RSA public key. The tests make **no network calls**.
- **`AuthenticatedClientExtensions.CreateAuthenticatedClientAsync()`** keeps its signature. It returns an
  `https://localhost` client whose `DefaultRequestHeaders.Authorization` is a Bearer token for a unique
  `sub` = `test|{guid}`. **The grocery test classes don't change.**

### 7.2 New tests: `AuthenticationTests`
Each test checks the status, the ProblemDetails body and, where relevant, the `WWW-Authenticate` header.

| Test | Expectation |
|---|---|
| `GetLists_WithoutToken_Returns401ProblemDetails` | `401`, `application/problem+json`, `WWW-Authenticate` starts with `Bearer` |
| `GetLists_WithExpiredToken_Returns401` | `401` |
| `GetLists_WithWrongAudience_Returns401` | `401` |
| `GetLists_WithWrongIssuer_Returns401` | `401` |
| `GetLists_WithTokenSignedByOtherKey_Returns401` | `401` |
| `GetLists_WithUnsignedToken_Returns401` | `401` (`alg: none` rejected) |
| `GetLists_WithoutTokenAcceptingXml_Returns401` | `401`, not `500` |
| `GetLists_WithValidToken_Returns200` | `200` `[]` |
| `GetList_WithAnotherUsersToken_Returns404` | The list created by `sub` A returns `404` for `sub` B |
| `AccountEndpoints_AreRemoved` | `POST /api/account/login` returns `404` |

The display name test is added once §10 Q1 is answered.

### 7.3 Removed tests
`AccountControllerLoginTests.cs` (3 tests: the Identity cookie on login, no cookie on failure, the `Secure` cookie flag)
is deleted **under D20**, which removes the account endpoints and the cookie. The commit message names D20
(AGENTS.md §8).

All 38 Core tests and the grocery integration tests must pass with no change other than the auth helper.

---

## 8. Security notes
- **Signature, issuer, audience and lifetime are always validated.** `ValidAlgorithms` is limited to RS256, so
  `alg: none` and key-confusion tokens are rejected.
- **CSRF no longer applies to the API.** Browsers attach cookies automatically, but not `Authorization` headers.
  Spec 001 §6.1's CSRF layers (`SameSite` cookie, `[Consumes]`, no CORS) are no longer load-bearing. `[Consumes]` stays
  for content-type correctness.
- **Token storage is the client's concern.** Bearer tokens can be stolen by XSS in a web client. The future frontend
  spec must follow Auth0's SPA guidance (in-memory tokens, refresh token rotation).
- **Revocation:** access tokens can't be revoked before they expire. Keep Auth0's API **token lifetime** short (the
  default is 24 hours; §10 Q5). Blocking a user in Auth0 stops new tokens being issued.
- **Transport:** HTTPS still ends at the Container Apps ingress. Since there's no cookie, spec 001 D8's `SecurePolicy`
  concern goes away.
- **Logging:** never log tokens or the `Authorization` header. The display name is user free text, so the existing
  "never log names" rule applies.

---

## 9. Decisions

Continues from spec 002 (D11–D17).

- **D18 — Use an external identity provider, not our own token issuer.** From the review of the first JWT plan
  (2026-10-30): a self-built issuer must also cover sign-up, user management, deactivation, password reset, MFA and
  refresh, which would take weeks. A free hosted provider already does all of it.
- **D19 — Auth0 is the provider.** It has a free plan (25,000 monthly active users at the time of writing), needs no
  cloud account (there's no Azure subscription for this project yet), and has hosted login and good .NET/SPA SDKs.
  - **Moving to another provider later** means changing the `Auth0:*` settings, the Swagger security definition and
    the claim names.
  - **Users don't move automatically:** password hashes generally can't be exported, and the new provider's `sub`
    values differ. A future switch needs a user migration or account linking. That's acceptable while there are no
    real users.
- **D20 — The API is a resource server only.** There are no account endpoints, no ASP.NET Core Identity, no
  cookies, and no tokens issued by the API.
  - **Users sign up, log in and reset passwords only on Auth0's hosted login page** (§1). Confirmed by the user on
    2026-10-30.
  - **Rejected:** keeping `/api/account/register` and `/login` as endpoints that forward the username and password to
    Auth0 (the "password grant"). Three reasons:
    - Passwords would pass through our API again.
    - It rules out MFA, social login and Auth0's bot and breached-password protection.
    - It ties the API to Auth0's login API, so switching provider would mean rewriting it.

  This supersedes spec 001's cookie auth (§2 Auth row, §5, §6.1, and the §7
  Data Protection prerequisite) and spec 002's "`AccountController` keeps using ASP.NET Identity" (§1, §2.3).
- **D21 — `Person.ExternalId` is the Auth0 `sub` claim.** It's stored as-is (`auth0|…`, `google-oauth2|…`). It's
  unique within a tenant, and each environment has its own database, so the issuer isn't added to the key.

---

## 10. Open questions

To be answered by the user before implementation, then moved into §9 as decisions (AGENTS.md §3).

1. **Where does `Person.DisplayName` come from?** Auth0 access tokens carry no `name` or `email` claim by default.
   - a) A Post-Login Action adds a namespaced claim (`https://growcery.app/name`) to the access token (§6 step 5).
   - b) Use `sub` as the display name for now. Nothing shows display names yet.
   - c) The API calls Auth0's `/userinfo` endpoint. That's a network call per new user, and it needs the `openid
     profile` scopes.
2. **Existing dev data:** reset local databases once (`docker compose down -v`), so the old `Person` rows tied to
   Identity IDs disappear? Or keep them as orphans?
3. **Tenants:** one Auth0 **dev** tenant now, and a separate **prod** tenant when hosting is decided? Or one tenant for
   everything?
4. **Swagger login:** Authorization Code + PKCE through Auth0 in Swagger UI (§4.4)? Or skip it and paste a token
   obtained elsewhere (e.g. a small script, or the Auth0 dashboard)?
5. **Access token lifetime** for the Growcery API in Auth0: keep Auth0's default of 24 hours, or shorten it (e.g. 1
   hour) now that tokens can't be revoked by the API?

---

## 11. Implementation plan

One commit per step, test-first (AGENTS.md §8), and `dotnet test` passes before each commit.

1. **Docs:** this spec, the "superseded by spec 003" notes in specs 001 and 002, and the AGENTS.md updates.
2. **Test infrastructure:** `TestTokens`, the factory's test validation settings, and the Bearer
   `CreateAuthenticatedClientAsync`. Write `AuthenticationTests` (§7.2) first. They fail until step 3.
3. **Api + Data, in one commit.** Removing `AddIdentity` and `IdentityDbContext` must happen together, or the build
   breaks.
   - Api:
     - JWT bearer validation and the `401` ProblemDetails
     - `HttpCurrentUser` claims and the configuration fail-fast
     - delete `AccountController` and the account models
   - Data:
     - plain `DbContext`
     - delete `ApplicationUser` and the Identity package
     - the `RemoveAspNetIdentity` migration (reviewed per §5)
   - Delete `AccountControllerLoginTests` under D20.
4. **Swagger and configuration:** the OAuth2 PKCE security definition (if §10 Q4 says so), the `Auth0__*` settings in
   `appsettings.Development.json`, `docker-compose.yml` and `.env.example`.
5. **README:** the auth section with a sequence diagram, the Auth0 setup (§6), the ERD without `AspNetUsers`, and the
   updated run and debug instructions.
6. **Verify against a real Auth0 dev tenant + PostgreSQL:**
   - The migrations apply to a fresh database, and no `AspNet*` tables remain.
   - Swagger **Authorize** → Auth0 login → create a list → the `People` row has `ExternalId = auth0|…`.
   - A second Auth0 user gets `404` on the first user's list.
   - `curl` with no token, a garbage token, or an ID token → `401` ProblemDetails.

---

## 12. Definition of done
- [ ] Every open question in §10 is answered and recorded in §9.
- [ ] No reference to `Microsoft.AspNetCore.Identity` remains in `src/`, and no `AspNet*` tables exist after migrating
      a fresh database.
- [ ] The grocery endpoints accept only valid Auth0 access tokens for the Growcery API audience. All the §4.3 cases
      return `401` ProblemDetails (covered by §7.2).
- [ ] The grocery integration tests pass unchanged apart from the auth helper. The Core tests are untouched.
- [ ] Swagger UI can log in through the dev tenant and call the endpoints (if §10 Q4 keeps it).
- [ ] The README documents the auth flow and the Auth0 setup. Specs 001/002 and AGENTS.md point to this spec.
- [ ] AGENTS.md §10 holds: no new warnings, `dotnet test` green locally and in CI, migrations reviewed.

---

## 13. Changelog

| Date | Change |
|---|---|
| 2026-10-30 | Initial draft. D18–D21, open questions Q1–Q5. |
| 2026-10-30 | D20: hosted login only, and the password-forwarding endpoints rejected. Added the sign-up and login walkthrough to §1. |
