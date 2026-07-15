# Codex Security Findings Implementation Plan

**Created:** 2026-07-12
**Scope:** Remediation of the seven findings validated against `ad6dcb7d359d66c4fc3d97fd6094f6667d02d4bb` and current `origin/main`
**Evidence:** `codex-security-findings-2026-07-12T16-07-36.816Z.csv` and the derived validation summary
**Status:** Approved implementation guidance; no finding is closed until its acceptance and release gates pass
**Delivery model:** Emergency containment followed by four reviewable implementation work packages

## 1. Objective

We need to remove the currently reachable account-claim, reservation disclosure/cancellation, payment-integrity, secret-exposure, and dependency-review weaknesses without weakening the existing public booking flow or mixing unrelated feature work into the security change.

This document is executable guidance. It defines ordering, change boundaries, security invariants, expected source locations, tests, rollout, rollback, and acceptance gates. A merged code change is not sufficient evidence of closure: each original vulnerable path must be revalidated against the resulting build and the deployed configuration.

## 2. Validated Finding Inventory

| ID | Finding | Current disposition | Implementation package |
| --- | --- | --- | --- |
| `05d3e4b9` | Unverified email claim exposes guest reservations | Reportable, critical | WP1 |
| `a6b2507e` | Public lookup exposes sensitive driver data | Reportable, high | WP2 |
| `16542709` | Production deployment defaults to mock payment | Reportable, high | WP3 |
| `3f489d71` | Tracked scan artifacts expose service tokens | Reportable, high | WP0 and WP4 |
| `151e6b3c` | Forged 3DS callbacks can mark reservations paid | Reportable, high | WP3 |
| `ca53a468` | Dependabot auto-merge bypasses human dependency review | Reportable, high | WP4 |
| `6bf4756e` | Unauthenticated reservation PII access and cancellation | Reportable, high | WP2 |

The seven rows represent five root-control families. We will preserve every row in verification so that grouping does not hide an unclosed instance.

## 3. Security Gap Analysis Checkpoint

The planning checkpoint found the following gaps. Items marked as unknown require operational confirmation and cannot be closed through source changes alone.

| Boundary | Observed gap | Required control | Proof required |
| --- | --- | --- | --- |
| Guest customer to authenticated customer | Knowing an email is enough to install a password on an existing guest record | Single-use, expiring proof of email control before credential installation | Negative and positive integration tests plus token replay test |
| Public reservation lookup | Public code returns the broad internal `ReservationDto` | Dedicated allowlisted public response or authenticated owner response | Contract test proving sensitive fields are absent |
| Public cancellation | GUID alone reaches reservation state mutation | Authenticated customer ownership or a separately issued scoped action capability | Anonymous/foreign-customer rejection tests |
| Browser to payment state | Arbitrary `BankResponse` reaches provider success and `Paid` | Provider-authenticated, amount/currency/intent-bound verification | Forged callback rejection and real sandbox success evidence |
| Production configuration | Missing/unknown provider silently selects Mock | Production startup validation that fails closed | Container startup-failure test and production config checklist |
| Repository to service credentials | Generated reports contain cleartext tokens and remain tracked | Rotation, history treatment, artifact exclusion, secret scanning | Provider-side rotation record and repository/history scan |
| Dependency update to `main` | Patch/minor updates can auto-approve and auto-merge | Human dependency review or a narrowly scoped allowlist with independent policy | Workflow review and branch-protection evidence |
| Logging and audit | Security flows may log email or provider payloads | PII-minimized structured audit actions | Log assertions and manual review |

### 3.1 Reviewed Areas

- Customer registration and customer reservation authorization.
- Public reservation read and cancellation routes.
- Payment intent completion, provider selection, mock/Iyzico verification, and production Compose settings.
- Tracked Ship Safe artifacts.
- Dependabot auto-merge workflow.

### 3.2 Unreviewed or Operationally Unknown Areas

- Whether exposed Resend and Upstash credentials have already been revoked.
- The actual Dokploy production environment values.
- The selected real payment provider's current API contract and webhook/3DS verification procedure.
- GitHub branch protection, environment protection, and required-review settings.
- Provider-side access logs and possible use of exposed credentials.

These unknowns are release gates, not reasons to suppress the findings.

## 4. Non-Negotiable Security Invariants

1. Knowledge of an email address must never install credentials on an existing customer record.
2. Authentication must be bound to the customer who completed an out-of-band proof, not merely to a matching normalized email.
3. Public reservation responses must be allowlisted and must not reuse internal/admin DTOs.
4. Reservation cancellation must require an authenticated owner or a dedicated, short-lived, single-purpose capability.
5. A reservation may enter `Paid` only after server-to-server verification binds provider transaction, local intent, reservation, amount, currency, and a successful provider state.
6. Production must refuse startup when payment configuration is absent, unknown, mock, sandbox, or incomplete.
7. Generated security artifacts must never contain or commit cleartext secrets.
8. Dependency changes must not satisfy their own human-review requirement.
9. Security logs must record action, result, actor type, and correlation data without tokens, raw payment payloads, identity numbers, or unnecessary email/phone values.

## 5. Change Boundaries

- Preserve unrelated reservation-extra, admin UX, pricing, public-content, and user-owned untracked work.
- Do not redesign the entire authentication subsystem while fixing guest account claim.
- Do not introduce a new payment provider library until its official integration contract and version are selected and reviewed.
- Do not keep the vulnerable endpoint behind an undocumented frontend convention; controls must be enforced by the API.
- Do not mark findings resolved from unit tests alone. Relevant integration, Docker, browser, configuration, and operational gates remain explicit.
- Do not include generated scan reports, secrets, provider payloads, or production data in test fixtures.

## 6. Delivery Sequence

```mermaid
flowchart LR
    A["WP0: Emergency containment"] --> B["WP1: Verified guest-account claim"]
    A --> C["WP2: Reservation access boundary"]
    A --> D["WP3: Payment integrity"]
    B --> E["WP4: Repository and CI hardening"]
    C --> E
    D --> E
    E --> F["WP5: Full revalidation and release decision"]
```

WP1, WP2, and WP3 may be implemented on separate branches after WP0. They should remain separate PRs unless a shared migration makes separation unsafe. WP4 and WP5 must evaluate the combined result.

## 7. WP0 - Emergency Containment

### 7.1 Purpose

Reduce exposure before the complete code solution is ready. This package includes operational actions and the smallest reversible source/config changes.

### 7.2 Actions

1. Revoke and replace every Resend and Upstash credential present in current files or Git history.
2. Review Resend and Upstash access/activity logs from the first exposed commit through rotation time. Escalate unexplained usage as an incident.
3. Remove `.ship-safe/context.json`, `.ship-safe/history.json`, and `ship-safe-report.html` from tracking. Preserve any needed sanitized report outside the repository.
4. Add generated Ship Safe paths and equivalent security-report outputs to `.gitignore`.
5. Run a history-aware secret scanner. Decide whether history rewriting is necessary based on repository visibility, clone population, and rotation completion; rotation remains mandatory either way.
6. Disable public payment completion in production until WP3 is deployed. Prefer an explicit feature flag that defaults to disabled in Production and returns `503` without mutating state.
7. Disable the anonymous public cancellation route immediately. The authenticated customer route remains available.
8. Temporarily stop Dependabot auto-merge while WP4 policy is reviewed.

### 7.3 Acceptance Criteria

- Old credentials are rejected by their providers and replacement values exist only in approved secret storage.
- Tracked files and the current working tree contain no matching live secret values.
- Anonymous public cancellation cannot reach `CancelReservationAsync`.
- Production cannot complete a payment while the emergency disable flag is active.
- Dependabot PRs require manual action.

### 7.4 Rollback

Do not roll back credential rotation. The payment/cancellation containment flags may be reverted only after WP2/WP3 acceptance passes. Generated secret-bearing artifacts must not be reintroduced.

## 8. WP1 - Verified Guest-Account Claim

### 8.1 Design Decision

We will separate registration from claiming an existing passwordless guest customer. A request containing an email may initiate a claim, but only possession of a single-use email token may install the first password on the existing customer.

### 8.2 Domain and Persistence

Add a purpose-specific entity such as `CustomerAccountClaimToken` rather than overloading password-reset semantics. Persist:

- `Id`;
- `CustomerId`;
- cryptographic token hash, never the raw token;
- `ExpiresAtUtc`;
- `UsedAtUtc`;
- `CreatedAtUtc`;
- bounded request metadata needed for abuse monitoring, excluding raw secrets and unnecessary PII.

Add an index supporting active-token lookup and cleanup. Token lifetime should be short and configuration-bound. Issuing a new token should invalidate or supersede previous unused claim tokens for the same customer.

Expected anchors:

- `backend/src/RentACar.Core/Entities/`
- `backend/src/RentACar.Infrastructure/Data/Configurations/`
- `backend/src/RentACar.Infrastructure/Data/Migrations/`
- `backend/src/RentACar.API/Controllers/CustomerAuthController.cs`
- `backend/src/RentACar.API/Contracts/Auth/`
- existing email service and background-delivery patterns

### 8.3 API Flow

1. `POST /api/customer/v1/auth/register` keeps a generic response for all cases.
2. New email: create a new customer according to the chosen email-verification policy. Do not weaken existing password validation.
3. Existing customer with a password: do not change credentials; return the same generic response.
4. Existing customer without a password: create and email a claim token; do not change profile or `PasswordHash`.
5. Add `POST /api/customer/v1/auth/claim` accepting the raw one-time token and new password.
6. In one transaction, validate token hash, purpose, expiry, unused state, customer state, and concurrency; then install the password and consume all active claim tokens.
7. Do not copy attacker-supplied profile fields onto an existing guest record during claim initiation. Profile updates happen only after authenticated login.
8. Revoke existing customer sessions if future flows permit credential replacement; first-time claim normally has no prior authenticated session.

### 8.4 Abuse Controls

- Strict IP and normalized-account rate limits.
- Generic responses and near-uniform behavior to reduce account enumeration.
- Audit events: `CustomerClaimRequested`, `CustomerClaimCompleted`, `CustomerClaimRejected` with non-sensitive identifiers.
- No raw token, password, email body, identity number, or phone in logs.
- Background cleanup for expired tokens.

### 8.5 Required Tests

- Knowing a guest customer's email cannot set or change `PasswordHash`.
- A valid claim token installs a password for the intended customer only.
- Expired, used, malformed, wrong-customer, and replayed tokens fail.
- Concurrent use permits exactly one successful claim.
- Initiation does not overwrite name, phone, identity number, birth date, nationality, or license fields.
- Existing registered email returns the same public response and does not change credentials.
- Successful claim permits login and access only to that customer's reservations.
- Logs and queued email metadata contain no raw password or stored token.

### 8.6 Exit Gate

Re-run the original critical attack path through the real HTTP API with PostgreSQL and the configured email test double. The attacker who knows only the victim email must not obtain a session or alter the victim record.

## 9. WP2 - Reservation Read and Mutation Boundary

### 9.1 Public Read Contract

Create a dedicated `PublicReservationSummaryDto` and a dedicated mapper/query. Do not map to `ReservationDto` and remove properties afterward.

The public response should contain only fields needed for a confirmation/status page, for example:

- public reservation code;
- coarse reservation status;
- pickup/return office display names;
- pickup/return timestamps;
- public vehicle-group display data;
- currency and customer-facing total if product requirements require it.

It must exclude internal GUIDs, customer ID/name/email/phone, plate, driver data, identity/license fields, customer statistics, notes, hold session IDs, internal pricing metadata, and payment/provider identifiers.

Expected anchors:

- `backend/src/RentACar.API/Contracts/Reservations/ReservationDtos.cs`
- `backend/src/RentACar.API/Controllers/ReservationsController.cs`
- `backend/src/RentACar.API/Services/IReservationService.cs`
- `backend/src/RentACar.API/Services/ReservationService.cs`
- `backend/src/RentACar.Infrastructure/Repositories/ReservationRepository.cs`

### 9.2 Cancellation Decision

Remove `POST /api/v1/reservations/{reservationId}/cancel` from the anonymous controller. Use the existing authenticated customer endpoint as the supported self-service path:

- require `CustomerOnly` policy;
- load the reservation;
- compare `reservation.CustomerId` with the validated token subject;
- return the same not-found response for absent and foreign resources;
- enforce cancellable-state business rules in the service;
- make concurrent/repeated cancellation idempotent or deterministically rejected.

If the product later requires cancellation without an account, design a separate short-lived cancellation capability bound to reservation, action, expiry, and one-time use. A public code or reservation GUID must not serve as that capability.

### 9.3 Public-Code Controls

- Confirm public codes are generated with cryptographically strong randomness and sufficient entropy.
- Apply strict rate limiting and uniform not-found behavior.
- Do not log full public codes; use a bounded fingerprint when correlation is necessary.
- Add cache headers preventing shared/proxy storage of reservation responses.

### 9.4 Required Tests

- Anonymous public lookup response schema contains none of the forbidden fields.
- Serialized nested objects also contain no driver, customer, hold-session, note, or provider data.
- Anonymous public cancellation returns `404` or `405` and performs no write.
- Authenticated owner cancellation succeeds for allowed states.
- Foreign customer cancellation returns not found and performs no write.
- Admin cancellation continues through the admin-only route.
- Public code enumeration is rate-limited and responses are not cacheable.
- Existing public confirmation UI renders correctly using the minimal contract in all five locales.

### 9.5 Exit Gate

Run API contract tests and Docker/Chromium confirmation-page tests. Capture a sanitized response schema as evidence. Revalidate both original PII findings and the anonymous cancellation finding separately.

## 10. WP3 - Payment Integrity and Production Fail-Closed Configuration

### 10.1 Immediate Architecture Rule

Mock and sandbox implementations are development/test capabilities only. They must be structurally unavailable in Production. Selecting an unknown provider must throw during startup instead of falling back to Mock.

### 10.2 Configuration Validation

Add startup validation covering:

- provider is explicitly configured;
- provider name is in an allowlist;
- `Mock` is rejected outside Development/Test;
- sandbox base URLs are rejected in Production;
- required API, secret, webhook, and callback configuration exists;
- callback/public base URLs are HTTPS and belong to configured hosts;
- no default webhook secret is accepted in Production.

Update:

- `backend/src/RentACar.Infrastructure/Services/Payments/PaymentOptions.cs`
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`
- `backend/src/RentACar.API/appsettings*.json`
- `docker-compose.yml`
- `.env.example`
- `deploy/dokploy-setup.md`

Use an explicit provider switch that throws for unknown values. Compose must pass the selected provider and required settings from secret-backed environment variables.

### 10.3 Provider Verification Contract

Replace the current trust in arbitrary `BankResponse` with server-to-server verification defined by the selected provider's official contract. Before coding this adapter, fetch and review the current official provider documentation and pin the supported SDK/API version.

A successful verification result must bind:

- configured provider;
- local payment intent ID;
- stored provider intent/conversation ID;
- reservation ID;
- expected amount and currency;
- successful/settled provider status;
- unique provider transaction ID;
- callback freshness or provider event identity where applicable.

The API must reject mismatches before any reservation mutation. Raw callback payloads must not become trusted state merely because they are non-empty.

### 10.4 State Transition and Idempotency

- Implement an explicit allowed transition from pending payment to paid.
- Process completion in a transaction with concurrency protection.
- Make repeated verified events idempotent by provider event/transaction ID.
- Reject attempts against expired, failed, cancelled, already-refunded, or unrelated intents.
- Record sanitized verification outcome and correlation IDs, not full card/provider payloads.
- Keep webhook signature, timestamp, replay, and provider-name validation independent from browser return handling.

### 10.5 Endpoint Strategy

Preferred design:

1. Browser return is a navigation/status signal only.
2. The backend queries the provider or consumes a signed provider webhook.
3. Only verified server-side evidence can call the paid transition.
4. The frontend polls or fetches final status from an owner-scoped endpoint.

If the provider requires a browser-posted token, accept only the provider-issued opaque token and verify it server-to-server. Do not accept an arbitrary status string or raw success flag.

### 10.6 Required Tests

- Production startup fails when provider is missing, Mock, unknown, sandbox, or incompletely configured.
- Development/Test can intentionally select Mock.
- Arbitrary `BankResponse = "ok"` cannot mark an intent or reservation succeeded.
- Provider intent, amount, currency, reservation, or transaction mismatch is rejected.
- Invalid signature, stale timestamp, replayed event, and wrong provider are rejected.
- A verified sandbox payment makes exactly one valid paid transition.
- Concurrent duplicate completion remains idempotent.
- Failed/cancelled provider status never becomes paid.
- Refund/deposit behavior remains consistent with the verified transaction.
- No sensitive provider payload or credential is logged.

### 10.7 Exit Gate

WP3 is not complete with Mock tests. Required proof is:

- focused unit and integration tests;
- Production configuration failure tests;
- Docker startup with production-like secret injection;
- one successful and multiple negative transactions against the selected provider's sandbox;
- database evidence that forged/mismatched callbacks produced no paid transition.

## 11. WP4 - Repository, Secret, and Dependency Workflow Hardening

### 11.1 Secret Artifact Controls

- Add generated scanner directories/reports to `.gitignore`.
- Configure scanners to redact matched values in machine and HTML outputs.
- Store durable reports as sanitized CI artifacts with bounded retention, not tracked source files.
- Add pre-commit or CI secret scanning over the working tree and Git history appropriate to the repository policy.
- Document rotation ownership, provider audit steps, and incident escalation.
- Verify test fixtures use unmistakably non-live values.

### 11.2 Dependabot Policy

Recommended baseline: remove auto-approval and automatic merging for application and GitHub Action dependency updates. Dependabot may open PRs and attach metadata, but a human review must remain required.

If a future exception is approved, it must be narrower than semver patch/minor alone and should require:

- an explicit package allowlist;
- non-runtime/development-only classification where possible;
- immutable SHA pinning for GitHub Actions;
- full required checks;
- dependency diff/license/security review;
- branch protection that the workflow token cannot satisfy by self-approval;
- an emergency deny switch.

Do not use `pull_request_target` with untrusted PR checkout or execution. Even without checkout, keep token permissions minimal and avoid granting merge authority when the workflow's only evidence is Dependabot metadata.

### 11.3 Required Tests and Evidence

- Secret scan returns no live or historical unrotated credential findings.
- Generated security outputs are ignored and sanitized.
- A test Dependabot patch/minor PR cannot merge without human approval.
- Major and security-sensitive dependency changes remain manual.
- GitHub Actions are pinned according to the selected repository policy.
- Branch protection and required-review settings are captured as operational evidence.

## 12. WP5 - Combined Verification and Release Gate

### 12.1 Automated Validation

Run at minimum:

```powershell
dotnet restore backend/RentACar.sln --configfile backend/NuGet.Config
dotnet build backend/RentACar.sln --no-restore
dotnet test backend/RentACar.sln --no-build
corepack pnpm -C frontend lint
corepack pnpm -C frontend test
corepack pnpm -C frontend build
```

Add focused security filters for account claim, reservation authorization/contracts, payment verification/configuration, and secret/log hygiene so failures are visible without running the entire suite.

### 12.2 Docker and Browser Validation

- Build the exact production-like API and frontend images.
- Confirm invalid payment configuration makes the API fail closed with a clear non-secret error.
- Confirm valid secret-injected configuration reaches healthy state.
- Exercise guest reservation, account claim, login, owner reservation view, public confirmation view, cancellation, paid booking, failed payment, and forged callback scenarios.
- Inspect browser network payloads to confirm PII is absent from public responses.
- Test desktop, tablet, and mobile confirmation/payment flows.

### 12.3 Finding Closure Matrix

| Finding | Required closure evidence |
| --- | --- |
| Guest email claim | Real HTTP attack regression, token expiry/replay/concurrency tests, no profile overwrite |
| Public driver PII | Serialized allowlist contract and browser network evidence |
| Production Mock | Production startup rejection and valid configured startup |
| Secret artifacts | Provider rotation, access-log review, tracked/history scan |
| Forged 3DS | Forged/mismatch negative tests and real sandbox success |
| Dependabot auto-merge | Workflow behavior plus branch-protection evidence |
| Public PII/cancel | Anonymous read schema and no-write cancellation proof |

### 12.4 Release Decision

Release is blocked while any of these remain true:

- exposed credential rotation is unconfirmed;
- anonymous cancellation remains reachable;
- existing guest records can receive a password without email proof;
- production can resolve Mock or sandbox payment behavior;
- arbitrary client data can drive a paid transition;
- public reservation responses include forbidden fields;
- the security-focused Docker/browser matrix has not run;
- operational GitHub and provider evidence is missing.

We must report implementation-complete, acceptance-complete, and release-ready as separate states.

## 13. Suggested PR and Commit Structure

| PR | Scope | Suggested commit |
| --- | --- | --- |
| PR A | WP0 containment and secret-artifact removal | `fix(security): contain exposed reservation and payment paths` |
| PR B | WP1 verified account claim | `fix(auth): require verified guest account claim` |
| PR C | WP2 public reservation boundary | `fix(reservations): enforce public data and ownership boundaries` |
| PR D | WP3 production payment verification | `fix(payments): enforce provider-verified paid transitions` |
| PR E | WP4 workflow and repository hardening | `fix(security): harden secret and dependency workflows` |
| PR F | Combined regression evidence/docs if needed | `test(security): validate remediated attack paths` |

Do not mix feature work into these PRs. Every PR description should map changed controls to finding IDs, include test evidence, state remaining operational gates, and avoid claiming unrelated security coverage.

## 14. Rollout and Rollback Strategy

### 14.1 Rollout

1. Complete credential rotation and containment before publishing code that references replacement secrets.
2. Deploy database additions for account-claim tokens additively.
3. Deploy backend account/reservation changes before frontend flows that depend on them.
4. Keep payment disabled until production configuration validation and provider sandbox proof pass.
5. Deploy payment changes behind a default-off production feature flag.
6. Enable for internal/test reservations, observe verification failures and state transitions, then expand deliberately.
7. Re-run the complete closure matrix after deployment.

### 14.2 Rollback

- Account claim: disable claim initiation/completion while retaining the additive token table; do not restore direct guest upgrade.
- Reservation boundary: disable public lookup if the minimal response breaks the UI; do not restore broad DTO or anonymous cancellation.
- Payment: disable payment completion and return to unpaid/manual handling; do not restore Mock in Production or arbitrary 3DS success.
- Secret handling: never restore old credentials or tracked cleartext reports.
- Dependabot: retain manual review if automation fails.

## 15. Observability

Add bounded metrics and alerts for:

- claim requested/completed/rejected/expired/replayed;
- public reservation lookup rate and throttling, without full codes or PII;
- owner/foreign cancellation attempts;
- payment verification success/failure/mismatch/replay;
- blocked production startup caused by unsafe payment configuration;
- webhook signature/timestamp/provider rejection;
- secret-scan and dependency-policy failures.

Alerts must avoid credentials, tokens, raw callbacks, identity numbers, driver data, email addresses, phone numbers, and full public reservation codes.

## 16. Definition of Done

The work is done only when:

- all five work packages are implemented and reviewed;
- all seven original finding instances have explicit closure evidence;
- backend and frontend full suites pass;
- production-like Docker build and browser scenarios pass;
- provider sandbox verification and production fail-closed tests pass;
- credential rotation and access-log review are documented outside Git;
- secret/history scanning is clean for active credentials;
- Dependabot cannot merge without an up-to-date branch, the required status checks, resolved review threads, and a manual merge decision;
- deployment, rollback, and monitoring evidence is attached;
- a focused post-implementation security validation finds no surviving original attack path.

Completion of this plan does not imply that the repository has received a complete security audit or is production-safe in unreviewed areas.

## 17. Open Decisions Before Coding

1. Which email delivery service and public frontend route will handle the account-claim link?
2. Must all newly registered customers verify email, or only existing guest records being claimed?
3. Which fields are strictly required on the unauthenticated confirmation page?
4. Is unauthenticated cancellation a product requirement? If yes, who owns the scoped capability lifecycle?
5. Which real payment provider/API version is selected, and what is its authoritative verification mechanism?
6. Is Git history rewriting required after rotation, given repository visibility and existing clones?
7. Resolved: Dependabot PRs must satisfy the active `Protect main - solo developer` repository ruleset, including a current branch, all seven required checks, resolved review threads, and a manual squash-merge decision. The required approving-review count is zero for the solo-developer workflow.

Unresolved decisions remain gates for their affected work packages. WP0 may proceed, but a provider-dependent design slice must not be treated as implementation-ready until its relevant decision is recorded.

## 18. Implementation Status Snapshot (15 July 2026)

| Work package | State | Implemented | Still required |
| --- | --- | --- | --- |
| WP0 | Partial | Anonymous cancellation removed; payment kill switch defaults off and covers intent, 3DS return, webhook, and admin retry paths; Dependabot auto-merge removed; generated Ship-Safe artifacts removed/ignored; PR #402 merged after the final exact-head Codex review and all required checks; post-merge `main` CI and GHCR publication succeeded | Credential rotation and access-log evidence; production deployment rerun |
| WP1 | Backend implemented, acceptance partial | Purpose-specific hashed/expiring/single-use tokens, supersession, generic response, five-minute normalized-account cooldown, database-enforced single-active-token concurrency boundary, relational conditional token consumption plus password update in one transaction, bounded metadata, 14-day/200-row worker cleanup, absolute configured claim links, claim page, focused tests, and self-cleaning Docker Chromium proof across all five locales | Resend integration and real production email-delivery proof remain deferred; before email delivery is enabled, set `Notifications__PublicFrontendBaseUrl` to the HTTPS public-site origin |
| WP2 | Locally acceptance-proven | Allowlisted public DTO, public frontend client paths limited to that DTO, strict rate limit, no-store, anonymous cancellation removal, owner/admin paths preserved; production-like Chromium confirms the exact public allowlist through all five locale pages and proves anonymous/non-owner no-write plus owner cancellation; final PR #402 review found no major issue at the reviewed head | Deployment rerun |
| WP3 | Deferred and contained | No payment provider is selected yet; payments default disabled; production `ValidateOnStart` rejects unsafe provider configuration while allowing a fully configured real provider to boot with the kill switch off; intent, 3DS return, and webhook paths return `503` without mutation | When payments are introduced: select provider/API version, implement authoritative server-to-server verification, mismatch/replay tests, and sandbox success |
| WP4 | Repository controls active; operational evidence partial | Generated artifact ignore/removal and auto-merge workflow removal; Gitleaks scans the working tree and full Git history and passed on PR #402 and post-merge `main`; active ruleset `Protect main - solo developer` requires a pull request, resolved threads, a current branch, seven strict checks, squash merge, and blocks deletion/non-fast-forward updates without bypass actors | Provider credential rotation/access-log evidence; triage and remediate or explicitly risk-accept the 11 open Dependabot alerts; observe one Dependabot PR created or refreshed after ruleset activation through required checks and a manual merge/close decision |
| WP5 | Blocked | Backend build and full backend suites pass; frontend lint/typecheck/test/build pass; the production payment `ValidateOnStart` host-start and production-like Docker startup/healthy-state matrices pass locally; local Docker disabled-payment HTTP/no-write proof passes; account-claim and public reservation/cancellation Chromium proofs pass across all five locales; PR #402 exact-head review and post-merge `main` workflows pass | Provider credential/access-log evidence, current dependency-alert triage/remediation, and deployment rerun; real-provider sandbox evidence remains deferred until payments are introduced |

### 18.1 Fresh Automated Evidence

- Backend build: 0 warnings, 0 errors.
- Account-claim concurrency regression: before the fix, two concurrent PostgreSQL HTTP requests both returned `200`; after the relational conditional-update transaction, the same test permits exactly one `200` and one `400`.
- Public tracking UI regression: the client consumes only `PublicReservationSummary` fields and the component test proves surplus customer name/email data is not rendered.
- Claim-link regression: queued account-claim links are absolute, use the configured public frontend origin, preserve locale routing, and reject relative origins.
- Production payment startup regression: a fully configured real Iyzico provider can boot with `EnablePayments=false`; missing, Mock, unknown, sandbox, and incomplete provider configurations remain rejected.
- Focused payment configuration tests: 13/13 passed. The real host-start path rejects missing, Mock, unknown, sandbox, and incomplete Production configurations; a fully configured Iyzico Production host, the same host with payments intentionally disabled, and an intentional Development Mock host start successfully.
- Production-like Docker payment configuration matrix passed against the current Release image. Missing, Mock, unknown, sandbox, and incomplete Production configurations each exited non-zero (`139`) with the expected general validation error and no synthetic credential value in logs. A syntactically valid, synthetic secret-injected Iyzico configuration stayed running and returned `/health` `200`; migration and local seeds were disabled, and the selected database count fingerprint remained unchanged.
- Focused Codex-review follow-up unit tests: 44/44 passed.
- Final PR #402 head validation: targeted payment/settings/reservation tests passed 133/133; backend build passed with 0 warnings and 0 errors; full backend passed 794/794 unit and 53/53 API integration tests; scoped risky-change review found no material concern in the final payment follow-up.
- The final Codex review of PR #402 at head `4371fce226` reported no major issue, and the PR was squash-merged as `f0da549b90fc4646267f3a027370c4d2e0a67b90`.
- Gitleaks full-history scan passed locally with the repository policy. A known historical fixture still makes the detector exit non-zero outside that policy, and the CI artifact is explicitly reduced to rule, location, commit, and fingerprint metadata before its seven-day upload.
- `git diff --check`: no whitespace errors.
- Frontend lint passed with 0 errors and 1 existing warning; Vitest passed 299/299 tests; Next.js production build passed on the final PR #402 head.
- Local Docker disabled-payment proof: intent creation, forged 3DS return, and forged webhook each returned `503`; `payment_intents` and payment-webhook job counts remained `4,0` before and after.
- Local Docker account-claim proof: `account-claim-security.spec.ts` passed in Chromium; all five localized claim pages rendered, the queued link completed once, replay returned `400`, the new credential logged in successfully, ignored registration profile fields remained unchanged, and the isolated customer/token/job/session/audit rows were removed in `finally`.
- Account-claim abuse/retention proof: focused tests passed 29/29; the current full backend run passed 774/774 unit and 52/52 API integration tests; two simultaneous Docker registration requests returned `200,200` while persisting one active token and one email job; the worker deleted an isolated 20-day-old token; migration `20260712214328_HardenAccountClaimAbuseControls` and its partial unique index were verified in Docker PostgreSQL.
- Public reservation/cancellation proof: the production-like Docker build completed and `reservation-boundary-security.spec.ts` passed 1/1 in Chromium. The `tr`, `en`, `ru`, `ar`, and `de` confirmation pages each fetched a response with exactly `publicCode`, `status`, `pickupOfficeName`, `returnOfficeName`, `pickupDateTime`, `returnDateTime`, `vehicleGroupName`, `totalAmount`, `depositAmount`, and `currency`; isolated PII/internal values were absent and `Cache-Control: no-store` remained visible in browser network. Anonymous cancellation returned `404/405` and non-owner cancellation returned `404` without changing `status/xmin/updated_at`; owner cancellation returned `200` and persisted `Cancelled`. Cleanup left zero test-owned customer, reservation, background-job, and audit rows. Focused backend tests passed 103/103, focused frontend tests passed 5/5, TypeScript passed, and scoped ESLint passed.
- Post-merge `main` evidence: CI passed backend unit/integration, frontend lint/test/build, Docker build, and Docker Push to GHCR; Secret Scan, React Doctor, CodeQL, and Dependabot Updates also completed successfully.
- Repository governance evidence: active ruleset `Protect main - solo developer` (ID `18985047`) targets only `refs/heads/main`, has no bypass actors, requires pull requests with resolved review threads, uses a zero-approval solo-developer threshold, permits squash merge only, enforces an up-to-date branch and the seven named CI/security checks, and blocks deletion and non-fast-forward updates.
- Dependabot policy evidence: existing PR #401 is Git-object mergeable but reports `BEHIND` current `main`; strict required-check policy therefore prevents merge until it is refreshed and the required checks rerun. Because the PR predates ruleset activation, a complete post-ruleset Dependabot lifecycle remains to be observed.
- Live dependency-alert evidence: GitHub reports 11 open Dependabot alerts in `frontend/pnpm-lock.yaml` (3 high, 4 medium, 4 low). Ten, including all three high alerts, are development scope; one low `@babel/core` alert is runtime scope. Patched versions are listed for every alert. A local `pnpm audit` attempt did not produce an independent vulnerability result because the registry endpoints used by the current client returned HTTP `410` (`ERR_PNPM_AUDIT_BAD_RESPONSE`); this failure is not a clean scan.

### 18.2 Current Release Decision

No finding is declared fully closed by this snapshot. The account-claim abuse-control/cleanup gates and the public reservation/cancellation boundaries are locally proven; real email delivery is intentionally deferred until Resend is integrated. The original arbitrary-client-data-to-paid-state path is not reachable while payments remain disabled, and both the local host-start and production-like Docker startup matrices prove the configured `ValidateOnStart` boundary, but payments must not be enabled until provider-authenticated verification is implemented and proven. The repository-governance, remote secret-scan, final exact-head review, and post-merge `main` workflow gates are evidenced. Provider credential rotation/access-log evidence, explicit resolution of the current dependency-alert set, and the production deployment rerun remain release blockers; one post-ruleset Dependabot lifecycle should still be observed as operational assurance.
