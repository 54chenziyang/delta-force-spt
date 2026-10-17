using System;

namespace Haavk.Core;

public static class TraderPricing
{
	public static double SellToTrader(double handbookPrice, double buyPriceCoefficient)
	{
		if (handbookPrice <= 0.0)
		{
			return 0.0;
		}
		double num = 100.0 - buyPriceCoefficient;
		if (num <= 0.0)
		{
			return 0.0;
		}
		return Math.Round(handbookPrice * num / 100.0);
	}
}
