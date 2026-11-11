namespace TrailHub.Trails.Application.Interfaces.Providers;

/// <summary>Who may review reports and hide or restore any trail (configured, not stored per user).</summary>
public interface IModerators
{
    bool IsModerator(string userId);
}
