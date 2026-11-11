namespace TrailHub.BuildingBlocks.Application.Exceptions;

/// <summary>404 Not Found: the item doesn't exist, or the caller isn't allowed to know it does.</summary>
public sealed class NotFoundException(string code, string message) : AppException(code, message);
