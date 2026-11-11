namespace TrailHub.BuildingBlocks.Application.Exceptions;

/// <summary>403 Forbidden: the caller can see the item but isn't allowed to do this.</summary>
public sealed class ForbiddenException(string code, string message) : AppException(code, message);
