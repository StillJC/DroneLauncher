using System.Text.Json;

namespace DroneRacingGenesisLoader;

internal sealed class ControlsForm : Form
{
    private readonly Installation installation;
    private readonly LoaderLog log;
    private readonly PhysicalInputManager? active;
    private readonly ControlsConfiguration configuration;
    private readonly CheckBox enabled, confirmExit;
    private ControlBinding? exitBinding;
    private readonly DataGridView bindings = new();
    private readonly NumericUpDown deadzone = new() { DecimalPlaces=2, Increment=.01m, Maximum=.99m, Width=80 };
    private readonly NumericUpDown sensitivity = new() { DecimalPlaces=2, Increment=.1m, Maximum=1000, Width=80 };
    private readonly Label tuningLabel = new() { AutoSize=true, Margin=new Padding(4,8,10,4) };
    private readonly Button reverse;
    private bool updatingTuning;
    private sealed record ActionChoice(string Label, string[] Names, bool Axis=false, bool Exit=false);
    private static readonly ActionChoice[] Actions =
    [
        new("Steering — Left / Right", ["Horizontal"], true),
        new("Vertical — Up: Dive / Down: Climb", ["Vertical"], true),
        new("Trigger / Confirm / Boost", ["Accelerate / Confirm", "Trigger", "Right Trigger", "Boost"]),
        new("Start", ["Start"]),
        new("Secondary", ["Brake / Cancel", "Secondary Button"]),
        new("Coin", ["Coin"]),
        new("Service", ["Service"]),
        new("Test", ["Test"]),
        new("Exit Game / Launcher", [], Exit:true)
    ];

    internal ControlsForm(Installation installation, LoaderLog log, PhysicalInputManager? active)
    {
        this.installation=installation; this.log=log; this.active=active;
        configuration=ControlsStore.Load(installation); exitBinding=configuration.ExitBinding;
        Text="Configure Controls - Drone Launcher"; LauncherStyle.Apply(this);
        ClientSize=new Size(1000,610); MinimumSize=new Size(920,610);
        var layout=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(12), ColumnCount=1, RowCount=5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        layout.Controls.Add(new Label { Dock=DockStyle.Fill, Text="Double-click a function, then press a key or controller button, or move an axis.\nFor flight controls, move Left / Up first, or assign two direction keys/buttons. Changes apply next launch." },0,0);

        bindings.Name="ControlBindings"; bindings.Dock=DockStyle.Fill; bindings.ReadOnly=true;
        bindings.AllowUserToAddRows=false; bindings.AllowUserToDeleteRows=false; bindings.AllowUserToResizeRows=false;
        bindings.RowHeadersVisible=false; bindings.MultiSelect=false; bindings.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
        bindings.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill; bindings.BackgroundColor=LauncherStyle.Surface;
        bindings.BorderStyle=BorderStyle.None; bindings.EnableHeadersVisualStyles=false;
        bindings.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle { BackColor=LauncherStyle.Background, ForeColor=Color.WhiteSmoke, Font=Font };
        bindings.ColumnHeadersHeight=30;
        bindings.DefaultCellStyle=new DataGridViewCellStyle { BackColor=LauncherStyle.Surface, ForeColor=Color.WhiteSmoke, SelectionBackColor=Color.FromArgb(42,89,110), SelectionForeColor=Color.White, Padding=new Padding(6,0,6,0) };
        bindings.GridColor=Color.FromArgb(58,77,95); bindings.RowTemplate.Height=35;
        bindings.Columns.Add(new DataGridViewTextBoxColumn { Name="Function", HeaderText="Function", FillWeight=37, SortMode=DataGridViewColumnSortMode.NotSortable });
        bindings.Columns.Add(new DataGridViewTextBoxColumn { Name="Input", HeaderText="Assigned input — double-click to change", FillWeight=63, SortMode=DataGridViewColumnSortMode.NotSortable });
        foreach(var action in Actions) bindings.Rows.Add(action.Label, Summary(action));
        bindings.CellDoubleClick+=(_,e)=>{ if(e.RowIndex>=0) Assign(Actions[e.RowIndex]); };
        bindings.KeyDown+=(_,e)=>{ if(e.KeyCode==Keys.Enter && Selected is {} action) { e.Handled=true; e.SuppressKeyPress=true; Assign(action); } };
        bindings.SelectionChanged+=(_,_)=>RefreshTuning();
        layout.Controls.Add(bindings,0,1);

        var tuning=new FlowLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(0,8,0,0), WrapContents=true };
        reverse=Button("Reverse directions",(_,_)=>{
            if(Selected is not {Axis:true} action)return;
            foreach(var b in Assigned(action)) b.Invert=!b.Invert;
            RefreshRow(action); RefreshTuning();
        });
        tuning.Controls.AddRange([tuningLabel, FieldLabel("Deadzone"), deadzone, FieldLabel("Sensitivity"), sensitivity, reverse]);
        deadzone.ValueChanged+=(_,_)=>Tune(true); sensitivity.ValueChanged+=(_,_)=>Tune(false);
        layout.Controls.Add(tuning,0,2);
        var options=new FlowLayoutPanel { Dock=DockStyle.Fill };
        enabled=new CheckBox { Text="Use configured game controls", Checked=configuration.UnityInputEnabled, AutoSize=true, Margin=new Padding(4,4,20,0) };
        confirmExit=new CheckBox { Text="Confirm before Exit", Checked=configuration.ConfirmExit, AutoSize=true, Margin=new Padding(4,4,4,0) };
        options.Controls.AddRange([enabled,confirmExit]); layout.Controls.Add(options,0,3);
        var footer=new FlowLayoutPanel { Dock=DockStyle.Fill };
        footer.Controls.AddRange([Button("Save",(_,_)=>Save()), Button("Test Controls",(_,_)=>Test()), Button("Clear selected",(_,_)=>{
            if(Selected is {} action){ Replace(action,[]); RefreshRow(action); RefreshTuning(); }
        }), Button("Close",(_,_)=>Close())]);
        layout.Controls.Add(footer,0,4); Controls.Add(layout); RefreshTuning();
    }
    private static Label FieldLabel(string text)=>new() { Text=text, AutoSize=true, Margin=new Padding(8,8,4,4) };
    private static Button Button(string text, EventHandler click)
    {
        var b=LauncherStyle.Button(text,click); b.Dock=DockStyle.None; b.AutoSize=true; b.Height=34; return b;
    }
    private ActionChoice? Selected=>bindings.CurrentRow is {} row ? Actions[row.Index] : null;
    private IEnumerable<ControlBinding> Assigned(ActionChoice action)=>action.Exit
        ? exitBinding is {} b ? [b] : []
        : configuration.Actions.Where(a=>action.Names.Contains(a.Name)).SelectMany(a=>a.Bindings);
    private static string BindingText(ControlBinding b)
    {
        string device=b.Kind=="keyboardKey" ? "Keyboard" : (b.Kind.StartsWith("xinput") ? "XInput" : "Joystick")+" "+b.Device;
        string control=b.Control.StartsWith("Pov") ? "D-pad "+b.Control[3..] : b.Kind=="joystickButton" ? "Button "+b.Control : b.Control;
        return device+" / "+control;
    }
    private string Summary(ActionChoice action)
    {
        string Describe(ControlBinding b)
        {
            string suffix=action.Axis
                ? b.Kind.EndsWith("Axis") ? b.Invert ? " (reversed)" : "" : " → "+(action.Names[0]=="Horizontal" ? b.Invert ? "Left" : "Right" : b.Invert ? "Dive" : "Climb")
                : b.Kind.EndsWith("Axis") ? b.Invert ? " (negative direction)" : " (positive direction)" : "";
            return BindingText(b)+suffix;
        }
        var inputs=Assigned(action).Select(Describe).Distinct().ToArray();
        return inputs.Length==0 ? "Unassigned" : string.Join("; ",inputs);
    }
    private void RefreshRow(ActionChoice action)=>bindings.Rows[Array.IndexOf(Actions,action)].Cells[1].Value=Summary(action);
    private void RefreshTuning()
    {
        updatingTuning=true;
        try {
            var action=Selected;
            var axes=action is {Axis:true} ? Assigned(action).Where(b=>b.Kind.EndsWith("Axis")).ToArray() : [];
            deadzone.Enabled=sensitivity.Enabled=axes.Length>0;
            reverse.Enabled=action is {Axis:true} && Assigned(action).Any();
            tuningLabel.Text=action is {Axis:true} ? action.Names[0]=="Horizontal" ? "Steering tuning" : "Vertical tuning" : "Select a flight control to tune";
            if(axes.Length>0) {
                deadzone.Value=(decimal)axes[0].Deadzone;
                sensitivity.Maximum=Math.Max(1000,(decimal)Math.Min(axes[0].Sensitivity,(float)decimal.MaxValue/2));
                sensitivity.Value=Math.Min(sensitivity.Maximum,(decimal)Math.Min(axes[0].Sensitivity,(float)decimal.MaxValue/2));
                if(axes.Select(b=>(b.Deadzone,b.Sensitivity)).Distinct().Count()>1) tuningLabel.Text+=" (mixed)";
            }
        } finally { updatingTuning=false; }
    }
    private void Tune(bool zone)
    {
        if(updatingTuning || Selected is not {Axis:true} action)return;
        foreach(var b in Assigned(action).Where(b=>b.Kind.EndsWith("Axis"))) {
            if(zone)b.Deadzone=(float)deadzone.Value; else b.Sensitivity=(float)sensitivity.Value;
        }
    }
    private void Replace(ActionChoice action, List<ControlBinding> values)
    {
        if(action.Exit) { exitBinding=values.FirstOrDefault(); return; }
        foreach(var a in configuration.Actions.Where(a=>action.Names.Contains(a.Name))) a.Bindings.Clear();
        var target=configuration.Actions.FirstOrDefault(a=>a.Name==action.Names[0]);
        if(target is null) { target=new ControlAction { Name=action.Names[0], ValueType=action.Axis?"analog":"digital" }; configuration.Actions.Add(target); }
        target.Bindings.AddRange(values);
        if(action.Names.Contains("Boost"))configuration.LinkTriggerAndBoost=true;
    }
    private void Assign(ActionChoice action)
    {
        PhysicalInputManager? preview=null;
        try {
            var broker=active??(preview=new PhysicalInputManager(installation,log,Read()));
            bool horizontal=action.Names.FirstOrDefault()=="Horizontal";
            string direction=horizontal ? "Left" : "Up / Dive";
            ControlBinding first;
            float capturedValue;
            using(var capture=new BindingCaptureForm(broker,action.Exit,action.Label,
                action.Axis ? "Move the axis fully "+direction+", or press the key/button for "+direction+"." : null)) {
                if(capture.ShowDialog(this)!=DialogResult.OK || capture.Captured is null)return;
                first=capture.Captured; capturedValue=capture.CapturedValue;
            }
            var values=new List<ControlBinding> {first};
            if(action.Axis) {
                if(first.Kind.EndsWith("Axis")) first.Invert=capturedValue>0;
                else {
                    using var second=new BindingCaptureForm(broker,true,action.Label,"Press the key/button for "+(horizontal?"Right":"Down / Climb")+".");
                    if(second.ShowDialog(this)!=DialogResult.OK || second.Captured is not {} positive)return;
                    if(first.Kind==positive.Kind && first.Device==positive.Device && first.Control==positive.Control) {
                        MessageBox.Show(this,"Choose different keys or buttons for opposite directions. The previous assignment is retained.","Direction buttons"); return;
                    }
                    first.Invert=true; values.Add(positive);
                }
            } else if(first.Kind.EndsWith("Axis")) first.Invert=capturedValue<0;
            Replace(action,values); RefreshRow(action); RefreshTuning();
            log.Write("Control assignment captured: "+action.Label+" = "+Summary(action)+" (not saved yet).");
        } catch(Exception error) { MessageBox.Show(this,error.Message,"Cannot capture binding"); }
        finally { preview?.Dispose(); }
    }
    private ControlsConfiguration Read()
    {
        var result=JsonSerializer.Deserialize<ControlsConfiguration>(JsonSerializer.Serialize(configuration))!;
        result.ExitBinding=exitBinding; result.ConfirmExit=confirmExit.Checked; result.UnityInputEnabled=enabled.Checked; result.AppliedToGame=enabled.Checked; return result;
    }
    private void Save()
    {
        try { ControlsStore.Save(installation,Read()); log.Write("Controls saved; hidden/custom actions and untouched bindings preserved."); MessageBox.Show(this,"Controls saved for the next launch.","Controls"); }
        catch(Exception e) { MessageBox.Show(this,e.Message,"Cannot save controls"); }
    }
    private void Test()
    {
        try { using var form=new ControlTestForm(installation,log,active,Read()); form.ShowDialog(this); }
        catch(Exception e) { MessageBox.Show(this,e.Message,"Input test"); }
    }
}
