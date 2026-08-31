using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ulak.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ulak.Infrastructure.Storage;

/// <summary>
/// S3-compatible object storage (MinIO in dev). Presigned URLs are generated
/// against the <see cref="StorageOptions.PublicEndpoint"/> so that the host
/// the client will actually call matches the SigV4-signed host.
/// </summary>
public sealed class MinioObjectStorage : IObjectStorage, IDisposable
{
    private readonly StorageOptions _options;
    private readonly ILogger<MinioObjectStorage> _logger;
    private readonly AmazonS3Client _internalClient;  // API -> MinIO (bucket ops)
    private readonly AmazonS3Client _presignClient;   // signs URLs for the public host

    public MinioObjectStorage(IOptions<StorageOptions> options, ILogger<MinioObjectStorage> logger)
    {
        _options = options.Value;
        _logger = logger;

        var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
        _internalClient = BuildClient(credentials, _options.Endpoint);
        _presignClient = BuildClient(credentials, _options.PublicEndpoint);
    }

    public PresignedUpload CreateUploadUrl(string objectKey, string contentType, TimeSpan ttl)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(ttl),
        };

        var uploadUrl = NormalizeScheme(_presignClient.GetPreSignedURL(request));
        var publicUrl = $"{_options.PublicEndpoint.TrimEnd('/')}/{_options.Bucket}/{objectKey}";
        return new PresignedUpload(uploadUrl, publicUrl, objectKey, (int)ttl.TotalSeconds);
    }

    public string CreateReadUrl(string objectKey, TimeSpan ttl)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(ttl),
        };

        return NormalizeScheme(_presignClient.GetPreSignedURL(request));
    }

    /// <summary>
    /// The AWS SDK emits https presigned URLs even for an http ServiceURL; MinIO
    /// in dev is plain http, so align the scheme with the configured endpoint.
    /// </summary>
    private string NormalizeScheme(string url) =>
        _options.PublicEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? string.Concat("http://", url.AsSpan("https://".Length))
                : url;

    public async Task<byte[]> ReadAllBytesAsync(string objectKey, CancellationToken ct)
    {
        using var response = await _internalClient.GetObjectAsync(
            new GetObjectRequest { BucketName = _options.Bucket, Key = objectKey }, ct);
        using var memory = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memory, ct);
        return memory.ToArray();
    }

    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_internalClient, _options.Bucket);
        if (exists)
        {
            return;
        }

        _logger.LogInformation("Creating object storage bucket {Bucket}", _options.Bucket);
        await _internalClient.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, ct);
    }

    private static AmazonS3Client BuildClient(AWSCredentials credentials, string endpoint)
    {
        var useHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        return new AmazonS3Client(credentials, new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            UseHttp = useHttp,
            AuthenticationRegion = "us-east-1",
        });
    }

    public void Dispose()
    {
        _internalClient.Dispose();
        _presignClient.Dispose();
    }
}
