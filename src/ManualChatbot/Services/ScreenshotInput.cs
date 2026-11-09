namespace ManualChatbot.Services;

public static class ScreenshotInput
{
    public const int MaxBytes = 3 * 1024 * 1024;
    public static void Validate(string dataUrl)
    {
        if (dataUrl.Length > MaxBytes * 4 / 3 + 100) throw new ArgumentException("Screenshot must be 3 MB or smaller.");
        var comma = dataUrl.IndexOf(',');
        if (comma < 0) throw new ArgumentException("Choose a PNG, JPEG or WebP screenshot.");
        var mime = dataUrl[..comma];
        if (mime is not ("data:image/png;base64" or "data:image/jpeg;base64" or "data:image/webp;base64"))
            throw new ArgumentException("Choose a PNG, JPEG or WebP screenshot.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]); }
        catch (FormatException) { throw new ArgumentException("The screenshot data is invalid. Choose the image again."); }
        if (bytes.Length > MaxBytes || bytes.Length < 12) throw new ArgumentException("Screenshot must be a valid image of 3 MB or smaller.");
        var valid = mime switch {
            "data:image/png;base64" => bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}),
            "data:image/jpeg;base64" => bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255,
            _ => bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)
        };
        if (!valid) throw new ArgumentException("The screenshot format does not match its contents.");
    }
}
