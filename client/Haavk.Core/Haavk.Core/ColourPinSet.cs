using System;
using System.Collections.Generic;

namespace Haavk.Core;

public sealed class ColourPinSet
{
	public readonly Dictionary<string, int> Tiers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	public readonly List<string> Warnings = new List<string>();
}
