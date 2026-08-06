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

## Next module

Waiting on direction — Property Management (Seller upload, Buyer browse)
or Fraud Detection Engine exposure are the logical next steps, each
authorizing against the policies this module just added
(`RequireSeller`/`RequireBuyer`/`RequireAdmin`/`RequireSellerOrAdmin`).
