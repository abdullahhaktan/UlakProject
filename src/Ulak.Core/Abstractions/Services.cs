namespace Ulak.Core.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string storedHash);
}

public interface ITokenService
{
    AccessToken CreateAccessToken(Domain.AppUser user);

    /// <summary>Opaque refresh token plus the SHA-256 hash stored server-side.</summary>
    (string Token, string TokenHash, DateTime ExpiresAtUtc) CreateRefreshToken();

    string HashRefreshToken(string token);
}

public sealed record AccessToken(string Value, int ExpiresInSeconds, DateTime ExpiresAtUtc);

public interface IObjectStorage
{
    /// <summary>Presigned PUT URL the client uploads bytes to directly.</summary>
    PresignedUpload CreateUploadUrl(string objectKey, string contentType, TimeSpan ttl);

    /// <summary>Presigned GET URL for displaying a stored object (panel/PDF).</summary>
    string CreateReadUrl(string objectKey, TimeSpan ttl);

    /// <summary>Reads a stored object's bytes (used for server-side PDF rendering).</summary>
    Task<byte[]> ReadAllBytesAsync(string objectKey, CancellationToken ct);

    Task EnsureBucketAsync(CancellationToken ct);
}

public sealed record PresignedUpload(string UploadUrl, string PublicUrl, string ObjectKey, int ExpiresInSeconds);

public interface ISmsSender
{
    /// <summary>Best-effort transactional SMS (driver invite, etc.). Implementations must not throw for a delivery failure.</summary>
    Task SendAsync(string toPhone, string body, CancellationToken ct);
}

public interface IProofDocumentService
{
    /// <summary>One-page PDF: delivery info + photos + signature + GPS + timestamps.</summary>
    Task<byte[]?> RenderProofPdfAsync(int companyId, long proofId, CancellationToken ct);

    /// <summary>Excel export of the filtered proof list for the ops panel.</summary>
    Task<byte[]> ExportProofsXlsxAsync(int companyId, ProofSearchQuery query, CancellationToken ct);
}
