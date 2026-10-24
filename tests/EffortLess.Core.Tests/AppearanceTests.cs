using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EffortLess.Core.Appearance;
using Xunit;

namespace EffortLess.Core.Tests;

public sealed class AppearanceTests : IDisposable
{
    private readonly string _root;

    public AppearanceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EffortLess.AppearanceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private string FilePath(string name) => Path.Combine(_root, name);

    [Theory]
    [InlineData("Pink", AppearancePresetIds.Pink, "Solar Pink")]
    [InlineData("Solar Pink", AppearancePresetIds.Pink, "Solar Pink")]
    [InlineData("pink", AppearancePresetIds.Pink, "Solar Pink")]
    [InlineData("Periwinkle", AppearancePresetIds.Periwinkle, "Celestial Periwinkle")]
    [InlineData("Celestial Periwinkle", AppearancePresetIds.Periwinkle, "Celestial Periwinkle")]
    [InlineData("periwinkle", AppearancePresetIds.Periwinkle, "Celestial Periwinkle")]
    [InlineData("Dark / Black", AppearancePresetIds.DarkBlack, "Void Black")]
    [InlineData("Void Black", AppearancePresetIds.DarkBlack, "Void Black")]
    [InlineData("dark-black", AppearancePresetIds.DarkBlack, "Void Black")]
    public async Task Service_RenamedThemesPreserveSelectionAndOtherPreferences(string saved, string id, string label)
    {
        var path = FilePath("legacy.json");
        await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(new AppearanceSettings
        {
            ThemeId = saved, GlowEnabled = false, GlowIntensity = 0.42,
            Density = AppearanceDensityIds.Comfortable, TranslucencyEnabled = true, CustomAccent = "#abcd12"
        }));
        var loaded = await new AppearanceService(path).LoadAsync();
        Assert.Equal(id, loaded.ThemeId);
        Assert.Equal(label, ThemeDefinition.Resolve(loaded).Theme.DisplayName);
        Assert.False(loaded.GlowEnabled);
        Assert.Equal(0.42, loaded.GlowIntensity);
        Assert.Equal(AppearanceDensityIds.Comfortable, loaded.Density);
        Assert.True(loaded.TranslucencyEnabled);
        Assert.Equal("#ABCD12", loaded.CustomAccent);
        Assert.Equal(id, (await new AppearanceService(path).LoadAsync()).ThemeId);
        if (saved != id)
            Assert.Equal(id, System.Text.Json.JsonSerializer.Deserialize<AppearanceSettings>(await File.ReadAllTextAsync(path))!.ThemeId);
    }

    [Fact]
    public void CelestialPeriwinkle_UsesReferenceAccentAcrossInteractiveTokens()
    {
        var resolved = ThemeDefinition.Resolve(new AppearanceSettings { ThemeId = AppearancePresetIds.Periwinkle });
        var accent = AppearanceColor.FromHex("#7898F6");
        foreach (var key in new[] { AppearanceTokens.AccentBrush, AppearanceTokens.PlayerFocusBrush,
            AppearanceTokens.SliderTrackValueFill, AppearanceTokens.SliderTrackValueFillPointerOver,
            AppearanceTokens.SliderTrackValueFillPressed })
            Assert.Equal(accent, resolved.Colors[key]);
        foreach (var key in new[] { AppearanceTokens.AccentGlowBrush, AppearanceTokens.SelectedBrush })
        {
            var actual = resolved.Colors[key];
            Assert.Equal(accent.R, actual.R);
            Assert.Equal(accent.G, actual.G);
            Assert.Equal(accent.B, actual.B);
        }
        Assert.Equal(AppearanceColor.VideoBackground, resolved.Colors[AppearanceTokens.PlayerVideoBrush]);
    }

    [Fact]
    public void Normalize_UnknownThemeInvalidCustomAndGlow_FallsBackToDefaults()
    {
        var settings = new AppearanceSettings
        {
            ThemeId = "does-not-exist",
            GlowIntensity = double.NaN,
            Density = "wide",
            CustomAccent = "red"
        }.Normalize();

        Assert.Equal(AppearancePresetIds.Default, settings.ThemeId);
        Assert.Equal(AppearanceSettings.DefaultGlowIntensity, settings.GlowIntensity);
        Assert.Equal(AppearanceDensityIds.Compact, settings.Density);
        Assert.Null(settings.CustomAccent);
        Assert.Null(AppearanceSettings.NormalizeCustomAccent("#123456\n"));
        Assert.True(settings.GlowEnabled);
        Assert.False(settings.TranslucencyEnabled);
    }

    [Fact]
    public void Normalize_ClampsGlowAndCanonicalizesCustomAccent()
    {
        var settings = new AppearanceSettings
        {
            GlowIntensity = 5,
            CustomAccent = "#ab12ef"
        }.Normalize();

        Assert.Equal(AppearanceSettings.MaximumGlowIntensity, settings.GlowIntensity);
        Assert.Equal("#AB12EF", settings.CustomAccent);
        Assert.Equal(0.0, new AppearanceSettings { GlowIntensity = -2 }.Normalize().GlowIntensity);
        Assert.Equal(
            AppearanceSettings.DefaultGlowIntensity,
            new AppearanceSettings { GlowIntensity = double.PositiveInfinity }.Normalize().GlowIntensity);
    }

    [Fact]
    public async Task Service_LoadsDefaults_WhenFileIsMissing()
    {
        var service = new AppearanceService(FilePath("missing.json"));
        var loaded = await service.LoadAsync();

        Assert.Equal(AppearancePresetIds.Default, loaded.ThemeId);
        Assert.Equal(AppearanceDensityIds.Compact, loaded.Density);
        Assert.Equal(AppearanceSettings.DefaultGlowIntensity, loaded.GlowIntensity);
        Assert.False(File.Exists(FilePath("missing.json")));
    }

    [Fact]
    public async Task Service_UpdatePersistsAndReloadsNormalizedSettings()
    {
        var path = FilePath("appearance.json");
        var service = new AppearanceService(path);
        var updated = await service.UpdateAsync(new AppearanceSettings
        {
            ThemeId = AppearancePresetIds.NeonViolet,
            GlowIntensity = 0.4,
            Density = AppearanceDensityIds.Comfortable,
            CustomAccent = "#11aa22"
        });

        Assert.Equal(AppearancePresetIds.NeonViolet, updated.ThemeId);
        Assert.Equal(0.4, updated.GlowIntensity);
        Assert.Equal(AppearanceDensityIds.Comfortable, updated.Density);
        Assert.Equal("#11AA22", updated.CustomAccent);
        Assert.True(File.Exists(path));

        var reloaded = await new AppearanceService(path).LoadAsync();
        Assert.Equal(AppearancePresetIds.NeonViolet, reloaded.ThemeId);
        Assert.Equal(0.4, reloaded.GlowIntensity);
        Assert.Equal(AppearanceDensityIds.Comfortable, reloaded.Density);
        Assert.Equal("#11AA22", reloaded.CustomAccent);
    }

    [Fact]
    public async Task Service_ResetRestoresDefaults()
    {
        var service = new AppearanceService(FilePath("reset.json"));
        await service.UpdateAsync(new AppearanceSettings
        {
            ThemeId = AppearancePresetIds.CrimsonRed,
            GlowIntensity = 0.9,
            GlowEnabled = false,
            Density = AppearanceDensityIds.Comfortable,
            TranslucencyEnabled = true,
            CustomAccent = "#123456"
        });

        var reset = await service.ResetAsync();
        Assert.Equal(AppearancePresetIds.Default, reset.ThemeId);
        Assert.Equal(AppearanceDensityIds.Compact, reset.Density);
        Assert.Equal(AppearanceSettings.DefaultGlowIntensity, reset.GlowIntensity);
        Assert.True(reset.GlowEnabled);
        Assert.False(reset.TranslucencyEnabled);
        Assert.Null(reset.CustomAccent);
    }

    [Fact]
    public async Task Service_LoadPreservesCorruptFileAndNormalizes()
    {
        var path = FilePath("appearance.json");
        await File.WriteAllTextAsync(path, "{ this is not valid json");

        var loaded = await new AppearanceService(path).LoadAsync();

        Assert.Equal(AppearancePresetIds.Default, loaded.ThemeId);
        Assert.Equal(AppearanceDensityIds.Compact, loaded.Density);
        Assert.False(File.Exists(path));
        Assert.NotEmpty(Directory.GetFiles(_root, "appearance.json.*.corrupt"));
    }

    [Fact]
    public async Task Service_LoadNormalizesUnrecognizedPreferences()
    {
        var path = FilePath("preferences.json");
        await File.WriteAllTextAsync(path, """
            {
              "ThemeId": "unknown",
              "GlowIntensity": -7,
              "Density": "wide",
              "CustomAccent": "purple",
              "GlowEnabled": false,
              "TranslucencyEnabled": true
            }
            """);

        var loaded = await new AppearanceService(path).LoadAsync();

        Assert.Equal(AppearancePresetIds.Default, loaded.ThemeId);
        Assert.Equal(0.0, loaded.GlowIntensity);
        Assert.Equal(AppearanceDensityIds.Compact, loaded.Density);
        Assert.Null(loaded.CustomAccent);
        Assert.False(loaded.GlowEnabled);
        Assert.True(loaded.TranslucencyEnabled);
    }

    [Fact]
    public void Presets_KeepVideoInvariantAndShareCompactLayout()
    {
        var ids = new[]
        {
            AppearancePresetIds.AuroraCyan,
            AppearancePresetIds.NeonViolet,
            AppearancePresetIds.EmberOrange,
            AppearancePresetIds.CrimsonRed,
            AppearancePresetIds.NeutralGraphite,
            AppearancePresetIds.Pink,
            AppearancePresetIds.Periwinkle,
            AppearancePresetIds.DarkBlack
        };

        var accents = new HashSet<AppearanceColor>();
        foreach (var id in ids)
        {
            var resolved = ThemeDefinition.Resolve(new AppearanceSettings
            {
                ThemeId = id,
                Density = AppearanceDensityIds.Compact
            });

            Assert.Equal(id, resolved.Settings.ThemeId);
            Assert.Equal(AppearanceColor.VideoBackground, resolved.Colors[AppearanceTokens.PlayerVideoBrush]);
            Assert.Equal(AppearanceDimensions.Compact, resolved.Dimensions);
            accents.Add(resolved.Colors[AppearanceTokens.AccentBrush]);
        }

        Assert.Equal(ids.Length, accents.Count);
    }

    [Fact]
    public void Resolve_CustomAccentGlowAndTranslucencyAffectIntendedTokensOnly()
    {
        var settings = new AppearanceSettings
        {
            ThemeId = AppearancePresetIds.NeutralGraphite,
            CustomAccent = "#00FF00",
            GlowEnabled = true,
            GlowIntensity = 0.5,
            TranslucencyEnabled = true
        };

        var resolved = ThemeDefinition.Resolve(settings);

        Assert.Equal(new AppearanceColor(255, 0, 255, 0), resolved.Colors[AppearanceTokens.AccentBrush]);
        Assert.Equal(resolved.Colors[AppearanceTokens.AccentBrush], resolved.Colors[AppearanceTokens.SliderTrackValueFill]);
        Assert.Equal(resolved.Colors[AppearanceTokens.AccentBrush], resolved.Colors[AppearanceTokens.PlayerFocusBrush]);
        Assert.Equal(0, resolved.Colors[AppearanceTokens.SliderOuterThumbBackground].A);
        Assert.Equal(0, resolved.Colors[AppearanceTokens.AccentGlowBrush].R);
        Assert.Equal(255, resolved.Colors[AppearanceTokens.AccentGlowBrush].G);
        Assert.Equal(0, resolved.Colors[AppearanceTokens.AccentGlowBrush].B);
        Assert.InRange(resolved.Colors[AppearanceTokens.AccentGlowBrush].A, (byte)1, (byte)254);
        Assert.InRange(resolved.Colors[AppearanceTokens.PlayerPanelBrush].A, (byte)1, (byte)254);
        Assert.Equal(AppearanceColor.VideoBackground, resolved.Colors[AppearanceTokens.PlayerVideoBrush]);

        var glowOff = ThemeDefinition.Resolve(new AppearanceSettings
        {
            ThemeId = AppearancePresetIds.NeutralGraphite,
            GlowEnabled = false
        });

        Assert.Equal(0, glowOff.Colors[AppearanceTokens.AccentGlowBrush].A);
    }

    [Fact]
    public async Task Service_ConcurrentUpdates_ApplyInCallOrder()
    {
        var path = FilePath("ordered.json");
        var service = new AppearanceService(path);

        var first = service.UpdateAsync(new AppearanceSettings
        {
            ThemeId = AppearancePresetIds.AuroraCyan,
            GlowIntensity = 0.1
        });
        var second = service.UpdateAsync(new AppearanceSettings
        {
            ThemeId = AppearancePresetIds.CrimsonRed,
            GlowIntensity = 0.9
        });

        await Task.WhenAll(first, second);

        Assert.Equal(AppearancePresetIds.CrimsonRed, service.Current.ThemeId);
        Assert.Equal(0.9, service.Current.GlowIntensity);

        var reloaded = await new AppearanceService(path).LoadAsync();
        Assert.Equal(AppearancePresetIds.CrimsonRed, reloaded.ThemeId);
        Assert.Equal(0.9, reloaded.GlowIntensity);
    }

    [Fact]
    public async Task FailedSave_DoesNotReplaceCurrentPreferences()
    {
        var parentFile = FilePath("not-a-directory");
        await File.WriteAllTextAsync(parentFile, "keep");
        var service = new AppearanceService(Path.Combine(parentFile, "appearance.json"));
        await Assert.ThrowsAsync<IOException>(() => service.UpdateAsync(new AppearanceSettings
        { ThemeId = AppearancePresetIds.CrimsonRed }));
        Assert.Equal(AppearancePresetIds.Default, service.Current.ThemeId);
        Assert.Equal("keep", await File.ReadAllTextAsync(parentFile));
    }

    [Fact]
    public void DarkCustomAccent_PreservesVisibleFocusAndThumbWithoutChangingVideo()
    {
        var appearance = ThemeDefinition.Resolve(new AppearanceSettings { CustomAccent = "#000000" });
        Assert.Equal(new AppearanceColor(255, 0, 0, 0), appearance.Colors[AppearanceTokens.AccentBrush]);
        Assert.Equal(appearance.Colors[AppearanceTokens.PlayerTextBrush], appearance.Colors[AppearanceTokens.PlayerFocusBrush]);
        Assert.Equal(appearance.Colors[AppearanceTokens.PlayerFocusBrush], appearance.Colors[AppearanceTokens.SliderThumbBackground]);
        Assert.Equal(AppearanceColor.VideoBackground, appearance.Colors[AppearanceTokens.PlayerVideoBrush]);
    }
}
