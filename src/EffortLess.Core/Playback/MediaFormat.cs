namespace EffortLess.Core.Playback;

public static class MediaFormat
{
    public static string ForSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "Unknown";
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && !uri.IsFile) return "Stream";
        string path = uri?.IsFile == true ? uri.LocalPath : source;
        string extension = Path.GetExtension(path).TrimStart('.');
        return extension.Length == 0 ? "Unknown" : extension.ToUpperInvariant();
    }
}
