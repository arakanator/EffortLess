using EffortLess.Core.Native;
using Xunit;

namespace EffortLess.Core.Tests;

public class PeArchitectureTests
{
    private static byte[] BuildHeader(int machine)
    {
        byte[] data = new byte[0x40 + 6];
        data[0] = (byte)'M';
        data[1] = (byte)'Z';
        data[0x3C] = 0x40;
        data[0x3D] = 0;
        data[0x3E] = 0;
        data[0x3F] = 0;
        data[0x40] = (byte)'P';
        data[0x41] = (byte)'E';
        data[0x44] = (byte)(machine & 0xFF);
        data[0x45] = (byte)((machine >> 8) & 0xFF);
        return data;
    }

    [Fact]
    public void ReadMachine_DetectsX64()
    {
        Assert.Equal(PeArchitecture.Machine.X64, PeArchitecture.ReadMachine(BuildHeader(0x8664)));
    }

    [Fact]
    public void ReadMachine_DetectsX86()
    {
        Assert.Equal(PeArchitecture.Machine.X86, PeArchitecture.ReadMachine(BuildHeader(0x014c)));
    }

    [Fact]
    public void ReadMachine_RejectsShortOrInvalidInput()
    {
        Assert.Equal(PeArchitecture.Machine.Unknown, PeArchitecture.ReadMachine(Array.Empty<byte>()));
        Assert.Equal(PeArchitecture.Machine.Unknown, PeArchitecture.ReadMachine(new byte[] { 1, 2, 3 }));
        byte[] notMz = new byte[0x100];
        Assert.Equal(PeArchitecture.Machine.Unknown, PeArchitecture.ReadMachine(notMz));
    }
}
