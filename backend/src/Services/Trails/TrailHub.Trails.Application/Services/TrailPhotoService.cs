using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trails.Application.Interfaces.Providers;
using TrailHub.Trails.Application.Interfaces.Repository;
using TrailHub.Trails.Application.Interfaces.Service;
using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Services;

internal sealed class TrailPhotoService(
    ITrailRepository trails,
    IPhotoStorage storage,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : ITrailPhotoService
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
        ["image/heic"] = "heic",
    };

    public async Task<TrailPhotoResponse> UploadAsync(Guid trailId, Stream content, string contentType, long length, string? caption, CancellationToken cancellationToken)
    {
        if (!Extensions.TryGetValue(contentType, out var extension))
        {
            throw Error.Validation("Photo.UnsupportedType", "Photos must be JPEG, PNG, WebP or HEIC.").ToException();
        }

        if (length is <= 0 or > MaxBytes)
        {
            throw Error.Validation("Photo.TooLarge", $"Photos must be smaller than {MaxBytes / 1024 / 1024} MB.").ToException();
        }

        var trail = await trails.GetByIdAsync(trailId, cancellationToken) ?? throw TrailErrors.NotFound.ToException();
        var now = clock.GetUtcNow().UtcDateTime;
        var key = $"{Trail.PhotoKeyPrefix(trail.Id)}{Guid.CreateVersion7():N}.{extension}";
        trail.AddUploadedPhoto(key, caption, user.UserId, now).ThrowIfFailure();

        // Upload first: if storage fails nothing is saved; if the DB save fails we only leave an orphaned object.
        await storage.SaveAsync(key, content, contentType, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new TrailPhotoResponse(storage.ResolveUrl(key), caption, now);
    }

    public async Task AddAsync(Guid trailId, AddPhotoRequest request, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(trailId, cancellationToken) ?? throw TrailErrors.NotFound.ToException();
        trail.AddPhoto(request.Url, request.Caption, user.UserId, clock.GetUtcNow().UtcDateTime).ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
