using System;
using System.Text.RegularExpressions;

namespace EffortLess.Core.Appearance;

public static class AppearancePresetIds
{
    public const string AuroraCyan = "aurora-cyan";
    public const string NeonViolet = "neon-violet";
    public const string EmberOrange = "ember-orange";
    public const string CrimsonRed = "crimson-red";
    public const string NeutralGraphite = "neutral-graphite";

    public const string Pink = "pink";
    public const string Periwinkle = "periwinkle";
    public const string DarkBlack = "dark-black";

    public const string Default = AuroraCyan;

    // IDs remain stable; display-label aliases migrate selections from older saves.
    public static string Normalize(string? id)
    {
        if (IsKnown(id)) return id!;
        return id?.Trim().ToLowerInvariant() switch
        {
            "pink" or "solar pink" => Pink,
            "periwinkle" or "celestial periwinkle" => Periwinkle,
            "dark / black" or "void black" => DarkBlack,
            _ => Default
        };
    }

    public static bool IsKnown(string? id) =>
        id switch
        {
            AuroraCyan => true,
            NeonViolet => true,
            EmberOrange => true,
            CrimsonRed => true,
            NeutralGraphite => true,
            Pink or Periwinkle or DarkBlack => true,
            _ => false
        };
}

public static class AppearanceDensityIds
{
    public const string Compact = "compact";
    public const string Comfortable = "comfortable";
}

public sealed class AppearanceSettings
{
    public const double MinimumGlowIntensity = 0.0;
    public const double MaximumGlowIntensity = 1.0;
    public const double DefaultGlowIntensity = 0.18;

    public string ThemeId { get; init; } = AppearancePresetIds.Default;
    public bool GlowEnabled { get; init; } = true;
    public double GlowIntensity { get; init; } = DefaultGlowIntensity;
    public string Density { get; init; } = AppearanceDensityIds.Compact;
    public bool TranslucencyEnabled { get; init; }
    public string? CustomAccent { get; init; }

    public AppearanceSettings Normalize() =>
        new AppearanceSettings
        {
            ThemeId = AppearancePresetIds.Normalize(ThemeId),
            GlowEnabled = GlowEnabled,
            GlowIntensity = NormalizeGlowIntensity(GlowIntensity),
            Density = NormalizeDensity(Density),
            TranslucencyEnabled = TranslucencyEnabled,
            CustomAccent = NormalizeCustomAccent(CustomAccent)
        };

    public static double NormalizeGlowIntensity(double value) =>
        double.IsFinite(value)
            ? Math.Clamp(value, MinimumGlowIntensity, MaximumGlowIntensity)
            : DefaultGlowIntensity;

    public static string NormalizeDensity(string? value) =>
        string.Equals(value, AppearanceDensityIds.Comfortable, StringComparison.Ordinal)
            ? AppearanceDensityIds.Comfortable
            : AppearanceDensityIds.Compact;

    public static string? NormalizeCustomAccent(string? value) =>
        value is { Length: 7 } && Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)
            ? value!.ToUpperInvariant()
            : null;
}
