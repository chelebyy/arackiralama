---
name: add-or-modify-database-table
description: Workflow command scaffold for add-or-modify-database-table in arackiralama.
allowed_tools: ["Bash", "Read", "Write", "Grep", "Glob"]
---

# /add-or-modify-database-table

Use this workflow when working on **add-or-modify-database-table** in `arackiralama`.

## Goal

Adds a new database table or modifies schema, including entity, configuration, migration, and context updates.

## Common Files

- `backend/src/RentACar.Core/Entities/*.cs`
- `backend/src/RentACar.Infrastructure/Data/Configurations/*.cs`
- `backend/src/RentACar.Infrastructure/Data/RentACarDbContext.cs`
- `backend/src/RentACar.Core/Interfaces/IApplicationDbContext.cs`
- `backend/src/RentACar.Infrastructure/Data/Migrations/*.cs`
- `backend/src/RentACar.Infrastructure/Data/Migrations/*ModelSnapshot.cs`

## Suggested Sequence

1. Understand the current state and failure mode before editing.
2. Make the smallest coherent change that satisfies the workflow goal.
3. Run the most relevant verification for touched files.
4. Summarize what changed and what still needs review.

## Typical Commit Signals

- Create or update Entity class in backend/src/RentACar.Core/Entities/
- Create or update Entity Configuration in backend/src/RentACar.Infrastructure/Data/Configurations/
- Update DbContext in backend/src/RentACar.Infrastructure/Data/RentACarDbContext.cs
- Update IApplicationDbContext interface
- Generate new EF migration in backend/src/RentACar.Infrastructure/Data/Migrations/

## Notes

- Treat this as a scaffold, not a hard-coded script.
- Update the command if the workflow evolves materially.