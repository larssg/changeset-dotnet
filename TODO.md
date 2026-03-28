# Edge Case Test Coverage

## ApplyChanges

- [ ] Type with no parameterless constructor and no factory — clear error message

## EntityFramework

- [ ] `ValidateUniqueAsync` with `null` value for the unique field
- [ ] `ApplyTo` update with navigation properties — should not touch them
- [ ] `ValidateUnique` on a field not in Changes

## Source Generator

- [ ] Record types with `[ChangesetTarget]`
- [ ] Types with inherited properties — generator picks up base class properties
- [ ] Types with static properties — should be excluded
