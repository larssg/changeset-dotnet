# EF Core & ASP.NET Core

The `Changeset.EntityFramework` package integrates changesets with EF Core persistence and ASP.NET Core responses.

```shell
dotnet add package Changeset.EntityFramework
```

## EF Core

```csharp
using Changeset.EntityFramework;

// Insert — adds to DbContext
var entity = await cs.ApplyToAsync(dbContext);

// Update — marks only changed properties as modified
cs.ApplyTo(dbContext);
await dbContext.SaveChangesAsync();

// Uniqueness validation against the database
cs = cs.ValidateUnique("Email", dbContext);
```

## ASP.NET Core

```csharp
// Minimal APIs
if (cs.ToValidationProblemOrNull() is { } problem)
    return problem;

// MVC Controllers
cs.AddToModelState(ModelState);

// ProblemDetails
var details = cs.ToProblemDetails();
```

A complete ASP.NET Core minimal API sample lives in the repository under [`samples/Changeset.Sample.WebApi`](https://github.com/larssg/changeset-dotnet/tree/main/samples/Changeset.Sample.WebApi).
