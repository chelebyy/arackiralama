---
wave: 2
depends_on:
  - 01-auth-domain-persistence-PLAN.md
  - 02-token-session-infrastructure-PLAN.md
files_modified:
  - backend/src/RentACar.API/Controllers/AuthController.cs
  - backend/src/RentACar.API/Contracts/Auth/*
  - backend/src/RentACar.API/Services/Auth/*
  - backend/src/RentACar.API/Validators/Auth/*
  - backend/src/RentACar.Infrastructure/Services/*
  - backend/tests/**/*Auth*
autonomous: true
requirements:
  - AUTH-01
  - AUTH-02
  - AUTH-03
  - AUTH-04
  - AUTH-05
  - AUTH-06
  - AUTH-10
---

# Plan 03: Customer Auth API & Recovery Flows

## Objective

Implement customer-facing registration, login, refresh, logout, `me`, forgot-password, and reset-password flows on top of the new auth/session foundation.

## Tasks

<tasks>
  <task id="P6-03-T1" title="Create customer auth contracts and service layer">
    <files>
      <file>backend/src/RentACar.API/Contracts/Auth/*</file>
      <file>backend/src/RentACar.API/Services/Auth/*</file>
      <file>backend/src/RentACar.API/Validators/Auth/*</file>
    </files>
    <steps>
      <step>Define request/response contracts for register, login, refresh, logout, forgot-password, reset-password, and profile bootstrap.</step>
      <step>Keep controllers thin and move business rules into explicit auth services.</step>
      <step>Normalize emails, validate payloads, and return consistent API responses.</step>
    </steps>
  </task>
  <task id="P6-03-T2" title="Implement registration and login against customer records">
    <files>
      <file>backend/src/RentACar.API/Services/Auth/*</file>
      <file>backend/src/RentACar.API/Controllers/AuthController.cs</file>
    </files>
    <steps>
      <step>Allow registration by claiming or upgrading an existing guest-created customer row when the normalized email matches.</step>
      <step>Implement customer login with password verification, failed-attempt tracking, and automatic lockout after five failures.</step>
      <step>Reset failed-attempt counters on successful login and stamp last-login data.</step>
    </steps>
  </task>
  <task id="P6-03-T3" title="Implement session lifecycle endpoints">
    <files>
      <file>backend/src/RentACar.API/Services/Auth/*</file>
      <file>backend/src/RentACar.API/Controllers/AuthController.cs</file>
    </files>
    <steps>
      <step>Implement refresh endpoint that rotates the current refresh token and issues a new access token.</step>
      <step>Implement current-device logout by revoking only the active session.</step>
      <step>Add `me` or equivalent bootstrap endpoint so frontend can restore customer auth state predictably.</step>
    </steps>
  </task>
  <task id="P6-03-T4" title="Implement forgot-password and reset-password flow">
    <files>
      <file>backend/src/RentACar.API/Services/Auth/*</file>
      <file>backend/src/RentACar.API/Controllers/AuthController.cs</file>
      <file>backend/src/RentACar.Infrastructure/Services/*</file>
    </files>
    <steps>
      <step>Create opaque, hashed reset tokens with 30-minute expiry and generic forgot-password responses.</step>
      <step>Reset password with single-use validation, token consumption, session revocation, and token-version increment.</step>
      <step>Prepare concise, security-first reset email content and delivery interface integration for the next notification phase.</step>
    </steps>
  </task>
</tasks>

## Must Haves

- Registration works whether the customer record is brand new or guest-created from reservations.
- Login enforces 5 failed attempts and cooldown-based lockout.
- Refresh rotates on every use.
- Logout revokes only the current session.
- Password reset logs out all devices and does not reveal whether an email exists.

## Verification Criteria

- Customer register/login/refresh/logout/reset endpoints exist and follow the repo’s controller/service conventions.
- Password reset tokens expire after 30 minutes and are single-use.
- Login path records lockout state correctly after five failures.
- Successful refresh invalidates the previous refresh token.
- Unit/integration tests cover happy path plus invalid token, lockout, and password-reset revocation behavior.

