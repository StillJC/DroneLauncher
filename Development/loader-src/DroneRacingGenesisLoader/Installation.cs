namespace DroneRacingGenesisLoader;

internal sealed class Installation
{
    public string LoaderDirectory { get; }
    public string ContentRoot { get; }
    public string ShellRoot => Path.Combine(ContentRoot, "Shell");
    public string ShellDataRoot => Path.Combine(ContentRoot, "ShellData");
    public string GameDataRoot => Path.Combine(ContentRoot, "GameData");
    public string GameRoot => Path.Combine(ContentRoot, "DroneRacing");
    // Shell's unmodified scanner already excludes Launcher. Keep loader-owned
    // directories below it so only game directories are eligible candidates.
    public string LogsRoot => Path.Combine(ContentRoot, "Launcher", "Logs", "Loader");
    public string ConfigRoot => Path.Combine(ContentRoot, "Launcher", "Config");

    private Installation(string loaderDirectory, string contentRoot)
    {
        LoaderDirectory = loaderDirectory;
        ContentRoot = contentRoot;
    }

    public static Installation ResolveFromExecutable()
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Windows did not provide the loader executable path.");
        return Resolve(Path.GetDirectoryName(Path.GetFullPath(executable))!);
    }

    public static Installation Resolve(string loaderDirectory)
    {
        loaderDirectory = Path.GetFullPath(loaderDirectory);
        var direct = Path.Combine(loaderDirectory, "Shell", "Shell.exe");
        if (File.Exists(direct)) return new(loaderDirectory, loaderDirectory);

        var nested = Path.Combine(loaderDirectory, "Sega", "Shell", "Shell.exe");
        if (File.Exists(nested)) return new(loaderDirectory, Path.Combine(loaderDirectory, "Sega"));

        throw new FileNotFoundException(
            "Shell.exe was not found. Expected one of:" + Environment.NewLine +
            direct + Environment.NewLine + nested);
    }

    public IReadOnlyList<string> MissingComponents()
    {
        string[] files =
        [
            @"Shell\Shell.exe", @"Shell\dk2win32.dll", @"Shell\Game.ini", @"Shell\config.ini",
            @"ShellData\ShellData.ini", @"ShellData\GameSettings.ini",
            @"GameData\system.bin", @"GameData\score.bin",
            @"DroneRacing\DroneRacing.exe", @"DroneRacing\UnityPlayer.dll",
            @"DroneRacing\GameAssembly.dll"
        ];
        var missing = files.Where(p => !File.Exists(Path.Combine(ContentRoot, p))).ToList();
        const string gameData = @"DroneRacing\DroneRacing_Data";
        if (!Directory.Exists(Path.Combine(ContentRoot, gameData))) missing.Add(gameData);
        var compatibility = Path.Combine(ConfigRoot, "shell-compatibility.json");
        if (File.Exists(compatibility))
        {
            using var configuration = System.Text.Json.JsonDocument.Parse(File.ReadAllText(compatibility));
            if (configuration.RootElement.TryGetProperty("EnableIOCompatibility", out var enabled) && enabled.GetBoolean())
                foreach (var file in new[] { @"Launcher\Plugins\IO\DroneRacingGenesis.IO.dll", @"Launcher\Plugins\IO\DroneRacingGenesis.IOBootstrap.exe" })
                    if (!File.Exists(Path.Combine(ContentRoot, file))) missing.Add(file);
        }
        var unity = Path.Combine(ConfigRoot, "unity-portability.json");
        if (File.Exists(unity))
        {
            using var configuration = System.Text.Json.JsonDocument.Parse(File.ReadAllText(unity));
            if (configuration.RootElement.GetProperty("Enabled").GetBoolean())
                foreach (var file in new[] { @"Launcher\Plugins\Unity\DroneRacingGenesis.Unity.dll", @"Launcher\Plugins\Unity\DroneRacingGenesis.UnityBootstrap.exe" })
                    if (!File.Exists(Path.Combine(ContentRoot, file))) missing.Add(file);
        }
        return missing;
    }
}
