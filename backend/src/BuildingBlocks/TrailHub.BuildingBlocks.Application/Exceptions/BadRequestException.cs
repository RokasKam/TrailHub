namespace TrailHub.BuildingBlocks.Application.Exceptions;

/// <summary>400 Bad Request: the request is invalid, e.g. a blank name or an out-of-range value.</summary>
public sealed class BadRequestException(string code, string message) : AppException(code, message);
