using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace EffortLess.Core.Library;

/// <summary>
/// Performs a one-time, copy-only migration of application data between two
/// directory paths. Destination content is never overwritten, source content
/// is never modified, and a versioned completion marker prevents re-import.
/// </summary>
public static class DirectoryDataMigration
{
    public const string CompletionMarkerFileName = ".appdata-migration.complete";
    public const string CompletionMarkerContent = "version=1";
    public const string LockFileName = ".appdata-migration.lock";

    public static MigrationResult Migrate(string sourceDirectory, string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceDirectory);
        ArgumentException.ThrowIfNullOrEmpty(destinationDirectory);

        string sourceRoot = Path.GetFullPath(sourceDirectory);
        string destinationRoot = Path.GetFullPath(destinationDirectory);

        ValidateDistinctPaths(sourceRoot, destinationRoot);
        EnsureSafeAncestors(sourceRoot);
        EnsureSafeAncestors(destinationRoot);

        if (File.Exists(sourceRoot))
            throw new ArgumentException(
                $"Source path '{sourceRoot}' exists but is a file, not a directory.",
                nameof(sourceDirectory));

        if (Directory.Exists(destinationRoot) && IsReparsePoint(destinationRoot))
            throw new IOException(
                $"Destination path '{destinationRoot}' is a reparse point (symlink or junction).");

        Directory.CreateDirectory(destinationRoot);

        EnsureNotReparsePoint(Path.Combine(destinationRoot, LockFileName));
        EnsureNotReparsePoint(Path.Combine(destinationRoot, CompletionMarkerFileName));

        using FileStream migrationLock = AcquireMigrationLock(destinationRoot);

        string markerPath = Path.Combine(destinationRoot, CompletionMarkerFileName);
        if (IsRecognizedCompletionMarker(markerPath))
            return new MigrationResult(sourceRoot, destinationRoot, 0, true);

        if (!Directory.Exists(sourceRoot))
        {
            WriteCompletionMarker(markerPath);
            return new MigrationResult(sourceRoot, destinationRoot, 0, true);
        }

        EnsureNoReparsePoints(sourceRoot);
        EnsureNoReparsePoints(destinationRoot);

        int copiedCount = CopyTree(sourceRoot, destinationRoot);

        WriteCompletionMarker(markerPath);
        return new MigrationResult(sourceRoot, destinationRoot, copiedCount, true);
    }

    private static void ValidateDistinctPaths(string sourceRoot, string destinationRoot)
    {
        StringComparison comparison = PathComparison;
        string source = sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string destination = destinationRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(source, destination, comparison))
            throw new ArgumentException("Source and destination must be different directories.");

        if (IsSameOrDescendant(source, destination, comparison) ||
            IsSameOrDescendant(destination, source, comparison))
        {
            throw new ArgumentException(
                "Source and destination must not overlap; neither may be located inside the other.");
        }
    }

    private static bool IsSameOrDescendant(string parent, string child, StringComparison comparison)
    {
        if (!child.StartsWith(parent, comparison))
            return false;
        if (child.Length == parent.Length)
            return true;
        char boundary = child[parent.Length];
        return boundary == Path.DirectorySeparatorChar || boundary == Path.AltDirectorySeparatorChar;
    }

    private static FileStream AcquireMigrationLock(string destinationRoot)
    {
        string lockPath = Path.Combine(destinationRoot, LockFileName);
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex)
        {
            throw new IOException(
                $"Another migration is already in progress for destination '{destinationRoot}'.", ex);
        }
    }

    private static bool IsRecognizedCompletionMarker(string markerPath)
    {
        if (!File.Exists(markerPath))
            return false;

        string content = File.ReadAllText(markerPath).Trim();
        return string.Equals(content, CompletionMarkerContent, StringComparison.Ordinal);
    }

    private static void WriteCompletionMarker(string markerPath)
    {
        string tempPath = markerPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, CompletionMarkerContent);
            File.Move(tempPath, markerPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static int CopyTree(string sourceRoot, string destinationRoot)
    {
        int copiedCount = 0;

        foreach (string sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
            string destinationFile = Path.Combine(destinationRoot, relativePath);

            if (Directory.Exists(destinationFile))
                throw new IOException($"A directory conflicts with migrating '{relativePath}'.");
            if (File.Exists(destinationFile))
                continue;

            string destinationDirectory = Path.GetDirectoryName(destinationFile)
                ?? throw new IOException($"Unable to determine destination directory for '{destinationFile}'.");

            Directory.CreateDirectory(destinationDirectory);

            if (CopyFileVerified(sourceFile, destinationFile, destinationDirectory))
                copiedCount++;
        }

        return copiedCount;
    }

    private static bool CopyFileVerified(string sourceFile, string destinationFile, string destinationDirectory)
    {
        string tempFile = Path.Combine(
            destinationDirectory, "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            File.Copy(sourceFile, tempFile, overwrite: false);
        }
        catch
        {
            TryDelete(tempFile);
            throw;
        }

        try
        {
            VerifySha256Match(sourceFile, tempFile);
        }
        catch
        {
            TryDelete(tempFile);
            throw;
        }

        try
        {
            File.Move(tempFile, destinationFile, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(destinationFile))
        {
            TryDelete(tempFile);
            return false;
        }
        catch
        {
            TryDelete(tempFile);
            throw;
        }
    }

    private static void VerifySha256Match(string sourceFile, string copyFile)
    {
        byte[] sourceHash = ComputeSha256(sourceFile);
        byte[] copyHash = ComputeSha256(copyFile);

        if (!CryptographicOperations.FixedTimeEquals(sourceHash, copyHash))
            throw new IOException($"SHA-256 verification failed after copying '{sourceFile}'.");
    }

    private static byte[] ComputeSha256(string path)
    {
        using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return SHA256.HashData(stream);
    }

    private static void EnsureNoReparsePoints(string root)
    {
        EnsureNotReparsePoint(root);

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)
        {
            string directory = pendingDirectories.Pop();
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                EnsureNotReparsePoint(entry);

                if (Directory.Exists(entry))
                    pendingDirectories.Push(entry);
            }
        }
    }

    private static void EnsureSafeAncestors(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            EnsureNotReparsePoint(current);
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if (IsReparsePoint(path))
            throw new IOException(
                $"Path '{path}' is a reparse point (symlink or junction); " +
                "migration aborted to avoid escaping the intended directory tree.");
    }

    private static bool IsReparsePoint(string path)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return new DirectoryInfo(path).LinkTarget is not null ||
                   new FileInfo(path).LinkTarget is not null;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
            return true;

        if ((attributes & FileAttributes.Directory) != 0)
            return new DirectoryInfo(path).LinkTarget is not null;

        return new FileInfo(path).LinkTarget is not null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

/// <summary>
/// Outcome of a data migration attempt.
/// </summary>
public sealed record MigrationResult(
    string SourceDirectory,
    string DestinationDirectory,
    int CopiedCount,
    bool Completed);
