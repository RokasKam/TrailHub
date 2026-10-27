using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trails.Application.Abstractions;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Trails;

/// <summary>Object storage for user photos (S3 or an S3-compatible store such as MinIO).</summary>
public interface IPhotoStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Public URL for a stored key; external absolute URLs are returned unchanged.</summary>
    string ResolveUrl(string keyOrUrl);
}

public sealed record UploadTrailPhotoCommand(Guid TrailId, Stream Content, string ContentType, long Length, string? Caption) : ICommand<TrailPhotoDto>;

internal sealed class UploadTrailPhotoHandler(
    ITrailRepository trails,
    IPhotoStorage storage,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock)
    : ICommandHandler<UploadTrailPhotoCommand, TrailPhotoDto>
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
        ["image/heic"] = "heic",
    };

    public async Task<Result<TrailPhotoDto>> Handle(UploadTrailPhotoCommand command, CancellationToken cancellationToken)
    {
        if (!Extensions.TryGetValue(command.ContentType, out var extension))
        {
            return Error.Validation("Photo.UnsupportedType", "Photos must be JPEG, PNG, WebP or HEIC.");
        }

        if (command.Length is <= 0 or > MaxBytes)
        {
            return Error.Validation("Photo.TooLarge", $"Photos must be smaller than {MaxBytes / 1024 / 1024} MB.");
        }

        var trail = await trails.GetByIdAsync(command.TrailId, cancellationToken);
        if (trail is null)
        {
            return TrailErrors.NotFound;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var key = $"{Trail.PhotoKeyPrefix(trail.Id)}{Guid.CreateVersion7():N}.{extension}";
        var added = trail.AddUploadedPhoto(key, command.Caption, user.UserId, now);
        if (added.IsFailure)
        {
            return added.Error!;
        }

        // Upload first: if storage fails nothing is saved; if the DB save fails we only leave an orphaned object.
        await storage.SaveAsync(key, command.Content, command.ContentType, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new TrailPhotoDto(storage.ResolveUrl(key), command.Caption, now);
    }
}
