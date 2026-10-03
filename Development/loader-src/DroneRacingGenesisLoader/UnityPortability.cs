using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace DroneRacingGenesisLoader;
internal static class UnityPortability
{
    public static void Configure(Installation installation, ProcessStartInfo start, LoaderLog log)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(installation.ConfigRoot, "unity-portability.json")));
        if (!config.RootElement.GetProperty("Enabled").GetBoolean()) throw new InvalidDataException("Native Unity compatibility must be enabled for portable startup.");
        if (!IOCompatibility.Enabled(installation)) throw new InvalidDataException("Native startup requires the cabinet IO bootstrap.");
        var controls = JsonSerializer.Deserialize<ControlsConfiguration>(File.ReadAllText(RuntimeConfiguration.EnsureControls(installation)))!;
        start.Environment["DRG_NATIVE_UNITY"] = "1";
        start.Environment["DRG_UNITY_INPUT"] = controls.UnityInputEnabled ? "1" : "0";
        log.Write("Native Unity initialization will precede child main-thread resume.");
    }
}
