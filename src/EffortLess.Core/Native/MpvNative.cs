using System.Runtime.InteropServices;

namespace EffortLess.Core.Native;

public static class MpvNative
{
    static MpvNative() => MpvLoader.EnsureResolver();

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ulong mpv_client_api_version();

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern MpvError mpv_request_log_messages(nint ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string level);

    public enum MpvError : int
    {
        Success = 0,
        Generic = -20
    }

    public enum MpvFormat : int
    {
        None = 0,
        String = 1,
        OsdString = 2,
        Flag = 3,
        Int64 = 4,
        Double = 5,
        Node = 6,
        NodeArray = 7,
        NodeMap = 8
    }

    public enum MpvEventId : int
    {
        None = 0,
        Shutdown = 1,
        LogMessage = 2,
        StartFile = 6,
        EndFile = 7,
        FileLoaded = 8,
        PropertyChange = 22
    }

    public enum MpvEndFileReason : int
    {
        Eof = 0,
        Stop = 2,
        Quit = 3,
        Error = 4,
        Redirect = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct mpv_event_property
    {
        public IntPtr name;
        public MpvFormat format;
        public IntPtr data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct mpv_event_log_message
    {
        public IntPtr prefix;
        public IntPtr level;
        public IntPtr text;
        public int log_level;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct mpv_event_end_file
    {
        public MpvEndFileReason reason;
        public MpvError error;
        public long playlist_entry_id;
        public long playlist_inserting_into;
        public int playlist_insert_num_entries;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct mpv_event
    {
        public MpvEventId event_id;
        public MpvError error;
        public ulong reply_userdata;
        public IntPtr data;
    }

    // mpv_node has an 8-byte union followed by its 4-byte format (x64 size 16).
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct MpvNode
    {
        [FieldOffset(0)] public nint Pointer;
        [FieldOffset(0)] public long Integer;
        [FieldOffset(0)] public int Flag;
        [FieldOffset(0)] public double Number;
        [FieldOffset(8)] public MpvFormat Format;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvNodeList
    {
        public int Count;
        public nint Values;
        public nint Keys;
    }

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern MpvError mpv_get_property(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, MpvFormat format, out MpvNode data);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void mpv_free_node_contents(ref MpvNode node);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_create();

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_initialize(IntPtr ctx);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_destroy(IntPtr ctx);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_set_option_string(
        IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_set_property_string(
        IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_get_property_string(
        IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_free(IntPtr data);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_observe_property(
        IntPtr ctx,
        ulong replyUserdata,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        MpvFormat format);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_wakeup(IntPtr ctx);

    [DllImport(MpvLoader.LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern MpvError mpv_command(IntPtr ctx, [In] IntPtr[] args);

    public static MpvError Command(IntPtr ctx, params string[] args)
    {
        IntPtr[] pointers = new IntPtr[args.Length + 1];
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                pointers[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
            }

            pointers[args.Length] = IntPtr.Zero;
            return mpv_command(ctx, pointers);
        }
        finally
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (pointers[i] != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pointers[i]);
                    pointers[i] = IntPtr.Zero;
                }
            }
        }
    }
}
