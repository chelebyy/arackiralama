---
name: code-refactor
description: Safely refactor existing code without changing externally observable behavior. Use only when the user explicitly requests refactoring or a concrete structural change such as refactoring a class, renaming symbols, extracting methods or functions, simplifying conditionals or control flow, deduplicating logic, moving code to the correct module, or removing dead code. Do not use for generic cleanup, broad maintainability improvements, bug fixing, code review, modernization, or performance work.
---

# Code Refactoring

Improve structure without changing externally observable behavior.

Keep this skill focused on safe refactoring, not feature work, rewrites, or style churn.

## Core Rules

1. Preserve behavior.
2. Change the smallest useful slice.
3. Verify with evidence, not intuition.
4. Prefer clarity over cleverness.
5. Stop when the next step becomes redesign instead of refactoring.

## Workflow

1. Identify the concrete pain:
   - duplication
   - unclear naming
   - long method
   - tangled conditional
   - dead code
   - wrong module ownership
2. Map the safety boundary:
   - public API
   - I/O
   - persistence
   - async/concurrency behavior
   - serialization format
   - framework lifecycle hooks
3. Check verification coverage:
   - tests
   - lint
   - typecheck
   - build
   - smoke or e2e coverage if relevant
4. If coverage is weak, add characterization tests before structural changes.
5. Apply one small refactoring at a time.
6. Re-run the strongest available verification after each meaningful step.
7. Stop once the original smell is resolved.

## Default Verification Gate

Before calling a refactor complete, prefer running all applicable checks:

- targeted tests for touched behavior
- full relevant test suite when risk is non-trivial
- lint
- typecheck or compilation
- build/package validation
- smoke or e2e checks for user-facing flows

If some checks are unavailable, say so explicitly and reduce change scope.

## Safe Techniques

Use these first because they are usually behavior-preserving when applied carefully:

- Rename variable, function, type, file, or module using semantic tooling
- Extract method or function around a single responsibility
- Inline trivial indirection
- Introduce explanatory variable or constant
- Replace magic values with named constants
- Split large conditionals into named predicates
- Replace nesting with guard clauses when control flow remains equivalent
- Move code to the owning module without changing contracts
- Remove verified dead code
- Consolidate duplicate logic after proving equivalence

See [references/refactoring-patterns.md](references/refactoring-patterns.md) for technique details and caution notes.

## Use Semantic Tools First

Prefer IDE, language server, compiler, or AST-aware tools for:

- rename
- move symbol
- change signature
- extract method
- safe import updates

Do not rely on raw search/replace for symbol-level changes unless the language and scope make that safe.

## Characterization Tests

When tests are missing but the behavior must be preserved:

1. Observe current behavior.
2. Capture inputs and outputs with narrow tests.
3. Refactor behind that safety net.

Characterization tests should document current behavior, even if the code is ugly.

## Refactoring Heuristics

Treat these as signals, not hard rules:

- long methods often hide multiple responsibilities
- large classes often hide multiple change reasons
- boolean parameters often hide mixed behaviors
- repeated branching on the same type often indicates misplaced behavior
- repeated comments explaining code often indicate naming or extraction problems

Do not refactor just because a numeric threshold was crossed.

## Risk Boundaries

Be extra cautious around:

- public APIs and shared libraries
- database queries, transactions, and migrations
- async flows, retries, queues, and locks
- caching
- serialization and deserialization
- auth, permissions, and validation
- framework lifecycle code
- performance-critical hot paths

In these areas, prove equivalence before and after the change.

## What Is Not Refactoring

Do not present these as safe refactors unless you explicitly call out the behavior risk:

- callback API to async/await API changes
- sync to async changes
- loop to higher-order collection rewrite in hot paths
- dictionary/object to dataclass/class conversion
- replacing conditionals with polymorphism across public contracts
- architecture rewrites
- broad style-only churn

Those may be worthwhile, but they are modernization or redesign tasks, not default refactors.

## Trigger Boundaries

Use this skill when the request is explicitly about behavior-preserving structural refactoring.

Positive examples:

- "refactor this class"
- "rename this function"
- "extract this logic into a helper"
- "remove dead code"
- "move this method to the right module"

Do not use this skill for broad or ambiguous quality requests.

Negative examples:

- "clean up this code"
- "make this code better"
- "fix this bug"
- "review this PR"
- "speed this up"

## Checklist

Before refactoring:

- [ ] Understand what the code does now
- [ ] Identify the exact smell
- [ ] Identify externally observable behavior
- [ ] Confirm available verification
- [ ] Add characterization tests if needed

During refactoring:

- [ ] Keep each step small
- [ ] Avoid mixing feature work
- [ ] Re-run relevant checks frequently
- [ ] Prefer reversible moves

After refactoring:

- [ ] Tests and other checks pass
- [ ] Public behavior is unchanged
- [ ] Readability improved
- [ ] Complexity or duplication decreased
- [ ] No unnecessary abstraction was introduced

## When Not to Refactor

- when behavior is not understood
- when verification is absent and cannot be added safely
- when the requested change is really a redesign
- when deadline pressure makes risky churn unacceptable
- when the code is stable, isolated, and not causing meaningful cost

## Integration Notes

Combine this skill with adjacent skills when relevant:

- `clean-code` for generic code quality or standards on new code
- debugging skills for bug investigation and fixes
- review skills for pull request review or review comments
- performance skills for speed or efficiency work
- framework-specific skills when refactoring inside opinionated ecosystems

Use this skill to keep the change safe and incremental.
