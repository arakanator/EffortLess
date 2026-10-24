using EffortLess.Core.Playback;

using EffortLess.Core.Library;

using Microsoft.UI.Windowing;

using Microsoft.UI.Xaml;

using Microsoft.UI.Xaml.Controls;

using Microsoft.UI.Xaml.Controls.Primitives;

using Microsoft.UI.Xaml.Automation;

using Microsoft.UI.Xaml.Input;

using Windows.Graphics;

using Windows.System;

using WinRT.Interop;



namespace EffortLess.App;



public sealed partial class MainWindow : Window

{

    private PlayerSession? _session;

    private readonly DispatcherTimer _inputTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    private ChildVideoWindow? _videoWindow;

    private bool _updatingControls, _seeking, _closed, _loaded, _picking;

    private PlaybackState _state = PlaybackState.CreateIdle();

    private PointInt32 _savedPosition;

    private SizeInt32 _savedSize;

    private OverlappedPresenter? _savedPresenter;

    private OverlappedPresenterState _savedPresenterState;

    private double _unmutedVolume = 100;

    private bool _layoutUpdatePending;

    private WindowPlacement? _normalWindowPlacement;

    private string? _feedback;

    private DateTime _feedbackUntil;

    private IReadOnlyList<MediaTrack>? _detailsTracks;

    private double _detailsSpeed;

    private string? _detailsContainer, _detailsEngine;



    public MainWindow()

    {

        InitializeAppearanceResources();

        InitializeComponent();

        InitializeAppearance();

        InitializeRefinements();

        Title = "EffortLess";

        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "EffortLess.ico"));

        VersionText.Text = "EffortLess " + typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)

            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().First().InformationalVersion.Split('+')[0];

        ExtendsContentIntoTitleBar = true;

        SetTitleBar(TitleDragRegion);

        ConfigureCaptionButtons();

        Activated += (_, e) => PlayerHeader.Opacity = e.WindowActivationState == WindowActivationState.Deactivated ? 0.65 : 1;

        AppWindow.Resize(new SizeInt32(1100, 700));

        AppWindow.Closing += Window_Closing;

        Closed += (_, _) => _videoWindow?.Dispose();

        AppWindow.Changed += (_, _) => QueueVideoHostLayout();

        _inputTimer.Tick += (_, _) =>

        {

            CheckAppearanceContrast();

            _videoWindow?.RouteRendererInputToHost();

            if (_feedback is not null && DateTime.UtcNow >= _feedbackUntil)

            {

                _feedback = null;

                OnStateChanged(_state);

            }

        };

        SeekSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(SeekPressed), true);

        SeekSlider.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(SeekReleased), true);

        SeekSlider.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(SeekReleased), true);

        AutomationProperties.SetName(SeekSlider, "Playback position");

        Root.PreviewKeyDown += Root_KeyDown;

    }



    private async void Root_Loaded(object sender, RoutedEventArgs e)

    {

        if (_loaded) return;

        _loaded = true;

        Root.XamlRoot.Changed += (_, _) =>

        {

            QueueVideoHostLayout();

        };

        await LoadAppearanceAsync();

        if (!EnsurePlayer()) return;

        try

        {

            await _session!.InitializeAsync();

            RestoreWindowPlacement(_session.State.Settings.Window);

            if (((App)Application.Current).MigrationWarning is { } migrationWarning)
                ShowFeedback(migrationWarning);

        }

        catch (Exception ex) { ShowError(ex); }

        _inputTimer.Start();



        string[] mediaPaths = Environment.GetCommandLineArgs().Skip(1)

            .Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToArray();

        if (mediaPaths.Length > 0) RunSessionAction(s => s.OpenAsync(mediaPaths));

    }



    private bool EnsurePlayer()

    {

        if (_closed) return false;

        if (_session is not null) return true;

        try

        {

            if (_videoWindow is null)

            {

                _videoWindow = new ChildVideoWindow(WindowNative.GetWindowHandle(this));

                // Host WndProc runs on this UI thread; read modifiers before queuing loses them.

                _videoWindow.KeyDown += key => HandleKey((VirtualKey)key);

                _videoWindow.PointerMoved += NativeVideoPointerMoved;

                _videoWindow.Clicked += () =>

                {

                    ControlsActivity();

                    FocusVideo();

                    DispatcherQueue.TryEnqueue(() => RunSessionAction(s => s.PlayPauseAsync()));

                };

                _videoWindow.DoubleClicked += () => DispatcherQueue.TryEnqueue(ToggleFullscreen);

                _videoWindow.Error += ex => DispatcherQueue.TryEnqueue(() => ShowError(ex));

                _videoWindow.FilesDropped += files => RunSessionAction(s => s.OpenAsync(files));

                QueueVideoHostLayout();

            }

            string directory = ((App)Application.Current).DataDirectory;

            _session = new PlayerSession((send, log) => new MpvPlayer(_videoWindow.Handle, send, log),

                directory, message => ((App)Application.Current).Diagnostics.Write(message));

            _session.Changed += Session_Changed;

            return true;

        }

        catch (Exception ex)

        {

            // A failed lazy initialization must not permanently poison later opens.

            ShowError(ex);

            return false;

        }

    }



    private void OnStateChanged(PlaybackState state)

    {

        if (_closed) return;

        // Queued event notifications can cross command callbacks. Read the latest

        // immutable snapshot so an older notification cannot rewind the controls.

        state = _session?.State.Playback ?? state;

        _state = state;

        SyncRefinementSettings();

        UpdateFullscreenControls();

        bool video = state.Status == PlaybackStatus.Opening ||

            (state.Status is PlaybackStatus.Playing or PlaybackStatus.Paused && state.Tracks.Any(track => track.Kind == MediaTrackKind.Video));

        bool leavingNativeFocus = !video && _videoWindow is not null && Win32.GetFocus() == _videoWindow.Handle;
        _videoWindow?.SetVisible(video);
        if (leavingNativeFocus) TransportOptionsButton.Focus(FocusState.Keyboard);

        EmptyState.Visibility = video ? Visibility.Collapsed : Visibility.Visible;

        EmptyTitle.Text = state.Status == PlaybackStatus.Failed ? "This file couldn’t be played" :

            state.Status == PlaybackStatus.Ended ? "Playback finished" : state.FilePath is null ? "Open a video" : "Audio playback";

        EmptyDescription.Text = state.Status == PlaybackStatus.Failed ? "Choose another file. Details are available in Media information." :

            state.Status == PlaybackStatus.Ended ? "Press Play to start again" : state.FilePath is null ? "Double-click to open a video · drop a file or press Ctrl+O" : System.IO.Path.GetFileName(state.FilePath);

        StatusText.Text = DateTime.UtcNow < _feedbackUntil ? _feedback :

            state.Error is not null ? "Unable to play this file · open Media information for details" :

            (state.FilePath is null ? "EffortLess" : System.IO.Path.GetFileName(state.FilePath));

        PlaybackStatusText.Text = state.Error ?? state.Status.ToString();

        ToolTipService.SetToolTip(StatusText, state.Error ?? state.FilePath);

        bool showPlay = state.IsPaused || state.Status is PlaybackStatus.Idle or PlaybackStatus.Ended;

        PlayPauseIcon.Glyph = showPlay ? "\uE768" : "\uE769";

        AutomationProperties.SetName(PlayPauseButton, showPlay ? "Play" : "Pause");

        ToolTipService.SetToolTip(PlayPauseButton, showPlay ? "Play" : "Pause");

        PlayPauseButton.IsEnabled = state.FilePath is not null && state.Status is not (PlaybackStatus.Opening or PlaybackStatus.Failed or PlaybackStatus.Disposed);

        SeekSlider.IsEnabled = state.Duration > 0 && state.Status is PlaybackStatus.Playing or PlaybackStatus.Paused;

        _updatingControls = true;

        if (!_seeking)

        {

            SeekSlider.Maximum = Math.Max(1, state.Duration);

            SeekSlider.Value = Math.Clamp(state.Position, 0, SeekSlider.Maximum);

            TimeText.Text = FormatTime(state.Position);

            DurationText.Text = FormatTime(state.Duration);

        }

        VolumeSlider.Value = Math.Clamp(state.Volume, 0, 100);

        _updatingControls = false;

        RenderMediaInformation(state);

        HwdecText.Text = state.Hwdec switch

        {

            HwdecStatus.Hardware => "Decoder: " + state.HwdecName,

            HwdecStatus.Software => "Decoder: software",

            HwdecStatus.Unknown => "Decoder: unknown",

            _ => "Decoder: idle"

        };

        if (!ReferenceEquals(state.Tracks, _detailsTracks) || state.Speed != _detailsSpeed ||

            state.Container != _detailsContainer || state.EngineVersion != _detailsEngine)

        {

            _detailsTracks = state.Tracks;

            _detailsSpeed = state.Speed;

            _detailsContainer = state.Container;

            _detailsEngine = state.EngineVersion;

            MediaDetailsText.Text = $"{MediaFormat.ForSource(state.FilePath)} · {state.Speed:0.##}×\n" +

                string.Join("\n", state.Tracks.Select(track => track.DisplayName)) +

                (state.EngineVersion.Length > 0 ? "\n" + state.EngineVersion : "");

        }

    }



    private async void OpenButton_Click(object sender, RoutedEventArgs e)

    {

        if (_picking || _closed) return;

        _picking = true;

        ControlsActivity();

        UpdateFullscreenControls();

        try

        {

            var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(AppWindow.Id);

            picker.FileTypeFilter.Add("*");

            var files = await picker.PickMultipleFilesAsync();

            if (!_closed && files.Count > 0) await _session!.OpenAsync(files.Select(file => file.Path));

        }

        catch (Exception ex) { if (!_closed) ShowError(ex); }

        finally { _picking = false; UpdateFullscreenControls(); }

    }



    private void OpenMedia(string path)

    {

        if (EnsurePlayer()) RunSessionAction(s => s.OpenAsync(new[] { path }));

    }



    private async void RunSessionAction(Func<PlayerSession, Task> action)

    {

        if (_closed || _session is null) return;

        try { await action(_session); } catch (Exception ex) { if (!_closed) ShowError(ex); }

    }



    private void ShowError(Exception ex)

    {

        ((App)Application.Current).Diagnostics.Write(ex.ToString());

        string message = ex switch

        {

            System.IO.FileNotFoundException => "The file is missing or inaccessible.",

            UnauthorizedAccessException => "Access to the file or local player data was denied.",

            DllNotFoundException or BadImageFormatException or EntryPointNotFoundException => "The playback engine is missing or incompatible.",

            _ when ex.Message.Contains("sub-add", StringComparison.Ordinal) => "The subtitle file could not be loaded.",

            _ when ex.Message.Contains("mpv is unavailable", StringComparison.Ordinal) => "The playback engine could not start.",

            _ => ex.Message

        };

        ShowFeedback(message);

        PlaybackStatusText.Text = ex.ToString();

        ToolTipService.SetToolTip(StatusText, ex.ToString());

    }



    private void ShowFeedback(string message)

    {

        _feedback = message;

        _feedbackUntil = DateTime.UtcNow.AddSeconds(6);

        StatusText.Text = message;

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(StatusText,

            Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);

    }



    private bool _allowClose;

    private bool _closing;

    private async void Window_Closing(AppWindow sender, AppWindowClosingEventArgs args)

    {

        if (_allowClose) return;

        args.Cancel = true;

        if (_closed || _closing) return;

        _closing = true;

        _appearanceSaveTimer.Stop();

        await SaveAppearanceAsync();

        if (_session is not null)

        {

            try { await _session.SaveWindowAsync(CurrentWindowPlacement()); }

            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

        }

        _closed = true;

        _inputTimer.Stop();

        _controlsHideTimer.Stop();

        if (_session is not null)

        {

            _session.Changed -= Session_Changed;

            try { await _session.DisposeAsync(); }

            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

        }

        _allowClose = true;

        await ((App)Application.Current).Diagnostics.DisposeAsync();

        Close();

    }



    private void Session_Changed(SessionState state) => DispatcherQueue.TryEnqueue(() =>

    {

        if (_closed) return;

        string? notice = state.Notice;

        state = _session?.State ?? state;

        OnStateChanged(state.Playback);

        RenderQueue(state);

        if (notice is not null)

        {

            PlaybackStatusText.Text = notice;

            ShowFeedback(notice.StartsWith("Frame saved", StringComparison.Ordinal) ? "Video frame saved to Pictures / EffortLess" : notice);

        }

    });





}





