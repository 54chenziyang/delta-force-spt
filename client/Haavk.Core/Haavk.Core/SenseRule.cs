using System;
using System.Collections.Generic;

namespace Haavk.Core;

public static class SenseRule
{
	public const int TierCount = 6;

	public const float FadePerSecond = 2f;

	public const float WaveSpeed = 20f;

	public const float RangeScanInterval = 0.5f;

	public const float RangeGraceSeconds = 1f;

	public const float DoubleTapWindow = 0.5f;

	public const float KeyCooldown = 2f;

	public const float BelowFeet = 1f;

	public const float AboveFeet = 3f;

	public const float MinSaturation = 0.95f;

	public const float AchromaticBelow = 0.15f;

	public const float NeighbourHorizontal = 0.6f;

	public const float NeighbourVertical = 0.6f;

	public static int PaletteIndex(string rgbHex, IReadOnlyList<string> palette)
	{
		string text = NormaliseRgb(rgbHex);
		if (text == null || palette == null)
		{
			return -1;
		}
		for (int i = 0; i < palette.Count; i++)
		{
			if (string.Equals(NormaliseRgb(palette[i]), text, StringComparison.Ordinal))
			{
				return i;
			}
		}
		return -1;
	}

	private static string NormaliseRgb(string hex)
	{
		if (string.IsNullOrEmpty(hex))
		{
			return null;
		}
		string text = ((hex[0] == '#') ? hex.Substring(1) : hex);
		foreach (char c in text)
		{
			if ((c < '0' || c > '9') && (c < 'a' || c > 'f') && (c < 'A' || c > 'F'))
			{
				return null;
			}
		}
		if (text.Length == 3)
		{
			text = new string(new char[6]
			{
				text[0],
				text[0],
				text[1],
				text[1],
				text[2],
				text[2]
			});
		}
		else if (text.Length == 8)
		{
			text = text.Substring(0, 6);
		}
		else if (text.Length != 6)
		{
			return null;
		}
		return text.ToUpperInvariant();
	}

	public static bool Passes(int tier, int minTier)
	{
		if (minTier <= 0)
		{
			return true;
		}
		return tier >= minTier;
	}

	public static int ClampMinTier(int raw)
	{
		if (raw < 0)
		{
			return 0;
		}
		if (raw > 5)
		{
			return 5;
		}
		return raw;
	}

	public static bool InHeightWindow(float itemY, float feetY)
	{
		if (itemY >= feetY - 1f)
		{
			return itemY <= feetY + 3f;
		}
		return false;
	}

	public static float PulseDelay(float distance)
	{
		if (distance <= 0f)
		{
			return 0f;
		}
		return distance / 20f;
	}

	public static bool IsDoubleTap(float previousPress, float now)
	{
		float num = now - previousPress;
		if (num > 0f)
		{
			return num <= 0.5f;
		}
		return false;
	}

	public static void Vivid(float r, float g, float b, out float vr, out float vg, out float vb)
	{
		float num = Math.Max(r, Math.Max(g, b));
		float num2 = Math.Min(r, Math.Min(g, b));
		float num3 = ((num <= 0f) ? 0f : ((num - num2) / num));
		if (num3 < 0.15f || num3 >= 0.95f)
		{
			vr = r;
			vg = g;
			vb = b;
		}
		else
		{
			float num4 = num * 0.050000012f;
			float num5 = (num - num4) / (num - num2);
			vr = num4 + (r - num2) * num5;
			vg = num4 + (g - num2) * num5;
			vb = num4 + (b - num2) * num5;
		}
	}

	public static bool AreNeighbours(float dx, float dy, float dz)
	{
		if (dx * dx + dz * dz <= 0.36f)
		{
			return Math.Abs(dy) <= 0.6f;
		}
		return false;
	}

	public static int FirstFreeLayer(IEnumerable<int> taken)
	{
		if (taken == null)
		{
			return 0;
		}
		HashSet<int> hashSet = new HashSet<int>(taken);
		int i;
		for (i = 0; hashSet.Contains(i); i++)
		{
		}
		return i;
	}

	public static int BestIndex(IReadOnlyList<int> tiers, IReadOnlyList<double> values)
	{
		if (tiers == null || values == null)
		{
			return -1;
		}
		int num = Math.Min(tiers.Count, values.Count);
		int num2 = -1;
		for (int i = 0; i < num; i++)
		{
			if (num2 < 0 || tiers[i] > tiers[num2] || (tiers[i] == tiers[num2] && values[i] > values[num2]))
			{
				num2 = i;
			}
		}
		return num2;
	}

	public static int SameTierOthers(IReadOnlyList<int> tiers, int best)
	{
		if (tiers == null || best < 0 || best >= tiers.Count)
		{
			return 0;
		}
		int num = tiers[best];
		if (num < 0)
		{
			return 0;
		}
		int num2 = 0;
		for (int i = 0; i < tiers.Count; i++)
		{
			if (i != best && tiers[i] == num)
			{
				num2++;
			}
		}
		return num2;
	}

	public static float BillboardScale(float distance, float clamp)
	{
		if (distance <= 0f)
		{
			return 0f;
		}
		if (!(distance < clamp))
		{
			return clamp;
		}
		return distance;
	}
}
