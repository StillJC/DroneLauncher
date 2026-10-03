using System.IO.MemoryMappedFiles;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace DroneRacingGenesisLoader;

internal sealed class OutputConfiguration
{
    public bool RumbleEnabled {get;set;}
    public int XInputSlot {get;set;}
    public int Strength {get;set;}=40;
    public bool TcpEnabled {get;set;}
    public string Host {get;set;}="127.0.0.1";
    public int Port {get;set;}=8000;
    internal void Validate()
    {
        if(XInputSlot is <0 or >3||Strength is <0 or >100||Port is <1 or >65535||string.IsNullOrWhiteSpace(Host)||Host.Length>253||Host.Any(char.IsControl))
            throw new InvalidDataException("Outputs need an XInput slot 0–3, strength 0–100%, destination host and port 1–65535.");
    }
}

internal sealed class OutputManager : IDisposable
{
    internal string MappingName {get;}=$"Local\\DRG.Outputs.{Environment.ProcessId}.{Guid.NewGuid():N}";
    private readonly MemoryMappedFile mapping;
    private readonly MemoryMappedViewAccessor view;
    private readonly OutputConfiguration config;
    private readonly LoaderLog log;
    private readonly CancellationTokenSource stop=new();
    private readonly Channel<string> queue=Channel.CreateBounded<string>(new BoundedChannelOptions(64){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true,SingleWriter=false});
    private readonly Task worker,tcp;
    private readonly object rumbleLock=new(),levelsLock=new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,int> levels=new();
    private static readonly (string Name,uint Mask)[] Lamps=[("controller_red",0x100),("controller_green",0x200),("controller_blue",0x400),("footwell_red",0x20),("footwell_green",0x40),("footwell_blue",0x80),("monitor_lower_red",4),("monitor_lower_green",8),("monitor_lower_blue",16)];
    private volatile int vibration,billboard;
    private int lastMotor=-1;
    private uint lastRumbleResult=uint.MaxValue;
    private int gameEpoch;
    private volatile bool sessionClosing;
    private long eventSequence;
    internal int ShellPid;
    internal OutputManager(Installation installation,LoaderLog log)
    {
        this.log=log;string path=Path.Combine(installation.ConfigRoot,"outputs.json");
        try{config=File.Exists(path)?JsonSerializer.Deserialize<OutputConfiguration>(File.ReadAllText(path))??throw new InvalidDataException("Outputs configuration is empty."):new();config.Validate();}
        catch(Exception e)when(e is IOException or JsonException or InvalidDataException){config=new();log.Write("Optional outputs disabled: "+e.Message);}
        mapping=MemoryMappedFile.CreateNew(MappingName,4128);view=mapping.CreateViewAccessor();view.Write(0,1u);
        foreach(var lamp in Lamps)levels[lamp.Name]=0;levels["vibration"]=levels["billboard"]=0;
        log.Write($"Outputs: rumble={config.RumbleEnabled} slot={config.XInputSlot} strength={config.Strength}; TCP={config.TcpEnabled}; optional nonblocking ring={MappingName}");
        worker=Task.Run(Poll);tcp=config.TcpEnabled?Task.Run(Tcp):Task.CompletedTask;
    }
    internal void GameEnded(){Interlocked.Increment(ref gameEpoch);SetMotor(0);}
    internal void SessionClosing(){sessionClosing=true;GameEnded();}
    private async Task Poll()
    {
        uint sequence=0;int epoch=0;long nextSnapshot=0,nextMotorCheck=0;bool stale=false;
        try
        {
            while(!stop.IsCancellationRequested)
            {
                uint now=unchecked((uint)Environment.TickCount),tick=view.ReadUInt32(12);
                bool fresh=view.ReadUInt32(0)==1&&ShellPid!=0&&view.ReadUInt32(4)==(uint)ShellPid&&unchecked(now-tick)<3000;
                if(epoch!=Volatile.Read(ref gameEpoch)){epoch=Volatile.Read(ref gameEpoch);sequence=view.ReadUInt32(8);vibration=0;Emit("vibration",0);}
                if(!fresh){SetMotor(0);if(!stale){vibration=0;billboard=0;foreach(string name in levels.Keys)Emit(name,0);stale=true;}}
                else
                {
                    stale=false;uint published=view.ReadUInt32(8);Thread.MemoryBarrier();
                    if(unchecked(published-sequence)>256){log.Write("Output observer overrun; stopping rumble and resynchronizing latest events.");SetMotor(0);sequence=published-256;}
                    while(sequence!=published)
                    {
                        uint next=sequence+1;long offset=32+((next-1)%256)*16;
                        uint first=view.ReadUInt32(offset);Thread.MemoryBarrier();uint word=view.ReadUInt32(offset+8);Thread.MemoryBarrier();
                        if(first!=next||view.ReadUInt32(offset)!=first)break;
                        sequence=next;Decode(word);
                    }
                }
                if(fresh&&Environment.TickCount64>=nextMotorCheck){nextMotorCheck=Environment.TickCount64+100;SetMotor(vibration!=0?config.Strength*65535/100:0);}
                if(Environment.TickCount64>=nextSnapshot){nextSnapshot=Environment.TickCount64+1000;SendSnapshot();}
                await Task.Delay(10,stop.Token).ConfigureAwait(false);
            }
        }
        catch(OperationCanceledException){}
        catch(Exception e){log.Write("Optional outputs stopped: "+e.Message);}
        finally{SetMotor(0);queue.Writer.TryComplete();}
    }
    // Shell banks: first bank is bits 0..13; second has selector 0x4000.
    // Vibration=original 0x2000; billboard=original 0x10000 -> bank 2 bit 2.
    private void Decode(uint word)
    {
        if(sessionClosing)return;
        if((word&0x8000)!=0)return; // Original board handshake/user protocol, not a level bank.
        if((word&0x4000)==0)
        {
            foreach(var lamp in Lamps){int state=(word&lamp.Mask)!=0?1:0;if(levels[lamp.Name]!=state)Emit(lamp.Name,state);}
            int value=(word&0x2000)!=0?1:0;
            if(vibration!=value){vibration=value;Emit("vibration",value);log.Write("Original output vibration="+value);}
            SetMotor(value!=0?config.Strength*65535/100:0);
        }
        else
        {
            int value=(word&4)!=0?1:0;
            if(billboard!=value){billboard=value;Emit("billboard",value);log.Write("Original output billboard="+value);}
        }
    }
    private void Emit(string name,int value)
    {
        lock(levelsLock){levels[name]=value;Send(name,value);}
    }
    private void SendSnapshot()
    {lock(levelsLock){foreach(var level in levels)Send(level.Key,level.Value);}}
    private void Send(string name,int value)
    {
        if(!config.TcpEnabled)return;
        queue.Writer.TryWrite(JsonSerializer.Serialize(new{version=1,sequence=Interlocked.Increment(ref eventSequence),time=DateTimeOffset.UtcNow.ToString("O"),output=name,value})+"\n");
    }
    private async Task Tcp()
    {
        while(!stop.IsCancellationRequested)
        {
            try
            {
                using var client=new TcpClient();using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop.Token)){deadline.CancelAfter(1000);await client.ConnectAsync(config.Host,config.Port,deadline.Token).ConfigureAwait(false);}
                client.NoDelay=true;log.Write($"Output TCP connected to {config.Host}:{config.Port}.");
                // Drop queued historical pulses; every new receiver starts with current logical state.
                lock(levelsLock){while(queue.Reader.TryRead(out _)){}SendSnapshot();}
                using var stream=client.GetStream();
                await foreach(var line in queue.Reader.ReadAllAsync(stop.Token).ConfigureAwait(false))
                {using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);deadline.CancelAfter(1000);await stream.WriteAsync(Encoding.UTF8.GetBytes(line),deadline.Token).ConfigureAwait(false);}
            }
            catch(Exception e)
            {if(!stop.IsCancellationRequested)log.Write("Output TCP unavailable; game continues. "+e.GetType().Name);}
            if(stop.IsCancellationRequested)break;
            try{await Task.Delay(2000,stop.Token).ConfigureAwait(false);}catch(OperationCanceledException){break;}
        }
    }
    private void SetMotor(int value)
    {
        if(!config.RumbleEnabled)return;
        if(sessionClosing)value=0;
        lock(rumbleLock)
        {
            if(value==lastMotor&&value==0)return;
            var state=new Vibration{Left=(ushort)value,Right=(ushort)value};
            uint result=XInputSetState((uint)config.XInputSlot,ref state);
            if(result!=lastRumbleResult || (result==0 && value!=lastMotor))
                log.Write($"XInput rumble slot={config.XInputSlot}; motor={value}; result={result}");
            lastRumbleResult=result;lastMotor=result==0?value:-1;
        }
    }
    [StructLayout(LayoutKind.Sequential)]private struct Vibration{public ushort Left,Right;}
    [DllImport("xinput1_4.dll")]private static extern uint XInputSetState(uint index,ref Vibration vibration);
    public void Dispose(){stop.Cancel();SetMotor(0);try{Task.WhenAll(worker,tcp).GetAwaiter().GetResult();}finally{SetMotor(0);view.Dispose();mapping.Dispose();stop.Dispose();}}
}
