# Refactoring Patterns Catalog

Reference guide for behavior-preserving refactoring techniques, smell signals, and caution notes.

## Use This File For

- picking a small safe refactoring
- mapping a smell to a likely technique
- spotting cases where a structural cleanup may actually change behavior

This reference supports explicit refactoring work only. It does not expand the skill to generic cleanup, bug fixing, code review, modernization, or performance tasks.

Treat all indicators as heuristics, not rigid thresholds.

## Code Smells

### Long Method

Signals:

- multiple responsibilities in one method
- repeated comment blocks separating sections
- nested control flow that hides intent

Prefer:

- Extract Method
- Introduce Explanatory Variable
- Decompose Conditional

### Large Class or Module

Signals:

- unrelated reasons to change
- mixed domain and infrastructure logic
- data shaping, validation, persistence, and orchestration living together

Prefer:

- Extract Class
- Extract Module
- Move Method

### Long Parameter List

Signals:

- repeated parameter groups
- flags changing behavior
- call sites that are hard to read

Prefer:

- Introduce Parameter Object
- Preserve Whole Object
- Split behavior instead of passing flags

Caution:

- changing a public signature can be a breaking change

### Duplicate Code

Signals:

- copy-pasted blocks
- same branching logic in several places
- repeated transformation pipelines

Prefer:

- Extract Method
- Pull Up Method
- Shared helper at the true ownership boundary

Caution:

- do not force a shared abstraction if the cases are only superficially similar

### Tangled Conditional Logic

Signals:

- nested branching
- repeated eligibility checks
- branching mixed with business actions

Prefer:

- Guard Clauses
- Decompose Conditional
- Extract Predicate

Caution:

- preserve evaluation order and side effects

### Dead Code

Signals:

- unused functions, fields, imports, or branches
- commented-out legacy code
- obsolete compatibility paths

Prefer:

- delete after reference search and verification

Caution:

- watch for reflection, dynamic imports, framework conventions, and string-based lookups

### Wrong Ownership

Signals:

- method uses another object more than its own data
- feature logic scattered across unrelated modules
- one module knows too much about another module's internals

Prefer:

- Move Method
- Move Field
- Extract Module

## Safe Technique Notes

### Rename

Use for:

- unclear names
- misleading names
- inconsistent domain terminology

Prefer semantic rename tools over text replacement.

### Extract Method

Use for:

- cohesive sub-steps inside a long method
- repeated logic
- logic needing a precise name

Keep extracted methods focused on one purpose.

### Inline Method

Use for:

- trivial indirection that hides logic instead of clarifying it

Do not inline if the method is a stable abstraction boundary.

### Extract Variable

Use for:

- dense boolean expressions
- repeated sub-expressions
- calculations that need domain language

### Replace Magic Value with Constant

Use for:

- unexplained numbers or strings
- domain thresholds
- sentinel values

Name the constant for business meaning, not implementation detail.

### Guard Clauses

Use for:

- deeply nested early exits
- precondition checks
- invalid-state exits

Caution:

- do not reorder checks if exceptions, logging, or side effects differ

### Move Method

Use for:

- methods that primarily depend on another type's data
- modules with low cohesion

Move the smallest unit that improves ownership.

### Extract Class or Module

Use for:

- one unit serving multiple unrelated change reasons

Caution:

- avoid speculative structure that adds ceremony without reducing coupling

### Remove Dead Code

Use for:

- code proven unused by references plus verification

Prefer deleting in small batches.

## Verification Patterns

For low-risk changes:

- targeted tests
- lint
- typecheck

For medium-risk changes:

- targeted tests
- relevant suite
- lint
- typecheck
- build

For high-risk changes:

- characterization tests
- integration or e2e coverage where relevant
- build or package validation
- targeted performance checks if on a hot path

## Behavior-Risk Warnings

These are often proposed as "refactors" but can change runtime behavior:

- callback interface to promise or async interface
- sync to async conversion
- object or dict to class or dataclass conversion
- loop to collection helper rewrites when control flow, laziness, or performance matters
- switch to polymorphism when type construction or serialization changes
- broad dependency injection rewrites

Call these out explicitly as redesign or modernization unless the behavior contract is proven unchanged.

## Anti-Patterns

### Wrong Abstraction

Problem:

- combining near-duplicates too early

Result:

- harder call sites
- more flags
- less clarity

### Style Churn

Problem:

- touching many files for cosmetic consistency without structural value

Result:

- noisy diffs
- harder reviews
- hidden regressions

### Big-Bang Refactor

Problem:

- wide changes without checkpoints

Result:

- hard debugging
- unclear blame
- risky merges

### Tool-Blind Editing

Problem:

- raw search/replace for symbols, signatures, or imports

Result:

- partial renames
- broken references

## Decision Guide

| Smell | Start With | Watch Out For |
|-------|------------|---------------|
| Long method | Extract Method | over-fragmentation |
| Large class/module | Extract Class or Module | speculative structure |
| Long parameter list | Parameter Object or split behavior | breaking public signatures |
| Duplicate code | Extract shared logic | wrong abstraction |
| Tangled conditional | Guard Clauses or Extract Predicate | reordered side effects |
| Dead code | Delete incrementally | dynamic references |
| Wrong ownership | Move Method | hidden coupling |

## Quick Rule

Choose the smallest change that makes the code easier to understand and easier to verify.
