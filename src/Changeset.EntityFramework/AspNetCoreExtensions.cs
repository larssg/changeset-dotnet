using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Changeset.EntityFramework;

public static class AspNetCoreExtensions
{
    /// <summary>
    /// Converts changeset errors into a ValidationProblemDetails-compatible dictionary
    /// suitable for Results.ValidationProblem().
    /// </summary>
    public static IDictionary<string, string[]> ToValidationErrors<T>(
        this Changeset<T> changeset) where T : class
    {
        return changeset.ErrorMap.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(e => e.Message).ToArray());
    }

    /// <summary>
    /// Returns a ValidationProblem IResult if the changeset is invalid, or null if valid.
    /// Useful for early-return patterns in minimal APIs.
    /// </summary>
    public static IResult? ToValidationProblemOrNull<T>(
        this Changeset<T> changeset) where T : class
    {
        if (changeset.IsValid)
            return null;

        return Results.ValidationProblem(changeset.ToValidationErrors());
    }

    /// <summary>
    /// Returns a HttpValidationProblemDetails if the changeset has errors.
    /// </summary>
    public static HttpValidationProblemDetails ToProblemDetails<T>(
        this Changeset<T> changeset) where T : class
    {
        return new HttpValidationProblemDetails(changeset.ToValidationErrors());
    }

    /// <summary>
    /// Adds changeset errors to a ModelStateDictionary for use in MVC controllers.
    /// </summary>
    public static void AddToModelState<T>(
        this Changeset<T> changeset, ModelStateDictionary modelState) where T : class
    {
        foreach (var error in changeset.Errors)
        {
            modelState.AddModelError(error.Field, error.Message);
        }
    }
}
