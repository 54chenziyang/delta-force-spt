using System;
using System.Globalization;

namespace Haavk.Core;

public static class KillWords
{
	public const string Meter = "米";

	public static string Action(bool head)
	{
		if (!head)
		{
			return "击败";
		}
		return "击中头部击败";
	}

	public static string Range(float meters, float farMeters)
	{
		if (!(((meters > 0f) ? Math.Round(meters, MidpointRounding.AwayFromZero) : 0.0) >= (double)farMeters))
		{
			return "近距离";
		}
		return "远距离";
	}

	public static string Meters(float meters)
	{
		if (!(meters > 0f))
		{
			return "0";
		}
		return Math.Round(meters, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
	}
}
