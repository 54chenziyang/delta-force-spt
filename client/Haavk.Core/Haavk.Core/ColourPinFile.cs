using System.Collections.Generic;

namespace Haavk.Core;

public sealed class ColourPinFile
{
	public string Source;

	public IDictionary<string, string> Entries;

	public ColourPinFile(string source, IDictionary<string, string> entries)
	{
		Source = source;
		Entries = entries;
	}
}
