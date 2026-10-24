using System.Diagnostics;
using EffortLess.Core.Library;
using EffortLess.Core.Playback;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace EffortLess.App;

public sealed partial class MainWindow
{
    private readonly FullscreenControlsPolicy _controlsPolicy = new();
    private readonly DispatcherTimer _controlsHideTimer = new();
    private readonly Stopwatch _controlsClock = Stopwatch.StartNew();
    private readonly HashSet<FlyoutBase> _openPlayerFlyouts = new();
    private bool _informationVisible = true, _syncingPlaybackSettings;
    private PlayerSettings? _presentedSettings;
    private Win32.Point? _lastPointer;
    private Storyboard? _transportAnimation;
    private bool _transportHidden;
    private readonly Windows.UI.ViewManagement.UISettings _animationSettings = new();

    private void InitializeRefinements()
    {
        InitializeSubtitleSettings();
        if (Win32.GetCursorPos(out var initialPointer)) _lastPointer = initialPointer;
        Closed += (_, _) => StopTransportAnimation();
        _controlsHideTimer.Tick += (_, _) =>
        {
            _controlsHideTimer.Stop();
            UpdateControlsPolicy();
            _controlsPolicy.TryHide(_controlsClock.Elapsed);
            PresentControlsVisibility();
            ScheduleControlsHide();
        };
        // Tunnel before Slider's built-in snapping so fractional intervals remain symmetric.
        SeekSlider.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is not (Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right) ||
                PlayerInput.IsKeyDown(0x12) || PlayerInput.IsKeyDown(0x11)) return;
            e.Handled = true;
            RunSessionAction(s => s.SeekByConfiguredIntervalAsync(e.Key == Windows.System.VirtualKey.Right));
        };
        Root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, _) => NativePointerMoved()), true);
        Root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            ControlsActivity();
            // Background clicks end a control interaction; clicks inside a
            // control retain normal XAML focus and text/menu editing semantics.
            DependencyObject? target = e.OriginalSource as DependencyObject;
            while (target is not null && target != Root)
            {
                if (target is Control) return;
                target = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(target);
            }
            if (_openPlayerFlyouts.Count == 0) FocusVideo();
        }), true);
        Root.GotFocus += (_, _) => UpdateFullscreenControls();
        Root.LostFocus += (_, _) => DispatcherQueue.TryEnqueue(UpdateFullscreenControls);
        TransportFrame.PointerEntered += (_, _) => { ControlsActivity(); UpdateFullscreenControls(); };
        VolumeSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => FocusVideo()), true);
        foreach (var flyout in new FlyoutBase[] { PlayerMenu, AppearanceFlyout, PlaybackInfoFlyout, LibraryFlyout, PlaybackSettingsFlyout, _subtitleSettingsFlyout })
        {
            flyout.Opening += (_, _) => { _openPlayerFlyouts.Add(flyout); UpdateFullscreenControls(); };
            flyout.Closed += (_, _) =>
            {
                _openPlayerFlyouts.Remove(flyout);
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_openPlayerFlyouts.Count == 0 && !_picking && !_playerInactive) FocusVideo();
                    UpdateFullscreenControls();
                });
            };
        }
        Activated += (_, e) =>
        {
            _playerInactive = e.WindowActivationState == WindowActivationState.Deactivated;
            UpdateFullscreenControls();
        };
    }
    private bool _playerInactive;

    private void NativeVideoPointerMoved()
    {
        NativePointerMoved();
        UpdateFullscreenControls();
    }
    private void NativePointerMoved()
    {
        if (!Win32.GetCursorPos(out var point)) return;
        // Layout can produce synthetic WM_MOUSEMOVE without actual pointer movement.
        if (_lastPointer is { } old && old.X == point.X && old.Y == point.Y) return;
        _lastPointer = point;
        ControlsActivity();
    }
    private void ControlsActivity()
    {
        if (_closed || Root.XamlRoot is null) return;
        UpdateControlsPolicy();
        _controlsPolicy.Activity(_controlsClock.Elapsed);
        PresentControlsVisibility();
        ScheduleControlsHide();
    }
    private void UpdateControlsPolicy()
    {
        bool fullscreen = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        bool playing = _state.Status == PlaybackStatus.Playing && !_state.IsPaused;
        _controlsPolicy.Update(fullscreen, playing,
            _openPlayerFlyouts.Count > 0 || _picking || _seeking,
            _session?.State.Settings.FullscreenControlsHideDelay ?? 3, _controlsClock.Elapsed);
    }
    private void UpdateFullscreenControls()
    {
        if (_closed || Root.XamlRoot is null) return;
        UpdateControlsPolicy();
        PresentControlsVisibility();
        ScheduleControlsHide();
    }
    private void PresentControlsVisibility()
    {
        bool fullscreen = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        bool hidden = fullscreen && !_controlsPolicy.Visible;
        if (hidden && !_transportHidden && FocusManager.GetFocusedElement(Root.XamlRoot) is Control) FocusVideo();
        _videoWindow?.SetCursorHidden(hidden);
        if (!fullscreen || !_animationSettings.AnimationsEnabled)
        {
            bool changed = _transportAnimation is not null || _transportHidden != hidden;
            StopTransportAnimation();
            _transportHidden = hidden;
            TransportViewport.Height = double.NaN;
            TransportFrame.Height = double.NaN;
            TransportFrame.Opacity = 1;
            TransportViewport.IsHitTestVisible = !hidden;
            TransportViewport.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
            if (changed) QueueVideoHostLayout();
            return;
        }
        if (_transportHidden == hidden) return;

        // Sample effective animated values before stopping so reversal never jumps.
        double height = TransportViewport.Visibility == Visibility.Collapsed ? 0 : TransportViewport.ActualHeight;
        double opacity = TransportViewport.Visibility == Visibility.Collapsed ? 0 : TransportFrame.Opacity;
        StopTransportAnimation();
        _transportHidden = hidden;
        TransportViewport.Visibility = Visibility.Visible;
        TransportViewport.IsHitTestVisible = !hidden;
        TransportFrame.Height = double.NaN;
        TransportFrame.Measure(new Size(Math.Max(0, PlayerCenter.ActualWidth), double.PositiveInfinity));
        double naturalHeight = TransportFrame.DesiredSize.Height;
        // Keep the controls' geometry stable while the viewport clips the shrinking strip.
        TransportFrame.Height = Math.Max(0, naturalHeight - TransportFrame.Margin.Top - TransportFrame.Margin.Bottom);
        TransportViewport.Height = height;
        TransportFrame.Opacity = opacity;
        var duration = new Duration(TimeSpan.FromMilliseconds(VisualSize("PlayerTransportTransitionMilliseconds")));
        var storyboard = new Storyboard();
        AddTransportAnimation(storyboard, TransportViewport, "Height", height, hidden ? 0 : naturalHeight, duration, true);
        AddTransportAnimation(storyboard, TransportFrame, "Opacity", opacity, hidden ? 0 : 1, duration, false);
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_transportAnimation, storyboard) || _closed) return;
            StopTransportAnimation();
            TransportViewport.Visibility = _transportHidden ? Visibility.Collapsed : Visibility.Visible;
            TransportViewport.Height = double.NaN;
            TransportFrame.Height = double.NaN;
            TransportFrame.Opacity = 1;
            QueueVideoHostLayout();
        };
        _transportAnimation = storyboard;
        storyboard.Begin();
    }
    private static void AddTransportAnimation(Storyboard storyboard, DependencyObject target, string property,
        double from, double to, Duration duration, bool dependent)
    {
        var animation = new DoubleAnimation
        {
            From = from, To = to, Duration = duration, EnableDependentAnimation = dependent,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }
    private void StopTransportAnimation()
    {
        var animation = _transportAnimation;
        _transportAnimation = null;
        animation?.Stop();
    }
    private void TransportViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        TransportClip.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
    }
    private void ScheduleControlsHide()
    {
        _controlsHideTimer.Stop();
        if (_closed) return;
        var remaining = _controlsPolicy.GetRemainingDelay(_controlsClock.Elapsed);
        if (remaining is null) return;
        _controlsHideTimer.Interval = remaining.Value > TimeSpan.FromMilliseconds(1) ? remaining.Value : TimeSpan.FromMilliseconds(1);
        _controlsHideTimer.Start();
    }

    private void SyncRefinementSettings()
    {
        SyncSubtitleSettings();
        var settings = _session?.State.Settings;
        if (settings is null || settings == _presentedSettings) return;
        _presentedSettings = settings;
        _libraryVisible = !settings.LeftPanelCollapsed;
        _informationVisible = !settings.RightPanelCollapsed;
        _syncingPlaybackSettings = true;
        try
        {
            HideDelayInput.Value = settings.FullscreenControlsHideDelay;
            SeekIntervalInput.Value = settings.ArrowKeySeekInterval;
            SeekSlider.SmallChange = settings.ArrowKeySeekInterval;
            SeekSlider.LargeChange = settings.ArrowKeySeekInterval;
        }
        finally { _syncingPlaybackSettings = false; }
        UpdatePanelAffordances();
        QueueVideoHostLayout();
    }
    private void PlaybackSetting_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_syncingPlaybackSettings || _session is null || _closed) return;
        double delay = PlayerSettings.NormalizeFullscreenControlsHideDelay(HideDelayInput.Value);
        double interval = PlayerSettings.NormalizeArrowKeySeekInterval(SeekIntervalInput.Value);
        _syncingPlaybackSettings = true;
        try { HideDelayInput.Value = delay; SeekIntervalInput.Value = interval; }
        finally { _syncingPlaybackSettings = false; }
        RunSessionAction(s => s.SetPlaybackPreferencesAsync(delay, interval));
        ControlsActivity();
    }
    private void VideoHost_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_state.FilePath is not null) return;
        e.Handled = true;
        OpenButton_Click(MoreButton, new RoutedEventArgs());
    }
    private void LeftPanelToggle_Click(object sender, RoutedEventArgs e) => ToggleLeftPanel();
    private void RightPanelToggle_Click(object sender, RoutedEventArgs e) => ToggleRightPanel();
    private void ToggleLeftPanel()
    {
        _libraryVisible = !_libraryVisible;
        SavePanelState();
    }
    private void ToggleRightPanel()
    {
        _informationVisible = !_informationVisible;
        SavePanelState();
    }
    private void SavePanelState()
    {
        bool left = !_libraryVisible, right = !_informationVisible;
        RunSessionAction(s => s.SetPanelPreferencesAsync(left, right));
        UpdatePanelAffordances();
        QueueVideoHostLayout();
    }
    private void UpdatePanelAffordances()
    {
        static void Label(Button button, string label)
        {
            AutomationProperties.SetName(button, label);
            ToolTipService.SetToolTip(button, label);
        }
        Label(LeftPanelToggle, _libraryVisible ? "Collapse library panel" : "Expand library panel");
        Label(RightPanelToggle, _informationVisible ? "Collapse information panel" : "Expand information panel");
        LeftPanelToggle.Background = _libraryVisible ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PlayerSelectedBrush"] : null;
        RightPanelToggle.Background = _informationVisible ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PlayerSelectedBrush"] : null;
    }
}
