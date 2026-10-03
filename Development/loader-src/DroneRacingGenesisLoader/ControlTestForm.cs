using System.Reflection;
using System.Text.Json;

namespace DroneRacingGenesisLoader;

internal static class LauncherStyle
{
    internal static readonly Color Background = Color.FromArgb(16,24,34), Surface = Color.FromArgb(28,40,54), Accent = Color.FromArgb(63,199,235);
    internal static void Apply(Form form)
    {
        form.Font=new Font("Segoe UI",10); form.BackColor=Background;form.ForeColor=Color.WhiteSmoke;
        form.AutoScaleMode=AutoScaleMode.Dpi;form.StartPosition=FormStartPosition.CenterParent;
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("DroneLauncher.Icon");
        if(stream is not null) form.Icon=new Icon(stream);
    }
    internal static Button Button(string text, EventHandler click)
    {
        var b=new Button{Text=text,Height=42,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Surface,ForeColor=Color.WhiteSmoke,Margin=new Padding(5),UseVisualStyleBackColor=false};
        b.FlatAppearance.BorderColor=Color.FromArgb(58,77,95); b.Click+=click;return b;
    }
    internal static Image Logo()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("DroneLauncher.Logo")!;
        using var image=Image.FromStream(stream);return new Bitmap(image);
    }
}

internal static class ControlsStore
{
    internal static ControlsConfiguration Load(Installation i)=>JsonSerializer.Deserialize<ControlsConfiguration>(File.ReadAllText(RuntimeConfiguration.EnsureControls(i))) ?? throw new InvalidDataException("Controls configuration is empty.");
    internal static void Save(Installation i,ControlsConfiguration value)
    {
        foreach(var binding in value.Actions.SelectMany(a=>a.Bindings)) PhysicalInputManager.Validate(binding);
        if(value.ExitBinding is {} exit) PhysicalInputManager.ValidateExit(exit);
        string path=RuntimeConfiguration.EnsureControls(i);
        File.Copy(path,path+".previous",true);
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(path+".tmp",path,true);
    }
}

internal sealed class ControlTestForm : Form
{
    private readonly PhysicalInputManager broker;
    private readonly bool ownsBroker,previousObserve,previousSuppress,previousExit;
    private readonly System.Windows.Forms.Timer timer=new(){Interval=100};
    private readonly StreamWriter evidence;
    private readonly ListView values=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true};
    private readonly ListView physical=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true};
    private readonly Label devices=new(){Dock=DockStyle.Bottom,Height=54,Padding=new Padding(8)};
    private string previous="";
    internal ControlTestForm(Installation installation,LoaderLog log,PhysicalInputManager? active,ControlsConfiguration? edited=null)
    {
        Text="Test Controls — Drone Launcher";ClientSize=new Size(780,660);MinimumSize=new Size(680,540);LauncherStyle.Apply(this);
        broker=active??new PhysicalInputManager(installation,log,edited??ControlsStore.Load(installation));ownsBroker=active is null;
        previousObserve=broker.ObservePhysicalInputs;previousSuppress=broker.SuppressKeyboard;previousExit=broker.SuppressExit;
        broker.ObservePhysicalInputs=true;broker.SuppressKeyboard=false;broker.SuppressExit=true;
        string path=Path.Combine(installation.LogsRoot,"controller-test-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".jsonl");
        evidence=new StreamWriter(path,false){AutoFlush=true};log.Write("Controller test evidence="+path);
        var help=new Label{Dock=DockStyle.Top,Height=78,Padding=new Padding(12),Text="Move each stick fully and release it. Press each button, then disconnect and reconnect.\nUp = Dive (negative); Down = Climb (positive). Exit is displayed only in this test.\n"+(active is null || (active.ShellPid==0 && active.GamePid==0)?"Offline test — the game is not running.":"Active session: these controls also reach the game and original operator menus.")};
        var tabs=new TabControl{Dock=DockStyle.Fill};var mapped=new TabPage("Game controls");var raw=new TabPage("Device diagnostics");
        values.Columns.Add("Control",350);values.Columns.Add("Value",130);values.Columns.Add("Level",190);
        physical.Columns.Add("Backend / device",190);physical.Columns.Add("Control",250);physical.Columns.Add("Value",150);
        mapped.Controls.Add(values);raw.Controls.Add(physical);tabs.TabPages.AddRange([mapped,raw]);
        foreach(string label in new[]{"Steering — Left / Right","Vertical — Dive / Climb","Trigger / Confirm / Boost","Start","Boost (including legacy bindings)","Secondary — raw button; no menu Back","Coin","Service","Test","Exit Game / Launcher"}) values.Items.Add(new ListViewItem([label,"",""]));
        var close=LauncherStyle.Button("Close",(_,_)=>Close());close.Dock=DockStyle.Bottom;
        Controls.AddRange([tabs,help,devices,close]);timer.Tick+=(_,_)=>RefreshValues();timer.Start();
    }
    private void RefreshValues()
    {
        var s=broker.Snapshot;
        float V(string name)=>s.GetValueOrDefault(name);
        float[] v=[V("Horizontal"),V("Vertical"),Math.Max(V("Accelerate / Confirm"),Math.Max(V("Trigger"),V("Right Trigger"))),V("Start"),V("Boost"),Math.Max(V("Brake / Cancel"),V("Secondary Button")),V("Coin"),V("Service"),V("Test"),V("Exit")];
        for(int n=0;n<v.Length;n++){values.Items[n].SubItems[1].Text=n<3?v[n].ToString("+0.000;-0.000;0.000"):v[n]>.5f?"ON":"OFF";values.Items[n].SubItems[2].Text=new string('|',(int)(Math.Abs(v[n])*20));}
        var samples=broker.PhysicalSnapshot;
        var connections=samples.Where(p=>p.Binding.Kind!="keyboardKey").Select(p=>(p.Binding.Kind.StartsWith("xinput")?"XInput ":"Windows joystick ")+p.Binding.Device).Distinct().ToArray();
        devices.Text=connections.Length==0?"No controller connected. Keyboard remains available.":"Connected: "+string.Join(", ",connections)+"\nDevice diagnostics may show two Windows interfaces for one physical controller.";
        physical.BeginUpdate();physical.Items.Clear();
        foreach(var p in samples.Where(p=>p.Binding.Kind.EndsWith("Axis")||p.Value>.5f)) physical.Items.Add(new ListViewItem([p.Binding.Kind+" "+p.Binding.Device,p.Binding.Control,p.Value.ToString("+0.000;-0.000;0.000")]));
        physical.EndUpdate();
        string current=JsonSerializer.Serialize(new{Mapped=v.Select(x=>Math.Round(x,2)),Devices=connections,Physical=samples.Select(p=>new{p.Binding.Kind,p.Binding.Device,p.Binding.Control,Value=Math.Round(p.Value,2)})});
        if(current!=previous){evidence.WriteLine("{\"Time\":\""+DateTimeOffset.Now.ToString("O")+"\",\"Sample\":"+current+"}");previous=current;}
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)=>true;
    protected override void Dispose(bool disposing)
    {
        if(disposing){timer.Dispose();evidence.Dispose();broker.ObservePhysicalInputs=previousObserve;broker.SuppressKeyboard=previousSuppress;broker.SuppressExit=previousExit;if(ownsBroker)broker.Dispose();}
        base.Dispose(disposing);
    }
}

internal sealed class BindingCaptureForm : Form
{
    private readonly PhysicalInputManager broker;
    private readonly bool previousObserve,previousSuppress,previousExit;
    private readonly System.Windows.Forms.Timer timer=new(){Interval=40};
    private readonly Dictionary<string,float> baseline=[];
    private readonly long readyAt=Environment.TickCount64+600;
    private readonly bool buttonOnly;
    internal ControlBinding? Captured {get;private set;}
    internal float CapturedValue {get;private set;}
    private static string Id(PhysicalSample s)=>s.Binding.Kind+"/"+s.Binding.Device+"/"+s.Binding.Control;
    internal BindingCaptureForm(PhysicalInputManager broker,bool buttonOnly=false,string? action=null,string? instruction=null)
    {
        this.broker=broker;this.buttonOnly=buttonOnly;Text="Capture Binding"+(action is null?"":" - "+action);ClientSize=new Size(500,180);LauncherStyle.Apply(this);
        previousObserve=broker.ObservePhysicalInputs;previousSuppress=broker.SuppressKeyboard;previousExit=broker.SuppressExit;
        broker.ObservePhysicalInputs=true;broker.SuppressKeyboard=false;broker.SuppressExit=true;
        Controls.Add(new Label{Text="Release all controls first.\n"+(instruction??(buttonOnly?"Press one keyboard key or controller button.":"Press a key/button or move one axis fully.")),Dock=DockStyle.Fill,Padding=new Padding(20)});
        var cancel=LauncherStyle.Button("Cancel",(_,_)=>Close());cancel.Dock=DockStyle.Bottom;Controls.Add(cancel);
        timer.Tick+=(_,_)=>Poll();timer.Start();
    }
    private void Poll()
    {
        var samples=broker.PhysicalSnapshot;
        if(Environment.TickCount64<readyAt){foreach(var s in samples)baseline[Id(s)]=s.Value;return;}
        // Released keyboard keys disappear from the snapshot; allow them to be pressed again.
        foreach(string key in baseline.Keys.Where(k=>k.StartsWith("keyboardKey/")&&!samples.Any(s=>Id(s)==k)).ToArray())baseline[key]=0;
        // Prefer XInput when both Windows interfaces expose the same controller.
        foreach(var s in samples.OrderBy(p=>p.Binding.Kind.StartsWith("joystick")?1:0))
        {
            bool axis=s.Binding.Kind.EndsWith("Axis");if(buttonOnly&&axis)continue;
            float start=baseline.GetValueOrDefault(Id(s));
            if((axis&&Math.Abs(s.Value-start)>.65f)||(!axis&&s.Value>.5f&&start<=.5f))
            {timer.Stop();Captured=s.Binding;CapturedValue=s.Value;if(axis){Captured.Deadzone=.15f;}DialogResult=DialogResult.OK;Close();return;}
            if(!axis&&s.Value<=.5f)baseline[Id(s)]=0;
        }
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)=>true;
    protected override void OnFormClosed(FormClosedEventArgs e){timer.Stop();base.OnFormClosed(e);}
    protected override void Dispose(bool disposing){if(disposing){timer.Dispose();broker.ObservePhysicalInputs=previousObserve;broker.SuppressKeyboard=previousSuppress;broker.SuppressExit=previousExit;}base.Dispose(disposing);}
}
