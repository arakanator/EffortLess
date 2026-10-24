using EffortLess.Core.Library;
using EffortLess.Core.Playback;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace EffortLess.App;

public sealed partial class MainWindow
{
    private async void Notices_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string notices = await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(AppContext.BaseDirectory, "NOTICES.md"));
            if (_closed) return;
            NoticesText.Text = notices;
            NoticesText.Visibility = Visibility.Visible;
        }
        catch (Exception ex) { if (!_closed) ShowError(ex); }
    }
    private void PlayerMenu_Opening(object sender, object args)
    {
        if (_session is null) return;
        var state = _session.State;
        PlayerMenu.Items.Clear();
        AddItem(PlayerMenu.Items, "Open files…", () => OpenButton_Click(MoreButton, new RoutedEventArgs()));
        AddItem(PlayerMenu.Items, "Media information", () =>
            DispatcherQueue.TryEnqueue(() => PlaybackInfoFlyout.ShowAt(TransportOptionsButton)));
        AddItem(PlayerMenu.Items, "Change Appearance", () =>
            DispatcherQueue.TryEnqueue(() => AppearanceFlyout.ShowAt(TransportOptionsButton)));
        AddItem(PlayerMenu.Items, "Modify Playback", () =>
            DispatcherQueue.TryEnqueue(() => PlaybackSettingsFlyout.ShowAt(TransportOptionsButton)));
        AddItem(PlayerMenu.Items, "Subtitle Settings", () =>
            DispatcherQueue.TryEnqueue(() => _subtitleSettingsFlyout.ShowAt(TransportOptionsButton)));
        var recent = new MenuFlyoutSubItem { Text = "Recent files", IsEnabled = state.Recent.Count > 0 };
        foreach (var file in state.Recent)
        {
            var item = AddItem(recent.Items, System.IO.Path.GetFileName(file.Path), () => OpenMedia(file.Path));
            ToolTipService.SetToolTip(item, file.Path);
        }
        PlayerMenu.Items.Add(recent);
        PlayerMenu.Items.Add(new MenuFlyoutSeparator());
        var speed = new MenuFlyoutSubItem { Text = $"Speed · {state.Settings.Speed:0.##}×" };
        foreach (double value in MediaFiles.Speeds)
        {
            var item = new ToggleMenuFlyoutItem { Text = $"{value:0.##}×", IsChecked = Math.Abs(state.Playback.Speed - value) < 0.001 };
            item.Click += (_, _) => RunSessionAction(s => s.SetSpeedAsync(value));
            speed.Items.Add(item);
        }
        PlayerMenu.Items.Add(speed);
        var audio = new MenuFlyoutSubItem { Text = "Audio tracks" };
        foreach (var track in state.Playback.Tracks.Where(t => t.Kind == MediaTrackKind.Audio))
        {
            var item = new ToggleMenuFlyoutItem { Text = track.DisplayName, IsChecked = track.Selected };
            item.Click += (_, _) => RunSessionAction(s => s.SelectAudioAsync(track.Id, state.Playback.FilePath));
            audio.Items.Add(item);
        }
        audio.IsEnabled = audio.Items.Count > 0;
        PlayerMenu.Items.Add(audio);
        var subtitles = new MenuFlyoutSubItem { Text = "Subtitles", IsEnabled = state.Playback.Status is PlaybackStatus.Playing or PlaybackStatus.Paused };
        var off = new ToggleMenuFlyoutItem { Text = "Off", IsChecked = !state.Playback.Tracks.Any(t => t.Kind == MediaTrackKind.Subtitle && t.Selected) };
        off.Click += (_, _) => RunSessionAction(s => s.SelectSubtitleAsync(null, state.Playback.FilePath));
        subtitles.Items.Add(off);
        foreach (var track in state.Playback.Tracks.Where(t => t.Kind == MediaTrackKind.Subtitle))
        {
            var item = new ToggleMenuFlyoutItem { Text = track.DisplayName, IsChecked = track.Selected };
            item.Click += (_, _) => RunSessionAction(s => s.SelectSubtitleAsync(track.Id, state.Playback.FilePath));
            subtitles.Items.Add(item);
        }
        subtitles.Items.Add(new MenuFlyoutSeparator());
        AddItem(subtitles.Items, "Add Subtitles", PickSubtitle);
        PlayerMenu.Items.Add(subtitles);
        AddItem(PlayerMenu.Items, "Save video frame", () => RunSessionAction(s => s.CaptureFrameAsync(
            ((App)Application.Current).CaptureDirectory)))
            .IsEnabled = state.Playback.Tracks.Any(t => t.Kind == MediaTrackKind.Video);
        PlayerMenu.Items.Add(new MenuFlyoutSeparator());
        AddItem(PlayerMenu.Items, _libraryVisible ? "Collapse library panel" : "Expand library panel", ToggleLeftPanel);
        AddItem(PlayerMenu.Items, _informationVisible ? "Collapse information panel" : "Expand information panel", ToggleRightPanel);
        AddItem(PlayerMenu.Items, _compact ? "Restore normal player" : "Compact always-on-top player", ToggleCompactPlayer)
            .IsEnabled = AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter;
        var resume = new ToggleMenuFlyoutItem { Text = "Resume previously viewed files", IsChecked = state.Settings.ResumeEnabled };
        resume.Click += (_, _) => RunSessionAction(s => s.SetResumeAsync(resume.IsChecked));
        PlayerMenu.Items.Add(resume);
    }

    private static MenuFlyoutItem AddItem(IList<MenuFlyoutItemBase> items, string text, Action action)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, _) => action();
        items.Add(item);
        return item;
    }

    private async void PickSubtitle()
    {
        if (_picking || _closed || _session is null) return;
        _picking = true;
        string? mediaPath = _session.State.Playback.FilePath;
        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(AppWindow.Id);
            foreach (string extension in new[] { ".srt", ".ass", ".ssa" }) picker.FileTypeFilter.Add(extension);
            var file = await picker.PickSingleFileAsync();
            if (!_closed && file is not null) await _session.LoadSubtitleAsync(file.Path, mediaPath);
        }
        catch (Exception ex) { if (!_closed) ShowError(ex); }
        finally { _picking = false; }
    }

    private async void PickQueueFiles()
    {
        if (_picking || _closed || _session is null) return;
        _picking = true;
        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(AppWindow.Id);
            picker.FileTypeFilter.Add("*");
            var files = await picker.PickMultipleFilesAsync();
            if (!_closed && files.Count > 0) await _session.OpenAsync(files.Select(f => f.Path), append: true);
        }
        catch (Exception ex) { if (!_closed) ShowError(ex); }
        finally { _picking = false; }
    }

    private void RenderQueue(SessionState state) => RenderLibrary(state);
    private void QueueList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not MediaLibraryRow row) return;
        if (row.Id is { } id) RunSessionAction(s => s.SelectAsync(id));
        else OpenMedia(row.Path);
    }
    private void QueueRemove_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is MediaLibraryRow { Id: not null } row) RunSessionAction(s => s.RemoveAsync(row.Id!.Value));
    }
    private void QueueUp_Click(object sender, RoutedEventArgs e) => MoveQueueItem(-1);
    private void QueueDown_Click(object sender, RoutedEventArgs e) => MoveQueueItem(1);
    private void MoveQueueItem(int offset)
    {
        int target = QueueList.SelectedIndex + offset;
        if (target < 0 || target >= QueueList.Items.Count || QueueList.SelectedItem is not MediaLibraryRow { Id: not null } row) return;
        RunSessionAction(s => s.MoveAsync(row.Id!.Value, target));
    }
    private void Root_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems) ? DataPackageOperation.Copy : DataPackageOperation.None;
        e.Handled = true;
    }
    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (_closed || _session is null) return;
        var deferral = e.GetDeferral();
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) throw new ArgumentException("Drop local media files onto the player.");
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 1000 || items.Any(item => item is not StorageFile)) throw new ArgumentException("Drop up to 1,000 local media files.");
            if (!_closed) await _session.OpenAsync(items.Select(item => item.Path));
        }
        catch (Exception ex) { if (!_closed) ShowError(ex); }
        finally { deferral.Complete(); }
    }
}

