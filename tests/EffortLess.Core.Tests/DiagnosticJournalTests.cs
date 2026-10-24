using EffortLess.Core.Diagnostics;
using Xunit;

namespace EffortLess.Core.Tests;

public sealed class DiagnosticJournalTests
{
    [Fact]
    public async Task ShutdownDrainsLogsAndLateWritesAreHarmless()
    {
        string directory = Path.Combine(Path.GetTempPath(), "EffortLess.JournalTests", Guid.NewGuid().ToString("N"));
        try
        {
            var journal = new DiagnosticJournal(directory);
            journal.Write("First entry");
            journal.Write("Second entry");
            await journal.DisposeAsync();
            journal.Write("After shutdown");
            string text = await File.ReadAllTextAsync(journal.FilePath);
            Assert.Contains("First entry", text);
            Assert.Contains("Second entry", text);
            Assert.DoesNotContain("After shutdown", text);
            Assert.Null(journal.LastError);
        }
        finally { DeleteTestDirectory(directory); }
    }

    [Fact]
    public async Task LogRotationRetainsOnlyOnePriorFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "EffortLess.JournalTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "player.log");
            await File.WriteAllTextAsync(path, new string('a', 1024 * 1024));
            await using (var journal = new DiagnosticJournal(directory)) journal.Write("After rotation");
            Assert.Equal(1024 * 1024, new FileInfo(path + ".1").Length);
            Assert.Contains("After rotation", await File.ReadAllTextAsync(path));
            Assert.Equal(2, Directory.GetFiles(directory).Length);
        }
        finally { DeleteTestDirectory(directory); }
    }
    private static void DeleteTestDirectory(string directory)
    {
        string prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "EffortLess.JournalTests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(directory).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
