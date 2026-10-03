using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DroneRacingGenesisLoader;

internal sealed class GraphicsConfiguration
{
    public bool Enabled {get;set;}
    public string Monitor {get;set;}="";
    public int Width {get;set;}=1920;
    public int Height {get;set;}=1080;
    public string Mode {get;set;}="Borderless";
    public int Quality {get;set;}=-1;
    internal void Validate()
    {
        if(Width<640||Width>16384||Height<480||Height>16384||Mode is not ("Exclusive" or "Borderless" or "Windowed")||Quality is < -1 or >5)
            throw new InvalidDataException("Invalid display size, display mode or quality preset.");
    }
}

internal sealed class DisplayManager
{
    internal static readonly string[] QualityNames=["Very Low","Low","Medium","High","Very High","Ultra"];
    private readonly GraphicsConfiguration configuration;
    private readonly LoaderLog log;
    private readonly Dictionary<int,string> previous=[];
    private readonly Dictionary<int,int> attempts=[];
    private Screen? target;
    internal string? Warning {get;private set;}
    internal DisplayManager(Installation installation,ProcessStartInfo info,LoaderLog log)
    {
        this.log=log;string path=Path.Combine(installation.ConfigRoot,"graphics.json");
        // Clear inherited feature values so a parent shell cannot accidentally enable an override.
        foreach(string key in new[]{"DRG_GRAPHICS","DRG_SCREEN_WIDTH","DRG_SCREEN_HEIGHT","DRG_SCREEN_MODE","DRG_SCREEN_QUALITY","DRG_PLAYER_ARGUMENTS"})info.Environment.Remove(key);
        try
        {
            configuration=File.Exists(path)?JsonSerializer.Deserialize<GraphicsConfiguration>(File.ReadAllText(path))??throw new InvalidDataException("Display configuration is empty."):new();
            configuration.Validate();
            if(!configuration.Enabled)return;
            target=Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==configuration.Monitor);
            if(target is null){target=Screen.PrimaryScreen!;if(configuration.Monitor!="")Warning="Selected monitor unavailable — using Primary Display";}
            var modes=Modes(target.DeviceName);
            if(!modes.Contains((configuration.Width,configuration.Height)))throw new InvalidDataException("Requested resolution is unavailable on the selected monitor.");
            info.Environment["DRG_GRAPHICS"]="1";info.Environment["DRG_SCREEN_WIDTH"]=configuration.Width.ToString();info.Environment["DRG_SCREEN_HEIGHT"]=configuration.Height.ToString();
            info.Environment["DRG_SCREEN_MODE"]=configuration.Mode switch{"Exclusive"=>"0","Borderless"=>"1",_=>"3"};info.Environment["DRG_SCREEN_QUALITY"]=configuration.Quality.ToString();
            int displayIndex=Array.FindIndex(Screen.AllScreens,s=>s.DeviceName==target.DeviceName)+1;
            string args=$"-screen-width {configuration.Width} -screen-height {configuration.Height} -screen-fullscreen {(configuration.Mode=="Windowed"?0:1)} -window-mode {(configuration.Mode=="Exclusive"?"exclusive":"borderless")} -monitor {displayIndex}";
            if(configuration.Quality>=0)args+=$" -screen-quality \"{QualityNames[configuration.Quality]}\"";
            info.Environment["DRG_PLAYER_ARGUMENTS"]=args;
            log.Write($"Display requested device={configuration.Monitor}; resolved={target.DeviceName}; Windows enumeration index={displayIndex}; bounds={target.Bounds}; mode={configuration.Mode}; size={configuration.Width}x{configuration.Height}; quality={configuration.Quality}. Arguments={args}; guarded RootScene override retains selection after game startup.");
        }
        catch(Exception e)when(e is IOException or JsonException or InvalidDataException)
        {configuration=new();target=null;Warning="Display settings invalid — using original game settings";log.Write(Warning+": "+e.Message);}
        if(Warning is not null)log.Write(Warning);
    }
    internal void Update(int gamePid,int shellPid)
    {
        if(!configuration.Enabled||target is null)return;
        // Re-evaluate topology: monitor identifiers persist, desktop coordinates do not.
        var current=Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==target.DeviceName);
        if(current is null){target=Screen.PrimaryScreen!;Warning="Selected monitor unavailable — using Primary Display";log.Write(Warning);}else target=current;
        Place(gamePid,"DroneRacing",false);Place(shellPid,"GameShell",true);
    }
    private void Place(int pid,string title,bool cabinet)
    {
        if(pid==0)return;
        EnumWindows((window,_)=>{
            GetWindowThreadProcessId(window,out uint owner);if(owner!=pid||!IsWindowVisible(window))return true;
            var text=new System.Text.StringBuilder(256);GetWindowText(window,text,256);if(text.ToString()!=title)return true;
            if(!GetWindowRect(window,out var rect))return true;
            var screen=Screen.FromHandle(window);
            if(screen.DeviceName!=target!.DeviceName&&attempts.GetValueOrDefault(pid)<30)
            {
                attempts[pid]=attempts.GetValueOrDefault(pid)+1;
                var bounds=target.Bounds;int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;
                int x=bounds.Left+Math.Max(0,(bounds.Width-width)/2),y=bounds.Top+Math.Max(0,(bounds.Height-height)/2);
                SetWindowPos(window,IntPtr.Zero,x,y,0,0,0x0001|0x0004|0x0010);GetWindowRect(window,out rect);screen=Screen.FromHandle(window);
            }
            string state=$"device={screen.DeviceName}; bounds={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}";
            if(previous.GetValueOrDefault(pid)!=state){previous[pid]=state;log.Write($"Actual {(cabinet?"Shell":"Unity")} window PID={pid}; {state}");}
            return false;
        },IntPtr.Zero);
    }
    internal static (int Width,int Height)[] Modes(string device)
    {
        var modes=new HashSet<(int,int)>();var mode=new DevMode{Size=(ushort)Marshal.SizeOf<DevMode>()};
        for(int n=0;EnumDisplaySettings(device,n,ref mode);n++)if(mode.Width>=640&&mode.Height>=480)modes.Add(((int)mode.Width,(int)mode.Height));
        return modes.OrderBy(m=>m.Item1).ThenBy(m=>m.Item2).ToArray();
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;
        public ushort Spec,Driver,Size,Extra;public uint Fields;public int X,Y;public uint Orientation,FixedOutput;
        public short Color,Duplex,YResolution,TTOption,Collate;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Form;
        public ushort LogPixels;public uint Bits,Width,Height,Flags,Frequency,IcmMethod,IcmIntent,Media,Dither,Reserved1,Reserved2,PanningWidth,PanningHeight;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool EnumDisplaySettings(string device,int mode,ref DevMode data);
    private delegate bool EnumProc(IntPtr window,IntPtr state);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumProc callback,IntPtr state);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr window);
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(IntPtr window,System.Text.StringBuilder text,int count);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
}
