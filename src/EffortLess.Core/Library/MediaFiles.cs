namespace EffortLess.Core.Library;

public static class MediaFiles
{
    private static readonly HashSet<string> _mediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mkv", "avi", "mov", "webm", "m4v", "mpg", "mpeg", "wmv", "flv",
        "ts", "m2ts", "mp3", "flac", "wav", "m4a", "aac", "ogg", "opus", "wma"
    };

    private static readonly HashSet<string> _subtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "srt", "ass", "ssa"
    };

    public static IReadOnlyList<double> Speeds { get; } = Array.AsReadOnly(new double[]
    {
        0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2
    });

    public static bool IsMediaPath(this string? path) => HasExtension(path, _mediaExtensions);

    public static bool IsSubtitlePath(this string? path) => HasExtension(path, _subtitleExtensions);

    public static string NormalizeLocalPath(string path)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));
        if (path.Contains((char)0))
            throw new ArgumentException("Path contains a null character.", nameof(path));

        if (TryParseUri(path, out Uri? uri) && uri is not null)
        {
            if (!uri.IsFile)
                throw new ArgumentException("Only local file paths are supported.", nameof(path));

            path = uri.LocalPath;
            if (path.Length == 0)
                throw new ArgumentException("File URI does not resolve to a local path.", nameof(path));
        }

        return Path.GetFullPath(path);
    }

    private static bool TryParseUri(string path, out Uri? uri)
    {
        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out uri))
                return true;

            uri = null;
            return false;
        }

        if (HasScheme(path))
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out uri))
                return true;
        }

        uri = null;
        return false;
    }

    private static bool HasScheme(string path)
    {
        int colon = path.IndexOf(':');
        if (colon <= 0)
            return false;

        if (colon == 1 && IsAsciiLetter(path[0]))
            return false;

        for (int i = 0; i < colon; i++)
        {
            char c = path[i];
            if (i == 0)
            {
                if (!IsAsciiLetter(c))
                    return false;
            }
            else if (!IsAsciiLetterOrDigit(c) && c != '+' && c != '-' && c != '.')
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExtension(string? path, HashSet<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        ReadOnlySpan<char> span = path.AsSpan().Trim();
        int query = span.IndexOfAny('?', '#');
        if (query >= 0)
            span = span[..query];

        int slash = span.LastIndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        ReadOnlySpan<char> name = slash >= 0 ? span[(slash + 1)..] : span;

        int dot = name.LastIndexOf('.');
        if (dot <= 0 || dot == name.Length - 1)
            return false;

        return extensions.Contains(name[(dot + 1)..].ToString());
    }

    private static bool IsAsciiLetter(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    private static bool IsAsciiLetterOrDigit(char c) =>
        IsAsciiLetter(c) || (c >= '0' && c <= '9');
}
