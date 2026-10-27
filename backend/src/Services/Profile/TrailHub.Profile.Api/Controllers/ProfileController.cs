using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Profile.Application.Profiles;
using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Api.Controllers;

/// <summary>The signed-in user's profile, preferences and achievements.</summary>
[Route("api/profiles/me")]
public sealed class ProfileController : ApiController
{
    /// <summary>The caller's profile. Created automatically on first call after sign-up.</summary>
    [HttpGet]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileDto>> Get([FromServices] IQueryHandler<GetMyProfileQuery, ProfileDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetMyProfileQuery(), ct));

    /// <summary>Updates display name, bio, home region and avatar URL.</summary>
    [HttpPut]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileDto>> Update(UpdateMyProfileCommand body, [FromServices] ICommandHandler<UpdateMyProfileCommand, ProfileDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(body, ct));

    /// <summary>Replaces preferences (units, map style, location sharing, notifications).</summary>
    [HttpPut("preferences")]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileDto>> UpdatePreferences(ProfilePreferences body, [FromServices] ICommandHandler<UpdatePreferencesCommand, ProfileDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new UpdatePreferencesCommand(body), ct));

    /// <summary>Every achievement, with whether and when the caller unlocked it.</summary>
    [HttpGet("achievements")]
    [ProducesResponseType<IReadOnlyList<AchievementDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AchievementDto>>> Achievements([FromServices] IQueryHandler<GetMyAchievementsQuery, IReadOnlyList<AchievementDto>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetMyAchievementsQuery(), ct));
}
