namespace Dspc.Application.Common;

public abstract class AppException(int status, string title, string detail) : Exception(detail)
{
    public int Status { get; } = status;
    public string Title { get; } = title;
    public IDictionary<string, string[]>? Errors { get; init; }
}

public sealed class NotFoundException(string entity, string code) : AppException(404, "Not found", $"{entity} '{code}' was not found.");

public sealed class ForbiddenException(string detail = "You do not have access to this resource.") : AppException(403, "Forbidden", detail);

public sealed class ValidationException(IDictionary<string, string[]> errors, string detail = "One or more validation errors occurred.")
    : AppException(400, "Validation failed", detail)
{
    public new IDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class ConflictException(string detail) : AppException(409, "Conflict", detail);

public sealed class PreconditionFailedException(string detail = "The resource was modified by someone else. Reload and retry.")
    : AppException(412, "Precondition failed", detail);

public sealed class UnprocessableException(string detail, object? payload = null) : AppException(422, "Unprocessable", detail)
{
    public object? Payload { get; } = payload;
}
