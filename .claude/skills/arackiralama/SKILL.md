```markdown
# arackiralama Development Patterns

> Auto-generated skill from repository analysis

## Overview

This skill provides a comprehensive guide to the development patterns, coding conventions, and core workflows used in the `arackiralama` repository. The project is primarily written in C# (backend) and TypeScript/React (frontend), and covers backend API, database migrations, frontend features, testing, and documentation. The repository follows conventional commit messages, structured file organization, and a clear workflow for collaborative development.

## Coding Conventions

### File Naming

- **C# files:** Use PascalCase for class, interface, and file names.
  - Example: `CarReservationService.cs`, `ICarRepository.cs`
- **Frontend files:** Use PascalCase for components, camelCase for hooks and utility files.
  - Example: `ReservationForm.tsx`, `useReservation.ts`

### Import Style

- **C#:** Use alias imports for namespaces when needed.
  ```csharp
  using Entities = RentACar.Core.Entities;
  using System.Collections.Generic;
  ```
- **TypeScript:** Standard ES6 imports.
  ```typescript
  import { fetchReservations } from '../lib/api/reservations';
  import ReservationForm from './ReservationForm';
  ```

### Export Style

- **C#:** Mixed (public classes, interfaces, etc.)
  ```csharp
  public class CarService { ... }
  public interface ICarRepository { ... }
  ```
- **TypeScript:** Named and default exports.
  ```typescript
  export default ReservationForm;
  export const useReservation = () => { ... };
  ```

### Commit Messages

- **Conventional Commits:** Prefixes include `fix`, `feat`, `test`, `docs`.
  - Example: `feat: add reservation cancellation endpoint`
  - Example: `fix: correct date validation in booking service`

## Workflows

### Add or Modify Database Table with Migrations
**Trigger:** When introducing a new database table or making significant schema changes  
**Command:** `/new-table`

1. Create or update entity classes in `backend/src/RentACar.Core/Entities/`.
2. Update ORM configuration files in `backend/src/RentACar.Infrastructure/Data/Configurations/`.
3. Add or update migration files in `backend/src/RentACar.Infrastructure/Data/Migrations/`.
4. Update `DbContext` in `backend/src/RentACar.Infrastructure/Data/RentACarDbContext.cs`.
5. Update interfaces in `backend/src/RentACar.Core/Interfaces/`.
6. Add or update integration tests in `backend/tests/RentACar.ApiIntegrationTests/Database/`.
7. Document the change in `docs/`.

**Example:**
```csharp
// backend/src/RentACar.Core/Entities/ExtraOption.cs
public class ExtraOption
{
    public int Id { get; set; }
    public string Name { get; set; }
}
```

### Add or Update API Endpoint with Tests
**Trigger:** When exposing new backend functionality via an API endpoint  
**Command:** `/new-api-endpoint`

1. Create or update DTOs in `backend/src/RentACar.API/Contracts/`.
2. Implement or modify controllers in `backend/src/RentACar.API/Controllers/`.
3. Implement or update services in `backend/src/RentACar.API/Services/`.
4. Register services in `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`.
5. Add or update integration and unit tests in `backend/tests/`.
6. Document the API in `docs/`.

**Example:**
```csharp
// backend/src/RentACar.API/Controllers/ReservationController.cs
[HttpPost]
public IActionResult CreateReservation([FromBody] CreateReservationDto dto)
{
    // ...
}
```

### Feature Development: Frontend Implementation & Tests
**Trigger:** When adding or enhancing a frontend feature or flow  
**Command:** `/new-frontend-feature`

1. Add or update page components in `frontend/app/`.
2. Add or update supporting components in `frontend/components/`.
3. Implement or update hooks in `frontend/hooks/`.
4. Update or create API client files in `frontend/lib/api/`.
5. Add or update tests for components, hooks, and API clients.
6. Update i18n files if needed.
7. Document the feature in `docs/`.

**Example:**
```tsx
// frontend/app/ReservationPage.tsx
import ReservationForm from '../components/ReservationForm';

export default function ReservationPage() {
  return <ReservationForm />;
}
```

### Test Gate Closure and Evidence Capture
**Trigger:** When formally closing a test/acceptance gate for a feature or milestone  
**Command:** `/close-test-gate`

1. Update relevant documentation and checklists in `docs/`.
2. Add or update test evidence files in `docs/test-evidence/`.
3. Add or update end-to-end tests in `frontend/e2e/tests/`.
4. Update related frontend/backend tests if needed.

**Example:**  
- Add screenshots or logs to `docs/test-evidence/`
- Update `docs/13_Local_Docker_Browser_Test_Checklist.md` with test results

### Documentation Update for Feature Progress
**Trigger:** When recording or updating the status, plan, or implementation details of a feature  
**Command:** `/update-docs`

1. Update architectural decision records in `docs/02_ADR_ENTERPRISE_FULL.md`.
2. Update TDD plans in `docs/03_TDD_ENTERPRISE_FULL.md`.
3. Update execution tracking in `docs/10_Execution_Tracking.md`.
4. Update feature-specific plans and implementation docs.

**Example:**  
- Add new section to `docs/16_Reservation_Extra_Options_Plan.md` describing implementation approach

### Bugfix or Review Feedback Cycle
**Trigger:** When fixing bugs or addressing code review findings  
**Command:** `/fix-bug`

1. Update implementation files (backend or frontend) as needed.
2. Update or add relevant tests.
3. Update documentation if required.

**Example:**
```csharp
// backend/src/RentACar.Core/Services/ReservationService.cs
public bool IsValidReservationDate(DateTime date)
{
    // Fixed: Ensure date is not in the past
    return date >= DateTime.UtcNow;
}
```

## Testing Patterns

- **Backend:**  
  - Test files use the `*Tests.cs` pattern.
  - Integration tests are placed in `backend/tests/RentACar.ApiIntegrationTests/`.
  - Example:
    ```csharp
    // backend/tests/RentACar.ApiIntegrationTests/Database/ReservationTests.cs
    [Fact]
    public void Should_Create_Reservation()
    {
        // Arrange, Act, Assert
    }
    ```
- **Frontend:**  
  - Uses `vitest` for unit and integration tests.
  - Test files are colocated as `.test.ts` or `.test.tsx`.
  - Example:
    ```typescript
    // frontend/lib/api/reservations.test.ts
    import { fetchReservations } from './reservations';
    import { describe, it, expect } from 'vitest';

    describe('fetchReservations', () => {
      it('returns reservations', async () => {
        const data = await fetchReservations();
        expect(data).toBeDefined();
      });
    });
    ```

## Commands

| Command               | Purpose                                                        |
|-----------------------|----------------------------------------------------------------|
| /new-table            | Add or modify a database table with migrations and tests       |
| /new-api-endpoint     | Add or update an API endpoint with contracts, services, tests  |
| /new-frontend-feature | Implement or enhance a frontend feature with tests             |
| /close-test-gate      | Close test gate and capture test evidence                     |
| /update-docs          | Update documentation for feature progress                      |
| /fix-bug              | Address bugfixes or review feedback with code and tests        |
```
