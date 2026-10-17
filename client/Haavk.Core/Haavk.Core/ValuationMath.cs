using System;

namespace Haavk.Core;

public static class ValuationMath
{
	public static double PerSlotPrice(double price, int height, int width)
	{
		return Math.Round(price / (double)(height * width));
	}

	public static int StackMultiplier(int stackObjectsCount, int stackMaxSize, bool unlimitedCount)
	{
		if (unlimitedCount)
		{
			return 1;
		}
		int num = Math.Max(1, stackObjectsCount);
		if (stackMaxSize <= 0)
		{
			return num;
		}
		return Math.Min(num, stackMaxSize);
	}
}
