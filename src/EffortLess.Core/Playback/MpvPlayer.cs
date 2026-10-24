using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using EffortLess.Core.Native;

namespace EffortLess.Core.Playback;

public sealed partial class MpvPlayer : IPlaybackEngine
{
    private readonly Action<PlaybackState> _send;
    private readonly Action<string> _log;
    private readonly bool _headless;
    private readonly nint _hwnd;

    private readonly object _gate = new();
    private readonly ManualResetEventSlim _ready = new(false);

    private IntPtr _mpv;
    private Thread? _workerThread;
    private volatile PlaybackState _state = PlaybackState.CreateIdle();
    private string? _fatalError;
    private bool _expectStartFile;
    private bool _selfDisposed;
    private volatile bool _disposed;

    public MpvPlayer(nint hwnd, Action<PlaybackState> send, Action<string> log)
        : this(hwnd, send, log, headless: false)
    {
    }

    internal MpvPlayer(Action<PlaybackState> send, Action<string> log)
        : this(IntPtr.Zero, send, log, headless: true)
    {
    }

    private MpvPlayer(nint hwnd, Action<PlaybackState> send, Action<string> log, bool headless)
    {
        _hwnd = hwnd;
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _headless = headless;
    }

    public PlaybackState State => _state;

    public void Dispose()
    {
        bool self;
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            self = _workerThread is not null && ReferenceEquals(Thread.CurrentThread, _workerThread);
            if (self)
                _selfDisposed = true;
        }

        if (self)
            return;

        Thread? thread;
        IntPtr wakeHandle;
        lock (_gate)
        {
            thread = _workerThread;
            wakeHandle = _mpv;
            // Protect against initialization-failure cleanup until wakeup returns.
            // Joining happens outside this lock.
            if (wakeHandle != IntPtr.Zero)
            {
                MpvNative.mpv_wakeup(wakeHandle);
            }
        }

        if (thread is not null)
            thread.Join();

        IntPtr handle = IntPtr.Zero;
        PlaybackState final;
        lock (_gate)
        {
            handle = _mpv;
            _mpv = IntPtr.Zero;
            final = _state with { Status = PlaybackStatus.Disposed, Idle = true };
            _state = final;
        }

        if (handle != IntPtr.Zero)
        {
            try
            {
                MpvNative.mpv_terminate_destroy(handle);
            }
            catch
            {
                // Best effort.
            }
        }

        _send(final);
        _ready.Dispose();
    }

    private void EnsureStarted()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MpvPlayer));

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_workerThread is null)
            {
                _workerThread = new Thread(WorkerMain)
                {
                    IsBackground = true,
                    Name = "EffortLess.mpv"
                };
                _workerThread.Start();
            }
        }

        _ready.Wait();

        lock (_gate)
        {
            if (_fatalError is not null)
                throw new InvalidOperationException("mpv is unavailable: " + _fatalError);
            if (_mpv == IntPtr.Zero)
                throw new InvalidOperationException("mpv is not initialized.");
        }
    }

    private void ThrowIfUnavailable()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MpvPlayer));
        if (_fatalError is not null)
            throw new InvalidOperationException("mpv is unavailable: " + _fatalError);
        if (_mpv == IntPtr.Zero)
            throw new InvalidOperationException("mpv is not initialized.");
    }

    private void WorkerMain()
    {
        IntPtr mpv = IntPtr.Zero;
        bool initialized = false;

        try
        {
            ulong api = MpvNative.mpv_client_api_version();
            if ((api >> 16) != 2 || (api & 0xffff) < 3)
                throw new InvalidOperationException($"Incompatible libmpv client API {api >> 16}.{api & 0xffff}; version 2.3 or newer within major version 2 is required.");
            mpv = MpvNative.mpv_create();
            if (mpv == IntPtr.Zero)
                throw new InvalidOperationException("mpv_create returned null.");

            lock (_gate)
            {
                _mpv = mpv;
            }

            Configure(mpv);

            MpvNative.MpvError initResult = MpvNative.mpv_initialize(mpv);
            if (initResult != MpvNative.MpvError.Success)
                throw new InvalidOperationException($"mpv_initialize failed ({(int)initResult}).");

            ObserveProperties(mpv);
            MpvNative.mpv_request_log_messages(mpv, "warn");

            initialized = true;
            _ready.Set();
            EventLoop(mpv);
        }
        catch (Exception ex)
        {
            PlaybackState failed;
            lock (_gate)
            {
                _fatalError = ex.Message;
                failed = _state with { Status = PlaybackStatus.Failed, Idle = false, Error = ex.Message, EngineUnavailable = true };
                _state = failed;
            }

            _send(failed);
            _log("mpv failed: " + ex.Message);
        }
        finally
        {
            IntPtr handle = IntPtr.Zero;
            PlaybackState? final = null;

            lock (_gate)
            {
                if (!initialized || _selfDisposed)
                {
                    handle = _mpv;
                    _mpv = IntPtr.Zero;
                }

                if (_selfDisposed)
                {
                    final = _state with { Status = PlaybackStatus.Disposed, Idle = true };
                    _state = final;
                }
            }

            if (handle != IntPtr.Zero)
            {
                try
                {
                    if (initialized)
                        MpvNative.mpv_terminate_destroy(handle);
                    else
                        MpvNative.mpv_destroy(handle);
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }

            if (final is not null)
                _send(final);

            _ready.Set();
        }
    }

    private void Configure(IntPtr mpv)
    {
        if (_headless)
        {
            SetOption(mpv, "vo", "null");
            SetOption(mpv, "ao", "null");
            SetOption(mpv, "hwdec", "no");
        }
        else
        {
            if (_hwnd == IntPtr.Zero)
                throw new InvalidOperationException("A non-zero window handle is required for video output.");

            SetOption(mpv, "wid", unchecked((uint)_hwnd.ToInt64()).ToString(CultureInfo.InvariantCulture));
            SetOption(mpv, "vo", "gpu");
            SetOption(mpv, "gpu-context", "d3d11");
            SetOption(mpv, "hwdec", "auto");
        }

        SetOption(mpv, "keep-open", "no");
        SetOption(mpv, "idle", "yes");
        SetOption(mpv, "config", "no");
        SetOption(mpv, "input-default-bindings", "no");
        SetOption(mpv, "input-vo-keyboard", "no");
        // Scripting-free builds do not register these options. An absent
        // script is already disabled; all other failures remain fatal.
        SetOption(mpv, "osc", "no", allowAbsent: true);
        SetOption(mpv, "ytdl", "no", allowAbsent: true);
    }

    private static void SetOption(IntPtr mpv, string name, string value, bool allowAbsent = false)
    {
        MpvNative.MpvError error = MpvNative.mpv_set_option_string(mpv, name, value);
        if (error != MpvNative.MpvError.Success && !(allowAbsent && error == MpvNative.MpvError.OptionNotFound))
            throw new InvalidOperationException($"option {name}={value} failed ({(int)error}).");
    }

    private static void ObserveProperties(IntPtr mpv)
    {
        Observe(mpv, "pause", MpvNative.MpvFormat.Flag);
        Observe(mpv, "time-pos", MpvNative.MpvFormat.Double);
        Observe(mpv, "duration", MpvNative.MpvFormat.Double);
        Observe(mpv, "volume", MpvNative.MpvFormat.Double);
        Observe(mpv, "hwdec-current", MpvNative.MpvFormat.String);
        Observe(mpv, "speed", MpvNative.MpvFormat.Double);
        Observe(mpv, "video-params/w", MpvNative.MpvFormat.Int64);
        Observe(mpv, "video-params/h", MpvNative.MpvFormat.Int64);
        Observe(mpv, "track-list", MpvNative.MpvFormat.Node);
        Observe(mpv, "aid", MpvNative.MpvFormat.String);
        Observe(mpv, "sid", MpvNative.MpvFormat.String);
    }

    private static void Observe(IntPtr mpv, string name, MpvNative.MpvFormat format)
    {
        MpvNative.MpvError error = MpvNative.mpv_observe_property(mpv, 0, name, format);
        if (error != MpvNative.MpvError.Success)
            throw new InvalidOperationException($"observe {name} failed ({(int)error}).");
    }

    internal static string? ReadStringPointer(IntPtr data)
    {
        if (data == IntPtr.Zero)
            return null;

        IntPtr str = Marshal.ReadIntPtr(data);
        return str == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(str);
    }

    private static string? ReadUtf8(IntPtr ptr)
        => ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);

    private static string? ReadString(IntPtr mpv, string name)
    {
        IntPtr raw = MpvNative.mpv_get_property_string(mpv, name);
        if (raw == IntPtr.Zero)
            return null;

        try
        {
            return Marshal.PtrToStringUTF8(raw);
        }
        finally
        {
            MpvNative.mpv_free(raw);
        }
    }

    private static bool ReadFlag(IntPtr mpv, string name)
    {
        string? raw = ReadString(mpv, name);
        return string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDouble(double value)
        => value.ToString("R", CultureInfo.InvariantCulture);
}
