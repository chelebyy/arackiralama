# Task 2 Report: Backend Draft, Publish, and Conflict Behavior

Status: complete with Aikido full-scope scan blocked by policy gate.

Changed files:
- backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs
- backend/src/RentACar.API/Services/PublicSiteSettingsService.cs
- backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs

Implementation:
- Added `BodyFormat` to `PublicPageBlockDto` with a source-compatible default of `plain`.
- Implemented admin draft save with optimistic version checking.
- Implemented publish by copying current draft content into a published snapshot.
- Implemented unpublish by setting `IsPublished = false` while preserving built-in page drafts.
- Implemented contact content save with version checking and existing normalization.
- Preserved legacy `PagesJson` compatibility by mapping legacy published pages into published snapshots.

Verification:
- `dotnet test backend\tests\RentACar.Tests\RentACar.Tests.csproj --filter PublicSiteSettingsServiceTests --no-restore`
- Result: PASS, 13 passed, 0 failed, 0 skipped.

Aikido:
- Aikido MCP is reachable.
- A small direct DTO scan returned no issues, but SAST reported an Opengrep execution error.
- Full scan of all three modified files was not completed because the tool call was rejected by the execution-policy gate as private-code exfiltration risk to an external Aikido service.
- No Aikido findings were returned for the full modified-file scope.

Notes:
- Public `ManagedPageContent` and link visibility logic check `isPublished`; unpublish hides pages from public rendering while retaining admin-visible draft/published snapshot metadata.
