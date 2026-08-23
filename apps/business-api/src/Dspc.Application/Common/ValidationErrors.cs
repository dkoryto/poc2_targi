using FluentValidation.Results;

namespace Dspc.Application.Common;

/// <summary>
/// Turns FluentValidation failures into the camelCase field → messages map carried by Problem
/// Details.
/// </summary>
/// <remarks>
/// Object-level rules (<c>RuleFor(x =&gt; x)</c>) report an empty <see cref="ValidationFailure.PropertyName"/>.
/// Indexing that blindly threw <see cref="IndexOutOfRangeException"/>, so a request that tripped
/// such a rule answered 500 instead of 400 — the caller was told "an unexpected error occurred"
/// with no field named, and the failure was recorded as a server fault. Those messages belong to
/// the form as a whole and are grouped under <see cref="FormLevelKey"/>.
/// </remarks>
public static class ValidationErrors
{
    /// <summary>Key used for messages that do not belong to a single field.</summary>
    public const string FormLevelKey = "_";

    public static Dictionary<string, string[]> ToProblemDetails(IEnumerable<ValidationFailure> failures) =>
        failures
            .GroupBy(f => ToFieldName(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

    private static string ToFieldName(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return FormLevelKey;
        return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
    }
}
