using System;

namespace Haavk.Core;

public static class ValuationLabels
{
	public static (string Total, string PerSlot, string Banned) For(string locale)
	{
		if (!string.Equals(locale, "ch", StringComparison.OrdinalIgnoreCase))
		{
			return (Total: "Total:", PerSlot: "Per Slot:", Banned: "Flea Banned");
		}
		return (Total: "总价", PerSlot: "每格", Banned: "禁售");
	}
}
