using System;
using System.Runtime.InteropServices;
using EffortLess.Core.Native;
using EffortLess.Core.Playback;
using Xunit;

namespace EffortLess.Core.Tests;

public class PlaybackPropertyHandlingTests
{
    [Fact]
    public void MpvEvent_LayoutMatchesX64Abi()
    {
        if (IntPtr.Size != 8)
            return;

        Assert.Equal(new IntPtr(0), Marshal.OffsetOf<MpvNative.mpv_event>("event_id"));
        Assert.Equal(new IntPtr(4), Marshal.OffsetOf<MpvNative.mpv_event>("error"));
        Assert.Equal(new IntPtr(8), Marshal.OffsetOf<MpvNative.mpv_event>("reply_userdata"));
        Assert.Equal(new IntPtr(16), Marshal.OffsetOf<MpvNative.mpv_event>("data"));
        Assert.Equal(24, Marshal.SizeOf<MpvNative.mpv_event>());
    }

    [Fact]
    public void MpvEventProperty_LayoutMatchesX64Abi()
    {
        if (IntPtr.Size != 8)
            return;

        Assert.Equal(new IntPtr(0), Marshal.OffsetOf<MpvNative.mpv_event_property>("name"));
        Assert.Equal(new IntPtr(8), Marshal.OffsetOf<MpvNative.mpv_event_property>("format"));
        Assert.Equal(new IntPtr(16), Marshal.OffsetOf<MpvNative.mpv_event_property>("data"));
        Assert.Equal(24, Marshal.SizeOf<MpvNative.mpv_event_property>());
    }

    [Fact]
    public void MpvEventEndFile_LayoutMatchesX64Abi()
    {
        if (IntPtr.Size != 8)
            return;

        Assert.Equal(new IntPtr(0), Marshal.OffsetOf<MpvNative.mpv_event_end_file>("reason"));
        Assert.Equal(new IntPtr(4), Marshal.OffsetOf<MpvNative.mpv_event_end_file>("error"));
        Assert.Equal(new IntPtr(8), Marshal.OffsetOf<MpvNative.mpv_event_end_file>("playlist_entry_id"));
        Assert.Equal(32, Marshal.SizeOf<MpvNative.mpv_event_end_file>());
    }

    [Fact]
    public void ReadStringPointer_DereferencesCharPointerPointer()
    {
        IntPtr utf8 = Marshal.StringToCoTaskMemUTF8("d3d11va-copy");
        IntPtr slot = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(slot, utf8);
            Assert.Equal("d3d11va-copy", MpvPlayer.ReadStringPointer(slot));
        }
        finally
        {
            Marshal.FreeHGlobal(slot);
            Marshal.FreeCoTaskMem(utf8);
        }
    }

    [Fact]
    public void ReadStringPointer_NullSlotReturnsNull()
    {
        Assert.Null(MpvPlayer.ReadStringPointer(IntPtr.Zero));
    }
}
