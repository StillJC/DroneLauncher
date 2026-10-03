namespace DroneRacingGenesisLoader;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var installation = Installation.ResolveFromExecutable();
            using var instance = new Mutex(false, "Local\\DRG.Loader." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(installation.ContentRoot.ToUpperInvariant()))));
            if (!instance.WaitOne(0)) throw new InvalidOperationException("This installation already has a running loader.");
            using var log = new LoaderLog(installation.LogsRoot);
            log.Write($"Loader executable={Environment.ProcessPath}; content root={installation.ContentRoot}");
            var missing = installation.MissingComponents();
            RuntimeConfiguration.EnsureControls(installation);
            Diagnostics.Update(installation, missing.Count == 0 ? "Preflight passed" : "Missing required files");

            if (args.Contains("--preflight", StringComparer.OrdinalIgnoreCase))
            {
                if (missing.Count > 0)
                {
                    log.Write("Preflight failed: " + string.Join(", ", missing));
                    MessageBox.Show("Drone Racing runtime is incomplete.\nMissing:\n- " +
                        string.Join("\n- ", missing), "Drone Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 2;
                }
                try { RuntimeConfiguration.Generate(installation, log); }
                catch (Exception exception)
                {
                    log.Error(exception);
                    MessageBox.Show(exception.Message, "Configuration error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 3;
                }
                log.Write("Preflight passed; Shell was not launched.");
                return 0;
            }

            Application.Run(new LoaderForm(installation, log, missing));
            return 0;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Drone Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
