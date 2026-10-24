using EffortLess.Core.Appearance;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace EffortLess.App;

public sealed partial class MainWindow
{
    private readonly AppearanceService _appearance = new(System.IO.Path.Combine(
        ((App)Application.Current).DataDirectory, "appearance.json"));
    private readonly AppearanceResources _appearanceResources = new();
    private readonly AccessibilitySettings _accessibility = new();
    private readonly DispatcherTimer _appearanceSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private AppearanceSettings _appearanceDraft = new();
    private bool _syncingAppearance;
    private bool _lastHighContrast;

    private void InitializeAppearanceResources() => _appearanceResources.Apply(_appearanceDraft, _accessibility.HighContrast);
    private void InitializeAppearance()
    {
        _syncingAppearance = true;
        ThemePreset.DisplayMemberPath = nameof(ThemeDefinition.DisplayName);
        ThemePreset.ItemsSource = ThemeDefinition.Presets.Values.ToArray();
        _syncingAppearance = false;
        _appearanceSaveTimer.Tick += async (_, _) =>
        {
            _appearanceSaveTimer.Stop();
            await SaveAppearanceAsync();
        };
        Closed += (_, _) =>
        {
            _appearanceSaveTimer.Stop();
        };
        SyncAppearanceControls();
        ApplyAppearance();
    }

    private void CheckAppearanceContrast()
    {
        // HighContrastChanged registration can fail for an unpackaged app on
        // this Windows build. Reuse the existing maintenance tick instead.
        if (_lastHighContrast != _accessibility.HighContrast) ApplyAppearance();
    }

    private async Task LoadAppearanceAsync()
    {
        try { _appearanceDraft = await _appearance.LoadAsync(); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        { AppearanceFeedback.Text = "Appearance preferences could not be read. Using defaults."; }
        SyncAppearanceControls();
        ApplyAppearance();
    }

    private void SyncAppearanceControls()
    {
        _syncingAppearance = true;
        try
        {
            ThemePreset.SelectedItem = ThemeDefinition.Get(_appearanceDraft.ThemeId);
            CustomAccent.Text = _appearanceDraft.CustomAccent ?? "";
            GlowEnabled.IsChecked = _appearanceDraft.GlowEnabled;
            GlowIntensity.Value = _appearanceDraft.GlowIntensity;
            DensityChoice.SelectedIndex = _appearanceDraft.Density == AppearanceDensityIds.Comfortable ? 1 : 0;
            TranslucencyEnabled.IsChecked = _appearanceDraft.TranslucencyEnabled;
            var color = ThemeDefinition.Resolve(_appearanceDraft).Colors[AppearanceTokens.AccentBrush];
            AccentPicker.Color = Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
        }
        finally { _syncingAppearance = false; }
    }

    private void Appearance_Changed(object sender, object args)
    {
        if (_syncingAppearance || ThemePreset.SelectedItem is not ThemeDefinition theme) return;
        _appearanceDraft = new AppearanceSettings
        {
            ThemeId = theme.Id,
            CustomAccent = _appearanceDraft.CustomAccent,
            GlowEnabled = GlowEnabled.IsChecked == true,
            GlowIntensity = GlowIntensity.Value,
            Density = DensityChoice.SelectedIndex == 1 ? AppearanceDensityIds.Comfortable : AppearanceDensityIds.Compact,
            TranslucencyEnabled = TranslucencyEnabled.IsChecked == true
        }.Normalize();
        ApplyAppearance();
        // Preset changes update the picker without creating a custom override.
        SyncAppearanceControls();
        ScheduleAppearanceSave();
    }
    private void GlowIntensity_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) =>
        Appearance_Changed(sender, e);

    private void CustomAccent_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_syncingAppearance) return;
        var text = CustomAccent.Text.Trim();
        if (text.Length != 0 && AppearanceSettings.NormalizeCustomAccent(text) is null)
        {
            AppearanceFeedback.Text = "Use a six-digit color such as #35D4EA, or clear it to use the preset.";
            return;
        }
        SetCustomAccent(text.Length == 0 ? null : text);
    }
    private void AccentPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_syncingAppearance) return;
        SetCustomAccent($"#{args.NewColor.R:X2}{args.NewColor.G:X2}{args.NewColor.B:X2}");
    }
    private void SetCustomAccent(string? value)
    {
        _appearanceDraft = new AppearanceSettings
        {
            ThemeId = _appearanceDraft.ThemeId, CustomAccent = value,
            GlowEnabled = _appearanceDraft.GlowEnabled, GlowIntensity = _appearanceDraft.GlowIntensity,
            Density = _appearanceDraft.Density, TranslucencyEnabled = _appearanceDraft.TranslucencyEnabled
        }.Normalize();
        _syncingAppearance = true;
        CustomAccent.Text = _appearanceDraft.CustomAccent ?? "";
        _syncingAppearance = false;
        AppearanceFeedback.Text = "";
        ApplyAppearance();
        ScheduleAppearanceSave();
    }
    private void ScheduleAppearanceSave()
    {
        _appearanceSaveTimer.Stop();
        _appearanceSaveTimer.Start();
    }
    private async Task SaveAppearanceAsync()
    {
        try { await _appearance.UpdateAsync(_appearanceDraft); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            AppearanceFeedback.Text = "The theme is active, but preferences could not be saved.";
            ((App)Application.Current).Diagnostics.Write(ex.ToString());
        }
    }
    private async void ResetAppearance_Click(object sender, RoutedEventArgs e)
    {
        _appearanceSaveTimer.Stop();
        _appearanceDraft = new();
        SyncAppearanceControls();
        ApplyAppearance();
        await SaveAppearanceAsync();
    }

    private void ApplyAppearance()
    {
        _lastHighContrast = _accessibility.HighContrast;
        var resolved = _appearanceResources.Apply(_appearanceDraft, _accessibility.HighContrast);
        var d = resolved.Dimensions;
        // Scalars resolved by StaticResource are not live bindings. Apply the
        // handful of density-sensitive dimensions explicitly to presentation.
        PlayerHeader.Height = d.HeaderHeight;
        TransportStrip.MinHeight = d.TransportHeight;
        QueuePanel.Width = d.QueueWidth;
        foreach (var button in new[] { LeftPanelToggle, RightPanelToggle, MoreButton, SettingsButton, PreviousButton, NextButton,
            LibraryButton, PlaybackInfoButton, TransportOptionsButton, FullscreenButton })
        { button.Width = button.Height = button.MinWidth = button.MinHeight = d.ControlSize; }
        PlayPauseButton.Width = PlayPauseButton.Height = d.ControlSize + d.IconSize;
        PlayPauseButton.CornerRadius = new CornerRadius((d.ControlSize + d.IconSize) / 2);
        PlayPauseIcon.FontSize = d.PlayIconSize;
        SeekSlider.Height = SeekSlider.MinHeight = VolumeSlider.Height = VolumeSlider.MinHeight = d.ControlSize;
        var panel = ThemeBrush("PlayerPanelBrush").Color;
        var glow = ThemeBrush("PlayerAccentGlowBrush").Color;
        TransportFrame.Background = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(0, 1),
            GradientStops = { new GradientStop { Color = Blend(panel, glow), Offset = 0 },
                new GradientStop { Color = panel, Offset = VisualSize("PlayerGlowFalloff") } }
        };
        bool translucent = _appearanceDraft.TranslucencyEnabled && !_accessibility.HighContrast &&
            Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported();
        SystemBackdrop = translucent ? new MicaBackdrop() : null;
        var shell = ThemeBrush("PlayerShellBrush").Color;
        Root.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(translucent ? (byte)Math.Round(255 * VisualSize("PlayerBackdropOpacity")) : (byte)255,
            shell.R, shell.G, shell.B));
        ConfigureCaptionButtons();
        RefreshLibrary();
        if (Root.XamlRoot is { } xamlRoot && AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter
            { State: Microsoft.UI.Windowing.OverlappedPresenterState.Restored })
        {
            int minWidth = (int)Math.Ceiling(d.MinimumWidth * xamlRoot.RasterizationScale);
            int minHeight = (int)Math.Ceiling(d.MinimumHeight * xamlRoot.RasterizationScale);
            if (AppWindow.Size.Width < minWidth || AppWindow.Size.Height < minHeight)
                AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Max(minWidth, AppWindow.Size.Width),
                    Math.Max(minHeight, AppWindow.Size.Height)));
        }
        QueueVideoHostLayout();
    }
    private static Windows.UI.Color Blend(Windows.UI.Color background, Windows.UI.Color glow)
    {
        double a = glow.A / 255d;
        return Windows.UI.Color.FromArgb(255,
            (byte)Math.Round(background.R * (1 - a) + glow.R * a),
            (byte)Math.Round(background.G * (1 - a) + glow.G * a),
            (byte)Math.Round(background.B * (1 - a) + glow.B * a));
    }
}
