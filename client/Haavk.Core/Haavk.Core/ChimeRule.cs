namespace Haavk.Core;

public static class ChimeRule
{
	public static bool UnknownTierChimes => false;

	public static bool ShouldChime(int tier, int minTier)
	{
		return tier >= minTier;
	}

	public static int ClampMinTier(int raw, int tierCount)
	{
		if (raw < 0)
		{
			return 0;
		}
		if (raw > tierCount)
		{
			return tierCount;
		}
		return raw;
	}

	public static bool IsTopTier(int tier, int tierCount)
	{
		return tier >= tierCount - 1;
	}
}
