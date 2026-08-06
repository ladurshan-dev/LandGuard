# LandGuard

A web-based Land Transaction System with a Rule-Based Fraud Detection Engine.
The goal is not to be another property portal — it is to catch fraudulent
land listings, using OCR + a Dummy Government Land Registry Dataset, before
a Buyer ever sees them.

This document covers Module 1 (the solution skeleton) and Module 2 (the
LandGuardDB database integration - see the dedicated section near the
bottom). Business modules (Auth, Property Upload, Fraud Detection Engine,
Admin Review, ...) are added on top of these one at a time.

## Solution layout

```
LandGuard/
├── LandGuard.sln
├── Directory.Build.props        # shared TFM/nullable/langversion settings
├── .gitignore
├── src/
│   ├── LandGuard.Domain/            # entities, enums, domain exceptions — no dependencies
│   ├── LandGuard.Application/       # use-case services, DTOs, interfaces — depends on Domain only
│   ├── LandGuard.Infrastructure/    # EF Core, SQL Server, JWT/OCR impls — depends on Application
│   └── LandGuard.API/               # ASP.NET Core Web API host — depends on Application + Infrastructure
└── web/                          # separate static HTML/CSS/JS frontend (added when the frontend module starts)
```

The frontend is intentionally **not** inside the API project's `wwwroot`.
It is a separate static site that calls the API over HTTP with a JWT — this
mirrors how the system will actually be deployed (API and client hosted
independently) and keeps `LandGuard.API` a pure API with no view-rendering
concerns.

## Why Clean Architecture, and why this dependency direction

```
API  ─────depends on────▶  Infrastructure ──depends on──▶  Application  ──depends on──▶  Domain
 │                                                                │
 └──────────────────────────depends on───────────────────────────┘
```

Domain has zero project/package references. Application references only
Domain and defines *interfaces* for everything it needs from the outside
world (`IApplicationDbContext`, `IRepository<T>`, `ICurrentUserService`,
`IDateTimeService`, and later `IOcrService`, `IFraudDetectionEngine`,
`IJwtTokenService`). Infrastructure implements those interfaces using EF
Core, SQL Server, Tesseract OCR, etc. API wires concrete Infrastructure
implementations to Application interfaces at startup and exposes HTTP
endpoints.

This is the **Dependency Inversion Principle** applied at the project
level: business rules (Domain/Application) never depend on how data is
stored or how OCR is performed. That has a very concrete payoff for this
project specifically — the Fraud Detection Engine's rules (Duplicate Deed
Number, Price Anomaly, Seller History, ...) can be unit tested against
plain Domain entities with zero database, zero HTTP, zero OCR library
involved.

## Why a Service Layer instead of MediatR/CQRS

The stack call out was "Repository Pattern, Service Layer, Dependency
Injection" — not CQRS. A straightforward Service Layer (`PropertyService`,
`AuthService`, `FraudDetectionService`, each behind an interface and
injected via DI) is simpler to explain and grade in a FYP viva than a
MediatR pipeline, while still demonstrating the same SOLID principles
(each service has one responsibility, depends on abstractions, is open to
extension via new methods rather than modification of existing ones).

## Why Repository Pattern *and* `IApplicationDbContext`

- `IApplicationDbContext` (Application) / `ApplicationDbContext`
  (Infrastructure) is the persistence boundary EF Core actually needs.
- `IRepository<T>` / `Repository<T>` is the boundary *business logic* is
  allowed to see. Services depend on `IPropertyRepository`,
  `IUserRepository`, etc. (each extending the generic `IRepository<T>`
  with its own domain-specific queries), never on `DbSet<T>` or
  `IQueryable<T>` directly. This keeps LINQ-to-SQL query shapes out of
  service method signatures and means a service can be unit tested with a
  fake/mocked repository instead of a real database.
- The generic `Repository<T>` in Infrastructure gives every future
  entity-specific repository CRUD for free (Open/Closed Principle — a new
  query need is a new method on a specific repository, never a change to
  the generic base).

## Why an audit interceptor instead of manual timestamps

`BaseAuditableEntity` (`CreatedAt`, `CreatedBy`, `LastModifiedAt`,
`LastModifiedBy`) is stamped automatically by
`AuditableEntitySaveChangesInterceptor` on every `SaveChanges` call,
regardless of which service triggered it. For a fraud-detection system,
"who touched this listing and when" is not a nice-to-have log line — it
can be part of the evidence trail when a Buyer disputes a listing or an
Admin overturns a rejection. Centralizing this in an interceptor means no
future service can forget to set it.

## Why `Result<T>` instead of exceptions for expected outcomes

Services return `Result` / `Result<T>` for outcomes that are a normal part
of the business flow (e.g. "email already registered", "listing not
eligible for resubmission"). Exceptions (`NotFoundException`,
`DomainException`, FluentValidation's `ValidationException`) are reserved
for genuinely exceptional conditions and are translated into consistent
JSON error responses by `ExceptionHandlingMiddleware`. This keeps
controllers thin: call the service, check `Succeeded`, return `Ok`/`BadRequest`.

## Why JWT wiring is in `Program.cs` now, before the Auth module exists

`Program.cs` configures the JWT Bearer authentication *pipeline*
(`AddAuthentication().AddJwtBearer(...)`) and Swagger's "Authorize" button,
because that is ASP.NET Core plumbing that has to exist before any
`[Authorize]` attribute can work — the same category of concern as
registering the DbContext. It does **not** contain login/registration
logic or token issuance; that is business logic and belongs to an
`AuthService` in Application/Infrastructure, built in the next module.

## What's deliberately *not* here yet

No `User`, `Property`, `FraudReport`, `LandRegistryRecord` entities. No
`AuthService`, `IOcrService`, or `IFraudDetectionEngine`. No `web/`
frontend files. Per the project's phased approach, each of those is its
own module, built and reviewed one at a time rather than scaffolded ahead
of time as placeholders.

## Running this module

The sandbox this was authored in has no outbound access to install the
.NET SDK, so these files have not been machine-compiled — they were
hand-written to be correct, but please run a build the moment you pull
this onto a machine with .NET 8 installed, before starting the next
module:

```bash
cd LandGuard
dotnet restore
dotnet build
```

To run the API once you've set a real `Jwt:Key` and a valid SQL Server
Express connection string in `src/LandGuard.API/appsettings.json` (or
better, `dotnet user-secrets` — see `.gitignore`, real secrets should
never be committed):

```bash
dotnet run --project src/LandGuard.API
```

Then open `/swagger` — you should see the `HealthController` `GET
/api/health` endpoint and a working "Authorize" button (with nothing to
authorize against yet).

## Package choices (pinned in each `.csproj`)

| Concern | Package | Why |
|---|---|---|
| ORM | `Microsoft.EntityFrameworkCore.SqlServer` 8.0.10 | Matches .NET 8 / SQL Server Express per the stack |
| Migrations tooling | `Microsoft.EntityFrameworkCore.Design` / `.Tools` | Needed for `dotnet ef migrations add` |
| Auth | `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.10 | Official ASP.NET Core JWT middleware |
| API docs | `Swashbuckle.AspNetCore` 6.6.2 | Swagger UI with JWT "Authorize" support, standard for .NET 8 |
| Mapping | `AutoMapper` 13.0.1 | Entity ↔ DTO mapping for the Service Layer |
| Validation | `FluentValidation` + `.DependencyInjectionExtensions` 11.9.2 | Declarative DTO validation, integrates cleanly with `ExceptionHandlingMiddleware` |
| Stored procedures | `Dapper` 2.1.35 | See Module 2 below |

---

# Module 2 — LandGuardDB integration

Module 2 wires this Clean Architecture skeleton up to **LandGuardDB**, a
complete, pre-existing Microsoft SQL Server database (schema, indexes,
views, a 27-procedure/3-function fraud engine, and seed data) designed and
supplied separately by the team's database owner. This module's job was
narrow and specific: make EF Core describe that database accurately, and
build the plumbing to call its stored procedures — **not** to design any
new schema, and not yet to build Auth, Property, Fraud Detection, or OCR
as features.

## What was analysed first

Every file in the uploaded package was read before any code was written:
`01_Schema.sql` through `06_TestQueries.sql`, all four docs
(`DataDictionary.md`, `ERD.md`, `FraudEngine.md`, `API_Mapping.md`), and
both Python verification harnesses (re-run independently here — both
passed: script validation clean, and all 31 seeded listings re-score to
their documented risk band). **No schema redesign was needed or made** —
LandGuardDB is used exactly as uploaded.

## Module 1's speculative Domain code, corrected against the real schema

Module 1's `PropertyStatus`, `RiskLevel`, and `UserRole` enums were
drafted before LandGuardDB existed. Nothing consumed them yet, so they
were corrected for free rather than carried forward as a mismatch:

- `PropertyStatus` — was 5 states, is now the 4 LandGuardDB actually
  enforces (`Pending`/`Approved`/`Flagged`/`Rejected`, `CK_Property_Status`).
  There's no "under analysis" state (the engine runs synchronously inside
  one stored-procedure call) and no "Suspended" listing state (suspension
  is a `Users.IsActive` concept, not a property one).
- `RiskLevel` — was 4 levels including an unused `Critical`, is now the 3
  `CK_RiskReport_Banding` enforces (`Low`/`Medium`/`High`).
- `UserRole` — kept the friendlier C# name `Administrator`, but it's now
  mapped to the database's literal `"Admin"` string via an EF Core
  `ValueConverter` in `UserConfiguration`, so no other layer needs to know
  about the spelling difference.
- `DocumentType` is unchanged and still unused — LandGuardDB has no
  document/OCR table (a deed is just `Property.DeedReference`, a varchar).
  If the OCR module later needs to persist raw OCR output, that's a new
  table and, per this project's rules, would be proposed before being added.

## Why the new entities don't use `BaseEntity`

LandGuardDB's 12 tables each have their own natural primary key name
(`UserID`, `PropertyID`, `FraudCheckID`, ...) and only single timestamp
columns, not the `CreatedBy`/`LastModifiedBy` audit pairs Module 1's
`BaseAuditableEntity` assumes. Rather than force a convention designed for
a from-scratch schema onto one that already exists, the 12 table-backed
entities (`LandGuard.Domain/Entities`) and 9 view-backed read models
(`LandGuard.Domain/ReadModels`) are plain POCOs with idiomatic C# property
names (`PropertyId`, not `PropertyID`) mapped explicitly to the real
column names. `AuditableEntitySaveChangesInterceptor` simply doesn't fire
for them — correctly, since they don't own that shape. `BaseEntity` stays
in `Domain/Common` untouched for any future entity that does.

## EF Core: mapping, not schema ownership

`ApplicationDbContext` (Infrastructure) and `IApplicationDbContext`
(Application) now expose all 21 LandGuardDB objects — `DbSet<T>` for the
12 tables plus `DbSet<T>` (keyless, `HasNoKey().ToView(...)`) for the 9
views. Every one of the 21 has its own `IEntityTypeConfiguration<T>` class
under `Infrastructure/Persistence/Configurations`, picked up automatically
by the `ApplyConfigurationsFromAssembly` call already in place from Module
1. Each configuration pins the exact SQL Server column type
(`decimal(14,2)`, `datetime2(0)`, `varchar(20)` vs `nvarchar(...)`, ...),
not just a CLR-inferred default — deliberate, since getting Unicode/
non-Unicode wrong on an indexed column (`NIC`, `DeedReference`,
`ImageHash`) can silently defeat the matching filtered index. All 15
foreign keys from the ERD are configured with an explicit `OnDelete` that
matches the database (`CASCADE` on 6, `Restrict`/NO ACTION on 9) — left to
EF Core's default convention, several of these would make EF think there's
a multiple-cascade-paths conflict that the real database doesn't have.

The three scalar functions (`fn_IsValidNIC`, `fn_RiskLevelFromScore`,
`fn_GetRuleWeight`) are mapped with `HasDbFunction`, so they're callable
inside a LINQ query and translated to SQL rather than pulling data
client-side to evaluate in C#.

**This `DbContext` does not own schema.** LandGuardDB is created and
owned entirely by `Database/Scripts/00_RunAll.sql`. EF Core Migrations are
never used against it — the Fluent configuration only *describes* what
that script already built. If the two ever disagree, the SQL scripts win.

## Stored procedures: EF Core for reads, Dapper for procedures

EF Core's `FromSqlRaw`/`ExecuteSqlRaw` can't read a stored procedure's 2nd
or 3rd result set (`usp_Property_GetById` returns 3; `usp_Admin_GetDashboard`
returns 3) and is awkward with output parameters. `Dapper` was added to
`LandGuard.Infrastructure` for stored-procedure calls specifically for
this reason — EF Core remains the ORM for everything else (entities,
views, LINQ).

The mechanism is two layers:

- `IStoredProcedureExecutor` / `DapperStoredProcedureExecutor`
  (`Infrastructure/Persistence/StoredProcedures`) — a thin wrapper around
  `ApplicationDbContext`'s own ADO.NET connection. **Internal to
  Infrastructure only**, never exposed through an Application-layer
  interface, so no Dapper type ever appears in an Application method
  signature.
- One `I{Area}StoredProcedures` interface per functional area
  (Application) with a matching `{Area}StoredProcedures` implementation
  (Infrastructure), each speaking only in plain DTOs/entities. This
  pass builds exactly one — `INotificationStoredProcedures` /
  `NotificationStoredProcedures`, wrapping `usp_Notification_GetByUser`
  and `usp_Notification_MarkRead` — as the reviewable, reusable pattern.
  The remaining six areas (`IUserStoredProcedures`, `IPropertyStoredProcedures`,
  `IFraudStoredProcedures`, `IBuyerFeatureStoredProcedures`,
  `IAdminStoredProcedures`, `IPodcastStoredProcedures`) get built the same
  way, one at a time, alongside their respective feature module.

**The rule going forward:** the 12 table `DbSet`s exist for LINQ
*querying*. Any INSERT/UPDATE/DELETE against them must go through a
stored procedure, never `SaveChanges()` — the business rules (fraud
engine trigger, notifications, audit trail, NIC/role validation) live in
T-SQL, not C#. `PriceBenchmark` is the sole exception: it's the one table
with no stored procedure of its own, so plain EF Core CRUD is correct
there.

## Config changes to Module 1 files

- `src/LandGuard.API/appsettings.json` — connection string key changed
  from the Module 1 placeholder `DefaultConnection` to `LandGuardDB`,
  value taken verbatim from the uploaded `Database/README.md`.
- `src/LandGuard.Infrastructure/DependencyInjection.cs` — reads the
  `LandGuardDB` key; registers `IStoredProcedureExecutor` and
  `INotificationStoredProcedures`; no longer configures a migrations
  assembly (migrations are never used against this database).
- `src/LandGuard.Infrastructure/LandGuard.Infrastructure.csproj` — added
  the `Dapper` package.
- `src/LandGuard.Application/LandGuard.Application.csproj` — added a
  direct `Microsoft.EntityFrameworkCore` package reference, needed solely
  for the `DbSet<T>` type used in `IApplicationDbContext`. This is the one
  accepted exception to "Application knows nothing about EF Core" — no
  lighter package defines `DbSet<T>` on its own, and Application still
  writes no provider-specific code.

## What's still deliberately not here

No `AuthService`, no JWT issuance, no `PropertyService` business logic, no
`FraudDetectionService` orchestration exposed to a controller, no OCR, no
API controllers beyond Module 1's `HealthController`. Six of the seven
stored-procedure wrapper areas remain unbuilt until their module starts.

## Verifying this module

Same constraint as Module 1: no outbound access to install the .NET SDK in
this sandbox, so nothing here was `dotnet build`-verified end-to-end.
What *was* verified in this sandbox: every `DbSet<T>` name matches 1:1
between `IApplicationDbContext` and `ApplicationDbContext` (21/21); every
entity/read-model has exactly one `IEntityTypeConfiguration<T>` (21/21);
all 15 ERD relationships have an explicit `OnDelete` matching the SQL
(6 Cascade + 9 Restrict = 15/15); no stale references to the old
`PropertyStatus`/`RiskLevel` enum values remain anywhere in the solution;
and both of the database owner's own Python verification scripts
(`verify_sql_scripts.py`, `verify_fraud_engine.py`) were re-run here and
pass. Please still run `dotnet restore && dotnet build` against a real
LandGuardDB instance before the next module starts, the same as after
Module 1.

---

# Module 3 — JWT Authentication and Role-Based Authorization

Module 3 builds the first real feature on top of Modules 1 and 2: Buyer and
Seller registration, login, "who am I", and change password, plus the JWT
issuance and role-based policy plumbing every later module (Property,
Fraud Review, Admin) will authorize against. It follows the
`INotificationStoredProcedures` pattern from Module 2 exactly —
`IUserStoredProcedures` / `UserStoredProcedures` — and adds nothing to
Domain/Application/Infrastructure that Module 2 didn't already establish a
shape for.

## The one database gap, and how it was resolved

LandGuardDB ships `usp_User_Register` (INSERT), `usp_User_Login` (SELECT,
includes `PasswordHash`) and `usp_User_GetById` (SELECT, excludes
`PasswordHash`) — but no procedure to update `Users.PasswordHash` after
registration, which the Change Password endpoint needs. Per this project's
"stop and ask before database schema changes" rule, this was raised before
any code was written. The agreed fix, confirmed by the project owner: add
one new, narrowly-scoped stored procedure — **not** a table or column
change.

`database/Module3_ChangePassword.sql` adds `usp_User_ChangePassword`
(`@UserID`, `@NewPasswordHash` → updates the row, inserts a security
notification, returns rows-affected), modelled directly on the existing
`usp_Admin_SetUserActive` for style consistency (existence/active check →
RAISERROR, TRY/CATCH transaction, final SELECT, `RETURN 0`). It is a
separate, additive file rather than an edit to Module 2's
`04_StoredProcedures.sql` — this checkout doesn't contain that canonical
script — with a header comment noting it should be folded in there next
time that repository is updated. No table, column, or constraint changed.

## Application layer: contracts before implementation

- `Common/Models/UserProfile.cs` — the safe, password-free shape returned
  by `usp_User_Register`/`usp_User_GetById` (9 columns) and by every Auth
  endpoint.
- `Common/Models/UserCredential.cs` — the one shape that carries
  `PasswordHash`, matching `usp_User_Login`'s result set exactly. Used only
  inside `AuthService` to verify a password, then discarded — never
  returned by a controller or referenced by a DTO.
- `Common/Models/AccessToken.cs` — `(Token, ExpiresAtUtc)`, what
  `IJwtTokenGenerator` returns.
- `Common/Interfaces/IPasswordHasher.cs`, `IJwtTokenGenerator.cs`,
  `StoredProcedures/IUserStoredProcedures.cs`, `IAuthService.cs` — the four
  abstractions `AuthService` composes. Each is implemented in
  Infrastructure and injected by interface only, the same Dependency
  Inversion pattern as every other Infrastructure concern in this
  solution.
- `DTOs/Auth/*` — `BuyerRegisterRequest`, `SellerRegisterRequest`,
  `LoginRequest`, `ChangePasswordRequest`, `AuthResponse`, each with a
  FluentValidation validator under `DTOs/Auth/Validators`. Password
  strength (8+ chars, upper, lower, digit) and the Sri Lankan NIC pattern
  (`dbo.fn_IsValidNIC`'s exact shape: 9 digits + V/X, or 12 digits) are
  centralized in `AuthValidationRules` so the two DTOs that need them
  (`BuyerRegisterRequest` optionally, `SellerRegisterRequest` required)
  don't duplicate the regex.
- `Services/AuthService.cs` — orchestrates all five operations. Contains
  no SQL and no HTTP. `RegisterBuyerAsync`/`RegisterSellerAsync` hash the
  password, call `usp_User_Register`, and log the caller straight in
  (returns a token, so the frontend never needs a second round trip after
  sign-up). `LoginAsync` returns the same generic "Invalid email or
  password" `Result.Failure` whether the email doesn't exist or the
  password is wrong — standard practice against account enumeration — and
  separately checks `IsActive` for a suspended account. `ChangePasswordAsync`
  re-verifies `CurrentPassword` against the stored hash before calling
  `usp_User_ChangePassword`.

## Why role claims carry `"Admin"`, not `"Administrator"`

`Users.Role` is `VARCHAR(20)` constrained to the literal strings `Buyer`,
`Seller`, `Admin` (`CK_Users_Role`). The C# enum keeps the friendlier
`UserRole.Administrator` (Module 2's own reasoning), so
`UserRoleExtensions.ToDbValue`/`FromDbValue` (Domain) is the single place
that translates between them — `UserConfiguration`'s EF Core value
converter now calls it too, rather than duplicating the mapping inline.
`JwtTokenGenerator` writes the claim via `role.ToDbValue()`, so the
`ClaimTypes.Role` claim inside every issued token, and therefore every
`[Authorize(Roles=...)]`/policy check, is always `"Admin"` — matching what
`CurrentUserService` and ASP.NET Core's role middleware compare against.

## Infrastructure: hashing, tokens, the fourth stored-procedure wrapper

- `BcryptPasswordHasher` — `BCrypt.Net-Next`, work factor 11, matching the
  work factor already baked into every seeded password hash (Module 2's
  `05_SeedData.sql`, every hash is BCrypt of `Test@123`) — freshly
  registered accounts and seeded test accounts hash the same way, not just
  verify the same way.
- `JwtSettings` / `JwtTokenGenerator` — `System.IdentityModel.Tokens.Jwt`,
  HMAC-SHA256, claims `NameIdentifier`/`Email`/`Name`/`Role`/`jti`. Reads
  the same `Jwt` configuration section Program.cs already used in Module 1
  for `TokenValidationParameters`, via `IOptions<JwtSettings>` bound once
  in `AddInfrastructureServices` — signing and validation can never drift
  onto two different keys by accident.
- `UserStoredProcedures` — the fourth `I{Area}StoredProcedures`
  implementation (after Notifications in Module 2). `RegisterAsync` is the
  first place this solution needs Dapper's `DynamicParameters` for a real
  OUTPUT parameter (`usp_User_Register`'s `@NewUserID`) — no change to
  `IStoredProcedureExecutor`'s `object? parameters` signature was needed,
  it already accepted anything Dapper can execute.

## API layer

- `Controllers/AuthController.cs` — `POST /api/auth/register/buyer`,
  `POST /api/auth/register/seller`, `POST /api/auth/login` (all
  `[AllowAnonymous]`); `GET /api/auth/me`, `POST /api/auth/change-password`
  (both `[Authorize]`, any authenticated role — the target user always
  comes from the JWT's `NameIdentifier` claim via `ICurrentUserService`,
  never from the request body). Every action is a thin `Result` → HTTP
  translation; all business logic stays in `AuthService`.
- `Authorization/AuthorizationPolicies.cs` — `RequireBuyer`,
  `RequireSeller`, `RequireAdmin`, `RequireSellerOrAdmin` named policies
  (the .NET 8 `AddAuthorizationBuilder()` idiom), registered in
  `Program.cs`. No controller beyond Auth needs them yet, but Property
  (Seller-only upload, Admin-only review) and Admin modules will consume
  them immediately without redefining role strings.
- `ExceptionHandlingMiddleware` — one new case: `SqlException` → 400 with
  the driver's message. `usp_User_Register` (duplicate email, invalid/
  duplicate Seller NIC) and `usp_User_ChangePassword` (inactive account)
  enforce their rules with `RAISERROR`, which surfaces as a `SqlException`
  in C# — this is an *expected*, business-rule outcome from the database's
  point of view, so it gets a clean 400 with the SQL-authored message
  rather than falling through to the generic 500 case.
- `Program.cs` — added the four `AddAuthorizationBuilder()` policies above
  `builder.Build()`. The JWT Bearer *authentication* pipeline itself needed
  no changes; it was already fully wired in Module 1 in anticipation of
  this module.
- Swagger's bearer "Authorize" button (Module 1) now has real endpoints to
  authorize against — `/api/auth/login` returns a token that can be pasted
  in directly.

## Config and package changes

- `src/LandGuard.Infrastructure/LandGuard.Infrastructure.csproj` — added
  `BCrypt.Net-Next` 4.0.3 and `System.IdentityModel.Tokens.Jwt` 8.0.2.
- `src/LandGuard.Infrastructure/DependencyInjection.cs` — registers
  `JwtSettings` (via `Configure<JwtSettings>`), `IPasswordHasher`,
  `IJwtTokenGenerator`, `IUserStoredProcedures`.
- `src/LandGuard.Application/DependencyInjection.cs` — registers
  `IAuthService`; the four Auth validators need no explicit line, they're
  picked up by the existing `AddValidatorsFromAssembly` scan.
- `appsettings.json` — no changes; the `Jwt` section Module 1 already
  added is exactly what `JwtSettings` binds to.

## What's still deliberately not here

No refresh tokens, no email verification/password-reset flow, no
account lockout after repeated failed logins, no `PropertyService`,
`FraudDetectionService`, or Admin endpoints. Password reset in particular
needs an email-delivery decision this project hasn't made yet, so it was
left out rather than half-built.

## Verifying this module

Same sandbox constraint as Modules 1 and 2 — no outbound access to install
the .NET SDK here, so this was reviewed statically rather than
`dotnet build`-verified: every DTO's properties match its validator and
its consuming `AuthService` method; `UserProfile`/`UserCredential`'s
properties match each stored procedure's actual SELECT list column-for-
column; `IUserStoredProcedures`/`IPasswordHasher`/`IJwtTokenGenerator` each
have exactly one Infrastructure implementation, registered once in DI; the
role string written into JWTs (`ToDbValue`) matches what
`AuthorizationPolicies` and `CK_Users_Role` both expect (`Admin`, not
`Administrator`); and no Module 2 file was changed beyond the two
documented above (`UserConfiguration`'s converter call site, and the two
DI/csproj files). **Please run `dotnet restore && dotnet build` against a
real LandGuardDB instance — with `usp_User_ChangePassword` applied via
`database/Module3_ChangePassword.sql` — before starting the next module.**

---

# Module 4 — Property Management

Module 4 builds Property CRUD, image upload, search and the seller/public
visibility rules around them, on top of the stored-procedure architecture
Module 2 already built for `dbo.Property`/`dbo.PropertyImage` and the
8-point fraud engine. Nothing here redesigns a table or a procedure —
`usp_Property_Create/Update/Delete/GetById/Search/GetBySeller`,
`usp_PropertyImage_Add` and `usp_Fraud_AnalyseProperty` already existed in
the uploaded LandGuardDB package and are used exactly as written.

## Scope: PropertyController only, matching `Database/Docs/API_Mapping.md`

The database owner's own API mapping document splits the property
endpoints into three phases — Phase 1 `PropertyController` (CRUD, search,
images), Phase 2 the Fraud Detection Engine (already fully implemented in
T-SQL since Module 2), Phase 3 `AdminController`
(`/api/admin/flagged`, `/approve/{id}`, `/reject/{id}`, `/dashboard`, ...).
Module 4 builds exactly Phase 1. `usp_Admin_ApproveProperty` and
`usp_Admin_RejectProperty` — the only stored procedures that let an Admin
change a listing's `Status` field itself — are deliberately left for a
future Admin module, per that same document's own phasing, rather than
bolted onto PropertyController. Within Phase 1's own scope, "owner or Admin
can modify/delete" is fully satisfied: `usp_Property_Update` only ever
lets the owning Seller edit a listing's fields (there is no admin bypass
in that procedure, so this module doesn't invent one), while
`usp_Property_Delete` explicitly authorizes the owner **or** an active
Admin.

## Where authorization actually lives

Two different enforcement points, chosen to match what each stored
procedure already does:

- **Update and Delete** — the stored procedures are the real
  authorization boundary. `usp_Property_Update` raises a `RAISERROR` (→
  `SqlException` → 400, via Module 3's `ExceptionHandlingMiddleware`) if
  `@SellerID` doesn't own the row; `usp_Property_Delete` does the same
  unless the caller owns the row or is an active Admin. `PropertyController`
  never passes a client-supplied owner id — `@SellerID`/`@UserID` always
  come from `ICurrentUserService.UserId`, the same "target always comes
  from the JWT, never the body" rule Module 3's Change Password endpoint
  established — so this can't be bypassed by claiming a different id.
  `PropertyService` doesn't duplicate this check; it only surfaces
  whatever the procedure decides.
- **AddImage and GetBySeller** — `usp_PropertyImage_Add` and
  `usp_Property_GetBySeller` have no caller awareness at all (the first is
  a plain insert, the second a plain `SELECT ... WHERE SellerID = @SellerID`
  with no check that the caller *is* that seller). `PropertyService`
  enforces "owner or Admin" for both directly, since the database
  genuinely has no opinion here.
- **GetById** — `usp_Property_GetById` returns a row regardless of
  status. `PropertyService` applies the visibility rule: an `Approved`
  listing is visible to anyone; a `Pending`/`Flagged`/`Rejected` one is
  visible only to its owner or an Admin. A Buyer probing another id gets
  the identical "Property not found" `Result.Failure` a nonexistent id
  would produce — the same account-enumeration-safe shape Module 3's login
  endpoint uses, so a listing's existence and its moderation status are
  never distinguishable to someone who shouldn't see either.

## Application layer

- `Common/Models/PropertyListingResult.cs`, `PropertySearchResult.cs`,
  `PropertyImageSummary.cs`, `PropertyFraudRuleResult.cs`,
  `PropertyDetail.cs` — dedicated Dapper-projection DTOs, one per distinct
  stored-procedure result-set shape, following the same reasoning Module 3
  gave for `UserProfile`/`UserCredential`/`NotificationSummary`: these come
  from a SQL view via Dapper, not a tracked EF Core query, so they stay
  decoupled from the EF Core-oriented `PropertyListing`/`PublishedProperty`
  read models Module 2 built for `IApplicationDbContext`, even though the
  column lists overlap heavily.
- `Common/Interfaces/StoredProcedures/IPropertyStoredProcedures.cs` — one
  method per Property/Image/Fraud-trigger procedure, including
  `AnalyseAsync` (wraps `usp_Fraud_AnalyseProperty` directly) so
  `PropertyService` can re-run the engine after images are attached — the
  submission sequence `Database/Docs/API_Mapping.md` documents
  (Create → AddImage(s) → Analyse), since `usp_Property_Create`'s own
  internal analysis run happens *before* any photo exists.
- `Common/Interfaces/IGeocodingService.cs` / `IFileStorageService.cs` — the
  two new external-concern seams this module needs, each with exactly one
  Infrastructure implementation, the same Dependency Inversion pattern as
  `IPasswordHasher`/`IJwtTokenGenerator` in Module 3.
- `DTOs/Property/*` — `CreatePropertyRequest`, `UpdatePropertyRequest`
  (every field optional, matching `usp_Property_Update`'s
  `ISNULL(@Param, Column)` pattern), `PropertySearchRequest`, each with a
  FluentValidation validator under `DTOs/Property/Validators`. Field
  lengths mirror `dbo.Property`'s actual column widths
  (`PropertyValidationRules`); Latitude/Longitude validation is only a
  general `[-90,90]`/`[-180,180]` sanity check — coordinates outside Sri
  Lanka are accepted by validation on purpose, since that's exactly what
  fraud rule 6 (Location Validation) is supposed to catch, not something
  a 400 should pre-empt.
- `Services/PropertyService.cs` — orchestrates all seven operations
  (Create, AddImage, GetById, Search, GetBySeller, Update, Delete). No SQL,
  no HTTP. Composes `IPropertyStoredProcedures`, `IGeocodingService`,
  `IFileStorageService` and the three validators.

## Geocoding: filling in what the fraud engine needs

`dbo.Property.Latitude`/`Longitude` are documented in Module 2's own
Data Dictionary as "written back from the Nominatim API" — and fraud rule
6 (Location Validation, inside `usp_Fraud_AnalyseProperty`) fires whenever
they're missing or fall outside Sri Lanka's bounding box. Module 2
deliberately left that integration for "the property module" (its own
words) to build. `IGeocodingService` / `NominatimGeocodingService`
(Infrastructure) call the public Nominatim (OpenStreetMap) API — a typed
`HttpClient` via `AddHttpClient`, 5s timeout, the descriptive `User-Agent`
Nominatim's usage policy requires — and `PropertyService.CreateAsync`
calls it automatically whenever a seller doesn't supply explicit
coordinates. A failed or empty geocode is treated as a normal outcome, not
an error: it's caught and returns `null` coordinates, which correctly lets
fraud rule 6 flag the listing rather than blocking submission. `Update`
supports the same behaviour opt-in via `RegeocodeLocation`, for a seller
who corrects the location text of a `Flagged`/`Rejected` listing without
knowing new coordinates themselves.

**Before real production traffic:** the public Nominatim instance is
rate-limited (1 request/second) and the `Geocoding:UserAgent` value in
`appsettings.json` is a placeholder — replace it with a real contact
method per Nominatim's usage policy, or point `Geocoding:BaseUrl` at a
self-hosted/commercial instance.

## Image upload: local disk today, swappable later

`dbo.PropertyImage.ImageURL` is just an `NVARCHAR(500)` — the schema
doesn't care where the bytes live. `IFileStorageService` /
`LocalFileStorageService` (Infrastructure) save uploads under
`wwwroot/uploads/properties/{propertyId}/{guid}.{ext}` (served back out via
`app.UseStaticFiles()`, added to `Program.cs`) and compute the SHA-256
fingerprint fraud rule 2 (Duplicate Image) compares, in one streaming pass
via `CryptoStream` so the upload is never read twice. This is the correct
choice for this FYP's local SQL Server Express/IIS Express deployment;
the interface seam means swapping in Azure Blob Storage or S3 later is one
new Infrastructure class and one DI registration, with zero change to
`PropertyService` or `PropertyController`.

`PropertyController.AddImage` accepts `multipart/form-data`
(`[Consumes]`, 6 MB request-size ceiling), and `PropertyService` rejects
an unsupported content type or an over-5MB file (`PropertyValidationRules`)
before ever touching the filesystem — deliberately duplicated as
Application-layer checks (no `IFormFile` dependency) rather than relying
solely on `LocalFileStorageService`'s own content-type guard, so the same
validation would apply to any future non-web caller of `PropertyService`.

## API layer

- `Controllers/PropertyController.cs` — `GET /api/properties` (search,
  anonymous, published listings only — FR10), `GET /api/properties/{id}`
  (anonymous, visibility rule applied), `GET /api/properties/seller/{id}`
  (authenticated, owner-or-Admin — FR08 dashboard grid), `POST /api/properties`
  (Seller only), `POST /api/properties/{id}/images` (owner or Admin),
  `PUT /api/properties/{id}` (Seller, ownership enforced by the
  procedure), `DELETE /api/properties/{id}` (owner or Admin, enforced by
  the procedure). Every action is a thin `Result`/`PropertyDetail`
  translation to HTTP; all business logic stays in `PropertyService`.
- `Program.cs` — added `app.UseStaticFiles()` so uploaded photos are
  servable at the URL `FileStorageSettings.PublicBaseUrl` points to.
  No new authorization policies were needed — Module 3's `RequireSeller`
  covers Create/Update, and `RequireSellerOrAdmin` is a policy for a future
  module rather than these endpoints, whose owner-or-Admin rule is
  per-resource (a specific `SellerID` match), not per-role, so it can't be
  expressed as a static `[Authorize(Policy=...)]` the way Module 3's
  role-only policies could.

## Config changes

- `src/LandGuard.API/appsettings.json` — added `Geocoding` (`BaseUrl`,
  `UserAgent`, `TimeoutSeconds`) and `FileStorage` (`RootPath`,
  `PublicBaseUrl`, `MaxFileSizeBytes`, `AllowedContentTypes`) sections.
- `src/LandGuard.Infrastructure/DependencyInjection.cs` — registers
  `IPropertyStoredProcedures`; `IGeocodingService` via a typed
  `AddHttpClient` (pooled-handler lifetime, rather than a plain
  `AddScoped` opening a raw `HttpClient` per request); `IFileStorageService`
  and its `FileStorageSettings` binding.
- No new NuGet packages — `IHttpClientFactory` (`AddHttpClient`),
  `IWebHostEnvironment`, and `System.Security.Cryptography.SHA256` all
  ship inside the `Microsoft.AspNetCore.App` shared framework/.NET base
  class library Infrastructure already references.

## What's still deliberately not here

No Admin review workflow (`/api/admin/flagged`, `/approve/{id}`,
`/reject/{id}`, `/dashboard`) — Phase 3 of `API_Mapping.md`, its own
future module. No `usp_PropertyImage_Delete` (the schema has no such
procedure; adding one wasn't requested and would be a database change,
which this module was told to avoid unless absolutely necessary — a
seller can only add photos, not remove one, until that procedure exists).
No `SuspiciousReport`/`SavedProperty`/`Notification`-consuming endpoints —
each belongs to the Buyer-features module the API mapping groups
separately.

## Verifying this module

Same sandbox constraint as Modules 1-3 — no outbound access to install the
.NET SDK here, so this was reviewed statically rather than
`dotnet build`-verified: every `IPropertyStoredProcedures`/`IPropertyService`
method signature matches its implementation exactly; every Dapper
projection DTO's property names match the stored procedure's actual
`SELECT` list (verified column-by-column against
`Database/Scripts/03_Views.sql` and `04_StoredProcedures.sql`); no
Module 1/2/3 file was changed beyond `Program.cs` (`UseStaticFiles` +
policy comment), `appsettings.json` (new config sections), and
`Infrastructure/DependencyInjection.cs` (new registrations) — Module 2's
`Property`/`PropertyImage` entities, configurations, and Module 3's Auth
files are untouched. **Please run `dotnet restore && dotnet build` against
a real LandGuardDB instance before starting the next module**, and confirm
`Nominatim` outbound access (or configure an alternative `Geocoding:BaseUrl`)
in whatever environment this deploys to.

## Next module

Waiting on direction — the Admin review workflow (flagged queue,
approve/reject, dashboard, user suspension, NIC verification, rule-weight
tuning) or the Buyer-features module (saved properties, suspicious
reports, notifications) are the logical next steps per
`Database/Docs/API_Mapping.md`'s own phasing.

---

# Module 5A — Fraud Detection Foundation

Module 5A's brief, taken literally, asked for a new C# rule engine
(`IFraudRule`, placeholder rules like `OwnerNameRule`/`SurveyPlanRule`,
Guid property ids, its own 6-rule/100-point weighting, a 4-tier
LOW/MEDIUM/HIGH/CRITICAL band) - a document-verification model with no
relationship to anything in LandGuardDB. Before writing any code, this was
raised directly: Module 2 already shipped a complete, tested, 7-rule fraud
engine entirely in T-SQL (`usp_Fraud_AnalyseProperty` /
`usp_Risk_GenerateReport` / `dbo.FraudCheck` / `dbo.RiskReport` /
`dbo.FraudRuleWeight`), already wired into Property Create/Update by
Module 4, using `int` property ids and a 3-tier Low/Medium/High band
(`CK_RiskReport_Banding`, 0-40/41-70/71-100) - not the spec's numbers.
Building the spec literally would have meant either a second, redundant
fraud engine sitting alongside the real one, or gutting Module 2/4's
already-shipped behaviour to fit a rule set (Owner Name, Survey Plan,
Land Extent, Registration Number, Parcel Number, Address) that doesn't
correspond to any column LandGuardDB actually has.

**Confirmed direction:** keep the existing fraud engine exactly as
implemented; do not create a second fraud subsystem or replace any
existing table, procedure, or rule model; build only the missing
Application/Infrastructure/API layers around the current SQL Server
engine; consume existing calculations instead of duplicating them in C#;
touch Modules 1-4 only where unavoidable. Concretely, this means:

- **No `IFraudRule`/rule-engine abstraction and no placeholder rules**
  (`OwnerNameRule`, `SurveyPlanRule`, etc.) - there is nothing for them to
  plug into. The real rule engine already lives entirely in
  `usp_Fraud_AnalyseProperty`; a parallel C# one would be exactly the
  "second fraud subsystem" ruled out above. Re-introducing a
  document-field-matching engine (Owner Name/Survey Plan/etc. compared
  against OCR output) is a Module 5B decision, once real OCR data and a
  land-registry comparison table exist to justify it.
- **`int propertyId`, not `Guid`** - `dbo.Property.PropertyID` is an
  `INT IDENTITY` everywhere in the schema; every method in this module
  uses `int`, consistent with `IPropertyService`/`IAuthService`.
- **`FraudCheck`/`RiskReport`/`FraudRuleWeight`** were already completed
  in Module 2 (Domain entities, EF Core configurations, seed data) - per
  the brief's own "only if they are not already completed," nothing was
  added here.

## What was actually missing

Reviewing the existing engine (`Database/Scripts/01_Schema.sql`,
`03_Views.sql`, `04_StoredProcedures.sql`) against the module's three
required operations - analyze, get the current report, get history -
showed two of the three already had a wrapper and one didn't:

- **Analyze** - `usp_Fraud_AnalyseProperty` already exists and is already
  called by `IPropertyStoredProcedures.AnalyseAsync` (Module 4). Nothing
  new needed at the data-access layer.
- **Current report** - `usp_Property_GetById`'s third result set
  (`vw_FraudCheckDetail`) is already wrapped as
  `PropertyDetail.FraudReport`, reachable via
  `IPropertyStoredProcedures.GetByIdAsync`/`IPropertyService.GetByIdAsync`.
  Nothing new needed here either.
- **History** - `dbo.FraudCheck` keeps one row per analysis run by design
  ("a property may be analysed more than once ... this table keeps full
  history"), but no existing view or procedure ever exposed more than the
  latest run - `vw_PropertyLatestRisk` and everything built on it
  (`vw_FraudCheckDetail`, `usp_Property_GetById`) are latest-only. This
  was the one genuine gap.

`database/Module5A_FraudHistory.sql` adds exactly one new, read-only,
additive procedure - `usp_Fraud_GetHistory(@PropertyID)` - joining
`FraudCheck` to `RiskReport` for a property, newest first. Same pattern as
Module 3's `Module3_ChangePassword.sql`: a separate file (this checkout
has no canonical `Database/Scripts` folder), a header noting it should be
folded into `04_StoredProcedures.sql` Section D next time that repository
updates, and nothing else touched.

## Application layer

- `Common/Models/FraudHistoryEntry.cs` - the one new Dapper-projection
  DTO, matching `usp_Fraud_GetHistory`'s result set exactly.
- `Common/Interfaces/StoredProcedures/IFraudStoredProcedures.cs` -
  deliberately one method (`GetHistoryAsync`). Its doc comment explains
  why Analyze and Report aren't declared here too: they already have
  wrappers, and re-declaring them would be exactly the duplication this
  module was told to avoid.
- `Common/Interfaces/IFraudDetectionService.cs` /
  `Services/FraudDetectionService.cs` - `AnalyzePropertyAsync`,
  `CalculateRiskScoreAsync`, `GetFraudReportAsync`,
  `GetFraudHistoryAsync`. No rule is evaluated and no score is computed
  anywhere in this class - `CalculateRiskScoreAsync` reads the score
  `usp_Risk_GenerateReport` already wrote, it doesn't recompute one.
  Composes `IPropertyStoredProcedures`, `IPropertyService`,
  `IUserStoredProcedures` (all Module 3/4, unchanged) and the one new
  `IFraudStoredProcedures`.
- `DTOs/Fraud/{FraudAnalysisResponse,FraudReportResponse,FraudRuleResponse,RiskSummaryResponse,FraudHistoryResponse}.cs` -
  every field is a direct projection of an existing column
  (`PropertyFraudRuleResult`/`PropertyListingResult`, both from Module 4)
  or the new `FraudHistoryEntry` - never a C#-computed value.

## Two different authorization checks, on purpose

`AnalyzePropertyAsync` and the three read methods enforce ownership
differently, because "who may trigger analysis" and "who may read a
report" are genuinely different rules:

- **Analyze** uses a strict check against the *raw* property
  (`IPropertyStoredProcedures.GetByIdAsync`, no visibility filtering):
  owning Seller or Admin only. Reusing `IPropertyService.GetByIdAsync`'s
  visibility rule here would have been a real authorization bug - it
  would let any Seller trigger re-analysis of any other Seller's already-
  Approved (therefore publicly visible) listing, since "Approved" alone
  passes that rule.
- **Report/History/CalculateRiskScore** reuse
  `IPropertyService.GetByIdAsync` directly (the exact visibility rule
  `PropertyController` already exposes: public once Approved, otherwise
  owner or Admin only) - precisely "Buyer read-only" once a listing is
  public, and no leak of a Pending/Flagged/Rejected listing's existence
  to anyone uninvolved with it.
- **"Property is active"** (the third validation point in the brief) is
  read as "the owning seller's account hasn't been suspended" -
  `dbo.Property` has no `IsActive` column of its own; this is exactly the
  definition `vw_PublishedProperty` and the engine's own NIC check
  (`usp_Fraud_AnalyseProperty`'s `Users.IsActive`) already use. Checked
  via the existing `IUserStoredProcedures.GetByIdAsync`, only for Analyze
  (reading an already-suspended seller's past report is still allowed).

## API layer

- `Controllers/FraudController.cs` - `POST /api/fraud/analyze/{propertyId}`
  (`[Authorize(Policy = RequireSellerOrAdmin)]` - a Buyer can't reach this
  route at all), `GET /api/fraud/report/{propertyId}` and
  `GET /api/fraud/history/{propertyId}` (`[Authorize]`, any authenticated
  role - ownership/visibility decided inside FraudDetectionService, not
  here). Every endpoint requires a JWT; there is no anonymous access,
  unlike `PropertyController`'s public search/GetById - the brief was
  explicit that all three fraud endpoints require authorization.
  No new authorization policy was needed - `RequireSellerOrAdmin`
  (Module 3) already expressed exactly "Seller or Admin."

## Config/DI changes

- `src/LandGuard.Application/DependencyInjection.cs` - registers
  `IFraudDetectionService`.
- `src/LandGuard.Infrastructure/DependencyInjection.cs` - registers
  `IFraudStoredProcedures`.
- No `appsettings.json` changes, no new NuGet packages, no changes to
  `Program.cs` - this module adds no new configuration surface or
  pipeline concern.

## What's still deliberately not here

No OCR, no Tesseract, no PDF reading, no image processing, no AI/vision
API of any kind - explicitly excluded, Module 5B's job. No document-field
comparison (Owner Name, Survey Plan, Land Extent, Registration Number,
Parcel Number, Address) - there is no data source for those fields yet
(no land-registry dataset table exists in LandGuardDB); this is exactly
what Module 5B's OCR + Dummy Land Registry Dataset work is expected to
supply, at which point a real (not placeholder) rule engine can be
designed against real inputs. No changes to `usp_Fraud_AnalyseProperty`,
`usp_Risk_GenerateReport`, `dbo.FraudRuleWeight`'s rows, or
`CK_RiskReport_Banding`.

## Verifying this module

Same sandbox constraint as Modules 1-4 - no outbound access to install the
.NET SDK here, so this was reviewed statically rather than
`dotnet build`-verified: `IFraudDetectionService`/`IFraudStoredProcedures`
method signatures match their implementations exactly;
`FraudHistoryEntry`'s properties match `usp_Fraud_GetHistory`'s `SELECT`
list column-for-column; every response DTO field traces to an existing
`PropertyListingResult`/`PropertyFraudRuleResult` field or the new
`FraudHistoryEntry`, with no C#-side calculation; no Module 1-4 file was
changed at all beyond the two `DependencyInjection.cs` registrations
listed above. **Please run `dotnet restore && dotnet build` against a
real LandGuardDB instance - with `usp_Fraud_GetHistory` applied via
`database/Module5A_FraudHistory.sql` - before starting Module 5B.**

## Next module

Module 5B (OCR + document verification against the Dummy Land Registry
Dataset) is the natural next step now that this module's foundation
(analyze/report/history, wired through JWT authorization) is in place -
that is where a real document-matching rule set, fed by actual OCR
output, would get designed and plugged in, rather than the simulated
placeholder rules this module deliberately did not build.
