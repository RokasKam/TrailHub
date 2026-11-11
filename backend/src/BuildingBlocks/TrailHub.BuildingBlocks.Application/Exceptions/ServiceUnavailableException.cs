namespace TrailHub.BuildingBlocks.Application.Exceptions;

/// <summary>503 Service Unavailable: a dependency such as a third-party API is down; worth retrying later.</summary>
public sealed class ServiceUnavailableException(string code, string message) : AppException(code, message);
