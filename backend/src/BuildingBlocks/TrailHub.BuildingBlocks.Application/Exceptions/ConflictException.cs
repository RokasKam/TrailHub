namespace TrailHub.BuildingBlocks.Application.Exceptions;

/// <summary>409 Conflict: the request clashes with the current state, e.g. a duplicate or a stale update.</summary>
public sealed class ConflictException(string code, string message) : AppException(code, message);
