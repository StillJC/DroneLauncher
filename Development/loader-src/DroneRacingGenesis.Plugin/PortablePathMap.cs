using System;
using System.IO;

namespace DroneRacingGenesis.Plugin;

/// <summary>
/// Pure path translation for a future IL2CPP hook. This assembly does not load into the game yet.
/// </summary>
public sealed class PortablePathMap
{
    public string ContentRoot { get; }

    public PortablePathMap(string gameExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(gameExecutablePath)) throw new ArgumentException("Game executable path required.", nameof(gameExecutablePath));
        var configured = Environment.GetEnvironmentVariable("DRG_CONTENT_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured) &&
            File.Exists(Path.Combine(configured, "Shell", "Shell.exe")))
        {
            ContentRoot = Path.GetFullPath(configured);
            return;
        }
        var gameDirectory = Path.GetDirectoryName(Path.GetFullPath(gameExecutablePath))!;
        var parent = Directory.GetParent(gameDirectory)?.FullName;
        if (parent != null && File.Exists(Path.Combine(parent, "Shell", "Shell.exe")) &&
            string.Equals(Path.GetFileName(gameDirectory), "DroneRacing", StringComparison.OrdinalIgnoreCase))
        {
            ContentRoot = parent;
            return;
        }

        throw new DirectoryNotFoundException("Portable content root was not found beside DroneRacing.exe or in DRG_CONTENT_ROOT.");
    }

    public string Translate(string original)
    {
        if (string.IsNullOrEmpty(original)) return original;
        var normalized = original.Replace('\\', '/');
        if (normalized.Equals("C:/Sega/ShellData/ShellData.ini", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(ContentRoot, "ShellData", "ShellData.ini");
        if (normalized.Equals("C:/Sega/ShellData/GameSettings.ini", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(ContentRoot, "ShellData", "GameSettings.ini");

        const string prefix = "C:/Sega/GameData/";
        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return original;
        var suffix = normalized.Substring(prefix.Length).TrimStart('/');
        var root = Path.GetFullPath(Path.Combine(ContentRoot, "GameData"));
        var mapped = Path.GetFullPath(Path.Combine(root, suffix.Replace('/', Path.DirectorySeparatorChar)));
        if (mapped != root && !mapped.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("GameData path leaves the portable content root.");
        return mapped;
    }
}
