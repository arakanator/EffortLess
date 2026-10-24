namespace EffortLess.Core.Library;

public sealed record PlaylistItem(Guid Id, string Path);

public sealed class PlaylistQueue
{
    private readonly List<PlaylistItem> _items = new();

    public IReadOnlyList<PlaylistItem> Items => Array.AsReadOnly(_items.ToArray());

    public int CurrentIndex { get; private set; } = -1;
    public long Revision { get; private set; }

    public PlaylistItem? Current =>
        CurrentIndex >= 0 && CurrentIndex < _items.Count ? _items[CurrentIndex] : null;

    public void Replace(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        List<PlaylistItem> replacement = CreateItems(paths);
        _items.Clear();
        _items.AddRange(replacement);
        Revision++;
        CurrentIndex = _items.Count > 0 ? 0 : -1;
    }

    public void Add(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        List<PlaylistItem> added = CreateItems(paths);
        _items.AddRange(added);
        if (added.Count > 0) Revision++;
        if (CurrentIndex < 0 && _items.Count > 0)
            CurrentIndex = 0;
    }

    public bool Select(Guid id)
    {
        int index = IndexOfId(id);
        if (index < 0)
            return false;

        CurrentIndex = index;
        return true;
    }

    public bool MoveNext()
    {
        if (_items.Count == 0)
            return false;

        if (CurrentIndex >= _items.Count - 1)
            return false;

        CurrentIndex++;
        return true;
    }

    public bool MovePrevious()
    {
        if (_items.Count == 0 || CurrentIndex <= 0)
            return false;

        CurrentIndex--;
        return true;
    }

    public bool Remove(Guid id)
    {
        int index = IndexOfId(id);
        if (index < 0)
            return false;

        _items.RemoveAt(index);
        Revision++;

        if (index < CurrentIndex)
        {
            CurrentIndex--;
        }
        else if (index == CurrentIndex)
        {
            CurrentIndex = _items.Count == 0
                ? -1
                : Math.Min(index, _items.Count - 1);
        }

        return true;
    }

    public bool Move(Guid id, int newIndex)
    {
        if (newIndex < 0 || newIndex >= _items.Count)
            throw new ArgumentOutOfRangeException(nameof(newIndex), newIndex, "Index must be within the bounds of the playlist.");

        int oldIndex = IndexOfId(id);
        if (oldIndex < 0)
            return false;

        if (oldIndex == newIndex)
            return true;

        PlaylistItem item = _items[oldIndex];
        _items.RemoveAt(oldIndex);
        _items.Insert(newIndex, item);
        Revision++;

        if (oldIndex == CurrentIndex)
        {
            CurrentIndex = newIndex;
        }
        else if (oldIndex < CurrentIndex && newIndex >= CurrentIndex)
        {
            CurrentIndex--;
        }
        else if (oldIndex > CurrentIndex && newIndex <= CurrentIndex)
        {
            CurrentIndex++;
        }

        return true;
    }

    private int IndexOfId(Guid id)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Id == id)
                return i;
        }

        return -1;
    }

    private static List<PlaylistItem> CreateItems(IEnumerable<string> paths)
    {
        var items = new List<PlaylistItem>();
        foreach (string path in paths)
            items.Add(new PlaylistItem(Guid.NewGuid(), MediaFiles.NormalizeLocalPath(path)));

        return items;
    }
}
