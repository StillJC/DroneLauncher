using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DroneRacingGenesisLoader;

// One sampler per launch. Both Shell and a future Unity adapter can read this mapping.
internal sealed class PhysicalInputManager : IDisposable
{
    public string MappingName { get; } = $"Local\\DRG.Controls.{Environment.ProcessId}.{Guid.NewGuid():N}";
    private readonly MemoryMappedFile mapping;
    private readonly MemoryMappedViewAccessor view;
    private readonly CancellationTokenSource stop = new();
    private readonly Thread worker;
    private readonly ControlsConfiguration config;
    private readonly Dictionary<int, bool> keys = [];
    private readonly Dictionary<int, XState?> pads = [];
    private readonly Dictionary<int, Joy?> joys = [];
    private IReadOnlyDictionary<string, float> lastValues = new Dictionary<string, float>();
    public IReadOnlyDictionary<string, float> Snapshot => Volatile.Read(ref lastValues);
    public volatile bool SuppressKeyboard;
    public volatile bool ObservePhysicalInputs;
    public volatile bool SuppressExit;
    private bool exitHeld = true;
    private int exitPresses;
    public int ExitPresses => Volatile.Read(ref exitPresses);
    private PhysicalSample[] physical = [];
    public PhysicalSample[] PhysicalSnapshot => Volatile.Read(ref physical);
    private readonly Dictionary<string, float> logical = new(StringComparer.OrdinalIgnoreCase);
    private readonly LoaderLog log;
    private readonly int xmin, xmid, xmax, ymin, ymid, ymax, tmin, tmax, testInput, serviceInput;
    private uint sequence;
    private ushort coins;
    private bool coinHeld;
    public volatile int ShellPid;
    public volatile int GamePid;
    private bool keyboardActive;
    private long requestedTestUntil;
    public bool OperatorRequested { get; private set; }
    public void GameLaunched() => OperatorRequested = false;
    public void RequestOperatorMenu()
    {
        OperatorRequested = true;
        Interlocked.Exchange(ref requestedTestUntil, Environment.TickCount64 + 250);
        log.Write("Operator menu requested through the normal cabinet TEST report.");
    }
    private readonly IntPtr loaderWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

    public PhysicalInputManager(Installation installation, LoaderLog log, ControlsConfiguration? previewConfiguration = null)
    {
        this.log = log;
        config = previewConfiguration ?? JsonSerializer.Deserialize<ControlsConfiguration>(File.ReadAllText(RuntimeConfiguration.EnsureControls(installation)))
            ?? throw new InvalidDataException("Invalid controls.json");
        if (config.Version != 2) throw new InvalidDataException("IO compatibility requires controls.json version 2.");
        foreach (var binding in config.Actions.SelectMany(action => action.Bindings)) Validate(binding);
        if (config.ExitBinding is { } exit) ValidateExit(exit);
        string ini = Path.Combine(installation.ShellDataRoot, "ShellData.ini");
        xmin = Ini("Joystick", "XMin", 0, ini); xmid = Ini("Joystick", "XCentre", 2048, ini); xmax = Ini("Joystick", "XMax", 4095, ini);
        ymin = Ini("Joystick", "YMin", 0, ini); ymid = Ini("Joystick", "YCentre", 2048, ini); ymax = Ini("Joystick", "YMax", 4095, ini);
        tmin = Ini("Throttle", "Min", 0, ini); tmax = Ini("Throttle", "Max", 4095, ini);
        if (!(0 <= xmin && xmin < xmid && xmid < xmax && xmax <= 65535 && 0 <= ymin && ymin < ymid && ymid < ymax && ymax <= 65535 && 0 <= tmin && tmin < tmax && tmax <= 65535))
            throw new InvalidDataException("ShellData.ini analog calibration has invalid bounds.");
        string shellConfig = Path.Combine(installation.ShellRoot, "config.ini");
        testInput = Ini("System", "TEST", 1, shellConfig); serviceInput = Ini("System", "SERVICE", 2, shellConfig);
        if (testInput is < 1 or > 15 || serviceInput is < 1 or > 15) throw new InvalidDataException("Unsupported TEST/SERVICE IO indices in config.ini.");
        mapping = MemoryMappedFile.CreateNew(MappingName, 64);
        view = mapping.CreateViewAccessor(0, 64, MemoryMappedFileAccess.ReadWrite);
        Sample(); // A fresh valid snapshot exists before Shell can open it.
        log.Write($"Shared input mapping={MappingName}; X={xmin}/{xmid}/{xmax}, Y={ymin}/{ymid}/{ymax}, throttle={tmin}/{tmax}; Shell calibration retained.");
        // Keep the heartbeat independent of thread-pool work during Unity startup.
        worker = new Thread(() =>
        {
            long previous = Environment.TickCount64;
            try { while (!stop.IsCancellationRequested) {
                Sample();
                long now = Environment.TickCount64;
                if (now - previous > 250) log.Write($"Input sampler gap={now - previous}ms; heartbeat threshold=2000ms.");
                previous = now;
                if (stop.Token.WaitHandle.WaitOne(8)) break;
            } }
            catch (Exception error) { log.Write($"Input sampling failed: {error}. IO heartbeat will expire."); }
        }) { IsBackground = true, Name = "DRG physical input sampler" };
        worker.Start();
    }

    private void Sample()
    {
        keys.Clear(); pads.Clear(); joys.Clear(); logical.Clear();
        var foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out uint foregroundPid);
        keyboardActive = !SuppressKeyboard && foregroundPid != 0 && (foregroundPid == ShellPid || foregroundPid == GamePid || foregroundPid == Environment.ProcessId);
        foreach (var action in config.Actions)
        {
            float value = 0;
            foreach (var binding in action.Bindings) value += Apply(Read(binding), binding);
            logical[action.Name] = Math.Clamp(value, -1, 1);
        }
        // Original cabinet uses one trigger for menu confirmation and race boost.
        // Preserve legacy additional Boost/View bindings; the shared trigger is the public action.
        if(config.LinkTriggerAndBoost)logical["Boost"]=Math.Max(logical.GetValueOrDefault("Boost"),Math.Max(logical.GetValueOrDefault("Accelerate / Confirm"),Math.Max(logical.GetValueOrDefault("Trigger"),logical.GetValueOrDefault("Right Trigger"))));
        bool exitDown = config.ExitBinding is { } exit && Read(exit) > .5f;
        logical["Exit"] = exitDown ? 1 : 0;
        if (exitDown && !exitHeld && !SuppressExit) Interlocked.Increment(ref exitPresses);
        exitHeld = exitDown;
        if (ObservePhysicalInputs) Observe();
        Volatile.Write(ref lastValues, new Dictionary<string, float>(logical, StringComparer.OrdinalIgnoreCase));
        float Value(string name) => logical.GetValueOrDefault(name);
        bool Down(params string[] names) => names.Any(name => Value(name) > .5f);
        byte[] report = new byte[22];
        void Word(int offset, ushort value) { report[offset] = (byte)value; report[offset + 1] = (byte)(value >> 8); }
        void Bit(int input, bool active) { if (active) report[13 + (input - 1) / 8] |= (byte)(1 << ((input - 1) % 8)); }
        Word(1, CabinetAxis(Value("Vertical"), ymin, ymid, ymax));
        Word(3, CabinetAxis(Value("Horizontal"), xmin, xmid, xmax));
        Word(5, (ushort)Math.Round(tmin + Math.Clamp(Value("Throttle"), 0, 1) * (tmax - tmin)));
        bool test = Down("Test") || Environment.TickCount64 < Interlocked.Read(ref requestedTestUntil);
        if (test) OperatorRequested = true;
        Bit(testInput, test); Bit(serviceInput, Down("Service")); Bit(6, Down("Start"));
        Bit(8, Down("Trigger", "Right Trigger", "Accelerate / Confirm"));
        Bit(9, Down("Secondary Button", "Brake / Cancel"));
        bool coin = Down("Coin"); if (coin && !coinHeld) { coins++; log.Write($"Cabinet coin pulse: cumulative counter={coins}"); } coinHeld = coin;
        Word(16, coins);
        report[18] = 0x10; report[19] = 0xb4;
        view.Write(0, ++sequence); // odd = writer owns snapshot
        Thread.MemoryBarrier();
        view.Write(4, unchecked((uint)Environment.TickCount));
        view.WriteArray(8, report, 0, report.Length);
        view.Write(30, (ushort)1); // Layout version in previously unused padding; still 64 bytes.
        string[] sharedActions = ["Horizontal", "Vertical", "Throttle", "Right Trigger", "Boost", "View", "Start", "Coin"];
        for (int index = 0; index < sharedActions.Length; index++) view.Write(32 + index * 4, Value(sharedActions[index]));
        Thread.MemoryBarrier(); view.Write(0, ++sequence);
    }

    internal static ushort CabinetAxis(float value, int min, int center, int max) =>
        (ushort)Math.Round(center + Math.Clamp(value, -1, 1) * (value < 0 ? center - min : max - center));
    internal static float Apply(float value, ControlBinding binding)
    {
        value = Math.Clamp(value, -1, 1);
        value = Math.Abs(value) <= binding.Deadzone ? 0 : Math.Sign(value) * (Math.Abs(value) - binding.Deadzone) / (1 - binding.Deadzone);
        return Math.Clamp(value * binding.Sensitivity * (binding.Invert ? -1 : 1), -1, 1);
    }
    internal static void Validate(ControlBinding binding)
    {
        if (binding.Deadzone is < 0 or >= 1 || !float.IsFinite(binding.Deadzone) || !float.IsFinite(binding.Sensitivity) || binding.Sensitivity < 0)
            throw new InvalidDataException("Bindings need a finite deadzone in [0,1) and nonnegative sensitivity.");
        if (binding.Kind == "keyboardKey") { _ = Key(binding.Control); return; }
        if (binding.Device != "any" && (!int.TryParse(binding.Device, out int device) || device < 0 || device > (binding.Kind.StartsWith("xinput", StringComparison.Ordinal) ? 3 : 15)))
            throw new InvalidDataException("Device must be 'any' or a supported zero-based device index.");
        bool valid = binding.Kind switch
        {
            "xinputAxis" => new[] { "LeftX", "LeftY", "RightX", "RightY", "LeftTrigger", "RightTrigger" }.Contains(binding.Control),
            "xinputButton" => XButtons.ContainsKey(binding.Control),
            "joystickAxis" => new[] { "X", "Y", "Z", "R", "U", "V" }.Contains(binding.Control),
            "joystickButton" => PovDirections.Contains(binding.Control) || (int.TryParse(binding.Control, out int button) && button is >= 1 and <= 32),
            _ => false
        };
        if (!valid) throw new InvalidDataException($"Unsupported binding {binding.Kind}/{binding.Control}.");
    }
    internal static void ValidateExit(ControlBinding binding)
    {
        Validate(binding);
        if (binding.Kind is not ("keyboardKey" or "xinputButton" or "joystickButton") || binding.Invert || binding.Deadzone != 0 || binding.Sensitivity != 1)
            throw new InvalidDataException("Exit needs one keyboard key or controller button, without axis adjustments.");
    }
    private void Observe()
    {
        var samples = new List<PhysicalSample>();
        void Add(string kind, string device, string control)
        {
            var binding = new ControlBinding { Kind=kind, Device=device, Control=control };
            samples.Add(new PhysicalSample(binding, Read(binding)));
        }
        // Use the same cached reads as game bindings; no second polling stack.
        for (int i=0;i<4;i++)
        {
            Add("xinputAxis",i.ToString(),"LeftX");
            if (pads[i] is null) { samples.RemoveAt(samples.Count-1); continue; }
            foreach (var axis in new[]{"LeftY","RightX","RightY","LeftTrigger","RightTrigger"}) Add("xinputAxis",i.ToString(),axis);
            foreach (string button in XButtons.Keys) Add("xinputButton",i.ToString(),button);
        }
        for (int i=0;i<16;i++)
        {
            Add("joystickAxis",i.ToString(),"X");
            if (joys[i] is null) { samples.RemoveAt(samples.Count-1); continue; }
            foreach (var axis in new[]{"Y","Z","R","U","V"}) Add("joystickAxis",i.ToString(),axis);
            for(int b=1;b<=32;b++) Add("joystickButton",i.ToString(),b.ToString());
            foreach(string direction in PovDirections) Add("joystickButton",i.ToString(),direction);
        }
        for(int key=8;key<255;key++) if(Enum.IsDefined(typeof(Keys),key))
        {
            var binding=new ControlBinding{Kind="keyboardKey",Device="",Control=((Keys)key).ToString()};
            if(Read(binding)>0) samples.Add(new PhysicalSample(binding,1));
        }
        Volatile.Write(ref physical,samples.ToArray());
    }
    private float Read(ControlBinding binding)
    {
        if (binding.Kind == "keyboardKey")
        {
            if (!keyboardActive) return 0;
            int key = Key(binding.Control);
            if (!keys.TryGetValue(key, out bool down)) keys[key] = down = (GetAsyncKeyState(key) & 0x8000) != 0;
            return down ? 1 : 0;
        }
        int count = binding.Kind.StartsWith("xinput", StringComparison.Ordinal) ? 4 : 16;
        var devices = binding.Device == "any" ? Enumerable.Range(0, count) : [int.Parse(binding.Device)];
        float result = 0;
        foreach (int device in devices)
        {
            float value;
            if (binding.Kind.StartsWith("xinput", StringComparison.Ordinal))
            {
                if (!pads.TryGetValue(device, out var state)) pads[device] = state = XInputGetState((uint)device, out var read) == 0 ? read : null;
                if (state is not { } pad) continue;
                value = binding.Kind == "xinputButton" ? ((pad.Buttons & XButtons[binding.Control]) != 0 ? 1 : 0) : binding.Control switch
                {
                    "LeftX" => Signed(pad.LX), "LeftY" => Signed(pad.LY), "RightX" => Signed(pad.RX), "RightY" => Signed(pad.RY),
                    "LeftTrigger" => pad.LT / 255f, "RightTrigger" => pad.RT / 255f, _ => 0
                };
            }
            else
            {
                if (!joys.TryGetValue(device, out var state))
                {
                    var joy = new Joy { Size = 52, Flags = 0xff }; joys[device] = state = joyGetPosEx((uint)device, ref joy) == 0 ? joy : null;
                }
                if (state is not { } stick) continue;
                value = binding.Kind == "joystickButton" ? (PovDirections.Contains(binding.Control) ? PovValue(stick.Pov,binding.Control) : (stick.Buttons & (1u << (int.Parse(binding.Control) - 1))) != 0 ? 1 : 0) :
                    (binding.Control switch { "X" => stick.X, "Y" => stick.Y, "Z" => stick.Z, "R" => stick.R, "U" => stick.U, "V" => stick.V, _ => 32768u }) / 32767.5f - 1;
            }
            if (Math.Abs(value) > Math.Abs(result)) result = value;
        }
        return result;
    }
    private static readonly string[] PovDirections=["PovUp","PovRight","PovDown","PovLeft"];
    internal static float PovValue(uint angle,string direction)
    {
        // JOYINFOEX: hundredths of a degree clockwise; centered is 0xffff.
        if(angle>=36000)return 0;
        int index=Array.IndexOf(PovDirections,direction);
        if(index<0)return 0;
        int delta=Math.Abs((int)angle-index*9000);
        return Math.Min(delta,36000-delta)<=4500 ? 1 : 0;
    }
    internal static string[] ConnectedDevices()
    {
        var result = new List<string>();
        for (uint i=0;i<4;i++) if (XInputGetState(i, out _) == 0) result.Add($"XInput {i}");
        for (uint i=0;i<16;i++) { var joy = new Joy { Size=52, Flags=0xff }; if (joyGetPosEx(i, ref joy)==0) result.Add($"Joystick {i}"); }
        return result.ToArray();
    }
    private static float Signed(short value) => value / (value < 0 ? 32768f : 32767f);
    private static int Key(string control) => Enum.TryParse<Keys>(control, true, out var key) && (int)key is > 0 and < 256
        ? (int)key : throw new InvalidDataException($"Invalid keyboard key '{control}'. Use Windows Keys names such as Left, D5, F2, Space.");
    private static readonly Dictionary<string, ushort> XButtons = new(StringComparer.OrdinalIgnoreCase)
    { ["A"] = 0x1000, ["B"] = 0x2000, ["X"] = 0x4000, ["Y"] = 0x8000, ["Start"] = 0x10, ["Back"] = 0x20,
      ["LeftShoulder"] = 0x100, ["RightShoulder"] = 0x200, ["LeftThumb"] = 0x40, ["RightThumb"] = 0x80, ["Up"] = 1, ["Down"] = 2, ["Left"] = 4, ["Right"] = 8 };
    private static int Ini(string section, string key, int fallback, string path) => (int)GetPrivateProfileInt(section, key, fallback, path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetPrivateProfileIntW")] private static extern uint GetPrivateProfileInt(string section, string key, int fallback, string file);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [StructLayout(LayoutKind.Sequential)] private struct XState { public uint Packet; public ushort Buttons; public byte LT, RT; public short LX, LY, RX, RY; }
    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index, out XState state);
    [StructLayout(LayoutKind.Sequential)] private struct Joy { public uint Size, Flags, X, Y, Z, R, U, V, Buttons, ButtonNumber, Pov, Reserved1, Reserved2; }
    [DllImport("winmm.dll")] private static extern uint joyGetPosEx(uint index, ref Joy joy);
    public void Dispose() { stop.Cancel(); worker.Join(); view.Dispose(); mapping.Dispose(); stop.Dispose(); }
}

internal sealed record PhysicalSample(ControlBinding Binding, float Value);
