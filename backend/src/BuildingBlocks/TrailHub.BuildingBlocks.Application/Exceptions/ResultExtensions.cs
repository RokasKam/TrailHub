using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.BuildingBlocks.Application.Exceptions;

/// <summary>
/// Bridges domain results to service exceptions: domain methods return a <see cref="Result"/>, and
/// services throw when it failed, so controllers only ever see successful values.
/// </summary>
public static class ResultExtensions
{
    /// <summary>The exception that reports <paramref name="error"/> with its matching HTTP status.</summary>
    public static AppException ToException(this Error error) => error.Type switch
    {
        ErrorType.NotFound => new NotFoundException(error.Code, error.Message),
        ErrorType.Forbidden => new ForbiddenException(error.Code, error.Message),
        ErrorType.Conflict => new ConflictException(error.Code, error.Message),
        ErrorType.Unavailable => new ServiceUnavailableException(error.Code, error.Message),
        _ => new BadRequestException(error.Code, error.Message),
    };

    /// <summary>Throws the result's error, if any.</summary>
    public static void ThrowIfFailure(this Result result)
    {
        if (result.IsFailure)
        {
            throw result.Error!.ToException();
        }
    }

    /// <summary>The result's value, or throws its error.</summary>
    public static T GetValueOrThrow<T>(this Result<T> result) =>
        result.IsSuccess ? result.Value : throw result.Error!.ToException();
}
