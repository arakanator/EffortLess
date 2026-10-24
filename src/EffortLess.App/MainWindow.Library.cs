using EffortLess.Core.Playback;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EffortLess.App;

public sealed partial class MainWindow
{
    private readonly MediaThumbnailService _thumbnails = new();
    private bool _recentView;
    private bool _libraryVisible = true;
    private SessionState? _libraryState;
    private string? _infoPath;
    private string _fileMetadata = "";

    private void PlaylistNav_Click(object sender, RoutedEventArgs e)
    {
        _recentView = false;
        RefreshLibrary();
    }
    private void RecentNav_Click(object sender, RoutedEventArgs e)
    {
        _recentView = true;
        RefreshLibrary();
    }
    private void RefreshLibrary()
    {
        _libraryState = null;
        if (_session is not null) RenderLibrary(_session.State);
    }
    private void LibraryButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshLibrary();
    }
    private void TransportOptions_Click(object sender, RoutedEventArgs e) =>
        PlayerMenu.ShowAt(TransportOptionsButton);
    private void AddVideo_Click(object sender, RoutedEventArgs e) => PickQueueFiles();
    private void PreviousButton_Click(object sender, RoutedEventArgs e) => RunSessionAction(s => s.PreviousAsync());
    private void NextButton_Click(object sender, RoutedEventArgs e) => RunSessionAction(s => s.NextAsync());

    private void RenderLibrary(SessionState state)
    {
        if (_libraryState is null || !ReferenceEquals(_libraryState.Queue, state.Queue) ||
            !ReferenceEquals(_libraryState.Recent, state.Recent))
        {
            IEnumerable<MediaLibraryRow> rows = _recentView
                ? state.Recent.Select(r => new MediaLibraryRow(null, r.Path, r.Duration))
                : state.Queue.Select(r => new MediaLibraryRow(r.Id, r.Path,
                    state.Recent.FirstOrDefault(h => StringComparer.OrdinalIgnoreCase.Equals(h.Path, r.Path))?.Duration ?? 0));
            QueueList.ItemsSource = rows.ToArray();
            NarrowLibraryList.ItemsSource = QueueList.ItemsSource;
            LibraryCount.Text = QueueList.Items.Count.ToString();
            PlaylistNav.Background = ThemeBrush(_recentView ? "PlayerPanelBrush" : "PlayerSelectedBrush");
            RecentNav.Background = ThemeBrush(_recentView ? "PlayerSelectedBrush" : "PlayerPanelBrush");
            QueueActions.Visibility = _recentView ? Visibility.Collapsed : Visibility.Visible;
        }
        var currentId = state.CurrentIndex >= 0 && state.CurrentIndex < state.Queue.Count
            ? state.Queue[state.CurrentIndex].Id : (Guid?)null;
        var currentRow = QueueList.Items.OfType<MediaLibraryRow>().FirstOrDefault(r => _recentView
            ? StringComparer.OrdinalIgnoreCase.Equals(r.Path, state.Playback.FilePath) : r.Id == currentId);
        if (QueueList.SelectedItem is MediaLibraryRow previous && !ReferenceEquals(previous, currentRow)) previous.IsCurrent = false;
        if (currentRow is not null) currentRow.IsCurrent = true;
        QueueList.SelectedItem = currentRow;
        PreviousButton.IsEnabled = state.CurrentIndex > 0;
        NextButton.IsEnabled = state.CurrentIndex >= 0 && state.CurrentIndex < state.Queue.Count - 1;
        _libraryState = state;
    }

    private async void QueueList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.Item is not MediaLibraryRow row || row.ThumbnailRequested) return;
        row.ThumbnailRequested = true;
        try { row.Thumbnail = await _thumbnails.GetAsync(row.Path); }
        catch (Exception ex) { ((App)Application.Current).Diagnostics.Write("Thumbnail: " + ex.Message); }
    }

    private async void RenderMediaInformation(PlaybackState state)
    {
        string? path = state.FilePath;
        bool audioOnly = state.Tracks.Any(t => t.Kind == MediaTrackKind.Audio) &&
            !state.Tracks.Any(t => t.Kind == MediaTrackKind.Video);
        MediaResolution.Text = "Resolution    " + VideoResolution.Format(state.VideoWidth, state.VideoHeight, audioOnly);
        MediaTitle.Text = path is null ? "No media selected" : System.IO.Path.GetFileNameWithoutExtension(path);
        MediaSummary.Text = path is null ? "Open a local file to start." :
            $"{FormatTime(state.Duration)} · {MediaFormat.ForSource(path)} · {state.Speed:0.##}×";
        if (!StringComparer.OrdinalIgnoreCase.Equals(_infoPath, path))
        {
            _infoPath = path;
            _fileMetadata = "";
            MediaThumbnail.Source = null;
            MediaThumbnail.Visibility = Visibility.Collapsed;
            if (path is not null)
            {
                try
                {
                    // File metadata access is off the UI thread; playback callbacks
                    // only use the cached result and available track metadata.
                    var metadata = await Task.Run(() =>
                    {
                        var file = new System.IO.FileInfo(path);
                        return file.Exists ? $"File size   {file.Length / (1024d * 1024):0.##} MB\n\nLocation\n{file.DirectoryName}" : "File unavailable";
                    });
                    if (_closed || !StringComparer.OrdinalIgnoreCase.Equals(_infoPath, path)) return;
                    _fileMetadata = metadata;
                    var thumbnail = await _thumbnails.GetAsync(path);
                    if (_closed || !StringComparer.OrdinalIgnoreCase.Equals(_infoPath, path)) return;
                    MediaThumbnail.Source = thumbnail;
                    MediaThumbnail.Visibility = thumbnail is null ? Visibility.Collapsed : Visibility.Visible;
                }
                catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
                { _fileMetadata = "File metadata unavailable"; }
            }
        }
        if (_closed || !StringComparer.OrdinalIgnoreCase.Equals(_infoPath, path)) return;
        MediaMetadata.Text = string.Join("\n\n", state.Tracks.Select(t => t.DisplayName)) +
            (_fileMetadata.Length > 0 ? "\n\n" + _fileMetadata : "");
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush ThemeBrush(string key) =>
        (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources[key];
}
