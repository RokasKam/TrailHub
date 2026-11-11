using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Profile.Application.Interfaces.Service;
using TrailHub.Profile.Application.Requests;
using TrailHub.Profile.Application.Responses;
using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Api.Controllers;

/// <summary>The signed-in user's profile, preferences and achievements.</summary>
[Route("api/profiles/me")]
public sealed class ProfileController(IProfileService profileService) : ApiController
{
    /// <summary>The caller's profile. Created automatically on first call after sign-up.</summary>
    [HttpGet]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileResponse>> Get(CancellationToken ct) =>
        Ok(await profileService.GetMyProfileAsync(ct));

    /// <summary>Updates display name, bio, home region and avatar URL.</summary>
    [HttpPut]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileResponse>> Update(UpdateProfileRequest body, CancellationToken ct) =>
        Ok(await profileService.UpdateMyProfileAsync(body, ct));

    /// <summary>Replaces preferences (units, map style, location sharing, notifications).</summary>
    [HttpPut("preferences")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileResponse>> UpdatePreferences(ProfilePreferences body, CancellationToken ct) =>
        Ok(await profileService.UpdatePreferencesAsync(body, ct));

    /// <summary>Every achievement, with whether and when the caller unlocked it.</summary>
    [HttpGet("achievements")]
    [ProducesResponseType<IReadOnlyList<AchievementResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AchievementResponse>>> Achievements(CancellationToken ct) =>
        Ok(await profileService.GetMyAchievementsAsync(ct));
}
