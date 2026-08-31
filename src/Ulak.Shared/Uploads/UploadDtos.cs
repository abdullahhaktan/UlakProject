namespace Ulak.Shared.Uploads;

public sealed record PresignRequest(string ContentType, string Kind); // Kind: photo | signature

public sealed record PresignResponse(
    string UploadUrl,   // PUT the bytes here
    string PublicUrl,   // store this on the proof; also used to display later
    string ObjectKey,
    int ExpiresInSeconds);
