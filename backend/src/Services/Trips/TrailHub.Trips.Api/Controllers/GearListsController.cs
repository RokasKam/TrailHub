using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Application.Responses;

namespace TrailHub.Trips.Api.Controllers;

/// <summary>Reusable packing lists. Add one to a trip with <c>POST /api/trips/{id}/checklist/from-list/{listId}</c>.</summary>
/// <remarks>Lists are private. The built-in starter lists (ids <c>starter:…</c>) are read-only.</remarks>
[Route("api/trips/gear-lists")]
public sealed class GearListsController(IGearListService gearListService) : ApiController
{
    /// <summary>Your gear lists, then the starter lists.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GearListResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GearListResponse>>> List(CancellationToken ct) =>
        Ok(await gearListService.GetListsAsync(ct));

    /// <summary>Creates a gear list. Blank and duplicate items are dropped.</summary>
    [HttpPost]
    [ProducesResponseType<GearListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GearListResponse>> Create(GearListRequest body, CancellationToken ct) =>
        Ok(await gearListService.CreateAsync(body, ct));

    /// <summary>Replaces a gear list's name and items.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<GearListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GearListResponse>> Update(Guid id, GearListRequest body, CancellationToken ct) =>
        Ok(await gearListService.UpdateAsync(id, body, ct));

    /// <summary>Deletes a gear list. Trips it was added to keep their items.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await gearListService.DeleteAsync(id, ct);
        return NoContent();
    }
}
