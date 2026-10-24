using System.Reflection;
using System.Runtime.InteropServices;

namespace EffortLess.Core.Native;

public static class MpvLoader
{
    public const string LibraryName = "libmpv-2.dll";
    public const string NativeSubdirectory = "native/win-x64";

    private static bool _resolverInstalled;
    private static IntPtr _libraryHandle;
    private static readonly object ResolverLock = new();

    public static void EnsureResolver()
    {
        lock (ResolverLock)
        {
            if (_resolverInstalled)
                return;

            NativeLibrary.SetDllImportResolver(typeof(MpvLoader).Assembly, Resolve);
            _resolverInstalled = true;
        }
    }

    public static string GetNativeDirectory(string baseDirectory)
        => Path.Combine(baseDirectory, "native", "win-x64");

    public static string GetExpectedLibraryPath(string baseDirectory)
        => Path.Combine(GetNativeDirectory(baseDirectory), LibraryName);

    public static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        lock (ResolverLock)
        {
        if (_libraryHandle != IntPtr.Zero)
            return _libraryHandle;
        string baseDirectory = AppContext.BaseDirectory;
        string expected = GetExpectedLibraryPath(baseDirectory);

        if (!File.Exists(expected))
        {
            throw new DllNotFoundException(
                $"libmpv native library was not found at '{expected}'. " +
                "Place the x64 libmpv-2.dll in the app's native/win-x64 folder or run native/acquire-libmpv.ps1.");
        }

        if (!IsX64(expected))
        {
            throw new BadImageFormatException(
                $"libmpv native library at '{expected}' is not x64. " +
                "EffortLess requires the win-x64 build of libmpv.");
        }

        if (!NativeLibrary.TryLoad(expected, assembly,
            DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.System32, out _libraryHandle))
        {
            throw new DllNotFoundException($"Failed to load libmpv native library from '{expected}'.");
        }

        return _libraryHandle;
        }
    }

    public static bool IsX64(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> dos = stackalloc byte[0x40];
            fs.ReadExactly(dos);
            if (dos[0] != 'M' || dos[1] != 'Z') return false;
            int offset = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(dos[0x3c..]);
            if (offset < 0x40 || offset > fs.Length - 6) return false;
            fs.Position = offset;
            Span<byte> pe = stackalloc byte[6];
            fs.ReadExactly(pe);
            return pe[0] == 'P' && pe[1] == 'E' && pe[2] == 0 && pe[3] == 0 &&
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(pe[4..]) == (ushort)PeArchitecture.Machine.X64;
        }
        catch
        {
            return false;
        }
    }
}
