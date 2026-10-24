using EffortLess.Core.Library;
using EffortLess.Core.Playback;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace EffortLess.App;

public sealed partial class MainWindow
{
    private void VideoHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_transportAnimation is not null)
        {
            // The animation already performed layout; do not remeasure the entire shell.
            UpdateVideoHostLayout();
            return;
        }
        QueueVideoHostLayout();
    }

    private void QueueVideoHostLayout()
    {
        if (_closed || _layoutUpdatePending) return;
        _layoutUpdatePending = true;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            // Restore messages run before WinUI finishes updating its content island.
            // Coalesce writers and reconcile the island only after those messages drain.
            try
            {
                if (_closed || Root.XamlRoot is null ||
                    !WindowLayoutInterop.AlignContentToClient(WindowNative.GetWindowHandle(this))) return;
                UpdateShellPresentation();
                Root.UpdateLayout();
                // Side columns change the central player's width. Evaluate its
                // compact transport against the completed column layout.
                UpdateShellPresentation();
                Root.UpdateLayout();
                UpdateVideoHostLayout();
                if (!_compact && AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
                    _normalWindowPlacement = new(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height, false);
            }
            finally { _layoutUpdatePending = false; }
        })) _layoutUpdatePending = false;
    }

    private void UpdateVideoHostLayout()
    {
        if (_closed || _videoWindow is null || Root.XamlRoot is null ||
            WindowLayoutInterop.IsMinimized(WindowNative.GetWindowHandle(this))) return;
        var origin = VideoHost.TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point());
        double scale = Root.XamlRoot.RasterizationScale;
        _videoWindow.MoveAndResize((int)Math.Round(origin.X * scale), (int)Math.Round(origin.Y * scale),
            (int)Math.Round(VideoHost.ActualWidth * scale), (int)Math.Round(VideoHost.ActualHeight * scale));
    }

    private void ConfigureCaptionButtons()
    {
        var bar = AppWindow.TitleBar;
        bar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        bar.ButtonForegroundColor = ThemeColor("PlayerTextBrush");
        bar.ButtonInactiveForegroundColor = ThemeColor("PlayerSecondaryBrush");
        bar.ButtonHoverBackgroundColor = ThemeColor("PlayerHoverBrush");
        bar.ButtonHoverForegroundColor = ThemeColor("PlayerTextBrush");
        bar.ButtonPressedBackgroundColor = ThemeColor("PlayerPressedBrush");
        bar.ButtonPressedForegroundColor = ThemeColor("PlayerTextBrush");
        bar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
    }

    private void UpdateShellPresentation()
    {
        bool fullscreen = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        double scale = Root.XamlRoot.RasterizationScale;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)Math.Ceiling((_appearanceResources.Dimensions?.MinimumWidth ?? VisualSize("PlayerMinimumWidth")) * scale);
            presenter.PreferredMinimumHeight = (int)Math.Ceiling((_appearanceResources.Dimensions?.MinimumHeight ?? VisualSize("PlayerMinimumHeight")) * scale);
        }
        bool narrow = PlayerCenter.ActualWidth < VisualSize("PlayerNarrowWidth");
        TransportStrip.ColumnSpacing = VisualSize(narrow ? "PlayerSmallSpacing" : "PlayerControlSpacing");
        VolumeSlider.Width = narrow ? VisualSize("PlayerNarrowVolumeWidth") : (_appearanceResources.Dimensions?.VolumeWidth ?? VisualSize("PlayerVolumeWidth"));
        VolumeIcon.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        bool library = _libraryVisible && !_compact && !fullscreen && Root.ActualWidth >= VisualSize("PlayerQueueMinimumWindowWidth");
        QueuePanel.Visibility = library ? Visibility.Visible : Visibility.Collapsed;
        InfoPanel.Visibility = _informationVisible && !_compact && !fullscreen && Root.ActualWidth >= VisualSize("PlayerWideWidth") ? Visibility.Visible : Visibility.Collapsed;
        Brand.Visibility = Visibility.Visible;
        // Leave a real title-bar drag area at the smallest supported width.
        BrandLabel.Visibility = !_compact && Root.ActualWidth < VisualSize("PlayerBrandLabelMinimumWidth") ? Visibility.Collapsed : Visibility.Visible;
        LeftPanelToggle.Visibility = RightPanelToggle.Visibility = _compact ? Visibility.Collapsed : Visibility.Visible;
        LibraryButton.Visibility = _compact || fullscreen ? Visibility.Collapsed : Visibility.Visible;
        PlaybackInfoButton.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        PlayerHeader.Visibility = fullscreen ? Visibility.Collapsed : Visibility.Visible;
        MediaRegion.Padding = fullscreen ? new Thickness(0) : (Thickness)Application.Current.Resources["PlayerWorkspacePadding"];
        PresentControlsVisibility();
        UpdatePanelAffordances();
        CaptionInset.Width = new GridLength(fullscreen ? 0 : AppWindow.TitleBar.RightInset / Root.XamlRoot.RasterizationScale);
        FullscreenIcon.Glyph = fullscreen ? "\uE73F" : "\uE740";
        string label = fullscreen ? "Exit fullscreen" : "Fullscreen";
        AutomationProperties.SetName(FullscreenButton, label);
        ToolTipService.SetToolTip(FullscreenButton, label);
    }
    private static double VisualSize(string key) => (double)Application.Current.Resources[key];
    private static Windows.UI.Color ThemeColor(string key) =>
        ((Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources[key]).Color;

    private WindowPlacement CurrentWindowPlacement()
    {
        if (_compact && _beforeCompact is { } beforeCompact) return beforeCompact;
        bool fullscreen = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        var position = fullscreen ? _savedPosition : AppWindow.Position;
        var size = fullscreen ? _savedSize : AppWindow.Size;
        bool maximized = fullscreen ? _savedPresenterState == OverlappedPresenterState.Maximized :
            (AppWindow.Presenter as OverlappedPresenter)?.State == OverlappedPresenterState.Maximized;
        if (maximized && _normalWindowPlacement is { } normal) return normal with { Maximized = true };
        return new(position.X, position.Y, size.Width, size.Height, maximized);
    }

    private void RestoreWindowPlacement(WindowPlacement? placement)
    {
        if (placement is null) return;
        _normalWindowPlacement = placement with { Maximized = false };
        // Clamp to a connected display so unplugging a monitor cannot strand the window.
        var area = DisplayArea.GetFromPoint(new PointInt32(placement.X, placement.Y), DisplayAreaFallback.Nearest).WorkArea;
        double scale = Root.XamlRoot?.RasterizationScale ?? 1;
        int width = Math.Min(Math.Max(placement.Width, (int)Math.Ceiling(VisualSize("PlayerMinimumWidth") * scale)), area.Width);
        int height = Math.Min(Math.Max(placement.Height, (int)Math.Ceiling(VisualSize("PlayerMinimumHeight") * scale)), area.Height);
        AppWindow.MoveAndResize(new RectInt32(Math.Clamp(placement.X, area.X, area.X + area.Width - width),
            Math.Clamp(placement.Y, area.Y, area.Y + area.Height - height), width, height));
        if (placement.Maximized && AppWindow.Presenter is OverlappedPresenter presenter) presenter.Maximize();
    }
}
