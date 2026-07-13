```markdown
# arackiralama Development Patterns

> Auto-generated skill from repository analysis

## Overview

This skill provides a comprehensive guide to contributing to the **arackiralama** C# codebase. It covers coding conventions, commit patterns, and the key workflows for backend, frontend, testing, and documentation. Whether you're adding features, fixing bugs, or hardening security, this guide will help you follow the established standards and processes for efficient, high-quality contributions.

---

## Coding Conventions

### File Naming

- **PascalCase** is used for all file names.
  - Example: `CarRentalService.cs`, `UserController.cs`

### Import Style

- **Alias imports** are used for clarity.
  - Example:
    ```csharp
    using Entities = RentACar.Core.Entities;
    ```

### Export Style

- **Default exports** are used for classes.
  - Example:
    ```csharp
    public class CarRentalService
    {
        // Implementation
    }
    ```

### Commit Patterns

- **Conventional commits** are used with prefixes:
  - `fix`, `feat`, `test`, `docs`
- Example commit message:
  ```
  feat: add booking cancellation endpoint to API
  ```

---

## Workflows

### Add or Modify Database Table

**Trigger:** When introducing or changing a persistent data structure  
**Command:** `/new-table`

1. Create or update the Entity class in `backend/src/RentACar.Core/Entities/`
2. Create or update Entity Configuration in `backend/src/RentACar.Infrastructure/Data/Configurations/`
3. Update `RentACarDbContext` in `backend/src/RentACar.Infrastructure/Data/RentACarDbContext.cs`
4. Update `IApplicationDbContext` interface
5. Generate a new EF migration in `backend/src/RentACar.Infrastructure/Data/Migrations/`
6. Update `RentACarDbContextModelSnapshot`
7. Add or update integration/unit tests for the new/changed entity
8. Update or create related documentation in `docs/`

**Example:**
```csharp
// backend/src/RentACar.Core/Entities/Car.cs
public class Car
{
    public int Id { get; set; }
    public string Model { get; set; }
    // ...
}
```

---

### Add API Endpoint and Service

**Trigger:** When exposing new backend functionality via REST API  
**Command:** `/new-api-endpoint`

1. Create or update Controller in `backend/src/RentACar.API/Controllers/`
2. Add or update DTOs/contracts in `backend/src/RentACar.API/Contracts/`
3. Implement or update Service in `backend/src/RentACar.API/Services/`
4. Register service in `ServiceCollectionExtensions`
5. Add or update integration and unit tests for endpoint and service
6. Update or create related documentation

**Example:**
```csharp
// backend/src/RentACar.API/Controllers/BookingController.cs
[ApiController]
[Route("api/[controller]")]
public class BookingController : ControllerBase
{
    // ...
}
```

---

### Feature Development: Frontend, Backend, Fullstack

**Trigger:** When delivering a new business capability end-to-end  
**Command:** `/new-feature`

1. Implement backend entities, migrations, services, and API endpoints
2. Implement frontend pages, components, hooks, and API clients
3. Add or update backend and frontend unit/integration/e2e tests
4. Update i18n files for new UI strings
5. Update or create related documentation and test evidence

**Example:**
```tsx
// frontend/app/Bookings/NewBooking.tsx
export default function NewBooking() {
    // ...
}
```

---

### Gate-Driven Test and Review Cycle

**Trigger:** When validating and finalizing a feature or security boundary before release  
**Command:** `/close-gate`

1. Add or update e2e test in `frontend/e2e/tests/`
2. Update documentation and execution tracking
3. Add or update test evidence (screenshots, reports)
4. Update or create related test/checklist markdown files

---

### Security Hardening and Compliance

**Trigger:** When addressing security findings, adding abuse controls, or improving compliance  
**Command:** `/security-hardening`

1. Update or add backend services, controllers, and configurations for security
2. Add or update EF migrations for security-related tables or fields
3. Update or add unit/integration/e2e tests for security boundaries
4. Update documentation, compliance, and validation summary files

---

### Bugfix or Review Feedback Cycle

**Trigger:** When addressing code review feedback or bug reports  
**Command:** `/fix-bug`

1. Update backend or frontend implementation files
2. Update or add relevant unit/integration/e2e tests
3. Update documentation and execution tracking

---

## Testing Patterns

- **Testing Framework:** [vitest](https://vitest.dev/) (for frontend), standard C# testing for backend
- **Test File Pattern:** `*Tests.cs`
- **Backend Tests:** Located in `backend/tests/`
- **Frontend e2e Tests:** Located in `frontend/e2e/tests/`
- **Example Backend Test:**
  ```csharp
  // backend/tests/RentACar.Tests/Unit/Services/CarServiceTests.cs
  [Fact]
  public void Should_Create_Car_Successfully()
  {
      // Arrange, Act, Assert
  }
  ```
- **Example Frontend Test:**
  ```ts
  // frontend/e2e/tests/booking.spec.ts
  import { test, expect } from '@playwright/test';

  test('user can create a booking', async ({ page }) => {
      // ...
  });
  ```

---

## Commands

| Command             | Purpose                                                         |
|---------------------|-----------------------------------------------------------------|
| /new-table          | Add or modify a database table and related backend code         |
| /new-api-endpoint   | Add a new API endpoint, service, and contracts                 |
| /new-feature        | Implement a new feature across backend and frontend            |
| /close-gate         | Finalize a feature or security boundary with tests and docs    |
| /security-hardening | Apply security improvements and compliance checks              |
| /fix-bug            | Address review feedback or bug reports                         |
```
