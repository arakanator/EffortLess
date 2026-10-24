using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace EffortLess.App;

/// <summary>Shell-provided local thumbnails; never decodes through the player engine.</summary>
internal sealed class MediaThumbnailService
{
    private readonly SemaphoreSlim _gate = new(2);
    private readonly Dictionary<string, BitmapImage?> _cache = new(StringComparer.OrdinalIgnoreCase);
    public async Task<BitmapImage?> GetAsync(string path)
    {
        if (_cache.TryGetValue(path, out var cached)) return cached;
        await _gate.WaitAsync();
        try
        {
            if (_cache.TryGetValue(path, out cached)) return cached;
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.VideosView, 256,
                ThumbnailOptions.UseCurrentScale);
            BitmapImage? image = null;
            if (thumbnail is { Type: ThumbnailType.Image })
            {
                image = new BitmapImage();
                await image.SetSourceAsync(thumbnail);
            }
            // Bound retained images. Visible rows hold their own references.
            if (_cache.Count >= 80) _cache.Clear();
            _cache[path] = image;
            return image;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException
            or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
        finally { _gate.Release(); }
    }
}
