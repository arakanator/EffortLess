namespace EffortLess.Core.Library;

public sealed record MediaRecord(
    string Path,
    long Length,
    long LastWriteUtcTicks,
    double Position,
    double Duration,
    DateTime LastOpenedUtc);

public sealed class MediaHistory
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private readonly int _capacity;
    private readonly List<MediaRecord> _records;

    public MediaHistory(IEnumerable<MediaRecord>? records = null, int capacity = 200)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _capacity = capacity;
        _records = new List<MediaRecord>(Math.Min(capacity, 64));

        if (records is null)
            return;

        foreach (MediaRecord? record in records.Where(record => record is not null).OrderByDescending(record => record.LastOpenedUtc))
        {
            if (record is null || !TryNormalize(record, out MediaRecord normalized))
                continue;

            if (IndexOfPath(normalized.Path) >= 0)
                continue;

            _records.Add(normalized);
            if (_records.Count >= capacity)
                break;
        }
    }

    public IReadOnlyList<MediaRecord> Records => Array.AsReadOnly(_records.ToArray());

    public void Touch(string path)
    {
        string normalizedPath = MediaFiles.NormalizeLocalPath(path);

        FileInfo info = new(normalizedPath);
        info.Refresh();
        if (!info.Exists)
            throw new FileNotFoundException("Media file was not found.", normalizedPath);

        long length = info.Length;
        long lastWriteTicks = info.LastWriteTimeUtc.Ticks;
        DateTime now = DateTime.UtcNow;

        int existing = IndexOfPath(normalizedPath);
        MediaRecord record;
        if (existing >= 0)
        {
            MediaRecord previous = _records[existing];
            record = previous with
            {
                Path = normalizedPath,
                Length = length,
                LastWriteUtcTicks = lastWriteTicks,
                LastOpenedUtc = now
            };
            if (previous.Length != length || previous.LastWriteUtcTicks != lastWriteTicks)
                record = record with { Position = 0, Duration = 0 };
            _records.RemoveAt(existing);
        }
        else
        {
            record = new MediaRecord(normalizedPath, length, lastWriteTicks, 0, 0, now);
        }

        _records.Insert(0, record);
        TrimToCapacity();
    }

    public void RecordPosition(string path, double position, double duration)
    {
        string normalizedPath = MediaFiles.NormalizeLocalPath(path);
        int index = IndexOfPath(normalizedPath);
        if (index < 0)
            return;

        double sanitizedDuration;
        double sanitizedPosition;
        if (!double.IsFinite(duration) || duration <= 0)
        {
            sanitizedDuration = 0;
            sanitizedPosition = 0;
        }
        else
        {
            sanitizedDuration = duration;
            sanitizedPosition = double.IsFinite(position)
                ? Math.Clamp(position, 0, duration)
                : 0;
        }

        MediaRecord current = _records[index];
        if (!TryGetCurrentMetadata(normalizedPath, out long length, out long ticks) ||
            current.Length != length || current.LastWriteUtcTicks != ticks) return;
        _records[index] = current with { Position = sanitizedPosition, Duration = sanitizedDuration };
    }

    public double GetResumePosition(string path)
    {
        string normalizedPath = MediaFiles.NormalizeLocalPath(path);
        int index = IndexOfPath(normalizedPath);
        if (index < 0)
            return 0;

        MediaRecord record = _records[index];
        if (record.Position < 10 || record.Duration <= 0)
            return 0;

        if (record.Duration - record.Position < 20)
            return 0;

        if (record.Position >= record.Duration * 0.95)
            return 0;

        if (!TryGetCurrentMetadata(normalizedPath, out long length, out long lastWriteTicks))
            return 0;

        if (record.Length != length || record.LastWriteUtcTicks != lastWriteTicks)
            return 0;

        return record.Position;
    }

    public IReadOnlyList<MediaRecord> GetRecent(int count = 20)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var result = new List<MediaRecord>(Math.Min(count, _records.Count));
        var retained = new List<MediaRecord>(_records.Count);

        foreach (MediaRecord record in _records)
        {
            bool drop;
            if (TryGetCurrentState(record, out drop))
            {
                if (drop)
                    continue;
            }

            retained.Add(record);
            if (result.Count < count)
                result.Add(record);
        }

        _records.Clear();
        _records.AddRange(retained);
        return result.AsReadOnly();
    }

    private bool TryGetCurrentState(MediaRecord record, out bool drop)
    {
        try
        {
            FileInfo info = new(record.Path);
            info.Refresh();
            if (!info.Exists) { drop = true; return true; }
            drop = record.Length != info.Length || record.LastWriteUtcTicks != info.LastWriteTimeUtc.Ticks;
            return true;
        }
        catch (IOException)
        {
            drop = false;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            drop = false;
            return false;
        }
    }

    private bool TryGetCurrentMetadata(string path, out long length, out long lastWriteTicks)
    {
        length = 0;
        lastWriteTicks = 0;
        try
        {
            FileInfo info = new(path);
            info.Refresh();
            if (!info.Exists)
                return false;

            length = info.Length;
            lastWriteTicks = info.LastWriteTimeUtc.Ticks;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private int IndexOfPath(string path)
    {
        for (int i = 0; i < _records.Count; i++)
        {
            if (PathComparer.Equals(_records[i].Path, path))
                return i;
        }

        return -1;
    }

    private void TrimToCapacity()
    {
        if (_records.Count > _capacity)
            _records.RemoveRange(_capacity, _records.Count - _capacity);
    }

    private static bool TryNormalize(MediaRecord record, out MediaRecord normalized)
    {
        normalized = null!;
        if (string.IsNullOrWhiteSpace(record.Path) ||
            record.Length < 0 ||
            record.LastWriteUtcTicks < 0 || record.LastWriteUtcTicks > DateTime.MaxValue.Ticks ||
            !double.IsFinite(record.Position) || record.Position < 0 ||
            !double.IsFinite(record.Duration) || record.Duration < 0)
        {
            return false;
        }

        try
        {
            normalized = record with { Path = MediaFiles.NormalizeLocalPath(record.Path) };
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }
}
