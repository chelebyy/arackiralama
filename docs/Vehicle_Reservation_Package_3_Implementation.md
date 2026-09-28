# Package 3: short checkout and guest reservation management

Publication handoff and next-session checklist: [Package 3 handoff](Vehicle_Reservation_Package_3_Handoff.md).

Status: implementation completed locally on `codex/guest-reservations`. Publication, remote CI, merge and deployment are separate gates.

## Delivered behavior

- Checkout collects contact details and an explicit age/licence-tenure declaration, with confirmation that valid documents will be presented at pickup. Full birth dates, identity numbers and licence numbers are ignored by reservation request binding and are no longer written by reservation creation/update paths.
- The legacy customer profile update path also ignores identity, birth-date and licence-year inputs. Sensitive legacy fields are omitted from serialized driver/profile responses. Historical database values remain intact; no data purge is included.
- Exact vehicle identity, current server pricing, Turkey date handling, minimum age/licence tenure, preparation occupancy and PostgreSQL overlap exclusion remain enforced. New exact reservation replay proofs use version 3. Version 2 attempts must restart checkout; they are not silently reinterpreted. Pricing quote schema remains version 2.
- `/{locale}/manage-reservation` supports reference/email verification, a private allowlisted booking view, cancellation and atomic date changes. Tracking and confirmation screens link to it. All five locales and Arabic RTL are supported; protected header/hero sources are unchanged.
- **Approved policy:** admins configure cancellation and date-change permissions, notice periods and fees on the pickup office. Missing, disabled or incomplete settings deny the corresponding operation. Only future confirmed payment-at-pickup bookings qualify.
- A cancellation requires the current reservation version and explicit fee acceptance. It changes status once, releases stock through the existing status rules, cancels pending reminders and records one audit event and one queued confirmation.
- A date-change quote preserves the exact vehicle, offices and extra quantities, uses current extra versions/prices and requires a fresh driver declaration. Confirmation checks reservation version, current policy, quote expiry, exact amount and availability inside the transaction.
- Previously accepted amendment fees remain in the new total and are shown separately. The new operation fee is added once. Prior financial snapshots are retained in amendment history. Response-loss retries return the saved result without another event or fee.
- Invalid, unavailable or expired extras/campaigns require operator resolution; no discount or replacement service is invented. The new flow does not collect online payments or issue refunds. Cancellation fees are accepted/audited for operator settlement.

## Access and notification boundaries

Verification uses a 128-bit random code with a 10-minute lifetime and at most five attempts. Only the digest is stored in the access table; the queue holds a Data Protection encrypted delivery copy. Valid exchange consumes the code and creates a 20-minute reservation-scoped session. Session and CSRF values are stored as digests. Changing the reservation customer's normalized email invalidates existing access.

The Next.js proxy allowlists routes, requires same-origin JSON for POST, checks CSRF, sets HttpOnly/SameSite=Strict cookies, marks responses no-store and strips session secrets from JSON. Cookies are Secure in production. It does not trust caller-supplied guest/authentication/forwarded-IP headers. Direct backend mutations validate both session and CSRF. GET cannot exchange a code or mutate a reservation.

Access requests use neutral responses, an existing IP rate limit and a database-enforced one-request-per-minute reservation cooldown. The IP rate limit is shared by clients behind the BFF address; production ingress/load testing must establish suitable trusted-proxy and abuse limits before activation.

Reservation changes, audit entries, reminder updates and queued messages commit together. Provider failures happen after commit and do not revert the booking. The dispatcher claims work atomically, recovers abandoned Processing jobs after five minutes, retries three times and records permanent failure. SMTP calls have a 30-second cancellation deadline. Delivery is **at least once**: an ambiguous SMTP response or a crashed sender can still cause a duplicate email. The booking operation remains idempotent.

API and Worker share Data Protection application name `RentACar.GuestReservations`. Configure the same durable `GuestAccess:KeyRingPath` and key protection in both hosts. `GuestAccess:CertificateThumbprint` supports certificate encryption at rest. Explicit file persistence disables automatic default key encryption; filesystem permissions and a protected certificate/private-key recovery procedure are deployment requirements. No real provider account, DNS, certificate or production volume was configured.

## Validation

| Check | Local result |
|---|---|
| Backend unit suite | 896 passed, including a loopback-only SMTP delivery test |
| Existing exact quote/hold API cases | 70 passed against isolated PostgreSQL and Redis |
| New guest API/service/queue cases | 19 passed against isolated PostgreSQL and Redis |
| Frontend Vitest | 71 files, 359 tests passed |
| TypeScript / production Webpack build | Passed; final verification recorded in the task |
| ESLint | No errors; one pre-existing SearchForm.test.tsx suppression warning |
| Browser acceptance | 12 Chromium desktop/mobile cases across five locales |
| EF model/migration | Pending-model check passed; additive migration applied in isolated test fixtures |

Browser tests use controlled API responses for UI acceptance. Real database/service/HTTP integration and loopback SMTP tests are separate evidence; they are not a single deployed browser-to-mail end-to-end run. Physical devices, screen readers and a production-sized restored database were not tested.

Integration coverage includes wrong/expired/exhausted/replayed codes, session revocation, missing CSRF, neutral unknown lookup, disallowed policy/fees, cancellation replay, current extra repricing, accumulated change fees, immutable history, expired/policy-changed/price-changed/overlapping offers, two concurrent amendments, cancellation versus amendment, response-loss retries, mail retries/permanent failure and concurrent/recovered mail dispatch.

Build warnings include existing frontend middleware/Edge/root-lockfile notices and backend package-pruning warnings from the shared ASP.NET Core framework reference. No dependency versions were upgraded.

## Migration and data handling

`20260927141937_GuestReservationManagement` adds guest access and amendment tables, indexes and foreign keys. It does not delete customer fields or change accepted reservation amounts. The model snapshot and migration Designer are included. Reverting this migration after use would discard new access/history tables; use a reviewed restore/forward-fix plan instead of an automatic production downgrade.

See [data handling and key lifecycle](Vehicle_Reservation_Package_3_Data_Handling.md) and the [count-only inventory query](Vehicle_Reservation_Package_3_Inventory.sql). The structural inventory is complete; no production customer rows or backup contents were inspected. Contact fields are not newly field-encrypted by this change; the staged encryption design and operator retention decision are explicit activation gates.

## Scoped security review and release checklist

Reviewed: new guest endpoints/BFF, session/code handling, explicit amount acceptance, state/version checks, transaction and stock-conflict boundaries, sensitive request binding, notification claims and new public UI. Material findings fixed during implementation include the old hold replay-version check, legacy profile collection, API/Worker key isolation, stale extra versions and loss of previously accepted amendment fees.

Not reviewed: the whole application, deployment ingress, production secrets/key storage, real email deliverability, dependency vulnerabilities or legal compliance. This is not a security certification. An independent focused security review is available before release.

Before authorized release:
1. Run remote CI and review the exact publication commit.
2. Migrate a representative restored database; prove backup/key restore with isolated synthetic mail.
3. Set operator rules, retention/deletion ownership and protected shared key storage.
4. Exercise full deployed HTTPS browser -> BFF -> API -> Worker -> controlled SMTP delivery, including expiry, key rotation and failure recovery.
5. Check trusted-proxy/rate-limit behavior, customer email changes, cookie isolation, cross-reservation denial and concurrent admin actions.
6. Confirm agreed settlement wording and live email/legal texts. Obtain separate deployment authorization.

[PR #457](https://github.com/chelebyy/arackiralama/pull/457) publishes the implementation and handoff for review. Remote CI must be checked at the current PR head; local results above do not establish CI success. No merge, production migration, live sending, historical purge or worktree removal was performed. The primary checkout's unrelated work remains intact. A read-only old-worktree audit timed out without a report; uncertain worktrees were preserved.

## PR 457 review validation

Review corrections passed 910 backend unit tests, 20 PostgreSQL/Redis guest integration tests, the 364-test frontend suite plus the subsequently added HTTP-error regression (8 BFF tests passed), production Webpack/TypeScript build, and lint (zero errors, one existing SearchForm test warning). Chromium passed seven guest scenarios on desktop and seven on mobile, including cooldown/Enter submission and all five locales; four controlled Step 4 payment/quote scenarios also passed. Their fixtures now use declarations and the current server conflict/explicit-acceptance contract. Compose configuration and Nginx syntax checks passed; independent Data Protection providers verified persisted-key decryption across provider recreation.

The broad real-catalog payment suite was attempted but did not reach checkout: the isolated local seed has 120 vehicles and zero configured `rental_terms`, so dated search correctly returns no offers. These changes do not claim full catalogue-driven E2E or full deployed HTTPS/SMTP acceptance. No production data or shared existing test database was modified. Security review covered the touched BFF signatures/partitioning, neutral challenge responses, key sharing, driver JSON minimization and expired-mail dispatch. Production ingress isolation, secret provisioning, encrypted key storage and delivery remain unverified; a focused independent security review and the release checklist above remain available before activation.

The first review commit exposed six stale mail fixtures in remote CI after the message switched to an absolute expiry. The SMTP and five-locale fixtures now provide and assert the exact stored expiry instead of a new ten-minute lifetime.

Follow-up validation passed all 910 backend units in Release, nine version-2 rollout/negative scenarios and three current-draft policy scenarios against PostgreSQL/Redis. The Worker now uses the ASP.NET 10 runtime image; its real image build and isolated database/Redis startup passed. Hold compatibility accepts stored proof versions 2 and 3, retaining session and exact-vehicle checks. Only existing version-2 drafts reuse stored driver dates; expiry, policy and eligibility are revalidated without fabricating declarations. New reservations still write version 3, and the request-replay fingerprint verifier remains strict rather than accepting unverifiable legacy sensitive inputs.

The subsequent review pass added challenge retirement on resend, pre-dispatch challenge usability checks, conditional cancellation of abandoned reminder jobs, and per-session consent/form reset. Validation after these changes: 910 Release unit tests, all 23 guest PostgreSQL/Redis integration cases, 20 Chromium desktop/mobile guest cases, and lint with zero errors (one existing suppression warning). New cases prove established sessions survive a resend, superseded access jobs are skipped, abandoned reminders are cancelled for both mutation types, and logout/401/403 cannot carry consent into another reservation.

## Baseline

Implementation started from freshly fetched remote default `main` at `1a352c984972a3c6a705b10073563eca470b92fc`, the merged Package 2 PR #447 commit. Local `main` was synchronized at start. The active feature checkout is separate from the dirty primary checkout. These are baseline facts, not a claim that the primary working directory is now on main.
