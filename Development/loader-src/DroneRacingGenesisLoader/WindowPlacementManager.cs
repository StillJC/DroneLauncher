using System.Runtime.InteropServices;

namespace DroneRacingGenesisLoader;

/// <summary>
/// Keeps the original Shell and Unity windows on the monitor where
/// Drone Launcher was located when the session was started.
///
/// This class deliberately does not alter Unity resolution, fullscreen
/// state, quality, or any other graphics setting.
/// </summary>
internal sealed class WindowPlacementManager
{
    private readonly LoaderLog log;
    private readonly string? requestedDeviceName;
    private readonly Dictionary<int, string> previous = [];
    private readonly Dictionary<int, int> attempts = [];
    private string? lastResolvedDevice;

    internal WindowPlacementManager(string? requestedDeviceName, LoaderLog log)
    {
        this.requestedDeviceName = requestedDeviceName;
        this.log = log;

        Screen target = ResolveTarget();

        log.Write(
            $"Session monitor requested={requestedDeviceName ?? "<none>"}; " +
            $"resolved={target.DeviceName}; bounds={target.Bounds}.");
    }

    internal void Update(int gamePid, int shellPid)
    {
        Screen target = ResolveTarget();

        Place(gamePid, "DroneRacing", false, target);
        Place(shellPid, "GameShell", true, target);
    }

    private Screen ResolveTarget()
    {
        Screen? target = null;

        if (!string.IsNullOrWhiteSpace(requestedDeviceName))
        {
            target = Screen.AllScreens.FirstOrDefault(
                screen => screen.DeviceName.Equals(
                    requestedDeviceName,
                    StringComparison.OrdinalIgnoreCase));
        }

        target ??= Screen.PrimaryScreen ?? Screen.AllScreens.First();

        if (!string.Equals(
            lastResolvedDevice,
            target.DeviceName,
            StringComparison.OrdinalIgnoreCase))
        {
            if (lastResolvedDevice is not null)
            {
                log.Write(
                    $"Session monitor topology changed; " +
                    $"requested={requestedDeviceName ?? "<none>"}; " +
                    $"resolved={target.DeviceName}; bounds={target.Bounds}.");
            }

            lastResolvedDevice = target.DeviceName;
        }

        return target;
    }

    private void Place(int pid, string title, bool shellWindow, Screen target)
    {
        if (pid == 0)
            return;

        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint owner);

            if (owner != pid || !IsWindowVisible(window))
                return true;

            var titleText = new System.Text.StringBuilder(256);
            GetWindowText(window, titleText, titleText.Capacity);

            if (!titleText.ToString().Equals(title, StringComparison.Ordinal))
                return true;

            if (!GetWindowRect(window, out Rect rect))
                return true;

            Screen current = Screen.FromHandle(window);

            if (!current.DeviceName.Equals(
                    target.DeviceName,
                    StringComparison.OrdinalIgnoreCase) &&
                attempts.GetValueOrDefault(pid) < 30)
            {
                attempts[pid] = attempts.GetValueOrDefault(pid) + 1;

                Rectangle bounds = target.Bounds;
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                int x = bounds.Left + Math.Max(0, (bounds.Width - width) / 2);
                int y = bounds.Top + Math.Max(0, (bounds.Height - height) / 2);

                SetWindowPos(
                    window,
                    IntPtr.Zero,
                    x,
                    y,
                    0,
                    0,
                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

                GetWindowRect(window, out rect);
                current = Screen.FromHandle(window);
            }

            string state =
                $"device={current.DeviceName}; " +
                $"bounds={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}";

            if (previous.GetValueOrDefault(pid) != state)
            {
                previous[pid] = state;

                log.Write(
                    $"Actual {(shellWindow ? "Shell" : "Unity")} " +
                    $"window PID={pid}; {state}");
            }

            return false;

        }, IntPtr.Zero);
    }

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumProc(IntPtr window, IntPtr state);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc callback, IntPtr state);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr window,
        out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(
        IntPtr window,
        System.Text.StringBuilder title,
        int maximum);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(
        IntPtr window,
        out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}