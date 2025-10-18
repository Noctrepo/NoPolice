using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace NoPolice;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public HashSet<string> BlocklistNames { get; set; } = new();
    public string? CachedSha { get; set; }
    public DateTime? LastFetchedUtc { get; set; }
}
