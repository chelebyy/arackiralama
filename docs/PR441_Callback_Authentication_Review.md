# PR #441 callback authentication follow-up

Review date: 17 October 2026. Reviewed PR head: `2dc00cc0ae1b9306da5809a5942e1550f6576726`.

## Verified problem

The signed callback verifier returned a normal `Failed` payment result for an unsigned or malformed callback. `PaymentService.CompleteThreeDsAsync` persisted that result. The PR's terminal-state guard then returned the stored failure without verifying a later valid callback. An unauthenticated callback could therefore prevent a pending payment from completing.

Four new regression cases reproduced this on the original PR head using the real Mock and simulated Iyzico providers. Each case submits an unsigned callback, checks that the pending payment and reservation remain unchanged, and then completes the same payment with a signed success callback. All four failed before the correction.

## Correction

- Distinguish authenticated callbacks from successful payments inside `PaymentThreeDsCallbackVerifier`.
- Reject unauthenticated callbacks with `UnauthorizedAccessException` before the payment service can write state.
- Return HTTP 401 with a generic error from the public callback controller.
- Preserve normal `Failed` results for signed provider failures and retain the existing terminal-state guard.
- Keep the existing production payment disablement and provider-selection constraints.

The correction does not add a provider protocol or change the public provider-result model. The Iyzico class remains simulated; the HMAC fixture format is not evidence of a real Iyzico integration.

## Validation

| Check | Result |
| --- | --- |
| New regression tests before correction | 4 failed, confirming the problem |
| Payment-related unit tests after correction | 176 passed |
| Full backend unit suite | 828 passed, 0 failed, 0 skipped |
| Release solution build, including integration tests | Passed, 0 errors; 60 pre-existing dependency warnings |
| Changed C# file whitespace verification | Passed |
| `git diff --check` | Passed |
| Full API integration suite | 54 passed, 0 failed, against isolated local PostgreSQL 16.15 and Redis 7 |

Relevant commands:

```bash
dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --filter 'FullyQualifiedName~Payment'
dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-build
dotnet build backend/RentACar.sln -c Release
dotnet test backend/tests/RentACar.ApiIntegrationTests/RentACar.ApiIntegrationTests.csproj -c Release --no-build
```

The integration fixtures create and remove isolated random test databases. Local PostgreSQL 16 evidence does not replace the repository's PostgreSQL 17 CI gate.

## Review and release boundary

PR #441 remains a draft. Its reviewed base predates five commits on current `main`; this follow-up neither merges `main` into the branch nor rebases it. Current-head CI, required reviews, compatibility with the current base, and the known dependency advisories still require assessment. This focused correction is not approval to enable payments.

No merge, automatic merge, shared-history rewrite, production deployment, or production database migration was performed. No real provider or customer data was used. The review covers this callback mutation boundary and its related tests, not the complete application's security.
