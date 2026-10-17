using System.Collections.Generic;
using SemanticVersioning;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace Haavk.TraderFill;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.haavk.traderfill";
    public string Name { get; init; } = "Haavk.TraderFill";
    public string Author { get; init; } = "Leo3306";
    public List<string> Contributors { get; init; } = new();
    public Version Version { get; init; } = new Version("1.0.0");
    public Range SptVersion { get; init; } = new Range("~4.1.0");
    public List<string> Incompatibilities { get; init; } = new();
    public Dictionary<string, Range> ModDependencies { get; init; } = new();
    public string Url { get; init; } = "";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; }
}
