using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using EffortLess.Core.Native;
using EffortLess.Core.Playback;
using Xunit;

namespace EffortLess.Core.Tests;

public class LibmpvSmokeTests
{
    private const string SmokeEnv = "EFFORTLESS_NATIVE_SMOKE";
    private const string MediaEnv = "EFFORTLESS_TEST_MEDIA";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    private static bool OptedIn =>
        string.Equals(Environment.GetEnvironmentVariable(SmokeEnv), "1", StringComparison.OrdinalIgnoreCase);

    private static string? TestMedia => Environment.GetEnvironmentVariable(MediaEnv);

    private static bool NativeLibraryPresent()
    {
        string expected = MpvLoader.GetExpectedLibraryPath(AppContext.BaseDirectory);
        if (File.Exists(expected))
            return true;

        return false;
    }

    private static void AssertPrerequisites()
    {
        Assert.True(
            NativeLibraryPresent(),
            $"Native smoke requested ({SmokeEnv}=1) but {MpvLoader.LibraryName} was not found under " +
            $"{MpvLoader.GetExpectedLibraryPath(AppContext.BaseDirectory)}. " +
            "Copy the x64 native library into the test output before running this opt-in test.");

        string? media = TestMedia;
        Assert.False(
            string.IsNullOrWhiteSpace(media),
            $"Native smoke requested ({SmokeEnv}=1) but {MediaEnv} was not set.");
        Assert.True(
            File.Exists(media),
            $"Native smoke requested ({SmokeEnv}=1) but the media file does not exist: {media}");
    }

    [NativeSmokeFact]
    public void HeadlessSmoke_LoadPauseSeekVolumeEofReopenErrorRecoveryAndDispose()
    {
        AssertPrerequisites();

        using var harness = new HeadlessHarness();
        string media = TestMedia!;

        harness.Player.Open(media);
        PlaybackState loaded = harness.WaitFor(
            s => s.Status == PlaybackStatus.Playing &&
                 string.Equals(s.FilePath, media, StringComparison.Ordinal),
            DefaultTimeout);
        Assert.Equal(PlaybackStatus.Playing, loaded.Status);
        Assert.Equal(media, loaded.FilePath);
        Assert.False(loaded.Idle);

        PlaybackState withDuration = harness.WaitFor(s => s.Duration > 0, DefaultTimeout);
        Assert.True(withDuration.Duration > 0);
        harness.WaitFor(s => s.Hwdec == HwdecStatus.Software, DefaultTimeout);

        harness.Player.PlayPause();
        PlaybackState paused = harness.WaitFor(s => s.IsPaused, TimeSpan.FromSeconds(3));
        Assert.True(paused.IsPaused);
        Assert.Equal(PlaybackStatus.Paused, paused.Status);

        Thread.Sleep(150);
        double pausedPosition = harness.Latest.Position;
        Thread.Sleep(350);
        PlaybackState stillPaused = harness.Latest;
        Assert.True(stillPaused.IsPaused, "Pause must persist across event-loop iterations.");
        Assert.InRange(Math.Abs(stillPaused.Position - pausedPosition), 0, 0.1);

        double pausedSeek = Math.Min(2, withDuration.Duration / 2);
        harness.Player.SeekAbsolute(pausedSeek);
        harness.WaitFor(s => s.IsPaused && Math.Abs(s.Position - pausedSeek) < 0.15, DefaultTimeout);

        harness.Player.SetVolume(42);
        PlaybackState volumeChanged = harness.WaitFor(
            s => Math.Abs(s.Volume - 42) < 0.01,
            TimeSpan.FromSeconds(3));
        Assert.Equal(42, volumeChanged.Volume);

        harness.Player.PlayPause();
        PlaybackState resumed = harness.WaitFor(
            s => !s.IsPaused && s.Status == PlaybackStatus.Playing,
            TimeSpan.FromSeconds(3));
        Assert.Equal(PlaybackStatus.Playing, resumed.Status);

        double seekTarget = Math.Min(1.0, withDuration.Duration - 0.3);
        harness.Player.SeekAbsolute(seekTarget);
        PlaybackState seeked = harness.WaitFor(
            s => s.Position >= seekTarget - 0.15 && s.Position <= seekTarget + 0.6,
            TimeSpan.FromSeconds(3));
        Assert.True(
            seeked.Position >= seekTarget - 0.15,
            "Seek must surface an actual time-pos property event near the target.");

        harness.Player.SeekAbsolute(withDuration.Duration - 0.2);
        PlaybackState ended = harness.WaitFor(
            s => s.Status == PlaybackStatus.Ended,
            DefaultTimeout);
        Assert.Equal(PlaybackStatus.Ended, ended.Status);
        Assert.True(ended.Idle);

        harness.Player.Open(media);
        PlaybackState reopened = harness.WaitFor(
            s => s.Status == PlaybackStatus.Playing &&
                 string.Equals(s.FilePath, media, StringComparison.Ordinal),
            DefaultTimeout);
        Assert.Equal(PlaybackStatus.Playing, reopened.Status);

        string badPath = Path.Combine(
            Path.GetTempPath(),
            "effortless-does-not-exist-" + Guid.NewGuid().ToString("N") + ".mkv");
        harness.Player.Open(badPath);
        PlaybackState failed = harness.WaitFor(
            s => s.Status == PlaybackStatus.Failed,
            DefaultTimeout);
        Assert.Equal(PlaybackStatus.Failed, failed.Status);
        Assert.False(string.IsNullOrEmpty(failed.Error));

        harness.Player.Open(media);
        PlaybackState recovered = harness.WaitFor(
            s => s.Status == PlaybackStatus.Playing &&
                 string.Equals(s.FilePath, media, StringComparison.Ordinal),
            DefaultTimeout);
        Assert.Equal(PlaybackStatus.Playing, recovered.Status);
        Assert.Null(recovered.Error);

        harness.Player.Dispose();
        Assert.Equal(PlaybackStatus.Disposed, harness.Player.State.Status);
        Assert.Throws<ObjectDisposedException>(() => harness.Player.SetVolume(50));
    }

    [NativeSmokeFact]
    public void ClosingWhileCommandsAreInFlightStopsEventThreadSafely()
    {
        AssertPrerequisites();
        using var harness = new HeadlessHarness();
        harness.Player.Open(TestMedia!);
        harness.WaitFor(s => s.Status == PlaybackStatus.Playing, DefaultTimeout);
        var commands = System.Threading.Tasks.Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                try { harness.Player.SetVolume(i); }
                catch (ObjectDisposedException) { return; }
            }
        });
        harness.Player.Dispose();
        Assert.True(commands.Wait(TimeSpan.FromSeconds(3)));
        Assert.Equal(PlaybackStatus.Disposed, harness.Player.State.Status);
    }

    [NativeSmokeFact]
    public void SpeedAndExternalSubtitleSelectionSurfaceActualNativeState()
    {
        AssertPrerequisites();
        using var harness = new HeadlessHarness();
        string subtitle = Path.Combine(Path.GetTempPath(), "EffortLess 字幕 " + Guid.NewGuid().ToString("N") + ".srt");
        try
        {
            File.WriteAllText(subtitle, "1\n00:00:00,000 --> 00:00:08,000\nNative subtitle test\n");
            harness.Player.Open(TestMedia!);
            harness.WaitFor(s => s.Status == PlaybackStatus.Playing && s.Tracks.Count >= 2, DefaultTimeout);
            harness.Player.PlayPause();
            harness.WaitFor(s => s.IsPaused, DefaultTimeout);
            harness.Player.SetSpeed(1.5);
            harness.WaitFor(s => Math.Abs(s.Speed - 1.5) < 0.001, DefaultTimeout);
            Assert.Throws<ArgumentOutOfRangeException>(() => harness.Player.SetSpeed(double.NaN));
            harness.Player.LoadSubtitle(subtitle);
            PlaybackState withSub = harness.WaitFor(s => s.Tracks.Any(t => t.Kind == MediaTrackKind.Subtitle && t.External && t.Selected), DefaultTimeout);
            var external = withSub.Tracks.First(t => t.Kind == MediaTrackKind.Subtitle && t.External);
            harness.Player.SelectSubtitleTrack(null);
            harness.WaitFor(s => !s.Tracks.Any(t => t.Kind == MediaTrackKind.Subtitle && t.Selected), DefaultTimeout);
            harness.Player.SelectSubtitleTrack(external.Id);
            harness.WaitFor(s => s.Tracks.Any(t => t.Kind == MediaTrackKind.Subtitle && t.Id == external.Id && t.Selected), DefaultTimeout);
            harness.Player.SelectAudioTrack(withSub.Tracks.First(t => t.Kind == MediaTrackKind.Audio).Id);
            harness.Player.SetSpeed(1);
            harness.WaitFor(s => Math.Abs(s.Speed - 1) < 0.001, DefaultTimeout);
        }
        finally { File.Delete(subtitle); }
    }

    [NativeMediaSuiteFact]
    public void RepresentativeFormatsExposeTheExpectedDecodedTracks()
    {
        AssertPrerequisites();
        string directory = Environment.GetEnvironmentVariable("EFFORTLESS_TEST_MEDIA_DIRECTORY")!;
        var cases = new[]
        {
            ("h264.mp4", "h264"), ("hevc.mp4", "hevc"), ("vp9.webm", "vp9"),
            ("av1.webm", "av1"), ("mpeg4.avi", "mpeg4"), ("h264.mov", "h264"),
            ("portrait.mp4", "h264"), ("ultrawide.mp4", "h264"),
            ("Unicode café 東京.mp4", "h264"), ("audio-only.flac", "flac")
        };
        using var harness = new HeadlessHarness();
        var results = new List<string>();
        foreach (var (file, codec) in cases)
        {
            string path = Path.Combine(directory, file);
            harness.Player.Open(path);
            var loaded = harness.WaitFor(s => s.Status == PlaybackStatus.Playing && s.Duration > 0 && s.Position > 0.1 &&
                s.FilePath == path && s.Tracks.Any(t => t.Codec == codec), DefaultTimeout);
            harness.Player.PlayPause();
            harness.WaitFor(s => s.IsPaused, DefaultTimeout);
            Assert.Contains(loaded.Tracks, track => track.Codec == codec);
            results.Add($"{file}: {string.Join(", ", loaded.Tracks.Select(t => t.Kind + "/" + t.Codec))}; duration {loaded.Duration:0.###}");
        }
        File.WriteAllLines(Path.Combine(directory, "native-format-results.txt"), results);
    }

    [NativeMediaSuiteFact]
    public void MultipleAudioEmbeddedAndExternalSubtitlesCanBeSelectedAndDisabled()
    {
        AssertPrerequisites();
        string directory = Environment.GetEnvironmentVariable("EFFORTLESS_TEST_MEDIA_DIRECTORY")!;
        using var harness = new HeadlessHarness();
        harness.Player.Open(Path.Combine(directory, "multi-track.mkv"));
        var loaded = harness.WaitFor(s => s.Status == PlaybackStatus.Playing && s.Tracks.Count(t => t.Kind == MediaTrackKind.Audio) == 2 &&
            s.Tracks.Any(t => t.Kind == MediaTrackKind.Subtitle), DefaultTimeout);
        harness.Player.PlayPause();
        harness.WaitFor(s => s.IsPaused, DefaultTimeout);
        foreach (var track in loaded.Tracks.Where(t => t.Kind == MediaTrackKind.Audio))
        {
            harness.Player.SelectAudioTrack(track.Id);
            harness.WaitFor(s => s.Tracks.Any(t => t.Kind == MediaTrackKind.Audio && t.Id == track.Id && t.Selected), DefaultTimeout);
        }
        var embedded = loaded.Tracks.First(t => t.Kind == MediaTrackKind.Subtitle);
        harness.Player.SelectSubtitleTrack(embedded.Id);
        harness.WaitFor(s => s.Tracks.Any(t => t.Kind == MediaTrackKind.Subtitle && t.Id == embedded.Id && t.Selected), DefaultTimeout);
        foreach (string subtitle in new[] { "external café.srt", "external.ass", "external.ssa" })
        {
            int before = harness.Latest.Tracks.Count;
            harness.Player.LoadSubtitle(Path.Combine(directory, subtitle));
            harness.WaitFor(s => s.Tracks.Count > before && s.Tracks.Any(t => t.Kind == MediaTrackKind.Subtitle && t.External && t.Selected), DefaultTimeout);
        }
        harness.Player.SelectSubtitleTrack(null);
        harness.WaitFor(s => !s.Tracks.Any(t => t.Kind == MediaTrackKind.Subtitle && t.Selected), DefaultTimeout);
        Assert.Throws<ArgumentException>(() => harness.Player.SelectAudioTrack(999999));
    }

    [NativeMediaSuiteFact]
    public void CorruptMediaRecoversAndMalformedSubtitlesDoNotPoisonPlayback()
    {
        AssertPrerequisites();
        string directory = Environment.GetEnvironmentVariable("EFFORTLESS_TEST_MEDIA_DIRECTORY")!;
        using var harness = new HeadlessHarness();
        harness.Player.Open(Path.Combine(directory, "corrupt.mp4"));
        harness.WaitFor(s => s.Status == PlaybackStatus.Failed && s.Error is not null, DefaultTimeout);
        harness.Player.Open(Path.Combine(directory, "h264.mp4"));
        harness.WaitFor(s => s.Status == PlaybackStatus.Playing && s.Duration > 0, DefaultTimeout);
        harness.Player.PlayPause();
        harness.WaitFor(s => s.IsPaused, DefaultTimeout);
        string malformed = Path.Combine(directory, "malformed.srt");
        File.WriteAllText(malformed, "This has no subtitle timestamps or events.");
        Assert.Throws<InvalidOperationException>(() => harness.Player.LoadSubtitle(malformed));
        Assert.Equal(PlaybackStatus.Paused, harness.Player.State.Status);
        Assert.Null(harness.Player.State.Error);
    }

    [NativeSmokeFact]
    public void RepeatedInitializationAndSwitchingReleaseThreadsAndNativeHandles()
    {
        AssertPrerequisites();
        using (var warmup = new HeadlessHarness())
        {
            warmup.Player.Open(TestMedia!);
            warmup.WaitFor(s => s.Status == PlaybackStatus.Playing && s.Duration > 0, DefaultTimeout);
        }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        int initialHandles = process.HandleCount;
        int initialThreads = process.Threads.Count;
        long initialMemory = process.PrivateMemorySize64;
        var samples = new List<string>();
        for (int cycle = 0; cycle < 12; cycle++)
        {
            using (var harness = new HeadlessHarness())
            {
                for (int open = 0; open < 5; open++)
                {
                    harness.Player.Open(TestMedia!);
                    harness.WaitFor(s => s.Status == PlaybackStatus.Playing && s.Duration > 0, DefaultTimeout);
                    harness.Player.PlayPause();
                    harness.WaitFor(s => s.IsPaused, DefaultTimeout);
                    harness.Player.SeekAbsolute(1);
                    harness.Player.SetVolume(50);
                }
            }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            process.Refresh();
            samples.Add($"Cycle {cycle + 1}: handles {process.HandleCount}, threads {process.Threads.Count}, private bytes {process.PrivateMemorySize64}");
        }
        Thread.Sleep(200);
        process.Refresh();
        // Codec/runtime caches and concurrent test infrastructure can retain
        // small pools. This bounds material growth, not every transient handle.
        Assert.InRange(process.HandleCount - initialHandles, -1000, 30);
        Assert.InRange(process.Threads.Count - initialThreads, -1000, 8);
        Assert.InRange(process.PrivateMemorySize64 - initialMemory, -1024L * 1024 * 1024, 96L * 1024 * 1024);
        string? directory = Environment.GetEnvironmentVariable("EFFORTLESS_TEST_MEDIA_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(directory))
            File.WriteAllLines(Path.Combine(directory, "native-resource-results.txt"),
                new[] { $"Baseline: handles {initialHandles}, threads {initialThreads}, private bytes {initialMemory}" }.Concat(samples));
    }

    private sealed class HeadlessHarness : IDisposable
    {
        private readonly object _sync = new();
        private readonly List<string> _logs = new();
        private PlaybackState _latest;

        public HeadlessHarness()
        {
            _latest = PlaybackState.CreateIdle();
            Player = new MpvPlayer(OnState, OnLog);
        }

        public MpvPlayer Player { get; }

        public PlaybackState Latest
        {
            get
            {
                lock (_sync)
                    return _latest;
            }
        }

        public PlaybackState WaitFor(Func<PlaybackState, bool> predicate, TimeSpan timeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                PlaybackState current;
                lock (_sync)
                    current = _latest;

                if (predicate(current))
                    return current;

                Thread.Sleep(20);
            }

            lock (_sync)
                throw new Xunit.Sdk.XunitException($"Playback condition timed out. Last state: {_latest}. Logs: {string.Join("; ", _logs)}");
        }

        public void Dispose() => Player.Dispose();

        private void OnState(PlaybackState state)
        {
            lock (_sync)
                _latest = state;
        }

        private void OnLog(string message)
        {
            lock (_sync)
                _logs.Add(message);
        }
    }
}

public sealed class NativeSmokeFactAttribute : FactAttribute
{
    public NativeSmokeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EFFORTLESS_NATIVE_SMOKE") != "1")
            Skip = "Set EFFORTLESS_NATIVE_SMOKE=1 and EFFORTLESS_TEST_MEDIA to enable native playback tests.";
    }
}

public sealed class NativeMediaSuiteFactAttribute : FactAttribute
{
    public NativeMediaSuiteFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EFFORTLESS_NATIVE_SMOKE") != "1" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EFFORTLESS_TEST_MEDIA_DIRECTORY")))
            Skip = "Generate the media suite and set EFFORTLESS_TEST_MEDIA_DIRECTORY with native smoke enabled.";
    }
}


