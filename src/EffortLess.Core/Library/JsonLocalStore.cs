using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EffortLess.Core.Library;

public sealed class JsonLocalStore<T> where T : class, new()
{
    private const long MaxReadBytes = 2L * 1024 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonLocalStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public async Task<T> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(_path))
                return new T();

            string? json = null;
            // FileShare.Read prevents another Windows writer growing the file
            // between our size check and the bounded read.
            await using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, FileOptions.Asynchronous))
            {
                if (stream.Length <= MaxReadBytes)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true);
                    json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            if (json is null)
            {
                PreserveDamagedFile(cancellationToken);
                return new T();
            }

            T? value;
            try
            {
                value = JsonSerializer.Deserialize<T>(json, SerializerOptions);
            }
            catch (JsonException)
            {
                PreserveDamagedFile(cancellationToken);
                return new T();
            }

            return value ?? new T();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(T data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        string json = JsonSerializer.Serialize(data, SerializerOptions);
        string directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        string tempPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, _path, true);
        }
        finally
        {
            TryDeleteTemp(tempPath);
            _gate.Release();
        }
    }

    private void PreserveDamagedFile(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string corruptPath = CreateCorruptPath(_path);
        File.Move(_path, corruptPath);
    }

    private static string CreateCorruptPath(string path)
    {
        string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture);
        string candidate = $"{path}.{stamp}.corrupt";
        int sequence = 0;
        while (File.Exists(candidate))
        {
            sequence++;
            candidate = $"{path}.{stamp}.{sequence}.corrupt";
        }

        return candidate;
    }

    private static void TryDeleteTemp(string tempPath)
    {
        try
        {
            File.Delete(tempPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
