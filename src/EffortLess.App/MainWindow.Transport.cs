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
    private void PlayPauseButton_Click(object sender, RoutedEventArgs e) => RunSessionAction(s => s.PlayPauseAsync());
    private void FullscreenButton_Click(object sender, RoutedEventArgs e) { ToggleFullscreen(); ControlsActivity(); }

    private void ToggleFullscreen()
    {
        if (_closed) return;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            _savedPresenter = presenter;
            _savedPresenterState = presenter.State;
            _savedPosition = AppWindow.Position;
            _savedSize = AppWindow.Size;
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        else if (_savedPresenter is not null)
        {
            AppWindow.SetPresenter(_savedPresenter);
            if (_savedPresenterState == OverlappedPresenterState.Maximized) _savedPresenter.Maximize();
            else AppWindow.MoveAndResize(new RectInt32(_savedPosition.X, _savedPosition.Y, _savedSize.Width, _savedSize.Height));
        }
        UpdateFullscreenControls();
        QueueVideoHostLayout();
    }

    private void SeekPressed(object sender, PointerRoutedEventArgs e) { _seeking = true; ControlsActivity(); UpdateFullscreenControls(); }
    private void SeekReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_seeking) return;
        _seeking = false;
        ControlsActivity();
        UpdateFullscreenControls();
        RunSessionAction(s => s.SeekAsync(SeekSlider.Value, true));
    }
    private void SeekSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updatingControls) return;
        TimeText.Text = FormatTime(e.NewValue);
        if (!_seeking) RunSessionAction(s => s.SeekAsync(e.NewValue, true));
    }
    private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_updatingControls) RunSessionAction(s => s.SetVolumeAsync(e.NewValue));
    }
    private void FocusVideo()
    {
        if (_closed || _videoWindow is null) return;
        Activate();
        Win32.SetFocus(_videoWindow.Handle);
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.O && PlayerInput.IsKeyDown(0x11) && !PlayerInput.IsKeyDown(0x12))
        {
            PlaybackSettingsFlyout.Hide(); PlaybackInfoFlyout.Hide(); PlayerMenu.Hide(); AppearanceFlyout.Hide(); LibraryFlyout.Hide(); _subtitleSettingsFlyout.Hide();
            e.Handled = HandleKey(e.Key);
            return;
        }
        if (PlaybackSettingsFlyout.IsOpen || PlaybackInfoFlyout.IsOpen || PlayerMenu.IsOpen || AppearanceFlyout.IsOpen || LibraryFlyout.IsOpen || _subtitleSettingsFlyout.IsOpen)
        {
            if (e.Key == VirtualKey.Escape) { PlaybackSettingsFlyout.Hide(); PlaybackInfoFlyout.Hide(); PlayerMenu.Hide(); AppearanceFlyout.Hide(); LibraryFlyout.Hide(); _subtitleSettingsFlyout.Hide(); e.Handled = true; }
            return;
        }
        if (e.Key == VirtualKey.Tab) return;
        var focused = FocusManager.GetFocusedElement(Root.XamlRoot);
        if (focused is TextBox or ComboBox or NumberBox) return;
        if (focused is ListView or ListViewItem && e.Key != VirtualKey.Space) return;
        if (focused is Slider && e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or
            VirtualKey.Down or VirtualKey.Home or VirtualKey.End) return;
        if (focused is Button && e.Key == VirtualKey.Enter) return;
        if (!e.Handled) e.Handled = HandleKey(e.Key);
    }
    private bool HandleKey(VirtualKey key)
    {
        // Do not steal OS/application chords such as Alt+F4 or Ctrl+editing keys.
        if (PlayerInput.IsKeyDown(0x12)) return false;
        if (PlayerInput.IsKeyDown(0x11))
        {
            if (key == VirtualKey.O) { OpenButton_Click(MoreButton, new RoutedEventArgs()); return true; }
            return false;
        }
        switch (key)
        {
            case VirtualKey.Escape:
                if (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen) ToggleFullscreen();
                FocusVideo();
                return true;
            case VirtualKey.F6:
                if (FocusManager.GetFocusedElement(Root.XamlRoot) is Control) FocusVideo();
                else PlayPauseButton.Focus(FocusState.Keyboard);
                return true;
            case VirtualKey.Tab:
                // A native HWND cannot participate in XAML's Tab traversal.
                // Enter the single XAML focus scope explicitly from video.
                (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen ? PlayPauseButton : MoreButton).Focus(FocusState.Keyboard);
                return true;
            case VirtualKey.F: ToggleFullscreen(); return true;
            case VirtualKey.Space: RunSessionAction(s => s.PlayPauseAsync()); return true;
            case VirtualKey.M:
                double volume = _session?.State.Playback.Volume ?? _state.Volume;
                if (volume > 0) { _unmutedVolume = volume; RunSessionAction(s => s.SetVolumeAsync(0)); }
                else RunSessionAction(s => s.SetVolumeAsync(_unmutedVolume));
                return true;
            case VirtualKey.Left:
            case VirtualKey.Right:
                RunSessionAction(s => s.SeekByConfiguredIntervalAsync(key == VirtualKey.Right));
                return true;
            case VirtualKey.Up:
            case VirtualKey.Down:
                RunSessionAction(s => s.SetVolumeAsync(Math.Clamp(s.State.Playback.Volume + (key == VirtualKey.Up ? 5 : -5), 0, 100)));
                return true;
            default: return false;
        }
    }
    private static string FormatTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || seconds > TimeSpan.MaxValue.TotalSeconds - 1) return "--:--";
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes:00}:{time.Seconds:00}";
    }
}
