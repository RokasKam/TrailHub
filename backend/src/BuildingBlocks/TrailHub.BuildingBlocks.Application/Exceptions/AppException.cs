namespace TrailHub.BuildingBlocks.Application.Exceptions;

/// <summary>
/// An expected failure a service reports to the caller, such as a missing trip or an invalid name.
/// The API turns each subclass into its HTTP status, with <see cref="Code"/> as the problem title.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string code, string message)
        : base(message) => Code = code;

    /// <summary>A stable, machine-readable code such as <c>Trip.NotFound</c>.</summary>
    public string Code { get; }
}
