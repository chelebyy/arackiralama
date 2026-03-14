# Coding Conventions

**Analysis Date:** 2026-03-14

## Naming Patterns

**Files:**
- **Backend (.NET):** PascalCase for class files (e.g., `UserService.cs`), kebab-case for test files (e.g., `user-service-tests.cs`)
- **Frontend (Next.js):** PascalCase for components (e.g., `LeaderboardCard.tsx`), snake_case for hooks (e.g., `use-auth.ts`), kebab-case for utilities (e.g., `date-utils.ts`)
- **Test Files:** `*.test.ts` or `*.spec.ts` for frontend, `*.Tests.cs` for backend

**Functions:**
- PascalCase for public methods and classes (e.g., `CreatePaymentIntentAsync`)
- camelCase for private methods and local variables (e.g., `_sanitizeForLog`)
- Async methods end with `Async` suffix

**Variables:**
- camelCase for local variables and parameters
- PascalCase for constants and readonly fields
- Descriptive names with context (e.g., `paymentIntentResult` instead of `result`)

**Types:**
- PascalCase for interfaces, classes, and enums
- `I` prefix for interfaces (e.g., `IPaymentProvider`)
- `Provider` suffix for service interfaces (e.g., `IPaymentProvider`)

## Code Style

**Formatting:**
- **Prettier** with the following settings:
  - `semi: true` - Require semicolons
  - `tabWidth: 2` - 2-space indentation
  - `printWidth: 100` - Line length limit
  - `singleQuote: false` - Use double quotes
  - `trailingComma: "none"` - No trailing commas
  - `jsxBracketSameLine: true` - JSX brackets on same line
  - `plugins: ["prettier-plugin-tailwindcss"]` - Tailwind CSS plugin

**Linting:**
- **ESLint** with Next.js and React plugins
- Key rules:
  - `@typescript-eslint/no-unused-vars: "off"` - Allow unused variables when prefixed with `_`
  - `@typescript-eslint/no-explicit-any: "off"` - Allow `any` type when necessary
  - `react-hooks/exhaustive-deps: "off"` - Disable exhaustive deps for custom hooks
  - `@next/next/no-img-element: "off"` - Allow img element usage
  - `react/no-children-prop: "off"` - Allow children prop usage

**Backend (.NET):**
- C# coding conventions with nullable reference types enabled
- Implicit usings enabled
- Async/await pattern for all I/O operations

## Import Organization

**Order:**
1. Built-in Node.js modules
2. External libraries
3. Internal utilities and components
4. Relative imports

**Path Aliases:**
- `@/` - Root of the project (frontend)
- `@/components/ui/` - shadcn/ui components
- `@/hooks/` - Custom React hooks
- `@/lib/` - Shared utilities

## Error Handling

**Patterns:**
- **Frontend:** Try-catch blocks with user-friendly error messages
- **Backend:** Custom exception handling middleware with logging
- Use specific exception types (e.g., `TimeoutException`, `ValidationException`)
- Log sanitized sensitive data (e.g., payment details)

**Example Backend Pattern:**
```csharp
try
{
    var result = await _paymentProvider.CreatePaymentIntentAsync(request, cancellationToken);
    return result;
}
catch (TimeoutException ex)
{
    _logger.LogError(ex, "Payment provider timeout for reservation {ReservationId}", request.ReservationId);
    throw new PaymentTimeoutException("Payment processing timed out", ex);
}
```

**Example Frontend Pattern:**
```typescript
try {
  const response = await api.createPaymentIntent(request);
  return response;
} catch (error) {
  if (error instanceof TimeoutError) {
    toast.error('Payment processing timed out');
  } else {
    toast.error('Failed to create payment intent');
  }
  throw error;
}
```

## Logging

**Framework:**
- **Backend:** Microsoft.Extensions.Logging with structured logging
- **Frontend:** Console logging with severity levels

**Patterns:**
- Log at appropriate levels (Info, Warning, Error, Debug)
- Include correlation IDs for tracing
- Sanitize sensitive data before logging
- Use structured logging with key-value pairs

**Example:**
```csharp
_logger.LogInformation(
    "Mock payment intent created for reservation {ReservationId} with idempotency {IdempotencyKey}",
    request.ReservationId,
    SanitizeForLog(request.IdempotencyKey));
```

## Comments

**When to Comment:**
- Complex business logic requiring explanation
- Public APIs with non-obvious behavior
- Workarounds or technical debt
- Algorithm implementations

**JSDoc/TSDoc:**
- Used for public functions and components
- Include parameter and return type descriptions
- Document edge cases and usage examples

## Function Design

**Size:**
- Functions should be small and focused (20-30 lines max)
- Single responsibility principle
- Extract complex logic into separate functions

**Parameters:**
- Use interfaces/DTOs for multiple parameters
- Validate input early
- Use optional parameters sparingly

**Return Values:**
- Consistent return types
- Use Result/Option patterns for error handling
- Document possible return states

## Module Design

**Exports:**
- Default exports for main components
- Named exports for utilities and helpers
- Barrel files for organizing exports

**Barrel Files:**
- Used to consolidate exports from a directory
- Example: `components/index.ts` re-exports all component files

---

*Convention analysis: 2026-03-14*