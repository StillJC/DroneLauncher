using System.Diagnostics;
using System.Text.Json;

namespace DroneRacingGenesisLoader;

internal static class IOCompatibility
{
    public static bool Enabled(Installation installation) => JsonSerializer.Deserialize<ShellCompatibility>(
        File.ReadAllText(Path.Combine(installation.ConfigRoot, "shell-compatibility.json")))?.EnableIOCompatibility ?? false;

    public static async Task<Process> StartAsync(Installation installation, ProcessStartInfo shell, LoaderLog log)
    {
        string directory = Path.Combine(installation.ContentRoot, "Launcher", "Plugins", "IO");
        var helper = new ProcessStartInfo(Path.Combine(directory, "DroneRacingGenesis.IOBootstrap.exe"))
        {
            WorkingDirectory = installation.ShellRoot, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var item in shell.Environment) helper.Environment[item.Key] = item.Value;
        helper.ArgumentList.Add(shell.FileName);
        helper.ArgumentList.Add(Path.Combine(directory, "DroneRacingGenesis.IO.dll"));
        helper.ArgumentList.Add(shell.ArgumentList.Single());
        using var bootstrap = Process.Start(helper) ?? throw new InvalidOperationException("Could not start IO bootstrap.");
        var stdout = bootstrap.StandardOutput.ReadToEndAsync(); var stderr = bootstrap.StandardError.ReadToEndAsync();
        await bootstrap.WaitForExitAsync().ConfigureAwait(false);
        string output = await stdout; string error = await stderr;
        if (bootstrap.ExitCode != 0 || !int.TryParse(output.Trim(), out int pid))
            throw new InvalidOperationException($"IO bootstrap failed ({bootstrap.ExitCode}): {error}");
        log.Write($"Native IO bootstrap PID={bootstrap.Id} initialized Shell PID={pid}; module is in memory only.");
        var process = Process.GetProcessById(pid);
        // Retain an OS handle now so ExitCode remains available after the
        // externally created process has disappeared from process enumeration.
        _ = process.Handle;
        return process;
    }
}
