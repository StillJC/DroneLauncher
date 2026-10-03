using System.Runtime.InteropServices;
using System.Text.Json;

namespace DroneRacingGenesisLoader;

internal static class FeatureSettings
{
    internal static T Load<T>(Installation i,string name) where T:new()
    {string path=Path.Combine(i.ConfigRoot,name);return File.Exists(path)?JsonSerializer.Deserialize<T>(File.ReadAllText(path))??throw new InvalidDataException(name+" is empty."):new();}
    internal static void Save<T>(Installation i,string name,T value)
    {
        string path=Path.Combine(i.ConfigRoot,name);if(File.Exists(path))File.Copy(path,path+".previous",true);
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));File.Move(path+".tmp",path,true);
    }
    private static Form Dialog(string title,out TableLayoutPanel table)
    {
        var form=new Form{Text=title+" — Drone Launcher",ClientSize=new Size(640,485),MinimumSize=new Size(600,450)};LauncherStyle.Apply(form);
        table=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=2,AutoScroll=true};table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,37));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,63));form.Controls.Add(table);return form;
    }
    private static void Row(TableLayoutPanel table,string label,Control control)
    {
        int n=table.RowCount++;table.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        table.Controls.Add(new Label{Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,n);control.Dock=DockStyle.Fill;control.Margin=new Padding(4,8,4,8);table.Controls.Add(control,1,n);
    }
    private static void Note(TableLayoutPanel table,string text,int height=84)
    {int n=table.RowCount++;table.RowStyles.Add(new RowStyle(SizeType.Absolute,height));var label=new Label{Text=text,Dock=DockStyle.Fill,Padding=new Padding(4,10,4,0)};table.Controls.Add(label,0,n);table.SetColumnSpan(label,2);}
    private static ComboBox Choice(IEnumerable<string> values,int index=0)
    {var box=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,ForeColor=Color.Black};box.Items.AddRange(values.Cast<object>().ToArray());box.SelectedIndex=Math.Clamp(index,0,box.Items.Count-1);return box;}
    internal static void Outputs(Form owner,Installation i,bool running)
    {
        using var form=Dialog("Outputs",out var table);var config=Load<OutputConfiguration>(i,"outputs.json");
        var rumble=new CheckBox{Text="Enable controller rumble",Checked=config.RumbleEnabled};Row(table,"Controller rumble",rumble);
        var connected=PhysicalInputManager.ConnectedDevices();
        var slot=Choice(Enumerable.Range(0,4).Select(n=>"XInput Controller "+(n+1)+(connected.Contains("XInput "+n)?" - connected":" - disconnected")),config.XInputSlot);Row(table,"Rumble device",slot);
        var strength=new NumericUpDown{Minimum=0,Maximum=100,Value=Math.Clamp(config.Strength,0,100)};Row(table,"Strength (%)",strength);
        var tcp=new CheckBox{Text="Enable TCP output",Checked=config.TcpEnabled};Row(table,"TCP",tcp);
        var host=new TextBox{Text=config.Host};Row(table,"Destination host",host);
        var port=new NumericUpDown{Minimum=1,Maximum=65535,Value=Math.Clamp(config.Port,1,65535)};Row(table,"Destination port",port);
        Note(table,"Rumble follows original cabinet vibration. TCP sends verified vibration, billboard and RGB states. External hardware is optional. Changes apply next launch.");
        var save=LauncherStyle.Button("Save",(_,_)=>{try{config.RumbleEnabled=rumble.Checked;config.XInputSlot=slot.SelectedIndex;config.Strength=(int)strength.Value;config.TcpEnabled=tcp.Checked;config.Host=host.Text.Trim();config.Port=(int)port.Value;config.Validate();Save(i,"outputs.json",config);form.Close();}catch(Exception e){MessageBox.Show(form,e.Message,"Cannot save outputs");}});Row(table,"",save);
        form.ShowDialog(owner);
    }
    internal static void Network(Form owner,Installation i,bool running)
    {
        using var form=Dialog("Network / LAN",out var table);var config=Load<NetworkConfiguration>(i,"network.json");
        var mode=Choice(["Standalone","LAN / Linked Cabinets"],config.Mode is "LAN" or "Original"?1:0);Row(table,"Network mode",mode);
        string ini=Path.Combine(i.ShellDataRoot,"ShellData.ini");
        int Read(string key,int fallback)=>(int)GetPrivateProfileInt("Network",key,fallback,ini);
        var id=new NumericUpDown{Minimum=1,Maximum=4,Value=Math.Clamp(config.ApplyOnNextLaunch?config.CabinetID:Read("CabinetID",1),1,4)};
        var count=new NumericUpDown{Minimum=2,Maximum=4,Value=Math.Clamp(config.ApplyOnNextLaunch?config.NumCabinets:Read("NumCabinets",2),2,4)};
        Row(table,"Cabinet ID",id);Row(table,"Number of cabinets",count);
        void Enable(){id.Enabled=count.Enabled=mode.SelectedIndex==1;}mode.SelectedIndexChanged+=(_,_)=>Enable();Enable();
        Note(table,"LAN uses the original cabinet-link system. Each cabinet needs a unique ID. After applying these values once, original operator network changes are preserved. Standalone remains the default.",110);
        Note(table,"Two-cabinet gameplay has not been verified here. Windows may request network access; Drone Launcher does not change Windows Firewall.",84);
        var save=LauncherStyle.Button("Save",(_,_)=>{try{if(id.Value>count.Value&&mode.SelectedIndex==1)throw new InvalidDataException("Cabinet ID must be within the cabinet count.");Save(i,"network.json",new NetworkConfiguration{Mode=mode.SelectedIndex==0?"Standalone":"LAN",CabinetID=(int)id.Value,NumCabinets=(int)count.Value,ApplyOnNextLaunch=mode.SelectedIndex==1});form.Close();}catch(Exception e){MessageBox.Show(form,e.Message,"Cannot save network settings");}});save.Enabled=!running;Row(table,running?"Close the game to change network settings.":"",save);
        form.ShowDialog(owner);
    }
    internal static void Display(Form owner,Installation i,bool running)
    {
        using var form=Dialog("Display / Graphics",out var table);var config=Load<GraphicsConfiguration>(i,"graphics.json");
        var enabled=new CheckBox{Text="Use custom display settings",Checked=config.Enabled};Row(table,"Display settings",enabled);
        var screens=Screen.AllScreens;var monitor=Choice(screens.Select(s=>$"{s.DeviceName.Replace("\\\\.\\","")} — {s.Bounds.Width}×{s.Bounds.Height}"+(s.Primary?" — Primary":"")),Math.Max(0,Array.FindIndex(screens,s=>s.DeviceName==config.Monitor)));Row(table,"Monitor",monitor);
        var resolution=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};Row(table,"Resolution",resolution);
        void Fill(){resolution.Items.Clear();foreach(var m in DisplayManager.Modes(screens[monitor.SelectedIndex].DeviceName))resolution.Items.Add($"{m.Width} × {m.Height}");int n=resolution.Items.IndexOf($"{config.Width} × {config.Height}");resolution.SelectedIndex=n>=0?n:resolution.Items.Count-1;}
        monitor.SelectedIndexChanged+=(_,_)=>Fill();Fill();
        var mode=Choice(["Borderless","Windowed"],Array.IndexOf(new[]{"Borderless","Windowed"},config.Mode));Row(table,"Display mode",mode);
        var quality=Choice(new[]{"Original game default"}.Concat(DisplayManager.QualityNames),config.Quality+1);Row(table,"Quality",quality);
        Note(table,"Changes apply next launch. Missing monitors fall back to the primary display.");
        var save=LauncherStyle.Button("Save",(_,_)=>{try{var parts=resolution.Text.Split('×');var value=new GraphicsConfiguration{Enabled=enabled.Checked,Monitor=screens[monitor.SelectedIndex].DeviceName,Width=int.Parse(parts[0].Trim()),Height=int.Parse(parts[1].Trim()),Mode=mode.Text,Quality=quality.SelectedIndex-1};value.Validate();Save(i,"graphics.json",value);form.Close();}catch(Exception e){MessageBox.Show(form,e.Message,"Cannot save display settings");}});Row(table,"",save);form.ShowDialog(owner);
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,EntryPoint="GetPrivateProfileIntW")]private static extern uint GetPrivateProfileInt(string section,string key,int fallback,string file);
}
internal sealed class NetworkConfiguration
{
    public string Mode{get;set;}="Standalone";
    public int CabinetID{get;set;}=1;
    public int NumCabinets{get;set;}=2;
    public bool ApplyOnNextLaunch{get;set;}
}
