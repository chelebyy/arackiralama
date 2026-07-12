---
name: add-or-modify-database-table-with-migrations
description: Workflow command scaffold for add-or-modify-database-table-with-migrations in arackiralama.
allowed_tools: ["Bash", "Read", "Write", "Grep", "Glob"]
---

# /add-or-modify-database-table-with-migrations

Use this workflow when working on **add-or-modify-database-table-with-migrations** in `arackiralama`.

## Goal

Adds or updates database tables/entities, updates ORM configurations, generates and applies migrations, and writes integration tests for persistence.

## Common Files

- `backend/src/RentACar.Core/Entities/*.cs`
- `backend/src/RentACar.Infrastructure/Data/Configurations/*.cs`
- `backend/src/RentACar.Infrastructure/Data/Migrations/*.cs`
- `backend/src/RentACar.Infrastructure/Data/RentACarDbContext.cs`
- `backend/src/RentACar.Core/Interfaces/*.cs`
- `backend/tests/RentACar.ApiIntegrationTests/Database/*.cs`

## Suggested Sequence

1. Understand the current state and failure mode before editing.
2. Make the smallest coherent change that satisfies the workflow goal.
3. Run the most relevant verification for touched files.
4. Summarize what changed and what still needs review.

## Typical Commit Signals

- Create or update entity classes in backend/src/RentACar.Core/Entities/
- Update ORM configuration files in backend/src/RentACar.Infrastructure/Data/Configurations/
- Add or update migration files in backend/src/RentACar.Infrastructure/Data/Migrations/
- Update DbContext in backend/src/RentACar.Infrastructure/Data/RentACarDbContext.cs
- Update interfaces in backend/src/RentACar.Core/Interfaces/

## Notes

- Treat this as a scaffold, not a hard-coded script.
- Update the command if the workflow evolves materially.