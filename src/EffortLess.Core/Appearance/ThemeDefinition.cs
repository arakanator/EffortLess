using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace EffortLess.Core.Appearance;

public static class AppearanceTokens
{
    public const string PlayerShellBrush = "PlayerShellBrush";
    public const string PlayerVideoBrush = "PlayerVideoBrush";
    public const string PlayerTextBrush = "PlayerTextBrush";
    public const string PlayerTitleBrush = "PlayerTitleBrush";
    public const string PlayerSecondaryBrush = "PlayerSecondaryBrush";
    public const string PlayerFocusBrush = "PlayerFocusBrush";
    public const string PlayerHoverBrush = "PlayerHoverBrush";
    public const string PlayerPressedBrush = "PlayerPressedBrush";

    public const string PlayerPanelBrush = "PlayerPanelBrush";
    public const string ElevatedPanelBrush = "PlayerElevatedPanelBrush";
    public const string BorderBrush = "PlayerBorderBrush";
    public const string AccentBrush = "PlayerAccentBrush";
    public const string AccentGlowBrush = "PlayerAccentGlowBrush";
    public const string SelectedBrush = "PlayerSelectedBrush";
    public const string DisabledBrush = "PlayerDisabledBrush";
    public const string SeparatorBrush = "PlayerSeparatorBrush";
    public const string IconBrush = "PlayerIconBrush";

    public const string SliderOuterThumbBackground = "SliderOuterThumbBackground";
    public const string SliderThumbBorderBrush = "SliderThumbBorderBrush";
    public const string SliderThumbBackground = "SliderThumbBackground";
    public const string SliderThumbBackgroundPointerOver = "SliderThumbBackgroundPointerOver";
    public const string SliderThumbBackgroundPressed = "SliderThumbBackgroundPressed";
    public const string SliderThumbBackgroundDisabled = "SliderThumbBackgroundDisabled";
    public const string SliderTrackFill = "SliderTrackFill";
    public const string SliderTrackFillPointerOver = "SliderTrackFillPointerOver";
    public const string SliderTrackFillPressed = "SliderTrackFillPressed";
    public const string SliderTrackFillDisabled = "SliderTrackFillDisabled";
    public const string SliderTrackValueFill = "SliderTrackValueFill";
    public const string SliderTrackValueFillPointerOver = "SliderTrackValueFillPointerOver";
    public const string SliderTrackValueFillPressed = "SliderTrackValueFillPressed";
    public const string SliderTrackValueFillDisabled = "SliderTrackValueFillDisabled";

    public static IReadOnlyList<string> AllBrushKeys { get; } = new[]
    {
        PlayerShellBrush, PlayerVideoBrush, PlayerTextBrush, PlayerTitleBrush, PlayerSecondaryBrush,
        PlayerFocusBrush, PlayerHoverBrush, PlayerPressedBrush, PlayerPanelBrush, ElevatedPanelBrush,
        BorderBrush, AccentBrush, AccentGlowBrush, SelectedBrush, DisabledBrush, SeparatorBrush, IconBrush,
        SliderOuterThumbBackground, SliderThumbBorderBrush, SliderThumbBackground,
        SliderThumbBackgroundPointerOver, SliderThumbBackgroundPressed, SliderThumbBackgroundDisabled,
        SliderTrackFill, SliderTrackFillPointerOver, SliderTrackFillPressed, SliderTrackFillDisabled,
        SliderTrackValueFill, SliderTrackValueFillPointerOver, SliderTrackValueFillPressed,
        SliderTrackValueFillDisabled
    };
}

public readonly record struct AppearanceColor(byte A, byte R, byte G, byte B)
{
    public const string VideoBackgroundHex = "#101113";

    public static AppearanceColor VideoBackground { get; } = FromHex(VideoBackgroundHex);

    public static AppearanceColor FromHex(string value)
    {
        if (!TryFromHex(value, out var color))
            throw new ArgumentException($"'{value}' is not a valid #RGB or #ARGB color.", nameof(value));
        return color;
    }

    public static bool TryFromHex(string? value, out AppearanceColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        if (span.Length < 1 || span[0] != '#')
            return false;

        if (span.Length == 7)
        {
            if (!TryParseByte(span.Slice(1, 2), out var r) ||
                !TryParseByte(span.Slice(3, 2), out var g) ||
                !TryParseByte(span.Slice(5, 2), out var b))
                return false;

            color = new AppearanceColor(255, r, g, b);
            return true;
        }

        if (span.Length == 9)
        {
            if (!TryParseByte(span.Slice(1, 2), out var a) ||
                !TryParseByte(span.Slice(3, 2), out var r) ||
                !TryParseByte(span.Slice(5, 2), out var g) ||
                !TryParseByte(span.Slice(7, 2), out var b))
                return false;

            color = new AppearanceColor(a, r, g, b);
            return true;
        }

        return false;
    }

    public AppearanceColor WithAlpha(byte alpha) => new(alpha, R, G, B);

    public AppearanceColor WithAlpha(double opacity) => WithAlpha(ToAlpha(opacity));

    public static byte ToAlpha(double opacity) =>
        (byte)Math.Clamp((int)Math.Round(opacity * 255.0), 0, 255);

    private static bool TryParseByte(ReadOnlySpan<char> text, out byte value) =>
        byte.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
}

public readonly record struct AppearanceThickness(double Left, double Top, double Right, double Bottom)
{
    public static AppearanceThickness Uniform(double value) => new(value, value, value, value);
}

public sealed record AppearanceDimensions(
    double HeaderHeight,
    double TransportHeight,
    double IconSize,
    double PlayIconSize,
    double InfoIconSize,
    double TitleFontSize,
    double TimeFontSize,
    double ControlSize,
    double VolumeWidth,
    double SmallSpacing,
    double ControlSpacing,
    double VolumeSpacing,
    double QueueWidth,
    double CompactWidth,
    double MinimumWidth,
    double MinimumHeight,
    double NarrowWidth,
    double NarrowVolumeWidth,
    double QueueMinimumWindowWidth,
    double DetailsMinWidth,
    double DetailsMaxWidth,
    double EmptyTitleFontSize,
    double NoticesHeight,
    double DetailsCornerRadius,
    double ButtonCornerRadius,
    double FocusThickness,
    AppearanceThickness SliderFocusMargin,
    AppearanceThickness HeaderPadding,
    AppearanceThickness TransportPadding,
    double DetailsPadding,
    TimeSpan HoverDuration)
{
    public static AppearanceDimensions Compact { get; } = new(
        56, 88, 16, 20, 14, 13, 11, 32, 72, 8, 14, 10,
        260, 440, 440, 260, 600, 48, 700, 190, 360, 18, 280,
        8, 5, 1,
        new AppearanceThickness(-3, 0, -3, 0),
        new AppearanceThickness(8, 0, 0, 0),
        new AppearanceThickness(12, 0, 12, 0),
        16,
        TimeSpan.FromMilliseconds(80));

    public static AppearanceDimensions Comfortable { get; } = new(
        64, 108, 20, 24, 17, 14, 13, 40, 88, 10, 18, 12,
        300, 520, 520, 300, 680, 60, 800, 220, 420, 20, 320,
        8, 6, 1,
        new AppearanceThickness(-4, 0, -4, 0),
        new AppearanceThickness(10, 0, 0, 0),
        new AppearanceThickness(16, 0, 16, 0),
        20,
        TimeSpan.FromMilliseconds(90));

    public static AppearanceDimensions ForDensity(string density) =>
        string.Equals(density, AppearanceDensityIds.Comfortable, StringComparison.Ordinal)
            ? Comfortable
            : Compact;
}

public sealed record ResolvedAppearance(
    AppearanceSettings Settings,
    ThemeDefinition Theme,
    IReadOnlyDictionary<string, AppearanceColor> Colors,
    AppearanceDimensions Dimensions);

public sealed class ThemeDefinition
{
    private const double DefaultPanelOpacity = 0.94;
    private const double DefaultElevatedPanelOpacity = 0.97;
    private const double DefaultDisabledOpacity = 0.45;
    private const double DefaultSelectedOpacity = 0.28;

    private static readonly IReadOnlyDictionary<string, ThemeDefinition> PresetById = BuildPresets();

    public string Id { get; }
    public string DisplayName { get; }
    public IReadOnlyDictionary<string, string> Palette { get; }
    public double PanelOpacity { get; }
    public double ElevatedPanelOpacity { get; }
    public double DisabledOpacity { get; }
    public double SelectedOpacity { get; }

    private ThemeDefinition(
        string id,
        string displayName,
        IReadOnlyDictionary<string, string> mainPalette,
        double panelOpacity,
        double elevatedPanelOpacity,
        double disabledOpacity,
        double selectedOpacity)
    {
        Id = id;
        DisplayName = displayName;
        PanelOpacity = panelOpacity;
        ElevatedPanelOpacity = elevatedPanelOpacity;
        DisabledOpacity = disabledOpacity;
        SelectedOpacity = selectedOpacity;

        var palette = new Dictionary<string, string>(mainPalette, StringComparer.Ordinal);
        palette[AppearanceTokens.AccentGlowBrush] = palette[AppearanceTokens.AccentBrush];
        palette[AppearanceTokens.SelectedBrush] = palette[AppearanceTokens.AccentBrush];
        AddSliderPalette(palette);
        Palette = new ReadOnlyDictionary<string, string>(palette);
    }

    public static IReadOnlyDictionary<string, ThemeDefinition> Presets => PresetById;
    public static ThemeDefinition Default => PresetById[AppearancePresetIds.Default];

    public static ThemeDefinition? Find(string? id) =>
        !string.IsNullOrEmpty(id) && PresetById.TryGetValue(id, out var theme) ? theme : null;

    public static ThemeDefinition Get(string id) => Find(id) ?? Default;

    public static ResolvedAppearance Resolve(AppearanceSettings settings) => Default.ResolveCore(settings);

    private ResolvedAppearance ResolveCore(AppearanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();
        var theme = Get(normalized.ThemeId);
        var accent = AppearanceColor.FromHex(
            normalized.CustomAccent ?? theme.Palette[AppearanceTokens.AccentBrush]);

        var colors = new Dictionary<string, AppearanceColor>(StringComparer.Ordinal);
        foreach (var entry in theme.Palette)
            colors[entry.Key] = theme.ApplyOpacity(entry.Key, AppearanceColor.FromHex(entry.Value), normalized);

        byte glowAlpha = normalized.GlowEnabled
            ? AppearanceColor.ToAlpha(normalized.GlowIntensity)
            : (byte)0;

        colors[AppearanceTokens.AccentBrush] = accent.WithAlpha(255);
        colors[AppearanceTokens.AccentGlowBrush] = accent.WithAlpha(glowAlpha);
        colors[AppearanceTokens.SelectedBrush] = accent.WithAlpha(AppearanceColor.ToAlpha(theme.SelectedOpacity));
        var focus = ContrastRatio(accent, colors[AppearanceTokens.PlayerShellBrush]) >= 3
            ? accent : colors[AppearanceTokens.PlayerTextBrush];
        colors[AppearanceTokens.PlayerFocusBrush] = focus;
        foreach (var token in new[] { AppearanceTokens.SliderTrackValueFill,
            AppearanceTokens.SliderTrackValueFillPointerOver, AppearanceTokens.SliderTrackValueFillPressed,
            AppearanceTokens.SliderThumbBackground, AppearanceTokens.SliderThumbBackgroundPointerOver,
            AppearanceTokens.SliderThumbBackgroundPressed }) colors[token] = accent;
        colors[AppearanceTokens.SliderThumbBackground] = focus;
        colors[AppearanceTokens.SliderThumbBackgroundPointerOver] = focus;
        if (theme.Id is AppearancePresetIds.NeutralGraphite or AppearancePresetIds.DarkBlack)
            colors[AppearanceTokens.AccentGlowBrush] = accent.WithAlpha(glowAlpha / 12d / 255d);
        colors[AppearanceTokens.PlayerVideoBrush] = AppearanceColor.VideoBackground.WithAlpha(255);

        return new ResolvedAppearance(
            normalized,
            theme,
            new ReadOnlyDictionary<string, AppearanceColor>(colors),
            AppearanceDimensions.ForDensity(normalized.Density));
    }

    private AppearanceColor ApplyOpacity(string token, AppearanceColor color, AppearanceSettings settings)
    {
        byte alpha = token switch
        {
            AppearanceTokens.PlayerPanelBrush when settings.TranslucencyEnabled => AppearanceColor.ToAlpha(PanelOpacity),
            AppearanceTokens.ElevatedPanelBrush when settings.TranslucencyEnabled => AppearanceColor.ToAlpha(ElevatedPanelOpacity),
            AppearanceTokens.DisabledBrush => AppearanceColor.ToAlpha(DisabledOpacity),
            _ => color.A
        };

        return color.WithAlpha(alpha);
    }

    // A user may choose black or another very dark accent. Keep keyboard focus
    // and the seek thumb visible without changing the chosen accent or media.
    private static double ContrastRatio(AppearanceColor first, AppearanceColor second)
    {
        static double Channel(byte value)
        {
            double c = value / 255d;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        static double Luminance(AppearanceColor color) =>
            0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static void AddSliderPalette(IDictionary<string, string> palette)
    {
        palette[AppearanceTokens.SliderOuterThumbBackground] = "#00000000";
        palette[AppearanceTokens.SliderThumbBorderBrush] = "#00000000";
        palette[AppearanceTokens.SliderThumbBackground] = palette[AppearanceTokens.PlayerTextBrush];
        palette[AppearanceTokens.SliderThumbBackgroundPointerOver] = palette[AppearanceTokens.PlayerFocusBrush];
        palette[AppearanceTokens.SliderThumbBackgroundPressed] = palette[AppearanceTokens.AccentBrush];
        palette[AppearanceTokens.SliderThumbBackgroundDisabled] = palette[AppearanceTokens.DisabledBrush];
        palette[AppearanceTokens.SliderTrackFill] = palette[AppearanceTokens.BorderBrush];
        palette[AppearanceTokens.SliderTrackFillPointerOver] = palette[AppearanceTokens.PlayerHoverBrush];
        palette[AppearanceTokens.SliderTrackFillPressed] = palette[AppearanceTokens.PlayerPressedBrush];
        palette[AppearanceTokens.SliderTrackFillDisabled] = palette[AppearanceTokens.DisabledBrush];
        palette[AppearanceTokens.SliderTrackValueFill] = palette[AppearanceTokens.AccentBrush];
        palette[AppearanceTokens.SliderTrackValueFillPointerOver] = palette[AppearanceTokens.PlayerFocusBrush];
        palette[AppearanceTokens.SliderTrackValueFillPressed] = palette[AppearanceTokens.PlayerFocusBrush];
        palette[AppearanceTokens.SliderTrackValueFillDisabled] = palette[AppearanceTokens.DisabledBrush];
    }

    private static IReadOnlyDictionary<string, ThemeDefinition> BuildPresets()
    {
        var presets = new[]
        {
            Create(
                AppearancePresetIds.AuroraCyan,
                "Aurora Cyan",
                MainPalette(
                    "#14191D", "#171D22", "#1B2228", "#2A353D", "#D8E1E6",
                    "#A9B8C2", "#7E8C96", "#86C5DA", "#233038", "#2C3A44",
                    "#5BD3E8", "#565F66", "#2A353D", "#A9B8C2")),
            Create(
                AppearancePresetIds.NeonViolet,
                "Neon Violet",
                MainPalette(
                    "#17151D", "#1A1723", "#201C2B", "#322A45", "#E2DCEF",
                    "#B9AFCB", "#8C829E", "#B39CFF", "#29223A", "#342A47",
                    "#A78BFA", "#5C5667", "#322A45", "#B9AFCB")),
            Create(
                AppearancePresetIds.EmberOrange,
                "Ember Orange",
                MainPalette(
                    "#1B1613", "#1E1814", "#251D17", "#3A2D22", "#F2E3D8",
                    "#C9AD97", "#967F6A", "#FFB37A", "#2E241B", "#3B2D21",
                    "#FF9A4D", "#655A50", "#3A2D22", "#C9AD97")),
            Create(
                AppearancePresetIds.CrimsonRed,
                "Crimson Red",
                MainPalette(
                    "#1A1316", "#1D1518", "#24191D", "#3A2228", "#F2DCE0",
                    "#CBA4AC", "#97777F", "#FF8296", "#2D1B20", "#3A2329",
                    "#FF5C74", "#665158", "#3A2228", "#CBA4AC")),
            Create(
                AppearancePresetIds.NeutralGraphite,
                "Neutral Graphite",
                MainPalette(
                    "#18191C", "#1B1D21", "#202329", "#2C2F36", "#DADCE1",
                    "#BABDC4", "#989CA5", "#AAB6C8", "#2A2C31", "#34373E",
                    "#C0C5CF", "#555962", "#2C2F36", "#BABDC4")),
            Create(AppearancePresetIds.Pink, "Solar Pink", MainPalette(
                "#1C151B", "#211820", "#291D27", "#443040", "#F0E1ED",
                "#D6B8CC", "#B59AAF", "#F0A4D2", "#342330", "#432C3C",
                "#EC8CC8", "#766172", "#443040", "#D6B8CC")),
            Create(AppearancePresetIds.Periwinkle, "Celestial Periwinkle", MainPalette(
                "#141826", "#191E30", "#20263C", "#343E5C", "#E2E7F5",
                "#BCC6E6", "#9BA8CD", "#A8BAFB", "#28314B", "#333D5A",
                "#7898F6", "#626D8C", "#343E5C", "#BCC6E6")),
            Create(AppearancePresetIds.DarkBlack, "Void Black", MainPalette(
                "#050607", "#090A0C", "#101215", "#292C31", "#E3E5E8",
                "#BBC0C8", "#9A9FA8", "#D4D8E0", "#1B1E23", "#292D34",
                "#C4C9D2", "#636870", "#292C31", "#BBC0C8"))
        };

        return new ReadOnlyDictionary<string, ThemeDefinition>(
            presets.ToDictionary(theme => theme.Id, StringComparer.Ordinal));
    }

    private static ThemeDefinition Create(
        string id,
        string displayName,
        IReadOnlyDictionary<string, string> mainPalette) =>
        new(id, displayName, mainPalette, DefaultPanelOpacity, DefaultElevatedPanelOpacity,
            DefaultDisabledOpacity, DefaultSelectedOpacity);

    private static IReadOnlyDictionary<string, string> MainPalette(
        string shell,
        string panel,
        string elevatedPanel,
        string border,
        string text,
        string title,
        string secondary,
        string focus,
        string hover,
        string pressed,
        string accent,
        string disabled,
        string separator,
        string icon) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AppearanceTokens.PlayerVideoBrush] = AppearanceColor.VideoBackgroundHex,
            [AppearanceTokens.PlayerShellBrush] = shell,
            [AppearanceTokens.PlayerPanelBrush] = panel,
            [AppearanceTokens.ElevatedPanelBrush] = elevatedPanel,
            [AppearanceTokens.BorderBrush] = border,
            [AppearanceTokens.PlayerTextBrush] = text,
            [AppearanceTokens.PlayerTitleBrush] = title,
            [AppearanceTokens.PlayerSecondaryBrush] = secondary,
            [AppearanceTokens.PlayerFocusBrush] = focus,
            [AppearanceTokens.PlayerHoverBrush] = hover,
            [AppearanceTokens.PlayerPressedBrush] = pressed,
            [AppearanceTokens.AccentBrush] = accent,
            [AppearanceTokens.DisabledBrush] = disabled,
            [AppearanceTokens.SeparatorBrush] = separator,
            [AppearanceTokens.IconBrush] = icon
        };
}
