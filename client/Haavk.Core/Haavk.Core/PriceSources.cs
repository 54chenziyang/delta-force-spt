namespace Haavk.Core;

public static class PriceSources
{
	public const int Count = 6;

	public static bool IsTrader(PriceSource source)
	{
		return source >= PriceSource.商人最高价;
	}
}
