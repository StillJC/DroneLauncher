using System.Diagnostics;
using System.Drawing;

namespace DroneRacingGenesisLoader;

internal sealed class LoaderForm : Form
{
    private readonly Installation installation;
    private readonly LoaderLog log;
    private readonly Button launchButton;
    private readonly Label statusLabel;
    private bool running;
    private CancellationTokenSource? shutdown;
    private bool closeAfterShutdown;
    private PhysicalInputManager? inputBroker;
    private readonly HashSet<Keys> capturedKeys = [];
    private readonly System.Windows.Forms.Timer uiTimer = new() { Interval=75 };
    private PhysicalInputManager? idleBroker;
    private PhysicalInputManager? observedBroker;
    private int observedExit;
    private bool exitDialogOpen;
    private PhysicalInputManager? CurrentBroker => inputBroker ?? idleBroker;

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        // The loader can be the only visible window during cabinet startup.
        // Bound game keys must not also activate its configuration/exit buttons.
        if (running && capturedKeys.Contains(keyData & Keys.KeyCode)) return true;
        return base.ProcessCmdKey(ref message, keyData);
    }

    public LoaderForm(Installation installation, LoaderLog log, IReadOnlyList<string> missing)
    {
        this.installation = installation;
        this.log = log;
        Text = "Drone Launcher";
        LauncherStyle.Apply(this);ClientSize=new Size(600,640);MinimumSize=new Size(540,640);StartPosition=FormStartPosition.CenterScreen;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=2,RowCount=10};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        foreach(int height in new[]{155,58,30,52,30,52,52,52,65,48})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        var logo=new PictureBox{Image=LauncherStyle.Logo(),SizeMode=PictureBoxSizeMode.Zoom,Dock=DockStyle.Fill,Margin=new Padding(10)};
        layout.Controls.Add(logo,0,0);layout.SetColumnSpan(logo,2);
        launchButton=LauncherStyle.Button("LAUNCH GAME",async (_,_)=>await LaunchAsync());launchButton.BackColor=LauncherStyle.Accent;launchButton.ForeColor=Color.FromArgb(10,25,36);launchButton.Font=new Font(Font,FontStyle.Bold);launchButton.Enabled=missing.Count==0;
        layout.Controls.Add(launchButton,0,1);layout.SetColumnSpan(launchButton,2);
        void Heading(string text,int row){var label=new Label{Text=text,Dock=DockStyle.Fill,Padding=new Padding(6,8,0,0),ForeColor=Color.Silver,Font=new Font(Font.FontFamily,9,FontStyle.Bold)};layout.Controls.Add(label,0,row);layout.SetColumnSpan(label,2);}
        Heading("CONTROLS",2);
        layout.Controls.Add(LauncherStyle.Button("Configure Controls",(_,_)=>OpenConfiguration()),0,3);
        layout.Controls.Add(LauncherStyle.Button("Test Controls",(_,_)=>TestControls()),1,3);
        Heading("SYSTEM",4);
        layout.Controls.Add(LauncherStyle.Button("Operator / Test Menu",(_,_)=>RequestOperator()),0,5);
        layout.Controls.Add(LauncherStyle.Button("Audio Settings",(_,_)=>{MessageBox.Show(this,"Open SOUND SETTINGS in the original operator menu. Service chooses; Test selects. Your audio settings are retained.","Audio Settings");RequestOperator();}),1,5);
        layout.Controls.Add(LauncherStyle.Button("Display / Graphics",(_,_)=>ShowFeatureStatus("Display / Graphics")),0,6);
        layout.Controls.Add(LauncherStyle.Button("Network / LAN",(_,_)=>ShowFeatureStatus("Network / LAN")),1,6);
        layout.Controls.Add(LauncherStyle.Button("Outputs",(_,_)=>ShowFeatureStatus("Outputs")),0,7);
        layout.Controls.Add(LauncherStyle.Button("Diagnostics",(_,_)=>ShowDiagnostics()),1,7);
        statusLabel=new Label{Text=missing.Count==0?"Ready":"Runtime incomplete",Dock=DockStyle.Fill,Padding=new Padding(6,18,0,0),Font=new Font(Font,FontStyle.Bold)};
        layout.Controls.Add(statusLabel,0,8);layout.SetColumnSpan(statusLabel,2);
        layout.Controls.Add(LauncherStyle.Button("Open Logs",(_,_)=>Process.Start(new ProcessStartInfo(installation.LogsRoot){UseShellExecute=true})),0,9);
        layout.Controls.Add(LauncherStyle.Button("Exit",(_,_)=>RequestExit()),1,9);
        Controls.Add(layout);
        uiTimer.Tick+=(_,_)=>CheckExit();uiTimer.Start();
        Shown+=(_,_)=>ResetIdleBroker();
        FormClosed+=(_,_)=>{uiTimer.Dispose();idleBroker?.Dispose();logo.Image?.Dispose();};
        FormClosing += (_, e) =>
        {
            if (!running) return;
            e.Cancel = true;
            closeAfterShutdown = true;
            statusLabel.Text = "Closing game and Shell...";
            shutdown?.Cancel();
        };

        if (missing.Count > 0)
        {
            log.Write("Preflight failed: " + string.Join(", ", missing));
            Shown += (_, _) => MessageBox.Show(this,
                "Drone Racing runtime is incomplete.\nMissing:\n- " + string.Join("\n- ", missing),
                "Missing files", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else log.Write("Preflight passed.");
    }

    private async Task LaunchAsync()
    {
        if (running) return;
        var missing = installation.MissingComponents();
        if (missing.Count > 0)
        {
            launchButton.Enabled = false;
            statusLabel.Text = "Runtime incomplete";
            MessageBox.Show(this, "Missing:\n- " + string.Join("\n- ", missing), "Missing files", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        idleBroker?.Dispose();idleBroker=null;
        shutdown = new CancellationTokenSource();
        running = true;
        launchButton.Enabled = false;
        statusLabel.Text = "Preparing runtime configuration...";
        try
        {
            RuntimeConfiguration.Generate(installation, log);
            capturedKeys.Clear();
            if (IOCompatibility.Enabled(installation))
            {
                var controls = System.Text.Json.JsonSerializer.Deserialize<ControlsConfiguration>(File.ReadAllText(RuntimeConfiguration.EnsureControls(installation)));
                foreach (var binding in controls!.Actions.SelectMany(action => action.Bindings).Where(binding => binding.Kind == "keyboardKey"))
                    if (Enum.TryParse<Keys>(binding.Control, true, out var key)) capturedKeys.Add(key);
            }
            await ProcessMonitor.LaunchAndMonitorAsync(installation, log, message =>
            {
                if (!IsDisposed) BeginInvoke(() => statusLabel.Text = FriendlyStatus(message));
            }, broker => inputBroker = broker, shutdown.Token);
        }
        catch (Exception exception)
        {
            log.Error(exception);
            statusLabel.Text = "Launch failed";
            Diagnostics.Update(installation,"Launch failed: "+exception.Message);
            MessageBox.Show(this, exception.Message + "\n\nDetails: " + log.FilePath,
                "Launch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            inputBroker = null;
            running = false;
            launchButton.Enabled = installation.MissingComponents().Count == 0;
            shutdown?.Dispose(); shutdown = null;
            if (closeAfterShutdown) Close();else ResetIdleBroker();
        }
    }

    private void OpenConfiguration()
    {
        if (inputBroker is not null) inputBroker.SuppressKeyboard = true;
        try { using var dialog = new ControlsForm(installation, log, inputBroker); dialog.ShowDialog(this);if(!running)ResetIdleBroker(); }
        finally { if (inputBroker is not null) inputBroker.SuppressKeyboard = false; }
    }

    private void ResetIdleBroker()
    {
        idleBroker?.Dispose();idleBroker=null;
        if(running)return;
        try{idleBroker=new PhysicalInputManager(installation,log);}
        catch(Exception e){log.Error(e);statusLabel.Text="Controls need attention — open Configure Controls";}
    }
    private void TestControls()
    {
        try{using var form=new ControlTestForm(installation,log,CurrentBroker);form.ShowDialog(this);}
        catch(Exception e){log.Error(e);MessageBox.Show(this,e.Message,"Cannot test controls");}
    }
    private void CheckExit()
    {
        var broker=CurrentBroker;if(broker!=observedBroker){observedBroker=broker;observedExit=broker?.ExitPresses??0;return;}
        if(broker is null||broker.ExitPresses==observedExit)return;observedExit=broker.ExitPresses;
        if(!exitDialogOpen&&!closeAfterShutdown&&OwnedForms.Length==0)RequestExit();
    }
    private void RequestExit()
    {
        if(exitDialogOpen||closeAfterShutdown)return;
        exitDialogOpen=true;var broker=CurrentBroker;if(broker is not null)broker.SuppressExit=true;
        try
        {
            var config=ControlsStore.Load(installation);
            log.Write("Exit requested; confirmation="+config.ConfirmExit+"; running="+running);
            if(config.ConfirmExit)
            {
                using var dialog=new Form{Text="Exit Drone Launcher",ClientSize=new Size(475,185),FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false,TopMost=true};LauncherStyle.Apply(dialog);
                var question=new Label{Text="Exit Drone Racing and close Drone Launcher?",Dock=DockStyle.Top,Height=65,Padding=new Padding(20)};
                var remember=new CheckBox{Text="Don't ask again",Location=new Point(22,73),AutoSize=true};
                var exit=new Button{Text="Exit",DialogResult=DialogResult.OK,Location=new Point(230,123),Size=new Size(105,38)};
                var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,Location=new Point(345,123),Size=new Size(105,38)};
                dialog.Controls.AddRange([question,remember,exit,cancel]);dialog.CancelButton=cancel;
                if(dialog.ShowDialog(this)!=DialogResult.OK){log.Write("Exit confirmation cancelled; session retained.");return;}
                log.Write("Exit confirmation accepted; doNotAskAgain="+remember.Checked);
                if(remember.Checked){config.ConfirmExit=false;ControlsStore.Save(installation,config);}
            }
            Close();
        }
        catch(Exception e){log.Error(e);MessageBox.Show(this,"Exit preferences could not be read or saved. Close the window to use normal shutdown.\n"+e.Message,"Exit settings");}
        finally{exitDialogOpen=false;if(broker is not null)broker.SuppressExit=false;}
    }
    private void ShowDiagnostics()
    {
        using var dialog=new Form{Text="Drone Launcher — Diagnostics",ClientSize=new Size(780,460),StartPosition=FormStartPosition.CenterParent};
        dialog.Controls.Add(new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,Text="Drone Launcher 1.1\r\nInstallation: "+installation.ContentRoot+"\r\nLog: "+log.FilePath+"\r\n\r\n"+File.ReadAllText(log.FilePath)});dialog.ShowDialog(this);
    }
    private void ShowFeatureStatus(string feature)
    {
        try{switch(feature){case "Display / Graphics":FeatureSettings.Display(this,installation,running);break;case "Network / LAN":FeatureSettings.Network(this,installation,running);break;case "Outputs":FeatureSettings.Outputs(this,installation,running);break;}}
        catch(Exception e){log.Error(e);MessageBox.Show(this,"Settings could not be opened. Check the configuration file in Diagnostics.\n"+e.Message,feature);}
    }
    private static string FriendlyStatus(string message)
    {
        if(message.StartsWith("Selected monitor")||message.StartsWith("Display settings"))return message;
        if(message.StartsWith("Game running"))return "Game Running";
        if(message.StartsWith("Shell running"))return "Starting Shell...";
        if(message.StartsWith("Operator menu")||message.Contains("operator flow"))return "Operator Menu";
        if(message.StartsWith("Shell exited"))return "Session closed";
        if(message.StartsWith("Game exited"))return "Game closed — monitoring cabinet";
        return "Error — see diagnostics";
    }

    private void RequestOperator()
    {
        if (inputBroker is null) { statusLabel.Text = "Launch Game first, then open the operator menu."; return; }
        if (ProcessMonitor.ReadShellState(installation, inputBroker.ShellPid) == 34)
        {
            ProcessMonitor.FocusOperatorWindow(inputBroker.ShellPid);
            statusLabel.Text = "Operator menu: Service chooses; Test selects";
            return;
        }
        inputBroker.RequestOperatorMenu();
        statusLabel.Text = "TEST requested; waiting for Shell's original operator flow.";
    }
}
