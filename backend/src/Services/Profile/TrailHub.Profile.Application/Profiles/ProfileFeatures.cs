using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Application.Profiles;

public interface IProfileRepository
{
    Task<UserProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);

    void Add(UserProfile profile);
}

public sealed record ProfileDto(
    Guid Id,
    string UserId,
    string DisplayName,
    string? Email,
    string? Bio,
    string? HomeRegion,
    string? AvatarUrl,
    ProfilePreferences Preferences,
    HikingTotals Totals,
    int AchievementCount,
    DateTime CreatedAtUtc);

public sealed record AchievementDto(string Code, string Title, string Description, bool Unlocked, DateTime? UnlockedAtUtc);

internal static class ProfileMappings
{
    public static ProfileDto ToDto(this UserProfile p) =>
        new(p.Id, p.UserId, p.DisplayName, p.Email, p.Bio, p.HomeRegion, p.AvatarUrl, p.Preferences, p.Totals, p.Achievements.Count, p.CreatedAtUtc);
}

/// <summary>Loads the caller's profile, creating it on first sign-in.</summary>
internal sealed class ProfileProvisioner(IProfileRepository profiles, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<UserProfile> GetOrCreateAsync(string userId, string? displayName, string? email, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByUserIdAsync(userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = UserProfile.Create(userId, displayName, email, clock.GetUtcNow().UtcDateTime);
        profiles.Add(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile;
    }
}

public sealed record GetMyProfileQuery : IQuery<ProfileDto>;

internal sealed class GetMyProfileHandler(ProfileProvisioner provisioner, ICurrentUser user) : IQueryHandler<GetMyProfileQuery, ProfileDto>
{
    public async Task<Result<ProfileDto>> Handle(GetMyProfileQuery query, CancellationToken cancellationToken) =>
        (await provisioner.GetOrCreateAsync(user.UserId, user.Name, user.Email, cancellationToken)).ToDto();
}

public sealed record UpdateMyProfileCommand(string DisplayName, string? Bio, string? HomeRegion, string? AvatarUrl) : ICommand<ProfileDto>;

internal sealed class UpdateMyProfileHandler(ProfileProvisioner provisioner, IUnitOfWork unitOfWork, ICurrentUser user)
    : ICommandHandler<UpdateMyProfileCommand, ProfileDto>
{
    public async Task<Result<ProfileDto>> Handle(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        var profile = await provisioner.GetOrCreateAsync(user.UserId, command.DisplayName, user.Email, cancellationToken);
        var result = profile.UpdateDetails(command.DisplayName, command.Bio, command.HomeRegion, command.AvatarUrl);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile.ToDto();
    }
}

public sealed record UpdatePreferencesCommand(ProfilePreferences Preferences) : ICommand<ProfileDto>;

internal sealed class UpdatePreferencesHandler(ProfileProvisioner provisioner, IUnitOfWork unitOfWork, ICurrentUser user)
    : ICommandHandler<UpdatePreferencesCommand, ProfileDto>
{
    public async Task<Result<ProfileDto>> Handle(UpdatePreferencesCommand command, CancellationToken cancellationToken)
    {
        var profile = await provisioner.GetOrCreateAsync(user.UserId, user.Name, user.Email, cancellationToken);
        profile.UpdatePreferences(command.Preferences);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile.ToDto();
    }
}

public sealed record GetMyAchievementsQuery : IQuery<IReadOnlyList<AchievementDto>>;

internal sealed class GetMyAchievementsHandler(ProfileProvisioner provisioner, ICurrentUser user)
    : IQueryHandler<GetMyAchievementsQuery, IReadOnlyList<AchievementDto>>
{
    public async Task<Result<IReadOnlyList<AchievementDto>>> Handle(GetMyAchievementsQuery query, CancellationToken cancellationToken)
    {
        var profile = await provisioner.GetOrCreateAsync(user.UserId, user.Name, user.Email, cancellationToken);
        return AchievementCatalog.All
            .Select(a =>
            {
                var unlocked = profile.Achievements.FirstOrDefault(u => u.Code == a.Code);
                return new AchievementDto(a.Code, a.Title, a.Description, unlocked is not null, unlocked?.UnlockedAtUtc);
            })
            .ToList();
    }
}

// Commands below are driven by integration events from the Activity service, not by HTTP,
// so they carry the user id explicitly instead of reading ICurrentUser.

public sealed record RecordCompletedHikeCommand(string UserId, Guid ActivityId, double DistanceMeters, double ElevationGainMeters, DateTime CompletedAtUtc)
    : ICommand<IReadOnlyList<string>>;

internal sealed class RecordCompletedHikeHandler(ProfileProvisioner provisioner, IUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<RecordCompletedHikeCommand, IReadOnlyList<string>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(RecordCompletedHikeCommand command, CancellationToken cancellationToken)
    {
        var profile = await provisioner.GetOrCreateAsync(command.UserId, null, null, cancellationToken);
        var unlocked = profile.RecordHike(command.ActivityId, command.DistanceMeters, command.ElevationGainMeters, command.CompletedAtUtc, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return unlocked.Select(a => a.Code).ToList();
    }
}

public sealed record RemoveHikeCommand(string UserId, Guid ActivityId) : ICommand;

internal sealed class RemoveHikeHandler(IProfileRepository profiles, IUnitOfWork unitOfWork) : ICommandHandler<RemoveHikeCommand>
{
    public async Task<Result> Handle(RemoveHikeCommand command, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByUserIdAsync(command.UserId, cancellationToken);
        if (profile is not null)
        {
            profile.RemoveHike(command.ActivityId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
