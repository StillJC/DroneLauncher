using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace DroneRacingGenesisLoader;
internal static class UnityPortability
{
    internal const string SupportedHash = "491F7C3E3AB392F78AEA8B3B9775B65AAD63ACDE2AE4E2FC4D04FDFE716FB652";
    public static void Configure(Installation installation, ProcessStartInfo start, LoaderLog log)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(installation.ConfigRoot, "unity-portability.json")));
        if (!config.RootElement.GetProperty("Enabled").GetBoolean()) throw new InvalidDataException("Native Unity compatibility must be enabled for portable startup.");
        using var assembly = File.OpenRead(Path.Combine(installation.GameRoot, "GameAssembly.dll"));
        if (Convert.ToHexString(SHA256.HashData(assembly)) != SupportedHash)
            throw new InvalidDataException("Unsupported GameAssembly.dll build. No compatibility hooks were installed. Restore the supported game files.");
        if (!IOCompatibility.Enabled(installation)) throw new InvalidDataException("Native startup requires the cabinet IO bootstrap.");
        var controls = JsonSerializer.Deserialize<ControlsConfiguration>(File.ReadAllText(RuntimeConfiguration.EnsureControls(installation)))!;
        start.Environment["DRG_NATIVE_UNITY"] = "1";
        start.Environment["DRG_UNITY_INPUT"] = controls.UnityInputEnabled ? "1" : "0";
        log.Write("GameAssembly SHA256 validated; native Unity initialization will precede child main-thread resume. No Python/Frida supervisor.");
    }
}
