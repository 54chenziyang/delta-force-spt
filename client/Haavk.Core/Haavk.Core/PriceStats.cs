using System.Collections.Generic;

namespace Haavk.Core;

public static class PriceStats
{
	public static PriceSpread Summarise(IEnumerable<double> prices)
	{
		PriceSpread result = default;
		if (prices == null)
		{
			return result;
		}
		double num = 0.0;
		foreach (double price in prices)
		{
			if (!(price <= 0.0))
			{
				if (result.Count == 0 || price > result.Max)
				{
					result.Max = price;
				}
				if (result.Count == 0 || price < result.Min)
				{
					result.Min = price;
				}
				num += price;
				result.Count++;
			}
		}
		if (result.Count > 0)
		{
			result.Avg = num / (double)result.Count;
		}
		return result;
	}
}
