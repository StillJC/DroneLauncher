using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DroneRacingGenesisLoader;

internal static class ProcessMonitor
{
    public static async Task LaunchAndMonitorAsync(Installation installation, LoaderLog log, Action<string> status, Action<PhysicalInputManager?>? brokerAvailable = null, CancellationToken shutdown = default, string? targetDisplayDevice = null)
    {
        var shellPath = Path.Combine(installation.ShellRoot, "Shell.exe");
        var gamePath = Path.GetFullPath(Path.Combine(installation.GameRoot, "DroneRacing.exe"));
        var startInfo = new ProcessStartInfo(shellPath)
        {
            WorkingDirectory = installation.ShellRoot,
            UseShellExecute = false
        };
        startInfo.Environment["DRG_CONTENT_ROOT"] = installation.ContentRoot;
        var startupArgument = ShellStartupArguments.ForExecutable(shellPath);
        startInfo.ArgumentList.Add(startupArgument);

        var requestedAt = DateTimeOffset.Now;
        log.Write($"Starting Shell command line: \"{shellPath}\" {startupArgument}");
        log.Write($"Shell working directory: {startInfo.WorkingDirectory}");
        log.Write($"DRG_CONTENT_ROOT={installation.ContentRoot}");
        using var inputs = IOCompatibility.Enabled(installation) ? new PhysicalInputManager(installation, log) : null;
        using var outputs = new OutputManager(installation, log);
        startInfo.Environment["DRG_OUTPUT_MAPPING"] = outputs.MappingName;
        brokerAvailable?.Invoke(inputs);
        UnityPortability.Configure(installation, startInfo, log);

        var placement = new WindowPlacementManager(targetDisplayDevice, log);
        if (inputs is not null) startInfo.Environment["DRG_IO_MAPPING"] = inputs.MappingName;
        using var shell = (inputs is null ? Process.Start(startInfo) : await IOCompatibility.StartAsync(installation, startInfo, log).ConfigureAwait(false))
            ?? throw new InvalidOperationException("Windows did not create the Shell process.");
        var started = new DateTimeOffset(shell.StartTime);
        if (inputs is not null) inputs.ShellPid = shell.Id;
        outputs.ShellPid = shell.Id;
        log.Write($"Shell PID={shell.Id}; requested={requestedAt:O}; started={started:O}");
        Diagnostics.Update(installation, "Shell running", shell: shell.Id);
        status($"Shell running (PID {shell.Id})");

        var games = new Dictionary<int, (Process Process, DateTimeOffset Started)>();
        DateTimeOffset? shellExitedAt = null;
        var diagnosticRefresh = DateTimeOffset.MinValue;
        int lastShellState = -1;
        DateTimeOffset? stopping = null;
        bool closeSent = false;
        var nextCloseAttempt = DateTimeOffset.MinValue;
        bool nativeErrorReported = false;
        string? launchFailure = null;

        try
        {
            while (true)
            {
                int shellState = ReadShellState(installation, shell.Id);

                placement.Update(inputs?.GamePid??0,shell.HasExited?0:shell.Id);
                if (!nativeErrorReported && NativeLaunchError(installation, shell.Id, started.UtcDateTime) is string nativeError)
                {
                    nativeErrorReported = true;
                    outputs.SessionClosing();
                    launchFailure = nativeError;
                    log.Write(nativeError); status(nativeError); stopping = DateTimeOffset.Now;
                }
                if (shutdown.IsCancellationRequested)
                {
                    outputs.SessionClosing();
                    log.Write("Immediate launcher shutdown requested; terminating owned process tree.");

                    foreach (var entry in games.Values)
                    if (!entry.Process.HasExited)
                    entry.Process.Kill(entireProcessTree:true);

                    if (!shell.HasExited)
                    shell.Kill(entireProcessTree:true);

                    break;
            }

            if (shell.HasExited && stopping is null)
            {
                outputs.SessionClosing();
                stopping = DateTimeOffset.Now;
                log.Write("Shell exited unexpectedly; closing any owned game.");
                }
                if (stopping is not null)
                {
                    var elapsed = DateTimeOffset.Now - stopping.Value;
                    if (DateTimeOffset.Now >= nextCloseAttempt && (shellState == 34 || shell.HasExited || elapsed.TotalSeconds > 10))
                    {
                        foreach (var entry in games.Values) if (!entry.Process.HasExited) entry.Process.CloseMainWindow();
                        bool shellRequested = !shell.HasExited && CloseShellWindow(shell.Id);
                        closeSent = true;
                        nextCloseAttempt = DateTimeOffset.Now.AddSeconds(1);
                        log.Write($"Orderly window-close attempt; current GameShell window requested={shellRequested}.");
                    }
                    if (elapsed.TotalSeconds > 25)
                    {
                        foreach (var entry in games.Values) if (!entry.Process.HasExited) { log.Write("Orderly game shutdown timed out; terminating owned PID=" + entry.Process.Id); entry.Process.Kill(); }
                        if (!shell.HasExited) { log.Write("Orderly Shell shutdown timed out; terminating owned PID=" + shell.Id); shell.Kill(); }
                    }
                }
                if (shellState != lastShellState && shellState >= 0)
                {
                    lastShellState = shellState;
                    if (shellState == 34)
                    {
                        log.Write("Original Shell operator menu active (state 34); awaiting original return/relaunch flow.");
                        status("Operator menu: Service chooses; Test selects");
                        if (stopping is null) FocusOperatorWindow(shell.Id);
                    }
                }
                if (DateTimeOffset.Now - diagnosticRefresh > TimeSpan.FromSeconds(3))
                {
                    Diagnostics.Update(installation, shell.HasExited ? "Monitoring game after Shell exit" : "Monitoring Shell and game");
                    diagnosticRefresh = DateTimeOffset.Now;
                }
                if (shell.HasExited && shellExitedAt is null)
                {
                    shellExitedAt = DateTimeOffset.Now;
                    var lifetime = shellExitedAt.Value - started;
                    log.Write($"Shell PID={shell.Id}; exited; code={shell.ExitCode}; lifetime={lifetime}");
                    Diagnostics.Update(installation, $"Shell exited with code {shell.ExitCode}", shellExited: true);
                    if (shell.ExitCode != 0 || lifetime < TimeSpan.FromSeconds(10))
                        log.Write("Shell termination was unexpected or too early to confirm normal game startup.");
                    status($"Shell exited (code {shell.ExitCode})");
                }
                foreach (var candidate in Process.GetProcessesByName("DroneRacing"))
                {
                    if (games.ContainsKey(candidate.Id)) { candidate.Dispose(); continue; }
                    try
                    {
                        var actual = Path.GetFullPath(candidate.MainModule?.FileName ?? "");
                        if (!actual.Equals(gamePath, StringComparison.OrdinalIgnoreCase))
                        {
                            candidate.Dispose();
                            continue;
                        }
                        _ = candidate.Handle;
                        var gameStart = candidate.StartTime;
                        if (gameStart < started.LocalDateTime.AddSeconds(-2))
                        {
                            candidate.Dispose();
                            continue;
                        }
                        games.Add(candidate.Id, (candidate, new DateTimeOffset(gameStart)));
                        if (closeSent) candidate.CloseMainWindow();
                        if (inputs is not null) { inputs.GamePid = candidate.Id; inputs.GameLaunched(); }
                        log.Write($"Game PID={candidate.Id}; executable={actual}; started={gameStart:O}");
                        Diagnostics.Update(installation, "Game observed", game: candidate.Id);
                        log.Write($"Game PID={candidate.Id}; exact child arguments/cwd are in the native IO log.");
                        status($"Game running (PID {candidate.Id})");

                    }
                    catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
                    {
                        candidate.Dispose();
                    }
                }

                foreach (var (id, entry) in games.ToArray())
                {
                    if (!entry.Process.HasExited) continue;
                    outputs.GameEnded();
                    var lifetime = DateTimeOffset.Now - entry.Started;
                    log.Write($"Game PID={id}; exited; code={entry.Process.ExitCode}; lifetime={lifetime}");
                    bool operatorExit = !shell.HasExited && entry.Process.ExitCode == 0 && (inputs?.OperatorRequested == true || shellState == 34);
                    string exitStatus = operatorExit ? "Game exited normally for operator flow; monitoring Shell and relaunch" : $"Game exited with code {entry.Process.ExitCode}; monitoring Shell";
                    log.Write(exitStatus);
                    status(exitStatus);
                    Diagnostics.Update(installation, exitStatus, gameExited: true);
                    entry.Process.Dispose();
                    if (inputs is not null && inputs.GamePid == id) inputs.GamePid = 0;
                    games.Remove(id);
                }
                if (shellExitedAt is not null && games.Count == 0 &&
                    DateTimeOffset.Now - shellExitedAt.Value >= TimeSpan.FromSeconds(5)) break;
                await Task.Delay(250).ConfigureAwait(false);
            }
            if (launchFailure is not null) throw new InvalidOperationException(launchFailure);
        }
        catch
        {
            outputs.SessionClosing();
            // Retain the broker until owned processes have had an orderly-close opportunity.
            foreach (var entry in games.Values) if (!entry.Process.HasExited) entry.Process.CloseMainWindow();
            if (!shell.HasExited) CloseShellWindow(shell.Id);
            var deadline = DateTimeOffset.Now.AddSeconds(25);
            while (DateTimeOffset.Now < deadline && (!shell.HasExited || games.Values.Any(entry => !entry.Process.HasExited)))
            {
                if (!shell.HasExited) CloseShellWindow(shell.Id);
                await Task.Delay(1000).ConfigureAwait(false);
            }
            foreach (var entry in games.Values) if (!entry.Process.HasExited) { log.Write("Error cleanup: orderly close timed out; terminating owned game " + entry.Process.Id); entry.Process.Kill(); }
            if (!shell.HasExited) { log.Write("Error cleanup: orderly close timed out; terminating owned Shell " + shell.Id); shell.Kill(); }
            throw;
        }
        finally
        {
            brokerAvailable?.Invoke(null);
            foreach (var entry in games.Values) entry.Process.Dispose();
        }
    }

    private static string? NativeLaunchError(Installation installation, int pid, DateTime startedUtc)
    {
        try
        {
            var path = Path.Combine(installation.LogsRoot, "native-launch-error.json");
            // Windows reuses process IDs. Preserve historical evidence without
            // treating an older launch's error as a failure of the current Shell.
            if (File.GetLastWriteTimeUtc(path) < startedUtc) return null;
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (json.RootElement.GetProperty("ShellPid").GetInt32() == pid) return json.RootElement.GetProperty("Error").GetString();
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException) { }
        return null;
    }

    internal static void FocusOperatorWindow(int pid)
    {
        var window = FindWindow(null, "GameShell");
        GetWindowThreadProcessId(window, out uint owner);
        if (window != IntPtr.Zero && owner == pid) { ShowWindow(window, 9); SetForegroundWindow(window); }
    }

    private static bool CloseShellWindow(int pid)
    {
        // Shell recreates its window during TEST transitions. Do not use a
        // cached Process.MainWindowHandle or a helper/IME window.
        bool sent = false;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != pid) return true;
            var title = new System.Text.StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            if (title.ToString() != "GameShell") return true;
            sent = PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
            return !sent;
        }, IntPtr.Zero);
        return sent;
    }

    internal static int ReadShellState(Installation installation, int pid)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(installation.LogsRoot, "shell-lifecycle.json")));
            return json.RootElement.TryGetProperty("ShellPid", out var owner) && owner.TryGetInt32(out int ownerId) && ownerId == pid &&
                json.RootElement.TryGetProperty("State", out var state) && state.TryGetInt32(out int value) ? value : -1;
        }
        catch (Exception e) when (e is IOException or JsonException) { return -1; }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    private delegate bool WindowCallback(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, System.Text.StringBuilder title, int maximum);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
