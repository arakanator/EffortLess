using System.Threading.Channels;

namespace EffortLess.Core.Diagnostics;

/// <summary>Local, bounded diagnostics; native callbacks never wait for disk I/O.</summary>
public sealed class DiagnosticJournal : IAsyncDisposable
{
    private readonly Channel<string> _entries = Channel.CreateBounded<string>(new BoundedChannelOptions(256)
    {
        SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly Task _writer;
    public string FilePath { get; }
    public string? LastError { get; private set; }

    public DiagnosticJournal(string directory)
    {
        FilePath = Path.Combine(directory, "player.log");
        _writer = Task.Run(WriteAsync);
    }
    public void Write(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        string bounded = message[..Math.Min(message.Length, 8192)];
        _entries.Writer.TryWrite($"{DateTime.UtcNow:O} {bounded.TrimEnd()}{Environment.NewLine}");
    }
    private async Task WriteAsync()
    {
        await foreach (string entry in _entries.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length >= 1024 * 1024)
                    File.Move(FilePath, FilePath + ".1", true);
                await File.AppendAllTextAsync(FilePath, entry).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LastError = ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }
    public async ValueTask DisposeAsync()
    {
        _entries.Writer.TryComplete();
        await _writer.ConfigureAwait(false);
    }
}
