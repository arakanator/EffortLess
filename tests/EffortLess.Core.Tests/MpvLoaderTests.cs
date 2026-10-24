using EffortLess.Core.Native;
using Xunit;

namespace EffortLess.Core.Tests;

public class MpvLoaderTests
{
    [Fact]
    public void NativeDirectory_IsAppRelativeNativeWinX64()
    {
        string expected = Path.Combine(@"C:\app", "native", "win-x64");
        Assert.Equal(expected, MpvLoader.GetNativeDirectory(@"C:\app"));
    }

    [Fact]
    public void ExpectedLibraryPath_PointsAtLibmpv2()
    {
        string expected = Path.Combine(@"C:\app", "native", "win-x64", "libmpv-2.dll");
        Assert.Equal(expected, MpvLoader.GetExpectedLibraryPath(@"C:\app"));
    }

    [Fact]
    public void IsX64_ReturnsFalseForMissingFile()
    {
        Assert.False(MpvLoader.IsX64(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll")));
    }

    [Fact]
    public void IsX64_ReadsPeHeaderBeyondDosHeader()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll");
        var data = new byte[0x206];
        data[0] = (byte)'M'; data[1] = (byte)'Z';
        BitConverter.GetBytes(0x200).CopyTo(data, 0x3c);
        data[0x200] = (byte)'P'; data[0x201] = (byte)'E';
        BitConverter.GetBytes((ushort)0x8664).CopyTo(data, 0x204);
        try
        {
            File.WriteAllBytes(path, data);
            Assert.True(MpvLoader.IsX64(path));
            BitConverter.GetBytes(int.MaxValue).CopyTo(data, 0x3c);
            File.WriteAllBytes(path, data);
            Assert.False(MpvLoader.IsX64(path));
        }
        finally { File.Delete(path); }
    }
}
