using EffortLess.Core.Library;
using Xunit;

namespace EffortLess.Core.Tests;

public class LibraryTests
{
    [Fact]
    public async Task JsonLocalStore_RoundTrips_IndentedJson()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "settings.json");
        var store = new JsonLocalStore<PlayerSettings>(path);
        var settings = new PlayerSettings
        {
            Volume = 42.5,
            Speed = 1.5,
            ResumeEnabled = false,
            Window = new WindowPlacement(-10, 20, 800, 600, true)
        };

        await store.SaveAsync(settings);

        string text = await File.ReadAllTextAsync(path);
        Assert.Contains("Volume", text);

        PlayerSettings loaded = await store.LoadAsync();
        Assert.Equal(settings.Volume, loaded.Volume);
        Assert.Equal(settings.Speed, loaded.Speed);
        Assert.False(loaded.ResumeEnabled);
        Assert.Equal(settings.Window, loaded.Window);
    }

    [Fact]
    public async Task JsonLocalStore_RecoversFromCorruptJson_AndPreservesDamagedFile()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "settings.json");
        await File.WriteAllTextAsync(path, "{ this is not valid json");

        var store = new JsonLocalStore<PlayerSettings>(path);
        PlayerSettings loaded = await store.LoadAsync();

        Assert.Equal(100, loaded.Volume);
        Assert.False(File.Exists(path));
        Assert.NotEmpty(Directory.EnumerateFiles(temp.FullPath, "settings.json.*.corrupt"));
    }

    [Fact]
    public async Task JsonLocalStore_RecoversFromOversizedFile()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "settings.json");
        await File.WriteAllTextAsync(path, new string('x', (int)(2L * 1024 * 1024) + 1));

        var store = new JsonLocalStore<PlayerSettings>(path);
        PlayerSettings loaded = await store.LoadAsync();

        Assert.Equal(100, loaded.Volume);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task JsonLocalStore_MissingFile_ReturnsDefault()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "missing.json");

        var store = new JsonLocalStore<PlayerSettings>(path);
        PlayerSettings loaded = await store.LoadAsync();

        Assert.Equal(100, loaded.Volume);
        Assert.Equal(1, loaded.Speed);
        Assert.True(loaded.ResumeEnabled);
        Assert.Null(loaded.Window);
    }

    [Fact]
    public async Task JsonLocalStore_PrecanceledSave_LeavesExistingFileIntact()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "settings.json");

        var store = new JsonLocalStore<PlayerSettings>(path);
        await store.SaveAsync(new PlayerSettings { Volume = 7 });
        string before = await File.ReadAllTextAsync(path);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.SaveAsync(new PlayerSettings { Volume = 99 }, cts.Token));

        Assert.Equal(before, await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.EnumerateFiles(temp.FullPath, "*.tmp"));
    }

    [Fact]
    public async Task JsonLocalStore_ConcurrentSaves_RemainValid()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "settings.json");
        var store = new JsonLocalStore<PlayerSettings>(path);

        var tasks = Enumerable.Range(0, 8)
            .Select(i => store.SaveAsync(new PlayerSettings { Volume = i }));
        await Task.WhenAll(tasks);

        PlayerSettings loaded = await store.LoadAsync();
        Assert.InRange(loaded.Volume, 0, 7);
        Assert.Equal(1, loaded.Speed);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(50, 50)]
    [InlineData(150, 100)]
    [InlineData(double.NaN, 100)]
    public void PlayerSettings_Normalize_ClampsVolume(double input, double expected)
    {
        var settings = new PlayerSettings { Volume = input };
        Assert.Equal(expected, settings.Normalize().Volume);
    }

    [Theory]
    [InlineData(0.25, 0.25)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.75, 0.75)]
    [InlineData(1, 1)]
    [InlineData(1.25, 1.25)]
    [InlineData(1.5, 1.5)]
    [InlineData(1.75, 1.75)]
    [InlineData(2, 2)]
    [InlineData(0.3, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    public void PlayerSettings_Normalize_SanitizesSpeed(double input, double expected)
    {
        var settings = new PlayerSettings { Speed = input };
        Assert.Equal(expected, settings.Normalize().Speed);
    }

    [Theory]
    [InlineData(0, 0, 800, 600, false, true)]
    [InlineData(100000, 0, 800, 600, false, false)]
    [InlineData(0, 0, 100, 100, false, false)]
    [InlineData(0, 0, 20000, 600, false, false)]
    public void PlayerSettings_Normalize_SanitizesWindow(
        int x, int y, int width, int height, bool maximized, bool expectedNonNull)
    {
        var settings = new PlayerSettings
        {
            Window = new WindowPlacement(x, y, width, height, maximized)
        };

        WindowPlacement? window = settings.Normalize().Window;
        if (expectedNonNull)
            Assert.NotNull(window);
        else
            Assert.Null(window);
    }

    [Fact]
    public void MediaHistory_Touch_AddsNewestFirst_AndReplacesDuplicate()
    {
        using var temp = new TempDirectory();
        string first = Path.Combine(temp.FullPath, "first.mp4");
        string second = Path.Combine(temp.FullPath, "second.mp4");
        File.WriteAllText(first, "1");
        File.WriteAllText(second, "22");

        var history = new MediaHistory();
        history.Touch(first);
        history.Touch(second);

        Assert.Equal(2, history.Records.Count);
        Assert.Equal(Path.GetFullPath(second), history.Records[0].Path);
        Assert.Equal(Path.GetFullPath(first), history.Records[1].Path);

        File.AppendAllText(first, "333");
        history.Touch(first);

        Assert.Equal(2, history.Records.Count);
        Assert.Equal(Path.GetFullPath(first), history.Records[0].Path);
        Assert.Equal(4, history.Records[0].Length);
    }

    [Fact]
    public void MediaHistory_RecordPosition_UpdatesOnlyPositionAndDuration()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "movie.mp4");
        File.WriteAllText(path, "12345");

        var history = new MediaHistory();
        history.Touch(path);
        MediaRecord before = history.Records[0];

        history.RecordPosition(path, 42, 100);
        MediaRecord after = history.Records[0];

        Assert.Equal(Path.GetFullPath(path), after.Path);
        Assert.Equal(before.Length, after.Length);
        Assert.Equal(before.LastWriteUtcTicks, after.LastWriteUtcTicks);
        Assert.Equal(before.LastOpenedUtc, after.LastOpenedUtc);
        Assert.Equal(42, after.Position);
        Assert.Equal(100, after.Duration);
    }

    [Fact]
    public void MediaHistory_RecordPosition_ClampsPosition()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "movie.mp4");
        File.WriteAllText(path, "12345");

        var history = new MediaHistory();
        history.Touch(path);

        history.RecordPosition(path, 150, 100);
        Assert.Equal(100, history.Records[0].Position);

        history.RecordPosition(path, double.NaN, 100);
        Assert.Equal(0, history.Records[0].Position);

        history.RecordPosition(path, -5, 100);
        Assert.Equal(0, history.Records[0].Position);
    }

    [Fact]
    public void MediaHistory_GetResumePosition_ReturnsPosition_WhenMetadataMatches()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "movie.mp4");
        File.WriteAllText(path, "123456789");

        var history = CreateResumableHistory(path, 42, 100);
        Assert.Equal(42, history.GetResumePosition(path));
    }

    [Fact]
    public void MediaHistory_GetResumePosition_WorksWhenRecentlyOpened()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "movie.mp4");
        File.WriteAllText(path, "123456789");

        FileInfo info = new FileInfo(path);
        var record = new MediaRecord(
            Path.GetFullPath(path),
            info.Length,
            info.LastWriteTimeUtc.Ticks,
            42,
            100,
            DateTime.UtcNow.AddSeconds(-5));
        var history = new MediaHistory(new[] { record });

        Assert.Equal(42, history.GetResumePosition(path));
    }

    [Fact]
    public void MediaHistory_GetResumePosition_ReturnsZero_WhenMetadataStale()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "movie.mp4");
        File.WriteAllText(path, "123456789");

        var history = CreateResumableHistory(path, 42, 100);
        File.WriteAllText(path, "a much longer content value");

        Assert.Equal(0, history.GetResumePosition(path));
    }

    [Fact]
    public void MediaHistory_GetResumePosition_ReturnsZero_WhenFileMissing()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "movie.mp4");
        File.WriteAllText(path, "123456789");

        var history = CreateResumableHistory(path, 42, 100);
        File.Delete(path);

        Assert.Equal(0, history.GetResumePosition(path));
    }

    [Theory]
    [InlineData(9.9, 0)]
    [InlineData(10, 10)]
    [InlineData(80, 80)]
    [InlineData(80.1, 0)]
    [InlineData(94.9, 0)]
    [InlineData(95, 0)]
    [InlineData(100, 0)]
    public void MediaHistory_GetResumePosition_AppliesResumeBounds(double position, double expected)
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "movie.mp4");
        File.WriteAllText(path, "content");

        var history = CreateResumableHistory(path, position, 100);
        Assert.Equal(expected, history.GetResumePosition(path));
    }

    [Fact]
    public void MediaHistory_GetResumePosition_ReturnsZero_WhenDurationInvalid()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "movie.mp4");
        File.WriteAllText(path, "content");

        var history = CreateResumableHistory(path, 50, 0);
        Assert.Equal(0, history.GetResumePosition(path));
    }

    [Fact]
    public void MediaHistory_GetRecent_ReturnsNewestFirst_AndCleansMissing()
    {
        using var temp = new TempDirectory();
        string a = Path.Combine(temp.FullPath, "a.mp4");
        string b = Path.Combine(temp.FullPath, "b.mp4");
        string c = Path.Combine(temp.FullPath, "c.mp4");
        File.WriteAllText(a, "a");
        File.WriteAllText(b, "b");
        File.WriteAllText(c, "c");

        var history = new MediaHistory();
        history.Touch(a);
        history.Touch(b);
        history.Touch(c);
        File.Delete(b);

        IReadOnlyList<MediaRecord> recent = history.GetRecent(20);

        Assert.Equal(2, recent.Count);
        Assert.Equal(Path.GetFullPath(c), recent[0].Path);
        Assert.Equal(Path.GetFullPath(a), recent[1].Path);
        Assert.Equal(2, history.Records.Count);
    }

    [Fact]
    public void MediaHistory_GetRecent_HonorsCount()
    {
        using var temp = new TempDirectory();
        string a = Path.Combine(temp.FullPath, "a.mp4");
        string b = Path.Combine(temp.FullPath, "b.mp4");
        string c = Path.Combine(temp.FullPath, "c.mp4");
        File.WriteAllText(a, "a");
        File.WriteAllText(b, "b");
        File.WriteAllText(c, "c");

        var history = new MediaHistory();
        history.Touch(a);
        history.Touch(b);
        history.Touch(c);

        IReadOnlyList<MediaRecord> recent = history.GetRecent(2);

        Assert.Equal(2, recent.Count);
        Assert.Equal(Path.GetFullPath(c), recent[0].Path);
        Assert.Equal(Path.GetFullPath(b), recent[1].Path);
    }

    [Fact]
    public void MediaHistory_RespectsCapacity_NewestFirst()
    {
        using var temp = new TempDirectory();
        string a = Path.Combine(temp.FullPath, "a.mp4");
        string b = Path.Combine(temp.FullPath, "b.mp4");
        string c = Path.Combine(temp.FullPath, "c.mp4");
        File.WriteAllText(a, "a");
        File.WriteAllText(b, "b");
        File.WriteAllText(c, "c");

        var history = new MediaHistory(capacity: 2);
        history.Touch(a);
        history.Touch(b);
        history.Touch(c);

        Assert.Equal(2, history.Records.Count);
        Assert.Equal(Path.GetFullPath(c), history.Records[0].Path);
        Assert.Equal(Path.GetFullPath(b), history.Records[1].Path);
    }

    [Fact]
    public void MediaHistory_Constructor_ExcludesInvalidRecords()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.FullPath, "movie.mp4");
        File.WriteAllText(path, "content");

        FileInfo info = new FileInfo(path);
        MediaRecord valid = new(
            Path.GetFullPath(path),
            info.Length,
            info.LastWriteTimeUtc.Ticks,
            10,
            100,
            DateTime.UtcNow.AddSeconds(-30));
        MediaRecord badPath = valid with { Path = "" };
        MediaRecord badPosition = valid with { Position = double.NaN };

        var history = new MediaHistory(new[] { badPath, badPosition, valid });

        Assert.Single(history.Records);
        Assert.Equal(Path.GetFullPath(path), history.Records[0].Path);
    }

    [Fact]
    public void MediaHistory_Touch_MissingFileThrows()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4");
        var history = new MediaHistory();
        Assert.Throws<FileNotFoundException>(() => history.Touch(path));
    }

    [Fact]
    public void PlaylistQueue_Replace_SelectsFirst_AndNormalizesPaths()
    {
        var queue = new PlaylistQueue();
        queue.Replace(new[] { "first.mp4", "second.mp4" });

        Assert.Equal(2, queue.Items.Count);
        Assert.Equal(0, queue.CurrentIndex);
        Assert.NotNull(queue.Current);
        Assert.Equal(Path.GetFullPath("first.mp4"), queue.Current!.Path);
    }

    [Fact]
    public void PlaylistQueue_Add_SelectsFirstOnlyWhenEmpty()
    {
        var queue = new PlaylistQueue();
        queue.Add(new[] { "a.mp4" });

        Assert.Equal(0, queue.CurrentIndex);

        queue.Add(new[] { "b.mp4", "c.mp4" });
        Assert.Equal(0, queue.CurrentIndex);
        Assert.Equal(3, queue.Items.Count);
        Assert.Equal(Path.GetFullPath("a.mp4"), queue.Current!.Path);
    }

    [Fact]
    public void PlaylistQueue_Navigation_DoesNotWrap()
    {
        var queue = new PlaylistQueue();
        queue.Replace(new[] { "a.mp4", "b.mp4", "c.mp4" });

        Assert.True(queue.MoveNext());
        Assert.Equal(1, queue.CurrentIndex);
        Assert.True(queue.MoveNext());
        Assert.Equal(2, queue.CurrentIndex);
        Assert.False(queue.MoveNext());
        Assert.Equal(2, queue.CurrentIndex);

        Assert.True(queue.MovePrevious());
        Assert.Equal(1, queue.CurrentIndex);
        Assert.True(queue.MovePrevious());
        Assert.Equal(0, queue.CurrentIndex);
        Assert.False(queue.MovePrevious());
        Assert.Equal(0, queue.CurrentIndex);
    }

    [Fact]
    public void PlaylistQueue_Select_ChangesCurrent_OrReturnsFalse()
    {
        var queue = new PlaylistQueue();
        queue.Replace(new[] { "a.mp4", "b.mp4" });

        Guid secondId = queue.Items[1].Id;
        Assert.True(queue.Select(secondId));
        Assert.Equal(1, queue.CurrentIndex);

        Assert.False(queue.Select(Guid.NewGuid()));
        Assert.Equal(1, queue.CurrentIndex);
    }

    [Fact]
    public void PlaylistQueue_Remove_AdjustsCurrent()
    {
        var queue = new PlaylistQueue();
        queue.Replace(new[] { "a.mp4", "b.mp4", "c.mp4" });

        Guid b = queue.Items[1].Id;
        queue.Select(b);
        Assert.True(queue.Remove(b));
        Assert.Equal(
            new[] { Path.GetFullPath("a.mp4"), Path.GetFullPath("c.mp4") },
            queue.Items.Select(i => i.Path).ToArray());
        Assert.Equal(1, queue.CurrentIndex);
        Assert.Equal(Path.GetFullPath("c.mp4"), queue.Current!.Path);

        Guid c = queue.Items[1].Id;
        Assert.True(queue.Remove(c));
        Assert.Equal(0, queue.CurrentIndex);
        Assert.Equal(Path.GetFullPath("a.mp4"), queue.Current!.Path);

        Guid a = queue.Items[0].Id;
        Assert.True(queue.Remove(a));
        Assert.Equal(-1, queue.CurrentIndex);
        Assert.Null(queue.Current);
    }

    [Fact]
    public void PlaylistQueue_Move_Reorders_AndKeepsCurrentIdentity()
    {
        var queue = new PlaylistQueue();
        queue.Replace(new[] { "a.mp4", "b.mp4", "c.mp4" });

        Guid b = queue.Items[1].Id;
        queue.Select(b);
        Assert.True(queue.Move(b, 0));
        Assert.Equal(0, queue.CurrentIndex);
        Assert.Equal(b, queue.Current!.Id);
        Assert.Equal(
            new[] { Path.GetFullPath("b.mp4"), Path.GetFullPath("a.mp4"), Path.GetFullPath("c.mp4") },
            queue.Items.Select(i => i.Path).ToArray());

        Assert.Throws<ArgumentOutOfRangeException>(() => queue.Move(b, 3));
        Assert.False(queue.Move(Guid.NewGuid(), 0));
    }

    [Fact]
    public void PlaylistQueue_Move_NonCurrent_KeepsCurrentIdentity()
    {
        var queue = new PlaylistQueue();
        queue.Replace(new[] { "a.mp4", "b.mp4", "c.mp4" });

        Guid c = queue.Items[2].Id;
        queue.Select(queue.Items[1].Id);
        queue.Move(c, 0);

        Assert.Equal(2, queue.CurrentIndex);
        Assert.Equal(Path.GetFullPath("b.mp4"), queue.Current!.Path);
        Assert.Equal(c, queue.Items[0].Id);
    }

    [Fact]
    public void PlaylistQueue_AllowsDuplicatePaths_WithUniqueIds()
    {
        var queue = new PlaylistQueue();
        queue.Replace(new[] { "a.mp4", "a.mp4" });

        Assert.Equal(2, queue.Items.Count);
        Assert.NotEqual(queue.Items[0].Id, queue.Items[1].Id);
    }

    [Fact]
    public void PlaylistQueue_RejectsNonLocalPaths_WithoutMutating()
    {
        var queue = new PlaylistQueue();
        Assert.Throws<ArgumentException>(() => queue.Replace(new[] { "a.mp4", "https://example.com/b.mp4" }));
        Assert.Empty(queue.Items);
        Assert.Equal(-1, queue.CurrentIndex);
    }

    [Fact]
    public void PlaylistQueue_Items_IsDetachedSnapshot()
    {
        var queue = new PlaylistQueue();
        queue.Add(new[] { "a.mp4" });
        IReadOnlyList<PlaylistItem> snapshot = queue.Items;
        queue.Add(new[] { "b.mp4" });

        Assert.Single(snapshot);
    }

    [Theory]
    [InlineData("movie.mp4")]
    [InlineData("movie.MKV")]
    [InlineData("C:/media/my video.m4a")]
    [InlineData("café 東京.wav")]
    public void MediaFiles_IsMediaPath_RecognizesSupportedExtensions(string path) =>
        Assert.True(path.IsMediaPath());

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("movie.mp4.txt")]
    [InlineData("")]
    public void MediaFiles_IsMediaPath_RejectsUnsupported(string path) =>
        Assert.False(path.IsMediaPath());

    [Fact]
    public void MediaFiles_IsMediaPath_NullReturnsFalse() =>
        Assert.False(((string?)null).IsMediaPath());

    [Theory]
    [InlineData("episode.srt")]
    [InlineData("episode.ass")]
    [InlineData("episode.ssa")]
    public void MediaFiles_IsSubtitlePath_RecognizesSupportedExtensions(string path) =>
        Assert.True(path.IsSubtitlePath());

    [Theory]
    [InlineData("episode.txt")]
    [InlineData("episode.vtt")]
    [InlineData("")]
    public void MediaFiles_IsSubtitlePath_RejectsUnsupported(string path) =>
        Assert.False(path.IsSubtitlePath());

    [Fact]
    public void MediaFiles_NormalizeLocalPath_SupportsSpacesAndUnicode()
    {
        string relative = Path.Combine("folder name", "café 東京.mp4");
        Assert.Equal(Path.GetFullPath(relative), MediaFiles.NormalizeLocalPath(relative));
    }

    [Fact]
    public void MediaFiles_NormalizeLocalPath_RejectsInvalidInput()
    {
        Assert.Throws<ArgumentNullException>(() => MediaFiles.NormalizeLocalPath((string)null!));
        Assert.Throws<ArgumentException>(() => MediaFiles.NormalizeLocalPath(""));
        Assert.Throws<ArgumentException>(() => MediaFiles.NormalizeLocalPath("bad" + (char)0 + "path.mp4"));
        Assert.Throws<ArgumentException>(() => MediaFiles.NormalizeLocalPath("https://example.com/movie.mp4"));
    }

    private static MediaHistory CreateResumableHistory(string path, double position, double duration)
    {
        FileInfo info = new FileInfo(path);
        var record = new MediaRecord(
            Path.GetFullPath(path),
            info.Length,
            info.LastWriteTimeUtc.Ticks,
            position,
            duration,
            DateTime.UtcNow.AddSeconds(-30));
        return new MediaHistory(new[] { record });
    }

    private sealed class TempDirectory : IDisposable
    {
        public string FullPath { get; }

        public TempDirectory()
        {
            FullPath = Path.Combine(Path.GetTempPath(), "EffortLess.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(FullPath);
        }

        public void Dispose()
        {
            string prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "EffortLess.Tests")) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(FullPath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            try
            {
                if (Directory.Exists(FullPath))
                    Directory.Delete(FullPath, true);
            }
            catch
            {
            }
        }
    }
}
