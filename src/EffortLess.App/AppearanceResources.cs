using System;
using System.Collections.Generic;
using EffortLess.Core.Appearance;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace EffortLess.App;

public sealed class AppearanceResources
{
    private readonly Dictionary<string, SolidColorBrush> _brushes = new(StringComparer.Ordinal);
    private readonly UISettings _uiSettings = new();

    public ResolvedAppearance? Current { get; private set; }

    public IReadOnlyDictionary<string, SolidColorBrush> Brushes => _brushes;

    public AppearanceDimensions? Dimensions => Current?.Dimensions;

    public ResolvedAppearance Apply(AppearanceSettings settings, bool highContrast)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Apply(ThemeDefinition.Resolve(settings), highContrast);
    }

    public ResolvedAppearance Apply(ResolvedAppearance appearance, bool highContrast)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var resources = Application.Current.Resources;

        foreach (var key in AppearanceTokens.AllBrushKeys)
        {
            var color = ResolveColor(key, appearance, highContrast);
            if (_brushes.TryGetValue(key, out var brush))
            {
                brush.Color = color;
            }
            else
            {
                brush = resources.TryGetValue(key, out var existing) && existing is SolidColorBrush shared
                    ? shared : new SolidColorBrush(color);
                brush.Color = color;
                _brushes[key] = brush;
                resources[key] = brush;
            }
        }

        Current = appearance;
        return appearance;
    }

    private Windows.UI.Color ResolveColor(string key, ResolvedAppearance appearance, bool highContrast)
    {
        if (key == AppearanceTokens.PlayerVideoBrush)
            return ToColor(AppearanceColor.VideoBackground);

        if (!highContrast)
            return ToColor(appearance.Colors[key]);

        return GetHighContrastColor(key);
    }

    private Windows.UI.Color GetHighContrastColor(string key)
    {
        if (key is AppearanceTokens.AccentGlowBrush or AppearanceTokens.SliderOuterThumbBackground
            or AppearanceTokens.SliderThumbBorderBrush) return Windows.UI.Color.FromArgb(0, 0, 0, 0);
        UIElementType type = key switch
        {
            AppearanceTokens.PlayerShellBrush or AppearanceTokens.PlayerPanelBrush or
            AppearanceTokens.ElevatedPanelBrush or AppearanceTokens.SelectedBrush or
            AppearanceTokens.PlayerHoverBrush or AppearanceTokens.PlayerPressedBrush or
            AppearanceTokens.SliderTrackFill or AppearanceTokens.SliderTrackFillPointerOver or
            AppearanceTokens.SliderTrackFillPressed => UIElementType.Window,
            AppearanceTokens.DisabledBrush or AppearanceTokens.SliderThumbBackgroundDisabled or
            AppearanceTokens.SliderTrackFillDisabled or AppearanceTokens.SliderTrackValueFillDisabled
                => UIElementType.GrayText,
            AppearanceTokens.PlayerTextBrush or AppearanceTokens.PlayerTitleBrush or
            AppearanceTokens.PlayerSecondaryBrush or AppearanceTokens.IconBrush => UIElementType.WindowText,
            _ => UIElementType.Highlight
        };
        return _uiSettings.UIElementColor(type);
    }

    private static Windows.UI.Color ToColor(AppearanceColor color) =>
        Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
}
