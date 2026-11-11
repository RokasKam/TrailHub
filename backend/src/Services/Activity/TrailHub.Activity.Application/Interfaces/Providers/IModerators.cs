namespace TrailHub.Activity.Application.Interfaces.Providers;

/// <summary>Who may review reports and hide, restore or delete any comment or hike photo (configured, not stored per user).</summary>
public interface IModerators
{
    bool IsModerator(string userId);
}
