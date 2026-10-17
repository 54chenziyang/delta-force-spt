using System.Collections.Generic;
using System.Runtime.InteropServices;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SemanticVersioning;

namespace Haavk.Server;

public record ModMetadata : IModMetadata
{
	public string ModGuid { get; init; } = "com.haavk.server";

	public string Name { get; init; } = "Haavk.Server";

	public string Author { get; init; } = "Leo3306";

	public List<string> Contributors { get; init; }

	public Version Version { get; init; }

	public Range SptVersion { get; init; }

	public List<string> Incompatibilities { get; init; }

	public Dictionary<string, Range> ModDependencies { get; init; }

	public string Url { get; init; }

	public string License { get; init; }

	public bool HasPrepatcher { get; init; }

	public ModMetadata()
	{
		int num = 1;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = "acidphantasm";
		Contributors = list;
		Version = new Version("2.2.0");
		SptVersion = new Range("~4.1.0");
		num = 1;
		List<string> list2 = new List<string>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = "com.odt.iteminfo";
		Incompatibilities = list2;
		License = "MIT";
	}
}
