# S01: Auth Domain & Persistence Foundation

**Goal:** principal entities and migration are ready
**Demo:** database shape supports lockout, session revocation, and password-reset logout-all semantics

## Must-Haves
- Customer auth builds on `Customer`, not a separate duplicate account root.
- Admin and customer principals remain separate.
- Refresh tokens and reset tokens are never stored in plaintext.
- Customer and admin email lookups are normalization-safe.

## Tasks

- [x] **T1: Extend customer and admin principals for auth state** `depends:[]`
  > Add normalized email, failed-login counters, lockout timestamp, last-login timestamp, and token-version fields to `Customer.cs` and `AdminUser.cs`.
- [x] **T2: Add persistence models for sessions and password reset** `depends:[T1]`
  > Create `AuthSession` and `PasswordResetToken` entities with hashed token storage, expiry, and single-use consumption state.
- [x] **T3: Map EF Core configuration and indexes** `depends:[T2]`
  > Register new entities in `ApplicationDbContext` with snake_case mapping. Add normalized-email uniqueness constraints.
- [x] **T4: Create migration with safe rollout notes** `depends:[T3]`
  > Generate migration for entity changes, new tables, and indexes. Handle existing guest-created customer rows carefully.

## Files Likely Touched
- `backend/src/RentACar.Core/Entities/Customer.cs`
- `backend/src/RentACar.Core/Entities/AdminUser.cs`
- `backend/src/RentACar.Core/Entities/AuthSession.cs`
- `backend/src/RentACar.Core/Entities/PasswordResetToken.cs`
- `backend/src/RentACar.Infrastructure/Data/RentACarDbContext.cs`
- `backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs`
- `backend/src/RentACar.Infrastructure/Data/Configurations/AdminUserConfiguration.cs`
- `backend/src/RentACar.Infrastructure/Data/Configurations/AuthSessionConfiguration.cs`
- `backend/src/RentACar.Infrastructure/Data/Configurations/PasswordResetTokenConfiguration.cs`
- `backend/src/RentACar.Infrastructure/Data/Migrations/*`

## Observability / Diagnostics
- **Runtime signals:** principal auth state is inspectable via persisted fields (`normalized_email`, `failed_login_count`, `lockout_end_utc`, `last_login_at_utc`, `token_version`) on `customers` and `admin_users`.
- **Inspection surfaces:** EF model metadata/tests, migration SQL diff, and direct DB queries provide visibility into schema shape and default/null behavior.
- **Failure visibility:** auth failure responses must stay structured and non-enumerating (generic unauthorized / validation errors) while lockout/session state is diagnosable from persisted counters/timestamps.
- **Redaction constraints:** never log or persist plaintext passwords, refresh tokens, or password-reset tokens; diagnostics may include principal IDs, timestamps, counters, and token hash presence only.

## Verification
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~PrincipalAuthStateTests"`
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~AdminAuthControllerTests.Login_WithMissingCredentials_ReturnsBadRequest"` (failure-path/diagnostic surface check)
- `dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter "FullyQualifiedName~DbContextTests"`
- Slice-final (T4): generate migration and verify expected auth columns/tables appear in SQL/model snapshot before completion.
