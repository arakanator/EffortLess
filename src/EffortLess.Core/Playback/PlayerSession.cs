using System.Threading.Channels;
using EffortLess.Core.Library;

namespace EffortLess.Core.Playback;

public sealed record SessionState(PlaybackState Playback, PlayerSettings Settings,
    IReadOnlyList<PlaylistItem> Queue, int CurrentIndex, IReadOnlyList<MediaRecord> Recent,
    string? Notice = null);

/// <summary>Serializes native commands and application policy independently of presentation.</summary>
public sealed class PlayerSession : IAsyncDisposable
{
    private readonly Channel<Action> _commands = Channel.CreateUnbounded<Action>(new() { SingleReader = true });
    private IPlaybackEngine _engine;
    private readonly Func<Action<PlaybackState>, Action<string>, IPlaybackEngine> _factory;
    private readonly Task _worker;
    private readonly JsonLocalStore<PlayerSettings> _settingsStore;
    private readonly JsonLocalStore<HistoryDocument> _historyStore;
    private readonly PlaylistQueue _queue = new();
    private MediaHistory _history = new();
    private PlayerSettings _settings = new();
    private PlaybackState _playback = PlaybackState.CreateIdle();
    private volatile SessionState _state;
    private string? _resumePath;
    private double _resumePosition;
    private bool _ended;
    private DateTime _lastHistorySave;
    private IReadOnlyList<MediaRecord> _recent = Array.Empty<MediaRecord>();
    private int _closing;
    private readonly Action<string> _log;
    private int _stateSignalPending;
    private long _queueRevision = -1;
    private IReadOnlyList<PlaylistItem> _queueView = Array.Empty<PlaylistItem>();
    private bool _storageWarningShown;

    public event Action<SessionState>? Changed;
    public SessionState State => _state;

    public PlayerSession(Func<Action<PlaybackState>, Action<string>, IPlaybackEngine> factory,
        string dataDirectory, Action<string>? log = null)
    {
        _log = log ?? (_ => { });
        _factory = factory;
        _settingsStore = new(Path.Combine(dataDirectory, "settings.json"));
        _historyStore = new(Path.Combine(dataDirectory, "history.json"));
        _state = Snapshot();
        _engine = factory(OnEngineState, _log);
        _worker = Task.Run(PumpAsync);
    }

    public Task InitializeAsync() => Enqueue(() =>
    {
        try
        {
            _settings = _settingsStore.LoadAsync().GetAwaiter().GetResult().Normalize();
            SaveSettings(); // Backfill only after settings were read successfully.
            _history = new(_historyStore.LoadAsync().GetAwaiter().GetResult().Records);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StorageFailure(ex); }
        _recent = _history.GetRecent();
        _playback = _playback with { Volume = _settings.Volume, Speed = _settings.Speed };
        Publish();
    });

    public Task SetSubtitleAppearanceAsync(SubtitleAppearance appearance) => Enqueue(() =>
    {
        var normalized = appearance.Normalize();
        _engine.SetSubtitleAppearance(normalized);
        _settings = _settings with { SubtitleAppearance = normalized };
        SaveSettings();
        Publish();
    });

    public Task ResetSubtitleAppearanceAsync() => SetSubtitleAppearanceAsync(new());

    public Task OpenAsync(IEnumerable<string> paths, bool append = false)
    {
        // Materialize input before returning so the caller can replace its collection.
        string[] files = paths.ToArray();
        return Enqueue(() =>
        {
            if (files.Length > 1000 || (append && _queue.Items.Count + files.Length > 1000))
                throw new ArgumentException("The queue can contain up to 1,000 files.");
            var valid = new List<string>();
            foreach (string path in files)
            {
                string normalized = MediaFiles.NormalizeLocalPath(path);
                if (!MediaFiles.IsMediaPath(normalized)) throw new ArgumentException("Choose a supported local media file.");
                if (!File.Exists(normalized)) throw new FileNotFoundException("The media file is missing or inaccessible.", normalized);
                valid.Add(normalized);
            }
            if (valid.Count == 0) throw new ArgumentException("No local media files were supplied.");
            if (append && _queue.Current is not null) { _queue.Add(valid); Publish(); return; }
            SaveCurrentPosition();
            _queue.Replace(valid);
            OpenCurrent();
        });
    }

    public Task SelectAsync(Guid id) => Enqueue(() => { SaveCurrentPosition(); if (_queue.Select(id)) OpenCurrent(); });
    public Task NextAsync() => Enqueue(() => { SaveCurrentPosition(); if (_queue.MoveNext()) OpenCurrent(); });
    public Task PreviousAsync() => Enqueue(() => { SaveCurrentPosition(); if (_queue.MovePrevious()) OpenCurrent(); });
    public Task RemoveAsync(Guid id) => Enqueue(() =>
    {
        bool current = _queue.Current?.Id == id;
        if (current) SaveCurrentPosition();
        _queue.Remove(id);
        if (current && _queue.Current is not null) OpenCurrent(); else Publish();
    });
    public Task MoveAsync(Guid id, int index) => Enqueue(() => { _queue.Move(id, index); Publish(); });
    public Task PlayPauseAsync() => Enqueue(() => _engine.PlayPause());
    public Task SeekAsync(double seconds, bool absolute = false) => Enqueue(() =>
    {
        if (absolute) _engine.SeekAbsolute(seconds); else _engine.Seek(seconds);
    });
    public Task SetVolumeAsync(double value) => Enqueue(() =>
    {
        _engine.SetVolume(value);
        _settings = (_settings with { Volume = value }).Normalize();
        SaveSettings();
    });
    public Task SetSpeedAsync(double value) => Enqueue(() =>
    {
        if (!MediaFiles.Speeds.Contains(value)) throw new ArgumentOutOfRangeException(nameof(value));
        _engine.SetSpeed(value);
        _settings = _settings with { Speed = value };
        SaveSettings();
    });
    public Task SetPlaybackPreferencesAsync(double hideDelay, double seekInterval) => Enqueue(() =>
    {
        _settings = (_settings with { FullscreenControlsHideDelay = hideDelay, ArrowKeySeekInterval = seekInterval }).Normalize();
        SaveSettings();
        Publish();
    });
    public Task SetPanelPreferencesAsync(bool leftCollapsed, bool rightCollapsed) => Enqueue(() =>
    {
        _settings = _settings with { LeftPanelCollapsed = leftCollapsed, RightPanelCollapsed = rightCollapsed };
        SaveSettings();
        Publish();
    });
    public Task SeekByConfiguredIntervalAsync(bool forward) => Enqueue(() =>
    {
        var playback = _engine.State;
        if (!double.IsFinite(playback.Duration) || playback.Duration <= 0 || !double.IsFinite(playback.Position)) return;
        double target = playback.Position + (forward ? 1 : -1) * _settings.ArrowKeySeekInterval;
        _engine.SeekAbsolute(Math.Clamp(target, 0, playback.Duration));
    });
    public Task SetResumeAsync(bool enabled) => Enqueue(() => { _settings = _settings with { ResumeEnabled = enabled }; SaveSettings(); Publish(); });
    public Task SaveWindowAsync(WindowPlacement placement) => Enqueue(() =>
    {
        _settings = (_settings with { Window = placement }).Normalize();
        SaveSettings();
    });
    public Task SelectAudioAsync(long id, string? expectedMediaPath = null) => Enqueue(() =>
    {
        RequireCurrentMedia(expectedMediaPath);
        _engine.SelectAudioTrack(id);
    });
    public Task SelectSubtitleAsync(long? id, string? expectedMediaPath = null) => Enqueue(() =>
    {
        RequireCurrentMedia(expectedMediaPath);
        _engine.SelectSubtitleTrack(id);
    });
    public Task LoadSubtitleAsync(string path, string? expectedMediaPath = null) => Enqueue(() =>
    {
        RequireCurrentMedia(expectedMediaPath);
        _engine.LoadSubtitle(MediaFiles.NormalizeLocalPath(path));
    });
    public Task CaptureFrameAsync(string directory) => Enqueue(() =>
    {
        Directory.CreateDirectory(directory);
        string name = Path.GetFileNameWithoutExtension(_playback.FilePath ?? "Frame");
        foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        name = name[..Math.Min(name.Length, 80)];
        string path = Path.Combine(directory, $"{name}-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}.png");
        _engine.CaptureFrame(path);
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw new IOException("The player did not produce a screenshot.");
        Publish("Frame saved to " + path);
    });

    private void OpenCurrent()
    {
        if (_queue.Current is not { } current) return;
        _resumePosition = _settings.ResumeEnabled ? _history.GetResumePosition(current.Path) : 0;
        _resumePath = current.Path;
        _history.Touch(current.Path);
        _recent = _history.GetRecent();
        _ended = false;
        if (_engine.State.EngineUnavailable)
        {
            _engine.Dispose();
            _engine = _factory(OnEngineState, _log);
        }
        _engine.Open(current.Path);
        _engine.SetVolume(_settings.Volume);
        _engine.SetSpeed(_settings.Speed);
        _engine.SetSubtitleAppearance(_settings.SubtitleAppearance);
        Publish();
    }

    private void OnEngineState(PlaybackState state)
    {
        if (Volatile.Read(ref _closing) != 0) return;
        if (Interlocked.Exchange(ref _stateSignalPending, 1) != 0) return;
        if (!_commands.Writer.TryWrite(() =>
        {
            Volatile.Write(ref _stateSignalPending, 0);
            if (Volatile.Read(ref _closing) != 0) return;
            // Queued callbacks may lag a newer open command; never let them
            // overwrite a newer engine snapshot with an old file's state.
            state = _engine.State;
            _playback = state;
            if (_resumePath is not null && state.FilePath == _resumePath && state.Duration > 0 &&
                state.Status is PlaybackStatus.Playing or PlaybackStatus.Paused)
            {
                double position = _resumePosition;
                _resumePath = null;
                if (position > 0 && position < state.Duration - 20) _engine.SeekAbsolute(position);
            }
            if (state.Status == PlaybackStatus.Ended && !_ended)
            {
                _ended = true;
                SaveCurrentPosition();
                if (_queue.MoveNext()) { OpenCurrent(); return; }
            }
            else if (state.Status != PlaybackStatus.Ended) _ended = false;
            if (DateTime.UtcNow - _lastHistorySave > TimeSpan.FromSeconds(10))
            {
                SaveCurrentPosition();
                _lastHistorySave = DateTime.UtcNow;
            }
            Publish();
        })) Volatile.Write(ref _stateSignalPending, 0);
    }

    private void SaveCurrentPosition()
    {
        _playback = _engine.State;
        if (_playback.FilePath is { } path)
        {
            if (_playback.Status == PlaybackStatus.Ended)
                _history.RecordPosition(path, 0, 0);
            else if (_playback.Duration > 0)
                _history.RecordPosition(path, _playback.Position, _playback.Duration);
        }
        try { _historyStore.SaveAsync(new HistoryDocument { Records = _history.Records.ToList() }).GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StorageFailure(ex); }
    }
    private void SaveSettings()
    {
        try { _settingsStore.SaveAsync(_settings).GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StorageFailure(ex); }
    }
    private void RequireCurrentMedia(string? expectedPath)
    {
        var current = _engine.State;
        if (current.Status is not (PlaybackStatus.Playing or PlaybackStatus.Paused))
            throw new InvalidOperationException("Open media before choosing tracks or subtitles.");
        if (expectedPath is not null && !StringComparer.OrdinalIgnoreCase.Equals(expectedPath, current.FilePath))
            throw new InvalidOperationException("The media changed. Open the track menu again.");
    }
    private void StorageFailure(Exception ex)
    {
        _log(ex.ToString());
        if (_storageWarningShown) return;
        _storageWarningShown = true;
        Publish("Local preferences could not be saved or read. Playback remains available.");
    }
    private SessionState Snapshot(string? notice = null)
    {
        if (_queueRevision != _queue.Revision)
        {
            _queueRevision = _queue.Revision;
            _queueView = _queue.Items;
        }
        return new(_playback, _settings, _queueView, _queue.CurrentIndex, _recent, notice);
    }
    private void Publish(string? notice = null)
    {
        _state = Snapshot(notice);
        if (Changed is not { } handlers) return;
        foreach (Action<SessionState> handler in handlers.GetInvocationList())
        {
            try { handler(_state); }
            catch (Exception ex) { _log(ex.ToString()); }
        }
    }
    private Task Enqueue(Action action)
    {
        if (Volatile.Read(ref _closing) != 0) return Task.FromException(new ObjectDisposedException(nameof(PlayerSession)));
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_commands.Writer.TryWrite(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception ex) { completion.SetException(ex); }
        })) completion.SetException(new ObjectDisposedException(nameof(PlayerSession)));
        return completion.Task;
    }
    private async Task PumpAsync()
    {
        try
        {
            await foreach (Action action in _commands.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try { action(); }
                catch (Exception ex) { _log(ex.ToString()); Publish("The player could not complete the operation."); }
            }
        }
        finally
        {
            // Disposal is after the channel drains, not an item within it:
            // an in-flight writer can otherwise enqueue behind the dispose item.
            try { _playback = _engine.State; SaveCurrentPosition(); SaveSettings(); }
            catch (Exception ex) { _log(ex.ToString()); }
            finally { _engine.Dispose(); }
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _closing, 1) == 0)
        {
            _commands.Writer.TryComplete();
        }
        await _worker.ConfigureAwait(false);
    }

    public sealed class HistoryDocument
    {
        public List<MediaRecord> Records { get; set; } = new();
    }
}

