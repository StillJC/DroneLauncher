using System.Text;
using System.Text.Json;
using DroneRacingGenesisLoader;
using DroneRacingGenesis.Plugin;

var testsDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
var fixture = Path.Combine(testsDirectory, "fixture " + Guid.NewGuid().ToString("N"));
var priorRoot = Environment.GetEnvironmentVariable("DRG_CONTENT_ROOT");
Environment.SetEnvironmentVariable("DRG_CONTENT_ROOT", null);
if (!fixture.StartsWith(testsDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Test fixture escaped the test directory.");

try
{
    var direct = Path.Combine(fixture, "direct layout with spaces");
    var nested = Path.Combine(fixture, "nested layout with spaces");
    Directory.CreateDirectory(Path.Combine(direct, "Shell"));
    Directory.CreateDirectory(Path.Combine(nested, "Sega", "Shell"));
    File.WriteAllText(Path.Combine(direct, "Shell", "Shell.exe"), "test stub");
    File.WriteAllText(Path.Combine(nested, "Sega", "Shell", "Shell.exe"), "test stub");
    Assert(Installation.Resolve(direct).ContentRoot == direct, "direct content root");
    Assert(Installation.Resolve(nested).ContentRoot == Path.Combine(nested, "Sega"), "nested content root");

    var installation = Installation.Resolve(direct);
    var required = new[]
    {
        @"Shell\dk2win32.dll", @"Shell\config.ini", @"ShellData\ShellData.ini", @"ShellData\GameSettings.ini",
        @"GameData\system.bin", @"GameData\score.bin", @"DroneRacing\DroneRacing.exe",
        @"DroneRacing\UnityPlayer.dll", @"DroneRacing\GameAssembly.dll"
    };
    foreach (var name in required)
    {
        var path = Path.Combine(direct, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test stub");
    }
    Directory.CreateDirectory(Path.Combine(direct, "DroneRacing", "DroneRacing_Data"));
    const string master = "[Variables]\r\nDirectory=.\\variables\r\nHiScoreName1=c:\\sega\\GameData\\score.bin\r\n" +
        "[Game]\r\nAssetDir=C:\\sega\\shelldata\r\n" +
        "[Shelldata]\r\nTournamentDir=C:\\Sega\\ShellData\r\nDirectory=c:\\Sega\\ShellData\r\n[Debug]\r\nEnabled=1\r\n";
    File.WriteAllText(Path.Combine(direct, "Shell", "Game.ini"), master, Encoding.Latin1);
    Assert(installation.MissingComponents().Count == 0, "complete preflight");
    using var log = new LoaderLog(installation.LogsRoot);
    RuntimeConfiguration.Generate(installation, log);
    var generated = File.ReadAllText(Path.Combine(direct, "Shell", "Game.ini"), Encoding.Latin1);
    Assert(generated.Contains("HiScoreName1=" + Path.Combine(direct, "GameData", "score.bin")), "score relocation");
    Assert(generated.Contains("AssetDir=" + Path.Combine(direct, "ShellData")), "asset relocation");
    Assert(generated.Contains("Directory=.\\variables"), "variables preserved");
    Assert(generated.Contains("GameDataDir=" + installation.GameDataRoot), "missing GameDataDir inserted");
    Assert(generated.Contains("SecurityDisabled=1"), "security fallback enabled through plugin library");
    var disabled = ShellSecurityWorkaround.Configure(generated, false);
    Assert(disabled.Contains("SecurityDisabled=0") && !disabled.Contains("SecurityDisabled=1"), "security workaround reversible");
    Assert(ShellSecurityWorkaround.Configure(disabled, false) == disabled, "security workaround idempotent");
    RuntimeConfiguration.Generate(installation, log);
    Assert(File.ReadAllText(Path.Combine(direct, "Shell", "Game.ini"), Encoding.Latin1) == generated, "configuration generation idempotent");
    Assert(File.ReadAllText(Path.Combine(installation.ConfigRoot, "Original", "Shell.Game.ini"), Encoding.Latin1) == master, "master preserved");
    var networkIni = Path.Combine(installation.ShellDataRoot, "ShellData.ini");
    const string cabinetMaster = "[Network]\r\nLinkPlay=1\r\nEnabled=1\r\nCabinetID=2\r\nNumCabinets=2\r\n[Joystick]\r\nXCentre=1604\r\n[Audio]\r\nMusicVolume=20\r\n";
    File.WriteAllText(networkIni, cabinetMaster, Encoding.Latin1);
    var networkConfiguration = Path.Combine(installation.ConfigRoot, "network.json");
    File.WriteAllText(networkConfiguration, "{\"Mode\":\"Standalone\"}");
    RuntimeConfiguration.GenerateNetwork(installation, log);
    var standalone = File.ReadAllText(networkIni, Encoding.Latin1);
    Assert(standalone.Contains("LinkPlay=0\r\nEnabled=1\r\nCabinetID=1\r\nNumCabinets=1"), "original standalone operator settings");
    Assert(File.ReadAllText(Path.Combine(installation.ConfigRoot, "Original", "ShellData.network-master.ini"), Encoding.Latin1) == cabinetMaster, "network master byte preservation");
    // A later operator calibration edit must survive both regeneration and rollback.
    File.WriteAllText(networkIni, standalone.Replace("XCentre=1604", "XCentre=1700").Replace("MusicVolume=20", "MusicVolume=100"), Encoding.Latin1);
    RuntimeConfiguration.GenerateNetwork(installation, log);
    File.WriteAllText(networkConfiguration, "{\"Mode\":\"Original\"}");
    RuntimeConfiguration.GenerateNetwork(installation, log);
    Assert(File.ReadAllText(networkIni, Encoding.Latin1) == cabinetMaster.Replace("XCentre=1604", "XCentre=1700").Replace("MusicVolume=20", "MusicVolume=100"), "network rollback preserves later operator calibration and audio edits");
    var mapper = new PortablePathMap(Path.Combine(direct, "DroneRacing", "DroneRacing.exe"));
    Assert(mapper.Translate("C:/Sega/ShellData/ShellData.ini") == Path.Combine(direct, "ShellData", "ShellData.ini"), "ShellData mapping");
    Assert(mapper.Translate("C:/Sega/ShellData/GameSettings.ini") == Path.Combine(direct, "ShellData", "GameSettings.ini"), "GameSettings mapping");
    Assert(mapper.Translate("C:/Sega/GameData//score.bin") == Path.Combine(direct, "GameData", "score.bin"), "GameData mapping");
    Assert(mapper.Translate("C:/other/file") == "C:/other/file", "unrelated path unchanged");
    Environment.SetEnvironmentVariable("DRG_CONTENT_ROOT", Path.Combine(nested, "Sega"));
    Assert(new PortablePathMap(Path.Combine(direct, "DroneRacing", "DroneRacing.exe")).ContentRoot == Path.Combine(nested, "Sega"), "environment root preferred");
    Environment.SetEnvironmentVariable("DRG_CONTENT_ROOT", "relative-root");
    Assert(new PortablePathMap(Path.Combine(direct, "DroneRacing", "DroneRacing.exe")).ContentRoot == direct, "invalid environment falls back to executable");
    var controlsPath = Path.Combine(installation.ConfigRoot, "controls.json");
    const string legacyControls = "{\"Version\":1,\"Actions\":[{\"Name\":\"Boost\",\"Bindings\":[{\"Kind\":\"keyboardKey\",\"Control\":\"B\"}]},{\"Name\":\"Custom\",\"Bindings\":[{\"Kind\":\"joystickButton\",\"Control\":\"7\"}]}]}";
    File.WriteAllText(controlsPath, legacyControls);
    RuntimeConfiguration.EnsureControls(installation);
    var controls = JsonSerializer.Deserialize<ControlsConfiguration>(File.ReadAllText(controlsPath))!;
    Assert(controls.Version == 2 && !controls.AppliedToGame, "controls v2 explicitly inactive");
    Assert(controls.Actions.Single(a => a.Name == "Boost").Bindings.Single().Control == "B", "existing binding preserved");
    Assert(controls.Actions.Any(a => a.Name == "Custom"), "custom action preserved");
    Assert(controls.Actions.Single(a => a.Name == "Throttle").ValueType == "analog", "real analog action created");
    Assert(File.ReadAllText(Path.Combine(installation.ConfigRoot, "Original", "controls.v1.json")) == legacyControls, "old controls backed up");
    var migrated = File.ReadAllText(controlsPath);
    RuntimeConfiguration.EnsureControls(installation);
    Assert(File.ReadAllText(controlsPath) == migrated, "v2 settings preserved");
    var workShell = Path.GetFullPath(Path.Combine(testsDirectory, "..", "..", "..", "Runtime", "Shell", "Shell.exe"));
    if (File.Exists(workShell))
        Assert(ShellStartupArguments.ForExecutable(workShell) == "/k=186CD76A", "checksum matches independently observed native Shell result");
    var rejected = false;
    try { ShellStartupArguments.ForExecutable(Path.Combine(direct, "Shell", "Shell.exe")); }
    catch (InvalidDataException) { rejected = true; }
    Assert(rejected, "unrecognized Shell cannot use build-specific checksum contract");
    Assert(PhysicalInputManager.CabinetAxis(-1, 0, 1604, 3703) == 0, "negative axis endpoint");
    Assert(PhysicalInputManager.CabinetAxis(0, 0, 1604, 3703) == 1604, "asymmetric axis center");
    Assert(PhysicalInputManager.CabinetAxis(1, 0, 1604, 3703) == 3703, "positive axis endpoint");
    Assert(PhysicalInputManager.CabinetAxis(2, 8, 1755, 3497) == 3497, "axis clamp");
    var binding = new ControlBinding { Deadzone = .2f, Invert = true };
    Assert(PhysicalInputManager.Apply(.1f, binding) == 0, "deadzone neutral");
    Assert(Math.Abs(PhysicalInputManager.Apply(.6f, binding) + .5f) < .0001f, "deadzone rescale and inversion");
    Assert(PhysicalInputManager.Apply(-1, binding) == 1, "inverted endpoint");
    var errorPath = Path.Combine(installation.LogsRoot, "native-launch-error.json");
    File.WriteAllText(errorPath, "{\"ShellPid\":123,\"Error\":\"expected guard rejection\"}");
    var errorReader = typeof(ProcessMonitor).GetMethod("NativeLaunchError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    var launchTime = DateTime.UtcNow.AddMinutes(-1);
    File.SetLastWriteTimeUtc(errorPath, launchTime.AddMinutes(-1));
    Assert(errorReader.Invoke(null, [installation, 123, launchTime]) is null, "old error with reused PID ignored");
    File.SetLastWriteTimeUtc(errorPath, DateTime.UtcNow);
    Assert((string?)errorReader.Invoke(null, [installation, 123, launchTime]) == "expected guard rejection", "current matching native failure surfaced");
    Assert(errorReader.Invoke(null, [installation, 124, launchTime]) is null, "other Shell failure ignored");
    Diagnostics.Update(installation, "before lock");
    var statusPath = Path.Combine(installation.LogsRoot, "diagnostics.json");
    using (var held = File.Open(statusPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        Diagnostics.Update(installation, "while reader denies replacement");
    Diagnostics.Update(installation, "after lock");
    Assert(File.ReadAllText(statusPath).Contains("after lock"), "diagnostic sharing failure is nonfatal and recovers");
    File.WriteAllText(Path.Combine(installation.ConfigRoot, "unity-portability.json"), "{\"Enabled\":true}");
    rejected = false;
    try { UnityPortability.Configure(installation, new System.Diagnostics.ProcessStartInfo(), log); }
    catch (InvalidDataException) { rejected = true; }
    Assert(rejected, "unsupported GameAssembly rejected before process launch");
    File.WriteAllText(networkConfiguration,"{\"Mode\":\"LAN\",\"CabinetID\":2,\"NumCabinets\":4,\"ApplyOnNextLaunch\":true}");
    RuntimeConfiguration.GenerateNetwork(installation,log);
    Assert(File.ReadAllText(networkIni).Contains("NumCabinets=4"),"LAN activation applies original fields");
    File.WriteAllText(networkIni,File.ReadAllText(networkIni).Replace("CabinetID=2","CabinetID=3"));
    RuntimeConfiguration.GenerateNetwork(installation,log);
    Assert(File.ReadAllText(networkIni).Contains("CabinetID=3"),"LAN subsequent operator edits survive relaunch");
    rejected=false;try{PhysicalInputManager.ValidateExit(new ControlBinding{Kind="xinputAxis",Device="0",Control="LeftX"});}catch(InvalidDataException){rejected=true;}
    Assert(rejected,"exit rejects axes");
    PhysicalInputManager.ValidateExit(new ControlBinding{Kind="xinputButton",Device="0",Control="RightThumb"});
    File.WriteAllText(Path.Combine(installation.ConfigRoot,"graphics.json"),"{\"Enabled\":true,\"Width\":-1}");
    var startInfo=new System.Diagnostics.ProcessStartInfo();startInfo.Environment["DRG_GRAPHICS"]="1";
    var display=new DisplayManager(installation,startInfo,log);
    Assert(display.Warning is not null&&!startInfo.Environment.ContainsKey("DRG_GRAPHICS"),"invalid graphics falls back and clears inherited override");
    var primary=System.Windows.Forms.Screen.PrimaryScreen!;
    File.WriteAllText(Path.Combine(installation.ConfigRoot,"graphics.json"),JsonSerializer.Serialize(new GraphicsConfiguration{Enabled=true,Monitor="missing-monitor-for-acceptance",Width=primary.Bounds.Width,Height=primary.Bounds.Height,Mode="Windowed"}));
    startInfo=new System.Diagnostics.ProcessStartInfo();display=new DisplayManager(installation,startInfo,log);
    Assert(display.Warning?.StartsWith("Selected monitor unavailable")==true&&startInfo.Environment.ContainsKey("DRG_GRAPHICS"),"missing display selects primary with nonfatal warning");
    rejected=false;try{PhysicalInputManager.Validate(new ControlBinding{Kind="xinputButton",Device="7",Control="A"});}catch(InvalidDataException){rejected=true;}
    Assert(rejected,"invalid controller index rejected before sampling or process launch");
    var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();
    try
    {
        int port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        File.WriteAllText(Path.Combine(installation.ConfigRoot,"outputs.json"),JsonSerializer.Serialize(new OutputConfiguration{TcpEnabled=true,Port=port}));
        using var outputs=new OutputManager(installation,log){ShellPid=123};
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var receiver=await listener.AcceptTcpClientAsync(timeout.Token);
        using var reader=new StreamReader(receiver.GetStream());
        using var outputMap=System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(outputs.MappingName);
        using var writer=outputMap.CreateViewAccessor();writer.Write(4,123u);writer.Write(12,unchecked((uint)Environment.TickCount));
        writer.Write(40,0x2000u);writer.Write(32,1u);writer.Write(8,1u);
        bool on=false;while(!on){string line=await reader.ReadLineAsync(timeout.Token)??throw new Exception("Output stream closed");using var message=JsonDocument.Parse(line);on=message.RootElement.GetProperty("output").GetString()=="vibration"&&message.RootElement.GetProperty("value").GetInt32()==1;}
        Assert(on,"original level ring event reaches TCP logical vibration");
        outputs.SessionClosing();
        bool off=false;while(!off){string line=await reader.ReadLineAsync(timeout.Token)??throw new Exception("Output stream closed");using var message=JsonDocument.Parse(line);off=message.RootElement.GetProperty("output").GetString()=="vibration"&&message.RootElement.GetProperty("value").GetInt32()==0;}
        Assert(off,"session shutdown resets logical vibration");
    }
    finally{listener.Stop();}
    PhysicalInputManager.Validate(new ControlBinding{Kind="joystickButton",Device="1",Control="PovLeft"});
    Assert(PhysicalInputManager.PovValue(0xffff,"PovUp")==0 && PhysicalInputManager.PovValue(uint.MaxValue,"PovUp")==0,"centered joystick hat releases");
    Assert(PhysicalInputManager.PovValue(31500,"PovUp")==1 && PhysicalInputManager.PovValue(31500,"PovLeft")==1 && PhysicalInputManager.PovValue(31500,"PovRight")==0,"joystick hat diagonal and wraparound");
    Assert(PhysicalInputManager.PovValue(18000,"PovDown")==1 && PhysicalInputManager.PovValue(18000,"PovUp")==0,"joystick hat opposite direction");
    Exception? editorFailure=null;
    var editorThread=new Thread(()=>{
        try {
            var original=ControlsStore.Load(installation);
            foreach(var binding in original.Actions.SelectMany(a=>a.Bindings).Where(b=>b.Kind!="keyboardKey"&&b.Device==""))binding.Device="any";
            original.Actions.Add(new ControlAction{Name="Custom future action",Bindings=[new ControlBinding{Kind="keyboardKey",Control="F8"}]});
            original.Actions.Add(new ControlAction{Name="Trigger",Bindings=[new ControlBinding{Kind="joystickButton",Device="1",Control="3"}]});
            ControlsStore.Save(installation,original);
            using var editor=new ControlsForm(installation,log,null);
            var read=typeof(ControlsForm).GetMethod("Read",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            var restored=(ControlsConfiguration)read.Invoke(editor,null)!;
            Assert(JsonSerializer.Serialize(original)==JsonSerializer.Serialize(restored),"editor preserves aliases, custom/hidden actions and all existing bindings");
        }catch(Exception error){editorFailure=error;}
    });
    editorThread.SetApartmentState(ApartmentState.STA);editorThread.Start();editorThread.Join();
    if(editorFailure is not null)throw new Exception("Controls editor preservation failed",editorFailure);
    Console.WriteLine("PASS: controls editor preserves custom/hidden/alias bindings without changes");
    Console.WriteLine("PASS: LAN operator preservation, single-button exit validation, invalid graphics fallback, bounded native-output-ring to TCP and session reset");
    Console.WriteLine("PASS: roots, spaces, preflight, preservation, mapper, controls, checksum/build rejection, analog transforms, stale-PID error isolation, diagnostics sharing recovery");
}
finally
{
    Environment.SetEnvironmentVariable("DRG_CONTENT_ROOT", priorRoot);
    if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
}

static void Assert(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
}
