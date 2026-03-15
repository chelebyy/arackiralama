# S02: JWT & Session Infrastructure

**Goal:** token generation and refresh logic implemented
**Demo:** tokens issued with sid/ver claims and rejected if session is revoked

## Must-Haves
- Access tokens expire in 15 minutes.
- Refresh token flow supports 7-day lifetime and rotation on every refresh.
- JWT validation alone is not the final authority; session state is checked against the database.
- Existing `AdminOnly` and `SuperAdminOnly` behavior remains intact.
- Refresh tokens are `HttpOnly`/secure ready for frontend consumption.

## Tasks

- [ ] **T1: Generalize JWT issuance for both principal types** `depends:[]`
  > Refactor `JwtTokenService` to create access tokens for customers and admins from a shared claim model. Use 15-minute lifetime and add `sid` plus `ver` claims.
- [ ] **T2: Add refresh-token generation and hashing helpers** `depends:[T1]`
  > Create cryptographically secure refresh-token generation. Add hashing and comparison helpers for safe storage. Support rotation and replay detection.
- [ ] **T3: Wire application-level session validation into auth pipeline** `depends:[T2]`
  > Hook into JWT bearer events to load the current session and principal token version after signature validation. Reject revoked, expired, or version-mismatched sessions.
- [ ] **T4: Define cookie and auth pipeline conventions** `depends:[T3]`
  > Standardize secure refresh-token cookie settings for frontend integration. Codify role claims and policy expectations for Guest, Customer, Admin, and SuperAdmin.

## Files Likely Touched
- `backend/src/RentACar.API/Services/IJwtTokenService.cs`
- `backend/src/RentACar.API/Services/JwtTokenService.cs`
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`
- `backend/src/RentACar.API/Options/JwtOptions.cs`
- `backend/src/RentACar.API/Authentication/*`
- `backend/src/RentACar.Infrastructure/Security/*`
