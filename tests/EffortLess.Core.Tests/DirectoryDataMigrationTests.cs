using EffortLess.Core.Library;

namespace EffortLess.Core.Tests;

public sealed class DirectoryDataMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "EffortLess.MigrationTests", Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(_root, "old");
    private string Destination => Path.Combine(_root, "new");
    private string Marker => Path.Combine(Destination, DirectoryDataMigration.CompletionMarkerFileName);

    private void Write(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    [Fact]
    public void CopiesEveryProfileAndSidecarExactly_WithoutModifyingOriginals()
    {
        string[] names = ["settings.json", "appearance.json", "history.json", "playlists/custom.json", "logs/player.log", "unknown-sidecar.bin"];
        byte[] bytes = [0, 1, 254, 255, 27, 0];
        foreach (string name in names) Write(Path.Combine(Source, name), bytes);
        var result = DirectoryDataMigration.Migrate(Source, Destination);
        Assert.Equal(names.Length, result.CopiedCount);
        foreach (string name in names)
        {
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(Source, name)));
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(Destination, name)));
        }
        Assert.True(result.Completed);
        Assert.True(File.Exists(Marker));
    }

    [Fact]
    public void ExistingDestinationPreferencesWin_AndAreNeverOverwritten()
    {
        Write(Path.Combine(Source, "settings.json"), [1]);
        Write(Path.Combine(Destination, "settings.json"), [2]);
        Assert.Equal(0, DirectoryDataMigration.Migrate(Source, Destination).CopiedCount);
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(Path.Combine(Destination, "settings.json")));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(Path.Combine(Source, "settings.json")));
    }

    [Fact]
    public void CompletionPreventsReimportEvenIfOldDataChanges()
    {
        Write(Path.Combine(Source, "settings.json"), [1]);
        DirectoryDataMigration.Migrate(Source, Destination);
        Write(Path.Combine(Source, "new-old-sidecar"), [2]);
        Assert.Equal(0, DirectoryDataMigration.Migrate(Source, Destination).CopiedCount);
        Assert.False(File.Exists(Path.Combine(Destination, "new-old-sidecar")));
    }

    [Fact]
    public void NoOldProfileCompletesWithoutLaterImportingAnUnrelatedProfile()
    {
        DirectoryDataMigration.Migrate(Source, Destination);
        Assert.True(File.Exists(Marker));
        Write(Path.Combine(Source, "settings.json"), [1]);
        DirectoryDataMigration.Migrate(Source, Destination);
        Assert.False(File.Exists(Path.Combine(Destination, "settings.json")));
    }

    [Fact]
    public void BusyMigrationDoesNotComplete_AndRetriesAfterLockReleased()
    {
        Write(Path.Combine(Source, "settings.json"), [1]);
        Directory.CreateDirectory(Destination);
        using (var held = File.Open(Path.Combine(Destination, DirectoryDataMigration.LockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => DirectoryDataMigration.Migrate(Source, Destination));
        Assert.False(File.Exists(Marker));
        Assert.Equal(1, DirectoryDataMigration.Migrate(Source, Destination).CopiedCount);
    }

    [Fact]
    public void LockedSourceDoesNotComplete_AndRetryCopiesMissingFile()
    {
        string file = Path.Combine(Source, "settings.json");
        Write(file, [1, 2, 3]);
        using (var held = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => DirectoryDataMigration.Migrate(Source, Destination));
        Assert.False(File.Exists(Marker));
        Assert.Equal(1, DirectoryDataMigration.Migrate(Source, Destination).CopiedCount);
        Assert.Empty(Directory.GetFiles(Destination, "*.tmp"));
    }

    [Fact]
    public void PartialMigrationPreservesCommittedFilesAndCompletesMissingOnes()
    {
        Write(Path.Combine(Source, "settings.json"), [1]);
        Write(Path.Combine(Source, "appearance.json"), [2]);
        Write(Path.Combine(Destination, "settings.json"), [1]);
        Assert.Equal(1, DirectoryDataMigration.Migrate(Source, Destination).CopiedCount);
        Assert.True(File.Exists(Marker));
    }

    [Theory]
    [InlineData("old", "old")]
    [InlineData("old", "old/nested")]
    [InlineData("old/nested", "old")]
    public void OverlappingPathsAreRejected(string first, string second) =>
        Assert.Throws<ArgumentException>(() => DirectoryDataMigration.Migrate(Path.Combine(_root, first), Path.Combine(_root, second)));

    [Fact]
    public void ConflictingDestinationDirectoryDoesNotLoseSourceOrMarkComplete()
    {
        Write(Path.Combine(Source, "settings.json"), [1]);
        Directory.CreateDirectory(Path.Combine(Destination, "settings.json"));
        Assert.Throws<IOException>(() => DirectoryDataMigration.Migrate(Source, Destination));
        Assert.False(File.Exists(Marker));
        Assert.True(File.Exists(Path.Combine(Source, "settings.json")));
    }

    [Fact]
    public async Task MigratedSettingsAndThemesRetainTypedPreferences()
    {
        var settings = new PlayerSettings
        {
            Volume = 37, Speed = 1.5, ArrowKeySeekInterval = 12,
            FullscreenControlsHideDelay = 2, RightPanelCollapsed = true,
            SubtitleAppearance = new() { FontFamily = "Arial", FontSize = 54, TextOpacity = .5 }
        };
        var appearance = new EffortLess.Core.Appearance.AppearanceSettings { ThemeId = "pink" };
        await new JsonLocalStore<PlayerSettings>(Path.Combine(Source, "settings.json")).SaveAsync(settings);
        await new JsonLocalStore<EffortLess.Core.Appearance.AppearanceSettings>(Path.Combine(Source, "appearance.json")).SaveAsync(appearance);
        DirectoryDataMigration.Migrate(Source, Destination);
        Assert.Equal(settings, await new JsonLocalStore<PlayerSettings>(Path.Combine(Destination, "settings.json")).LoadAsync());
        var migratedAppearance = await new JsonLocalStore<EffortLess.Core.Appearance.AppearanceSettings>(Path.Combine(Destination, "appearance.json")).LoadAsync();
        Assert.Equal(appearance.ThemeId, migratedAppearance.ThemeId);
        Assert.Equal(appearance.GlowEnabled, migratedAppearance.GlowEnabled);
        Assert.Equal(appearance.GlowIntensity, migratedAppearance.GlowIntensity);
        Assert.Equal(appearance.Density, migratedAppearance.Density);
        Assert.Equal(File.ReadAllBytes(Path.Combine(Source, "appearance.json")), File.ReadAllBytes(Path.Combine(Destination, "appearance.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
