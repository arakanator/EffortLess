using System.Runtime.InteropServices;
using EffortLess.Core.Playback;

namespace EffortLess.Core.Native;

internal static class MpvTrackReader
{
    // Borrowed event nodes must be copied before the next mpv_wait_event. Only
    // the owned result from mpv_get_property is freed by Read; Parse never frees.
    internal static IReadOnlyList<MediaTrack> Read(nint handle)
    {
        if (MpvNative.mpv_get_property(handle, "track-list", MpvNative.MpvFormat.Node, out var node) != MpvNative.MpvError.Success)
            return Array.Empty<MediaTrack>();
        try { return Parse(node); }
        finally { MpvNative.mpv_free_node_contents(ref node); }
    }

    internal static IReadOnlyList<MediaTrack> Parse(MpvNative.MpvNode node)
    {
        var tracks = new List<MediaTrack>();
        if (node.Format != MpvNative.MpvFormat.NodeArray || node.Pointer == 0) return tracks.AsReadOnly();
        var array = Marshal.PtrToStructure<MpvNative.MpvNodeList>(node.Pointer);
        if (array.Count < 0 || array.Count > 1024 || array.Values == 0) return tracks.AsReadOnly();
        for (int i = 0; i < array.Count; i++)
        {
            var item = Marshal.PtrToStructure<MpvNative.MpvNode>(array.Values + i * 16);
            if (item.Format != MpvNative.MpvFormat.NodeMap || item.Pointer == 0) continue;
            var map = Marshal.PtrToStructure<MpvNative.MpvNodeList>(item.Pointer);
            if (map.Count < 0 || map.Count > 128 || map.Values == 0 || map.Keys == 0) continue;
            var fields = new Dictionary<string, MpvNative.MpvNode>(StringComparer.Ordinal);
            for (int j = 0; j < map.Count; j++)
            {
                var keyPtr = Marshal.ReadIntPtr(map.Keys, j * IntPtr.Size);
                if (keyPtr == 0) continue;
                string? key = Marshal.PtrToStringUTF8(keyPtr);
                if (key is not null) fields[key] = Marshal.PtrToStructure<MpvNative.MpvNode>(map.Values + j * 16);
            }
            string type = Text(fields, "type");
            MediaTrackKind? kind = type switch { "video" => MediaTrackKind.Video, "audio" => MediaTrackKind.Audio, "sub" => MediaTrackKind.Subtitle, _ => null };
            if (kind is null || !fields.TryGetValue("id", out var id) || id.Format != MpvNative.MpvFormat.Int64 || id.Integer <= 0) continue;
            tracks.Add(new MediaTrack(id.Integer, kind.Value, Text(fields, "title"), Text(fields, "lang"),
                Text(fields, "codec"), Flag(fields, "selected"), Flag(fields, "external")));
        }
        return tracks.AsReadOnly();
    }

    private static string Text(Dictionary<string, MpvNative.MpvNode> fields, string name)
        => fields.TryGetValue(name, out var node) && node.Format == MpvNative.MpvFormat.String && node.Pointer != 0
            ? Marshal.PtrToStringUTF8(node.Pointer) ?? "" : "";
    private static bool Flag(Dictionary<string, MpvNative.MpvNode> fields, string name)
        => fields.TryGetValue(name, out var node) && node.Format == MpvNative.MpvFormat.Flag && node.Flag != 0;
}
