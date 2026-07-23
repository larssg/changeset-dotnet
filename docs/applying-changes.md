# Applying Changes

Once a changeset is valid, materialize the model:

```csharp
// Create a new instance
User user = cs.ApplyChanges();

// With a factory (no parameterless constructor needed)
User user = cs.ApplyChanges(() => new User(someArg));

// Pattern matching with result type
var result = cs.ToResult();
// result is ChangesetResult<User>.Valid(user) or ChangesetResult<User>.Invalid(errors)
```

To persist changes to a database via EF Core, see [EF Core & ASP.NET Core](ef-core.md).
