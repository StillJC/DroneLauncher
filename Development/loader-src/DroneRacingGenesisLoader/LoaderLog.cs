namespace DroneRacingGenesisLoader;

internal sealed class LoaderLog : IDisposable
{
    private readonly StreamWriter writer;
    private readonly object gate = new();
    public string FilePath { get; }

    public LoaderLog(string logsRoot)
    {
        Directory.CreateDirectory(logsRoot);
        FilePath = Path.Combine(logsRoot, $"loader-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
        writer = new StreamWriter(new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        { AutoFlush = true };
    }

    public void Write(string message)
    {
        lock (gate) writer.WriteLine($"{DateTimeOffset.Now:O} {message}");
    }

    public void Error(Exception exception) => Write("ERROR " + exception);
    public void Dispose() { lock (gate) writer.Dispose(); }
}
