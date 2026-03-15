---
wave: 1
depends_on: []
files_modified:
  - backend/src/RentACar.API/Services/IJwtTokenService.cs
  - backend/src/RentACar.API/Services/JwtTokenService.cs
  - backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs
  - backend/src/RentACar.API/Options/JwtOptions.cs
  - backend/src/RentACar.API/Authentication/*
  - backend/src/RentACar.Infrastructure/Security/*
autonomous: true
requirements:
  - AUTH-03
  - AUTH-04
  - AUTH-05
  - AUTH-09
  - AUTH-10
---

# Plan 02: Token, Session, and Authorization Infrastructure

## Objective

Upgrade the existing JWT stack so both admin and customer flows can issue 15-minute access tokens, rotate 7-day refresh tokens, validate revocation, and enforce RBAC safely.

## Tasks

<tasks>
  <task id="P6-02-T1" title="Generalize JWT issuance for both principal types">
    <files>
      <file>backend/src/RentACar.API/Services/IJwtTokenService.cs</file>
      <file>backend/src/RentACar.API/Services/JwtTokenService.cs</file>
      <file>backend/src/RentACar.API/Options/JwtOptions.cs</file>
    </files>
    <steps>
      <step>Refactor token issuance so it can create access tokens for customers and admins from a shared claim model.</step>
      <step>Switch access-token lifetime to 15 minutes and add `sid` plus `ver` claims for session/version validation.</step>
      <step>Keep signing and validation on the built-in ASP.NET Core JWT stack.</step>
    </steps>
  </task>
  <task id="P6-02-T2" title="Add refresh-token generation and hashing helpers">
    <files>
      <file>backend/src/RentACar.Infrastructure/Security/*</file>
    </files>
    <steps>
      <step>Create cryptographically secure refresh-token generation.</step>
      <step>Add hashing and comparison helpers so refresh tokens are stored and checked safely.</step>
      <step>Support rotation and replay detection using replacement/revocation metadata.</step>
    </steps>
  </task>
  <task id="P6-02-T3" title="Wire application-level session validation into auth pipeline">
    <files>
      <file>backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs</file>
      <file>backend/src/RentACar.API/Authentication/*</file>
    </files>
    <steps>
      <step>Hook into JWT bearer events to load the current session and principal token version after signature validation.</step>
      <step>Reject revoked, expired, replaced, or version-mismatched sessions.</step>
      <step>Preserve existing admin policies and extend authorization wiring for customer-protected flows where needed.</step>
    </steps>
  </task>
  <task id="P6-02-T4" title="Define cookie and auth pipeline conventions">
    <files>
      <file>backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs</file>
      <file>backend/src/RentACar.API/Authentication/*</file>
    </files>
    <steps>
      <step>Standardize secure refresh-token cookie settings for frontend integration.</step>
      <step>Document or codify role claims and policy expectations for Guest, Customer, Admin, and SuperAdmin boundaries.</step>
    </steps>
  </task>
</tasks>

## Must Haves

- Access tokens expire in 15 minutes.
- Refresh token flow supports 7-day lifetime and rotation on every refresh.
- JWT validation alone is not the final authority; session state is checked against the database.
- Existing `AdminOnly` and `SuperAdminOnly` behavior remains intact.
- Refresh tokens are `HttpOnly`/secure ready for frontend consumption.

## Verification Criteria

- `JwtTokenService` can issue tokens with `sid`, `ver`, and role claims for both admins and customers.
- Auth pipeline rejects revoked sessions before controller code runs.
- Refresh-token helper code never stores raw token values in persistence.
- Configuration values and cookie conventions are explicit and not hardcoded ad hoc in controllers.

