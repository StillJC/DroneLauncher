using System;
using System.Collections.Generic;
using System.IO;

namespace DroneRacingGenesis.Plugin;

/// <summary>Activates Shell's existing missing-key fallback through its working INI.</summary>
public static class ShellSecurityWorkaround
{
    public static string Configure(string ini, bool enabled)
    {
        var newline = ini.Contains("\r\n") ? "\r\n" : "\n";
        var lines = new List<string>(ini.Replace("\r\n", "\n").Split('\n'));
        var debugIndex = -1;
        var inDebug = false;
        var found = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                inDebug = trimmed.Equals("[Debug]", StringComparison.OrdinalIgnoreCase);
                if (inDebug) debugIndex = i;
                continue;
            }
            var equals = lines[i].IndexOf('=');
            if (!inDebug || equals < 0 || !lines[i].Substring(0, equals).Trim().Equals("SecurityDisabled", StringComparison.OrdinalIgnoreCase)) continue;
            lines[i] = lines[i].Substring(0, equals + 1) + (enabled ? "1" : "0");
            found = true;
        }
        if (!found)
        {
            if (debugIndex < 0) throw new InvalidDataException("Expected [Debug] section is missing from Shell Game.ini.");
            lines.Insert(debugIndex + 1, "SecurityDisabled=" + (enabled ? "1" : "0"));
        }
        return string.Join(newline, lines);
    }
}
