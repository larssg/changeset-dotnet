# Suggested Library Improvements

This document records a review of the library and tracks the resulting work.
The review covered the core API, validation and casting behavior, Entity
Framework integration, source generator, tests, packaging, and CI
configuration.

The current standard solution test run covers 154 tests:

- 119 core tests
- 19 Entity Framework tests
- 16 source-generator tests

## Recommended priorities

### 1. Add the generator projects to the solution and CI — completed

`Changeset.Generators` and `Changeset.Generators.Tests` are now included in
`Changeset.slnx`, so the standard restore, build, and test workflow covers the
generator package and its tests.

### 2. Complete association materialization — completed

Core and Entity Framework application now recursively materialize valid child
changesets. Core application creates a copy of an updated association, preserving
unchanged data. Entity Framework updates the existing tracked association and
marks only its changed scalar properties as modified.

Covered behavior includes:

- Association inserts and updates
- Initially null associations
- Invalid child changesets
- Preservation of unchanged association data
- Nested associations

Association collections remain out of scope until collection casting is
introduced.

### 3. Harden the source generator

The generator assumes every marked model can be instantiated with `new T()` and
that all selected setters can be assigned after construction. Unsupported model
shapes can therefore produce invalid generated code instead of focused
diagnostics.

Add explicit support or diagnostics for:

- Types without an accessible parameterless constructor
- Required members
- `init`-only properties
- Properties with non-public setters
- Abstract types
- Nested types
- Generic types
- Generated type and hint-name collisions

### 4. Fix inherited-property analysis

The generator walks a model's base types and includes inherited properties, but
`FieldNameAnalyzer` only examines members directly available through
`targetType.GetMembers()`. This can cause an inherited permitted field to be
accepted by the generator while the analyzer reports it as invalid.

The analyzer and generator should share the same symbol-inspection logic and
test cases.

### 5. Strengthen field-expression validation

`ExpressionFieldExtractor` currently accepts any member expression and returns
the final member name. For example, an expression such as
`x => x.Address.City` can incorrectly become `"City"` even though `City` is not
a direct property of the target type.

Anonymous member aliases can create a similar mismatch between the alias and
the property being selected.

Require every field expression to be a direct property access from the lambda
parameter, and validate each argument in anonymous-object selectors.

### 6. Improve NuGet packaging and framework compatibility

The package projects currently contain very little publishing metadata. Before
broad publication, add:

- Package descriptions and tags
- Authors
- Repository URL
- License expression
- README and release notes
- XML documentation files
- Symbol packages
- Deterministic and continuous-integration build settings
- Package validation
- Public API compatibility checks

Consider targeting `net8.0` for the core package, or multi-targeting
`net8.0;net10.0`. Most of the core implementation does not appear to require
.NET 10, and supporting an LTS target would substantially increase the
potential user base.

### 7. Clarify and strengthen uniqueness validation

`ValidateUnique` is useful as a preflight check, but it cannot guarantee
uniqueness because a competing write can occur between validation and
persistence. Database unique constraints should remain authoritative.

Potential improvements include:

- Mapping unique-constraint exceptions into changeset errors
- Supporting scoped or composite uniqueness
- Excluding the current row by primary key
- Supporting a caller-provided scope or exclusion predicate
- Explicitly documenting the method's advisory nature

### 8. Add API and behavioral quality gates

Consider adding:

- Public API approval tests
- `dotnet pack` validation in CI
- Coverage reporting with meaningful thresholds
- Roslyn test-harness tests that assert exact generator diagnostics
- Entity Framework integration tests using SQLite in addition to InMemory
- Property-based or mutation tests for type-coercion boundaries
- Benchmarks for casting, validation, and generated versus reflection-based
  application

## Smaller improvements

- Add timeouts to user-supplied regular expressions, or support cached/generated
  regular expressions.
- Validate contradictory or invalid length and number constraints.
- Add cancellation-token-aware asynchronous validator delegates.
- Centralize duplicated property discovery and reflection caches.
- Add tests for duplicate association validation and stale propagated errors.
- Document or standardize case sensitivity for error lookup and string-based
  field APIs.

## Suggested implementation order

The next three recommended improvements are:

1. Harden generator behavior and diagnostics.
2. Fix inherited-property analysis.
3. Strengthen field-expression validation.
