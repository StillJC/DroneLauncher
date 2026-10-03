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
        var tcp=new CheckBox{Text="Enable TCP output server",Checked=config.TcpEnabled};Row(table,"TCP",tcp);
        var port=new NumericUpDown{Minimum=1,Maximum=65535,Value=Math.Clamp(config.Port,1,65535)};Row(table,"Listen port",port);
        Note(table,"Rumble follows original cabinet vibration. The TCP server listens on localhost and provides vibration, billboard and RGB output states to external software. Changes apply next launch.");
        var buttons=new FlowLayoutPanel{FlowDirection=FlowDirection.RightToLeft,Dock=DockStyle.Fill,AutoSize=true};

        var cancel=LauncherStyle.Button("Cancel",(_,_)=>form.Close());
        cancel.AutoSize=true;
        cancel.Dock=DockStyle.None;

        var save=LauncherStyle.Button("Save",(_,_)=>{try{config.RumbleEnabled=rumble.Checked;config.XInputSlot=slot.SelectedIndex;config.Strength=(int)strength.Value;config.TcpEnabled=tcp.Checked;config.Host="127.0.0.1";config.Port=(int)port.Value;config.Validate();Save(i,"outputs.json",config);form.Close();}catch(Exception e){MessageBox.Show(form,e.Message,"Cannot save outputs");}});
        save.AutoSize=true;
        save.Dock=DockStyle.None;

        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        int actionRow=table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        table.Controls.Add(buttons,0,actionRow);
        table.SetColumnSpan(buttons,2);
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
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,EntryPoint="GetPrivateProfileIntW")]private static extern uint GetPrivateProfileInt(string section,string key,int fallback,string file);
}
internal sealed class NetworkConfiguration
{
    public string Mode{get;set;}="Standalone";
    public int CabinetID{get;set;}=1;
    public int NumCabinets{get;set;}=2;
    public bool ApplyOnNextLaunch{get;set;}
}
