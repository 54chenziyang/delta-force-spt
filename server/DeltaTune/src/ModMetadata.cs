using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SemanticVersioning;

namespace DeltaTune;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.delta.tune";

    public string Name { get; init; } = "DeltaTune";

    public string Author { get; init; } = "Delta";

    public List<string> Contributors { get; init; } = new List<string>();

    public Version Version { get; init; } = new Version("1.0.0");

    public Range SptVersion { get; init; } = new Range("~4.1.0");

    public List<string> Incompatibilities { get; init; } = new List<string>();

    public Dictionary<string, Range> ModDependencies { get; init; } = new Dictionary<string, Range>();

    public string Url { get; init; } = "";

    public string License { get; init; } = "MIT";

    public bool HasPrepatcher { get; init; }
}
