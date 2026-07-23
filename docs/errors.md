# Errors

Errors accumulate — multiple validators can report multiple errors on the same field. Errors serialize cleanly for API responses.

## Inspecting errors

```csharp
cs.IsValid                  // bool
cs.Errors                   // all errors
cs.ErrorsOn("Email")        // errors for a specific field
cs.ErrorMap                 // Dictionary<string, ImmutableArray<ChangesetError>>
cs.HasErrorOn("Email")      // bool
```

## Serialization

The error map serializes into a shape that works well for API responses:

```json
{
  "Name": [{"message": "can't be blank", "code": "required"}],
  "Email": [{"message": "has already been taken", "code": "uniqueness"}]
}
```

For ASP.NET Core helpers that convert errors into `ProblemDetails` or `ModelState`, see [EF Core & ASP.NET Core](ef-core.md#aspnet-core).
