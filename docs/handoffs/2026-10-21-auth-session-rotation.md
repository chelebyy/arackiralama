# Atomic refresh rotation and credential version binding

Scope: Codex Security finding `csf_3b769ccad3969fe3776c08d3`, occurrence `occ_fd1808a2e12948fd6c38a432`, reviewed against `1a352c984972a3c6a705b10073563eca470b92fc`.

## Required behavior

- One active refresh token can produce at most one committed replacement session, including concurrent requests across API instances.
- A refresh session retains its original credential version. Password reset or an existing account change that increments `TokenVersion` prevents that session and its descendants from authenticating.
- Admin, SuperAdmin, and customer login/refresh response contracts, cookie settings, and generic unauthorized responses remain unchanged.

## Implementation

Both refresh controllers call `IAuthSessionRotationService`. The implementation conditionally consumes the parent with an affected-row check and inserts the replacement in the same relational database transaction. Failed insertion rolls back consumption. No process-local lock or nonrelational production fallback is used.

Login captures `TokenVersion` from the same loaded principal used for password verification and JWT issuance. Refresh checks that version against the principal and copies it to the child. Access validation checks the JWT against both the session and current principal version.

No global concurrency token was added to session revocation fields: password reset, admin updates, and logout can continue to revoke sessions without acquiring new optimistic-concurrency failure modes. A child inserted after reset's session query may still physically exist, but its old version cannot authenticate or refresh after reset commits.

## Migration and compatibility

`BindAuthSessionsToCredentialVersion` adds nullable `auth_sessions.token_version`. Existing sessions remain null and fail closed, requiring users to log in again. Backfilling the current principal version would incorrectly bless surviving old sessions, so it is deliberately avoided.

Apply the migration as part of the reviewed release before serving requests with the new application. This change does not apply a production migration or deploy. A code rollback would restore the prior refresh behavior and therefore reopen the finding.

## Verification

- Controller tests cover successful/failed atomic consumption, unchanged failure payloads, no cookie on failure, and null/stale/future session versions.
- Access-token tests reject a legacy or stale session even when the JWT has the current principal version.
- PostgreSQL tests use independent contexts and explicit barriers to exercise simultaneous redemption, reset before consumption, and child insertion after reset's revocation query, for both admin and customer accounts. They also exercise replay rejection, fresh login/refresh after reset, and rollback after child insertion fails.
- The repository CI runs the new PostgreSQL cases in its existing backend integration job.

Frontend refresh and identity-route failures still return 401, but do not emit cookie deletion headers: a late losing response cannot erase the successful rotation's newer credentials. Explicit logout continues to clear cookies. Route regression tests cover the losing response arriving after success.

The scope does not include concurrent credential writers losing an increment, reset-token reuse, or cross-tab refresh scheduling. Concurrent refresh callers still receive one success and one failure; the server never coalesces old-token replays into another successful redemption.
