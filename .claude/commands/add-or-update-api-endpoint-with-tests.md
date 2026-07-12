---
name: add-or-update-api-endpoint-with-tests
description: Workflow command scaffold for add-or-update-api-endpoint-with-tests in arackiralama.
allowed_tools: ["Bash", "Read", "Write", "Grep", "Glob"]
---

# /add-or-update-api-endpoint-with-tests

Use this workflow when working on **add-or-update-api-endpoint-with-tests** in `arackiralama`.

## Goal

Implements new API endpoints or updates existing ones, adds contracts, services, controllers, and corresponding tests.

## Common Files

- `backend/src/RentACar.API/Contracts/**/*.cs`
- `backend/src/RentACar.API/Controllers/**/*.cs`
- `backend/src/RentACar.API/Services/**/*.cs`
- `backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs`
- `backend/tests/RentACar.ApiIntegrationTests/Endpoints/*.cs`
- `backend/tests/RentACar.Tests/Unit/Controllers/*.cs`

## Suggested Sequence

1. Understand the current state and failure mode before editing.
2. Make the smallest coherent change that satisfies the workflow goal.
3. Run the most relevant verification for touched files.
4. Summarize what changed and what still needs review.

## Typical Commit Signals

- Create or update DTOs in backend/src/RentACar.API/Contracts/
- Implement or modify controllers in backend/src/RentACar.API/Controllers/
- Implement or update services in backend/src/RentACar.API/Services/
- Register services in backend/src/RentACar.API/Configuration/ServiceCollectionExtensions.cs
- Add or update integration and unit tests in backend/tests/

## Notes

- Treat this as a scaffold, not a hard-coded script.
- Update the command if the workflow evolves materially.