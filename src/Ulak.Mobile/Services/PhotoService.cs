using SkiaSharp;

namespace Ulak.Mobile.Services;

/// <summary>
/// Captures a photo from the device camera and re-encodes it so the file
/// stays well under 2 MB (acceptance criterion #3) while remaining legible.
/// </summary>
public sealed class PhotoService
{
    private const int MaxDimension = 1600;
    private const long TargetBytes = 2 * 1024 * 1024;

    public async Task<byte[]?> CaptureCompressedAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            return null;
        }

        var photo = await MediaPicker.Default.CapturePhotoAsync();
        if (photo is null)
        {
            return null;
        }

        await using var source = await photo.OpenReadAsync();
        return Compress(source);
    }

    internal static byte[] Compress(Stream source)
    {
        using var original = SKBitmap.Decode(source);
        using var resized = Resize(original);

        // step the JPEG quality down until the payload fits
        foreach (var quality in new[] { 80, 70, 60, 45, 30 })
        {
            using var data = resized.Encode(SKEncodedImageFormat.Jpeg, quality);
            var bytes = data.ToArray();
            if (bytes.LongLength <= TargetBytes || quality == 30)
            {
                return bytes;
            }
        }

        return resized.Encode(SKEncodedImageFormat.Jpeg, 30).ToArray();
    }

    private static SKBitmap Resize(SKBitmap bitmap)
    {
        var longest = Math.Max(bitmap.Width, bitmap.Height);
        if (longest <= MaxDimension)
        {
            return bitmap.Copy();
        }

        var scale = (float)MaxDimension / longest;
        var info = new SKImageInfo(
            (int)(bitmap.Width * scale), (int)(bitmap.Height * scale), bitmap.ColorType, bitmap.AlphaType);
        return bitmap.Resize(info, SKFilterQuality.Medium) ?? bitmap.Copy();
    }
}
