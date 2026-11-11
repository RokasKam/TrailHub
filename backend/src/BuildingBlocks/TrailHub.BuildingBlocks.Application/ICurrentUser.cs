namespace TrailHub.BuildingBlocks.Application;

/// <summary>The authenticated caller. <see cref="UserId"/> is the Auth0 subject (<c>sub</c>) claim.</summary>
public interface ICurrentUser
{
    string UserId { get; }

    string? Email { get; }

    string? Name { get; }
}
