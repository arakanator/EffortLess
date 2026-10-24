namespace EffortLess.Core.Native;

public static class PeArchitecture
{
    public enum Machine
    {
        Unknown = -1,
        X86 = 0x014c,
        X64 = 0x8664,
        Arm64 = 0xaa64
    }

    public static Machine ReadMachine(ReadOnlySpan<byte> peHeader)
    {
        if (peHeader.Length < 0x40 || peHeader[0] != (byte)'M' || peHeader[1] != (byte)'Z')
            return Machine.Unknown;

        int peOffset = BitConverter.ToInt32(peHeader[0x3C..0x40]);
        if (peOffset < 0x40 || peOffset > peHeader.Length - 6)
            return Machine.Unknown;

        if (peHeader[peOffset] != (byte)'P' || peHeader[peOffset + 1] != (byte)'E' ||
            peHeader[peOffset + 2] != 0 || peHeader[peOffset + 3] != 0)
            return Machine.Unknown;

        int machineOffset = peOffset + 4;
        if (machineOffset + 2 > peHeader.Length)
            return Machine.Unknown;

        return (Machine)BitConverter.ToUInt16(peHeader[machineOffset..]);
    }
}
