using System.Runtime.InteropServices;
using EffortLess.Core.Native;
using static EffortLess.Core.Native.MpvNative;
using Xunit;

namespace EffortLess.Core.Tests;

public class NativeTrackTests
{
    [Fact]
    public void NodeAbiMatchesTheX64ClientHeader()
    {
        Assert.Equal(8, IntPtr.Size);
        Assert.Equal(16, Marshal.SizeOf<MpvNode>());
        Assert.Equal(8, Marshal.OffsetOf<MpvNode>(nameof(MpvNode.Format)).ToInt32());
        Assert.Equal(24, Marshal.SizeOf<MpvNodeList>());
        Assert.Equal(8, Marshal.OffsetOf<MpvNodeList>(nameof(MpvNodeList.Values)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<MpvNodeList>(nameof(MpvNodeList.Keys)).ToInt32());
    }

    [Fact]
    public void AbsentOrWrongFormatTracksAreEmpty()
    {
        Assert.Empty(MpvTrackReader.Parse(default));
        Assert.Empty(MpvTrackReader.Parse(new MpvNode { Format = MpvFormat.NodeArray }));
        Assert.Empty(MpvTrackReader.Parse(new MpvNode { Format = MpvFormat.String }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1025)]
    public void UnboundedNativeTrackCountsAreRejected(int count)
    {
        nint pointer = Marshal.AllocHGlobal(Marshal.SizeOf<MpvNodeList>());
        try
        {
            Marshal.StructureToPtr(new MpvNodeList { Count = count, Values = (nint)1 }, pointer, false);
            Assert.Empty(MpvTrackReader.Parse(new MpvNode { Format = MpvFormat.NodeArray, Pointer = pointer }));
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }
}

