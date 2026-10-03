using System.Text.Json;
namespace DroneRacingGenesisLoader;
internal static class Diagnostics
{
    private static readonly object Gate=new();
    private static int? shellPid,gamePid;
    private static string lastResult="Not launched";
    public static void Update(Installation installation,string phase,int? shell=null,int? game=null,bool shellExited=false,bool gameExited=false)
    {
        lock(Gate)
        {
            if(!phase.StartsWith("Monitoring",StringComparison.Ordinal))lastResult=phase;
            if(shell is not null)shellPid=shell;if(game is not null)gamePid=game;
            if(shellExited)shellPid=null;if(gameExited)gamePid=null;
            JsonElement? native=null;bool active=false;
            try
            {
                if(gamePid is not null)
                {
                    using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(installation.LogsRoot,$"unity-{gamePid}.json")));
                    native=document.RootElement.Clone();var value=native.Value;
                    active=value.GetProperty("GamePid").GetInt32()==gamePid && value.GetProperty("Active").GetBoolean() && Environment.TickCount64-value.GetProperty("Tick").GetInt64() is >=0 and <10000;
                }
            }
            catch(Exception e) when(e is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { }
            JsonElement? io=null;bool ioReady=false;
            try
            {
                if(shellPid is not null)
                {
                    using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(installation.LogsRoot,"shell-lifecycle.json")));
                    var value=document.RootElement;
                    if(value.GetProperty("ShellPid").GetInt32()==shellPid && Environment.TickCount64-value.GetProperty("Tick").GetInt64() is >=0 and <10000)
                    {io=value.Clone();ioReady=value.GetProperty("IOReady").ValueKind==JsonValueKind.True || value.GetProperty("IOReady").ToString()=="1";}
                }
            }
            catch(Exception e) when(e is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { }
            var data=new { ObservedAt=DateTimeOffset.Now,Phase=phase,installation.ContentRoot,ShellPid=shellPid,GamePid=gamePid,
                ShellState=shellPid is null?-1:ProcessMonitor.ReadShellState(installation,shellPid.Value),
                Architecture="Native IO x86 + per-child Unity compatibility x64; no research runtime",
                ShellBuild=shellPid is null?"not running; validated on launch":"SHA256 verified before launch by ShellStartupArguments",GameAssemblyBuild=active?"native SHA256/signatures verified":"awaiting native activation",
                IOStatus=shellPid is null?"not running":ioReady?"native module active; IO ready":io.HasValue?"native module active; waiting for IO ready":"awaiting fresh native status",IOLifecycle=io,UnityCompatibilityActive=active,NativeStatus=native,
                SharedInputMapping=active&&native?.GetProperty("Mapped").GetBoolean()==true?"mapped":"not currently mapped",LastLaunchResult=lastResult,
                NetworkMode=File.Exists(Path.Combine(installation.ConfigRoot,"network.json"))?File.ReadAllText(Path.Combine(installation.ConfigRoot,"network.json")):"Original",
                MissingRequiredComponents=installation.MissingComponents() };
            Directory.CreateDirectory(installation.LogsRoot);string path=Path.Combine(installation.LogsRoot,"diagnostics.json");
            // Readers such as Explorer and diagnostic tools may omit FILE_SHARE_DELETE.
            // A busy status file must never end monitoring or dispose the input broker.
            try { File.WriteAllText(path+".tmp",JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));File.Move(path+".tmp",path,true); }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException) { }
        }
    }
}
