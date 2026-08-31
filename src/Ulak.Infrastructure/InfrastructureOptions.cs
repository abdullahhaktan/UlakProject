using System.ComponentModel.DataAnnotations;

namespace Ulak.Infrastructure;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "ulak";

    [Required]
    public string Audience { get; set; } = "ulak";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Endpoint reachable from the API process (compose network), e.g. http://minio:9000.</summary>
    [Required]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Endpoint browsers/mobile use for presigned URLs, e.g. http://localhost:9000.</summary>
    [Required]
    public string PublicEndpoint { get; set; } = string.Empty;

    [Required]
    public string AccessKey { get; set; } = string.Empty;

    [Required]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    public string Bucket { get; set; } = "proofs";

    public int PresignTtlMinutes { get; set; } = 15;
}
