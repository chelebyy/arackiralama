Status: DONE_WITH_CONCERNS

Commits:
- `c770f5a` `feat(admin): add public content contracts`

Changed Files:
- `backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs`
- `backend/src/RentACar.API/Services/IPublicSiteSettingsService.cs`
- `backend/src/RentACar.API/Services/PublicSiteSettingsService.cs`
- `backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs`

Concise Test Summary:
- Added the required failing-first service test for `GetAdminContentAsync`.
- Verified the test failed first with compile error because `GetAdminContentAsync` did not exist.
- Implemented Task 1 contracts and minimal admin content mapping.
- Re-ran the focused brief test and it passed: `1 passed, 0 failed`.
- Ran Aikido full scan on all modified Task 1 files: `0 issues`.

Concerns:
- `UpdatePageDraftAsync`, `PublishPageAsync`, `UnpublishPageAsync`, and `UpdateContactContentAsync` are intentionally temporary stubs that throw `NotSupportedException` until Task 2 is implemented.
- I did not start Task 2 draft/publish/contact mutation behavior.
- I did not run the broader backend test suite; only the focused brief test was executed, per task scope.

What I Implemented:
- Added new backend admin content DTO contracts:
  - `PublicPagePublishedSnapshotDto`
  - `AdminPublicManagedPageDto`
  - `AdminPublicContentDto`
  - `UpdateAdminPublicPageDraftRequest`
  - `PublishAdminPublicPageRequest`
  - `UpdateAdminPublicContactRequest`
- Extended `IPublicSiteSettingsService` with the five admin content method signatures required by the brief.
- Implemented `GetAdminContentAsync` in `PublicSiteSettingsService` by:
  - loading current settings through the existing persistence path,
  - mapping through the current public DTO flow,
  - projecting an admin-specific content shape with version derived from `settings.UpdatedAt.Ticks.ToString(CultureInfo.InvariantCulture)`.
- Added `ToAdminContentDto(...)` helper to map existing public page/contact data into the new admin contract shape without changing current public `GetAsync()` behavior.
- Added the focused unit test `GetAdminContentAsync_returns_versioned_page_content`.

Tests Run / Results:
1. `dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter GetAdminContentAsync_returns_versioned_page_content --no-restore`
   - First run result: compile failure, expected by brief.
   - Error observed: `PublicSiteSettingsService` did not contain `GetAdminContentAsync`.
2. `dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter GetAdminContentAsync_returns_versioned_page_content --no-restore`
   - Second run result: PASS.
3. Aikido `aikido_full_scan`
   - Result: `0 issues`.

Self-Review Notes:
- Execution boundary changed: `PublicSiteSettingsService` admin content read contract only.
- Root issue: admin content DTOs and service signatures were missing, so the required test could not compile.
- Smallest safe fix: add new contracts and a read-only admin projection that reuses existing settings mapping; keep existing public contracts and persistence behavior untouched.
- Async/cancellation: `GetAdminContentAsync` propagates the incoming `CancellationToken` through the settings load path; admin reads no longer depend on payment-flag lookup.
- Backward compatibility: existing `GetAsync` and `UpdateAsync` behavior was not changed.
- Residual risk: the four new admin write methods are only placeholders in this task and will throw until Task 2 supplies the actual draft/publish/contact mutation path.

## Review Fix: Admin Read Decoupling

- Fixed reviewer finding in `backend/src/RentACar.API/Services/PublicSiteSettingsService.cs`.
- Changed `GetAdminContentAsync` to build `AdminPublicContentDto` directly from `PublicSiteSettings` JSON-backed fields.
- Removed the admin read path dependency on `PaymentMethodFeatureFlags.GetAvailabilityAsync(...)`.
- Removed the admin read path dependency on the public `Map(...)` projection.
- Reused existing deserialize/default helpers for pages, contact channels, offices, and working hours.
- Left `GetAsync`, `UpdateAsync`, and all Task 2 mutation stubs unchanged.

### Focused Validation

1. `dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter GetAdminContentAsync_returns_versioned_page_content --no-restore`
   - Result: PASS (`1 passed, 0 failed`)
2. Aikido `aikido_full_scan` on modified first-party code
   - Result: `0 issues`

### Remaining Concern

- The focused brief test still validates the success path for admin content retrieval, but there is not yet a dedicated regression test that proves admin reads stay independent from future public payment/projection changes.
