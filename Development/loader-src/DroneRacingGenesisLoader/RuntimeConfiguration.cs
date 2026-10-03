using System.Text;
using System.Text.Json;

namespace DroneRacingGenesisLoader;

internal static class RuntimeConfiguration
{
    private static readonly Dictionary<(string Section, string Key), Func<Installation, string>> PathFields = new()
    {
        [("Variables", "HiScoreName1")] = i => Path.Combine(i.GameDataRoot, "score.bin"),
        [("Game", "AssetDir")] = i => i.ShellDataRoot,
        [("Shelldata", "TournamentDir")] = i => i.ShellDataRoot,
        [("Shelldata", "Directory")] = i => i.ShellDataRoot,
        [("Shelldata", "GameDataDir")] = i => i.GameDataRoot
    };

    public static string Generate(Installation installation, LoaderLog log)
    {
        var source = Path.Combine(installation.ShellRoot, "Game.ini");
        var originals = Path.Combine(installation.ConfigRoot, "Original");
        Directory.CreateDirectory(originals);
        var master = Path.Combine(originals, "Shell.Game.ini");
        if (!File.Exists(master))
        {
            File.Copy(source, master);
            log.Write($"Saved untouched Game.ini master: {master}");
        }

        // The supplied INI is single-byte text. Latin-1 keeps every byte outside edited fields intact.
        var original = File.ReadAllText(source, Encoding.Latin1);
        var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var hadFinalNewline = original.EndsWith("\n", StringComparison.Ordinal);
        var lines = original.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (hadFinalNewline) lines.RemoveAt(lines.Count - 1);
        string section = "";
        var found = new HashSet<(string Section, string Key)>();
        for (int index = 0; index < lines.Count; index++)
        {
            var trimmed = lines[index].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                section = trimmed[1..^1].Trim();
                continue;
            }
            var equal = lines[index].IndexOf('=');
            if (equal < 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#')) continue;
            var key = lines[index][..equal].Trim();
            foreach (var entry in PathFields)
            {
                if (!section.Equals(entry.Key.Section, StringComparison.OrdinalIgnoreCase) ||
                    !key.Equals(entry.Key.Key, StringComparison.OrdinalIgnoreCase)) continue;
                lines[index] = lines[index][..(equal + 1)] + entry.Value(installation);
                found.Add(entry.Key);
                break;
            }
        }
        var gameDataKey = (Section: "Shelldata", Key: "GameDataDir");
        if (!found.Contains(gameDataKey))
        {
            var sectionIndex = lines.FindIndex(line => line.Trim().Equals("[Shelldata]", StringComparison.OrdinalIgnoreCase));
            if (sectionIndex >= 0)
            {
                lines.Insert(sectionIndex + 1, "GameDataDir=" + installation.GameDataRoot);
                found.Add(gameDataKey);
            }
        }
        var missing = PathFields.Keys.Where(k => !found.Contains(k)).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException("Game.ini is missing expected path fields: " +
                string.Join(", ", missing.Select(k => $"[{k.Section}] {k.Key}")));

        var generated = string.Join(newline, lines) + (hadFinalNewline ? newline : "");
        var compatibilityPath = Path.Combine(installation.ConfigRoot, "shell-compatibility.json");
        if (!File.Exists(compatibilityPath))
            File.WriteAllText(compatibilityPath, JsonSerializer.Serialize(new ShellCompatibility(), new JsonSerializerOptions { WriteIndented = true }));
        var compatibility = JsonSerializer.Deserialize<ShellCompatibility>(File.ReadAllText(compatibilityPath))
            ?? throw new InvalidDataException("shell-compatibility.json must contain a configuration object.");
        generated = DroneRacingGenesis.Plugin.ShellSecurityWorkaround.Configure(generated, compatibility.EnableSecurityWorkaround);
        log.Write($"Shell security workaround requested={compatibility.EnableSecurityWorkaround}; built-in [Debug] SecurityDisabled; does not bypass the independent IO gate.");
        var temporary = source + ".loader-tmp";
        File.WriteAllText(temporary, generated, Encoding.Latin1);
        File.Move(temporary, source, true);
        foreach (var entry in PathFields) log.Write($"Generated Game.ini [{entry.Key.Section}] {entry.Key.Key}={entry.Value(installation)}");
        log.Write($"Shell config.ini left unchanged; Shell working directory={installation.ShellRoot}");
        GenerateNetwork(installation, log);
        return source;
    }

    // Original operator settings only. Preserve calibration/audio/credit changes
    // in the working INI; switching back restores only these three network keys.
    internal static void GenerateNetwork(Installation installation, LoaderLog log)
    {
        var configuration = Path.Combine(installation.ConfigRoot, "network.json");
        if (!File.Exists(configuration)) return;
        using var document = JsonDocument.Parse(File.ReadAllText(configuration));
        string? mode = document.RootElement.GetProperty("Mode").GetString();
        if (mode is not ("Original" or "Standalone" or "LAN"))
            throw new InvalidDataException("network.json Mode must be Standalone or LAN (legacy Original is also accepted).");
        bool applyLan = mode == "LAN" && document.RootElement.TryGetProperty("ApplyOnNextLaunch",out var pending) && pending.GetBoolean();
        if(mode == "LAN" && !applyLan) { log.Write("LAN mode: retaining current original operator network settings.");return; }
        var path = Path.Combine(installation.ShellDataRoot, "ShellData.ini");
        var master = Path.Combine(installation.ConfigRoot, "Original", "ShellData.network-master.ini");
        if (mode == "Original" && !File.Exists(master)) return;
        string current = File.ReadAllText(path, Encoding.Latin1);
        var keys = new HashSet<string>(["LinkPlay", "CabinetID", "NumCabinets"], StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> Read(string text)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string section = "";
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']')) section = trimmed[1..^1].Trim();
                int equal = line.IndexOf('=');
                if (!section.Equals("Network", StringComparison.OrdinalIgnoreCase) || equal < 0) continue;
                string key = line[..equal].Trim();
                if (keys.Contains(key) && !values.TryAdd(key, line[(equal + 1)..]))
                    throw new InvalidDataException($"Duplicate Network/{key} in ShellData.ini.");
            }
            if (values.Count != keys.Count) throw new InvalidDataException("ShellData.ini needs Network/LinkPlay, CabinetID and NumCabinets.");
            return values;
        }
        _ = Read(current); // Validate before backing up or mutating any configuration.
        var values = mode == "Standalone"
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["LinkPlay"] = "0", ["CabinetID"] = "1", ["NumCabinets"] = "1" }
            : applyLan ? Read(current) : Read(File.ReadAllText(master, Encoding.Latin1));
        if(applyLan)
        {
            int id=document.RootElement.GetProperty("CabinetID").GetInt32(),count=document.RootElement.GetProperty("NumCabinets").GetInt32();
            if(id<1||count<2||count>4||id>count)throw new InvalidDataException("LAN needs 2–4 cabinets and a unique Cabinet ID within that count.");
            values["LinkPlay"]="1";values["CabinetID"]=id.ToString();values["NumCabinets"]=count.ToString();
        }
        string sectionName = "";
        // Splitting on LF retains any CR on each line; preserve all other bytes.
        var lines = current.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index], trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']')) sectionName = trimmed[1..^1].Trim();
            int equal = line.IndexOf('=');
            if (!sectionName.Equals("Network", StringComparison.OrdinalIgnoreCase) || equal < 0) continue;
            if (values.TryGetValue(line[..equal].Trim(), out string? value))
                lines[index] = line[..(equal + 1)] + value.TrimEnd('\r') + (line.EndsWith('\r') ? "\r" : "");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(master)!);
        if (!File.Exists(master)) File.Copy(path, master);
        File.WriteAllText(path + ".loader-tmp", string.Join('\n', lines), Encoding.Latin1);
        File.Move(path + ".loader-tmp", path, true);
        if(applyLan)
        {
            var settings=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(configuration))!.AsObject();settings["ApplyOnNextLaunch"]=false;
            File.WriteAllText(configuration+".tmp",settings.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));File.Move(configuration+".tmp",configuration,true);
        }
        log.Write($"Network mode={mode}; ShellData.ini " + string.Join(", ", values.Select(pair => $"{pair.Key}={pair.Value.Trim()}")) + $"; master={master}");
    }

    public static string EnsureControls(Installation installation)
    {
        Directory.CreateDirectory(installation.ConfigRoot);
        var path = Path.Combine(installation.ConfigRoot, "controls.json");
        ControlsConfiguration? prior = null;
        if (File.Exists(path))
        {
            prior = JsonSerializer.Deserialize<ControlsConfiguration>(File.ReadAllText(path));
            if (prior is null) throw new InvalidDataException("controls.json does not contain a configuration object.");
            if (prior.Version >= 2) return path;
            var backup = Path.Combine(installation.ConfigRoot, "Original", "controls.v1.json");
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            if (!File.Exists(backup)) File.Copy(path, backup);
        }
        var analog = new[] { "Horizontal", "Vertical", "Right Trigger", "Throttle" };
        var digital = new[] { "Accelerate / Confirm", "Brake / Cancel", "Boost", "View", "Start", "Service", "Shot", "Roll", "Test", "Coin", "Debug" };
        var configuration = new ControlsConfiguration();
        foreach (var name in analog.Concat(digital))
        {
            var old = prior?.Actions.FirstOrDefault(action => action.Name == name);
            configuration.Actions.Add(new ControlAction { Name = name, ValueType = analog.Contains(name) ? "analog" : "digital", Bindings = old?.Bindings ?? [] });
        }
        // Preserve any custom mappings attached to old placeholder names.
        if (prior is not null)
            configuration.Actions.AddRange(prior.Actions.Where(action => action.Bindings.Count > 0 && !configuration.Actions.Any(item => item.Name == action.Name)));
        var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
        return path;
    }
}

internal sealed class ShellCompatibility
{
    public bool EnableSecurityWorkaround { get; set; } = true;
    public bool EnableIOCompatibility { get; set; }
}

internal sealed class ControlsConfiguration
{
    public ControlBinding? ExitBinding { get; set; } = new() { Kind="keyboardKey", Control="Escape" };
    public bool ConfirmExit { get; set; } = true;
    public bool LinkTriggerAndBoost { get; set; } = true;
    public int Version { get; set; } = 2;
    public bool UnityInputEnabled { get; set; }
    public bool AppliedToGame { get; set; } = false;
    public string[] SupportedBindingKinds { get; set; } = ["keyboardKey", "joystickButton", "joystickAxis", "xinputButton", "xinputAxis"];
    public List<ControlAction> Actions { get; set; } = [];
}

internal sealed class ControlAction
{
    public string Name { get; set; } = "";
    public string ValueType { get; set; } = "digital";
    public List<ControlBinding> Bindings { get; set; } = [];
}

internal sealed class ControlBinding
{
    public string Kind { get; set; } = "";
    public string Device { get; set; } = "";
    public string Control { get; set; } = "";
    public bool Invert { get; set; }
    public float Deadzone { get; set; }
    public float Sensitivity { get; set; } = 1;
}
