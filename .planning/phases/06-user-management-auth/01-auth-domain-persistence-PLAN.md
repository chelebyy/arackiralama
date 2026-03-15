---
wave: 1
depends_on: []
files_modified:
  - backend/src/RentACar.Core/Entities/Customer.cs
  - backend/src/RentACar.Core/Entities/AdminUser.cs
  - backend/src/RentACar.Core/Entities/AuthSession.cs
  - backend/src/RentACar.Core/Entities/PasswordResetToken.cs
  - backend/src/RentACar.Infrastructure/Data/ApplicationDbContext.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/AdminUserConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/AuthSessionConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Configurations/PasswordResetTokenConfiguration.cs
  - backend/src/RentACar.Infrastructure/Data/Migrations/*
autonomous: true
requirements:
  - AUTH-01
  - AUTH-06
  - AUTH-07
  - AUTH-08
  - AUTH-09
  - AUTH-10
---

# Plan 01: Auth Domain & Persistence Foundation

## Objective

Create the database and entity foundation for customer/admin authentication, refresh-session tracking, password reset, lockout, and role enforcement without introducing ASP.NET Identity.

## Tasks

<tasks>
  <task id="P6-01-T1" title="Extend customer and admin principals for auth state">
    <files>
      <file>backend/src/RentACar.Core/Entities/Customer.cs</file>
      <file>backend/src/RentACar.Core/Entities/AdminUser.cs</file>
    </files>
    <steps>
      <step>Add normalized email, failed-login counters, lockout timestamp, last-login timestamp, and token-version fields.</step>
      <step>Keep customer auth fields nullable so guest-created customers can later register without data migration hacks.</step>
      <step>Preserve current role model for admins so existing authorization policies continue to work.</step>
    </steps>
  </task>
  <task id="P6-01-T2" title="Add persistence models for sessions and password reset">
    <files>
      <file>backend/src/RentACar.Core/Entities/AuthSession.cs</file>
      <file>backend/src/RentACar.Core/Entities/PasswordResetToken.cs</file>
    </files>
    <steps>
      <step>Create an auth session entity that supports principal type, principal id, hashed refresh token, session metadata, revocation, and replacement chaining.</step>
      <step>Create a password reset token entity with hashed token storage, expiry, and single-use consumption state.</step>
    </steps>
  </task>
  <task id="P6-01-T3" title="Map EF Core configuration and indexes">
    <files>
      <file>backend/src/RentACar.Infrastructure/Data/ApplicationDbContext.cs</file>
      <file>backend/src/RentACar.Infrastructure/Data/Configurations/CustomerConfiguration.cs</file>
      <file>backend/src/RentACar.Infrastructure/Data/Configurations/AdminUserConfiguration.cs</file>
      <file>backend/src/RentACar.Infrastructure/Data/Configurations/AuthSessionConfiguration.cs</file>
      <file>backend/src/RentACar.Infrastructure/Data/Configurations/PasswordResetTokenConfiguration.cs</file>
    </files>
    <steps>
      <step>Register the new entities in the DbContext and create snake_case EF mappings consistent with the rest of the repo.</step>
      <step>Replace customer email indexing with normalized-email uniqueness that supports login and registration.</step>
      <step>Ensure admin email lookup becomes normalization-safe instead of exact-case matching.</step>
    </steps>
  </task>
  <task id="P6-01-T4" title="Create migration with safe rollout notes">
    <files>
      <file>backend/src/RentACar.Infrastructure/Data/Migrations/*</file>
    </files>
    <steps>
      <step>Create the migration for entity changes, new tables, and indexes.</step>
      <step>Handle customer email uniqueness carefully so existing guest-created rows can be assessed before the constraint is enforced.</step>
      <step>Document any pre-migration data cleanup assumptions inside the migration comments or plan notes if needed.</step>
    </steps>
  </task>
</tasks>

## Must Haves

- Customer auth builds on `Customer`, not a separate duplicate account root.
- Admin and customer principals remain separate.
- Refresh tokens and reset tokens are never stored in plaintext.
- Customer and admin email lookups are normalization-safe.
- Database shape supports lockout, session revocation, and password-reset logout-all semantics.

## Verification Criteria

- Migration adds `auth_sessions` and `password_reset_tokens` plus auth fields on `customers` and `admin_users`.
- `CustomerConfiguration` and `AdminUserConfiguration` expose unique normalized-email access paths suitable for login.
- `ApplicationDbContext` compiles with the new entities registered.
- No ASP.NET Identity packages or schema are introduced.

