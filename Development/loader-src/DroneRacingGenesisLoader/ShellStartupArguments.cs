using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace DroneRacingGenesisLoader;

internal static class ShellStartupArguments
{
    // Shell RVA E6D0 computes this checksum and compares it to the /k= handoff.
    // RVA 4D5B0 uses this build's table, an MSB-first update and no final xor.
    public static string ForExecutable(string shellPath)
    {
        var bytes = File.ReadAllBytes(shellPath);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (hash != "59BF7C7676A4AAEC584B4FAF81CDBAF36A6F9B267E69BC0D01250989053AF82B")
            throw new InvalidDataException("Shell.exe is not the analyzed build. Its startup checksum contract must be verified before launch.");
        using var pe = new PEReader(new MemoryStream(bytes, false));
        var table = pe.GetSectionData(0x168540).GetContent(0, 256 * sizeof(uint));
        uint checksum = 0x6F5A87D5;
        foreach (var value in bytes)
        {
            var index = (int)(((checksum >> 24) ^ value) & 255) * sizeof(uint);
            checksum = (checksum << 8) ^ System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(index, 4));
        }
        return "/k=" + checksum.ToString("X8");
    }
}
