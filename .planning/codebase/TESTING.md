# Testing Patterns

**Analysis Date:** 2026-03-14

## Test Framework

**Frontend (Next.js):**
- **Runner:** Vitest v3.0.0
- **Config:** `vitest.config.ts`
- **Environment:** jsdom (browser-like environment)
- **Assertion Library:** Built-in Vitest assertions

**Backend (.NET):**
- **Runner:** xUnit v2.9.3
- **Assertion Library:** FluentAssertions v8.8.0
- **Test Runner:** Microsoft.NET.Test.Sdk v18.3.0

**Run Commands:**
```bash
# Frontend
pnpm test                 # Run all tests
pnpm test:watch           # Watch mode
pnpm test:coverage        # Run with coverage

# Backend
dotnet test               # Run all tests
dotnet test --collect:"XPlat Code Coverage"  # Run with coverage
```

## Test File Organization

**Location:**
- **Frontend:** Co-located with source files (e.g., `components/leaderboard-card.test.tsx`)
- **Backend:** Separate test projects (e.g., `tests/RentACar.Tests/`)

**Naming:**
- **Frontend:** `*.test.ts` or `*.spec.ts` (e.g., `leaderboard-card.test.tsx`)
- **Backend:** `*.Tests.cs` (e.g., `MockPaymentProviderTests.cs`)

**Structure:**
```
frontend/
├── app/
├── components/
│   ├── ui/
│   └── leaderboard-card.tsx
│   └── leaderboard-card.test.tsx
└── tests/
    └── RentACar.Tests/
        ├── Unit/
        │   └── Services/
        │       └── MockPaymentProviderTests.cs
        └── Integration/
```

## Test Structure

**Frontend Test Pattern:**
```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { LeaderboardCard } from './leaderboard-card';

describe('LeaderboardCard', () => {
  it('renders leaderboard with students', () => {
    render(<LeaderboardCard />);

    expect(screen.getByText('Leaderboard')).toBeInTheDocument();
    expect(screen.getByText('Liam Smith')).toBeInTheDocument();
    expect(screen.getByText('5000 pts')).toBeInTheDocument();
  });
});
```

**Backend Test Pattern:**
```csharp
using Xunit;
using FluentAssertions;

namespace RentACar.Tests.Unit.Services;

public sealed class MockPaymentProviderTests
{
    [Fact]
    public async Task CreatePaymentIntentAsync_WhenIdempotencyKeyContainsControlCharacters_LogsSanitizedValue()
    {
        var logger = new TestLogger<MockPaymentProvider>();
        var sut = new MockPaymentProvider(
            Options.Create(new PaymentOptions { IntentExpiresMinutes = 15 }),
            logger);

        await sut.CreatePaymentIntentAsync(new CreatePaymentIntentProviderRequest {
            // test data
        });

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("idem-123forged-entry", entry.State["IdempotencyKey"]);
    }
}
```

## Mocking

**Framework:**
- **Frontend:** Vitest built-in mocking
- **Backend:** Moq v4.20.72 for interface mocking

**Patterns:**
```typescript
// Frontend mocking
vi.mock('@/services/api', () => ({
  api: {
    createPaymentIntent: vi.fn().mockResolvedValue(mockResult)
  }
}));

// Backend mocking
var mockPaymentProvider = new Mock<IPaymentProvider>();
mockPaymentProvider.Setup(x => x.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentProviderRequest>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(mockResult);
```

**What to Mock:**
- External dependencies (API calls, database)
- Time-based functions
- File system operations
- Network requests

**What NOT to Mock:**
- Core business logic
- Simple utility functions
- Domain entities

## Fixtures and Factories

**Test Data:**
- **Frontend:** Inline test data in test files
- **Backend:** Factory classes for complex objects

**Example Backend Factory:**
```csharp
public static class PaymentIntentRequestFactory
{
    public static CreatePaymentIntentProviderRequest CreateValidRequest()
    {
        return new CreatePaymentIntentProviderRequest
        {
            ReservationId = Guid.NewGuid(),
            Amount = 1000m,
            Currency = "TRY",
            IdempotencyKey = "test-idempotency-key",
            InstallmentCount = 1,
            Card = new ProviderCardData
            {
                HolderName = "Test User",
                Number = "4111111111111111",
                ExpiryMonth = "12",
                ExpiryYear = "2030",
                Cvv = "123"
            }
        };
    }
}
```

**Location:**
- Test data files in the same directory as tests
- Shared fixtures in dedicated directories

## Coverage

**Requirements:**
- **Frontend:** V8 coverage with HTML report
- **Backend:** .NET code coverage with coverlet

**View Coverage:**
```bash
# Frontend
pnpm test:coverage  # Generates ./coverage/ directory

# Backend
dotnet test --collect:"XPlat Code Coverage"  # Generates coverage files
```

**Coverage Thresholds:**
- Not explicitly defined, but aiming for high coverage
- Both unit and integration tests included in coverage

## Test Types

**Unit Tests:**
- **Scope:** Isolated components and functions
- **Approach:** Mock all external dependencies
- **Backend:** Tests in `tests/RentACar.Tests/Unit/`
- **Frontend:** Tests in `**/*.test.ts` files

**Integration Tests:**
- **Scope:** Multiple components or services together
- **Approach:** Test real interactions
- **Backend:** Tests in `tests/RentACar.Tests/Integration/`
- **Frontend:** Component integration tests

**E2E Tests:**
- **Framework:** Not detected in current codebase
- **Approach:** Browser-based testing (not implemented)

## Common Patterns

**Async Testing:**
```typescript
// Frontend
it('fetches data asynchronously', async () => {
  const data = await fetchData();
  expect(data).toEqual(expectedData);
});

// Backend
[Fact]
public async Task CreatePaymentIntentAsync_WhenCalled_ReturnsResult()
{
    var result = await sut.CreatePaymentIntentAsync(request);
    result.Should().NotBeNull();
}
```

**Error Testing:**
```typescript
// Frontend
it('handles API errors', async () => {
  vi.mocked(api.createPaymentIntent).mockRejectedValue(new Error('Network error'));

  await expect(fetchData()).rejects.toThrow('Network error');
});

// Backend
[Fact]
public async Task CreatePaymentIntentAsync_WhenCalledWithInvalidData_ThrowsValidationException()
{
    var request = PaymentIntentRequestFactory.CreateInvalidRequest();

    await Assert.ThrowsAsync<ValidationException>(() =>
        sut.CreatePaymentIntentAsync(request));
}
```

---

*Testing analysis: 2026-03-14*