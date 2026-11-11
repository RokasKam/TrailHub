using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrailHub.Trails.Application.Interfaces.Providers;

namespace TrailHub.Trails.Infrastructure.Photos;

/// <summary>
/// Section <c>Photos</c>: <c>ServiceUrl</c> (S3-compatible endpoint, e.g. SeaweedFS or MinIO; omit for AWS S3),
/// <c>Region</c>, <c>Bucket</c>, <c>AccessKey</c>, <c>SecretKey</c>, and <c>PublicBaseUrl</c>, the address
/// clients load photos from (a CDN, or the gateway's <c>/photos</c> route locally). <c>CreateBucket</c>
/// creates the bucket on first upload (local stores); in production create it with infrastructure tooling.
/// </summary>
internal sealed class S3PhotoStorage : IPhotoStorage, IDisposable
{
    private readonly AmazonS3Client _s3;
    private readonly string _bucket;
    private readonly string _publicBaseUrl;
    private readonly bool _createBucket;
    private readonly ILogger<S3PhotoStorage> _logger;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    public S3PhotoStorage(IConfiguration configuration, ILogger<S3PhotoStorage> logger)
    {
        _logger = logger;
        var section = configuration.GetSection("Photos");
        _bucket = section["Bucket"] ?? throw new InvalidOperationException("Photos:Bucket is not configured.");
        _createBucket = section.GetValue("CreateBucket", false);
        _publicBaseUrl = (section["PublicBaseUrl"] ?? throw new InvalidOperationException("Photos:PublicBaseUrl is not configured.")).TrimEnd('/');

        var config = new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(section["Region"] ?? "us-east-1") };
        if (section["ServiceUrl"] is { Length: > 0 } serviceUrl)
        {
            config.ServiceURL = serviceUrl;
            config.ForcePathStyle = true; // most S3-compatible stores need path-style URLs
        }

        _s3 = section["AccessKey"] is { Length: > 0 } accessKey
            ? new AmazonS3Client(new BasicAWSCredentials(accessKey, section["SecretKey"]), config)
            : new AmazonS3Client(config); // AWS default credential chain (IAM role, env vars, …)
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        // The upload stream from ASP.NET isn't seekable; buffer it (max 10 MB, enforced by the handler)
        // so the SDK can compute the content length and checksum.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        await _s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = buffer,
                ContentType = contentType,
                Headers = { CacheControl = "public, max-age=31536000, immutable" }, // keys are never reused
            },
            cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(key, UriKind.Absolute, out _))
        {
            return; // linked from elsewhere; nothing stored
        }

        try
        {
            await _s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = key }, cancellationToken);
        }
        catch (Exception ex) when (ex is AmazonServiceException or HttpRequestException or TaskCanceledException)
        {
            // Best effort: the photo is already removed from the trail; an orphaned object is harmless.
            _logger.LogWarning(ex, "Could not delete trail photo {Key}", key);
        }
    }

    public string ResolveUrl(string keyOrUrl) =>
        Uri.TryCreate(keyOrUrl, UriKind.Absolute, out _) ? keyOrUrl : $"{_publicBaseUrl}/{keyOrUrl}";

    public void Dispose()
    {
        _s3.Dispose();
        _bucketLock.Dispose();
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (!_createBucket || _bucketReady)
        {
            return;
        }

        await _bucketLock.WaitAsync(cancellationToken);
        try
        {
            if (!_bucketReady)
            {
                try
                {
                    await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, cancellationToken);
                }
                catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
                {
                }

                _bucketReady = true;
            }
        }
        finally
        {
            _bucketLock.Release();
        }
    }
}
