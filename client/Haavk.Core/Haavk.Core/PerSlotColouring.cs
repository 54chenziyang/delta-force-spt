using System.Collections.Generic;

namespace Haavk.Core;

public sealed class PerSlotColouring
{
	private readonly CategoryColouring _inner;

	private readonly TierLadder _ladder;

	private readonly string[] _palette;

	private readonly bool _colourBanned;

	private readonly string _bannedColour;

	public int TierCount => _ladder.Count;

	public IReadOnlyList<string> Palette => _palette;

	public string BannedColour => _bannedColour;

	private PerSlotColouring(CategoryColouring inner, TierLadder ladder, string[] palette, bool colourBanned, string bannedColour)
	{
		_inner = inner;
		_ladder = ladder;
		_palette = palette;
		_colourBanned = colourBanned;
		_bannedColour = bannedColour;
	}

	public static bool TryCreate(double[] perSlotTiers, string[] palette, string bannedColour, bool enabled, bool colourBanned, out PerSlotColouring result, out string error)
	{
		result = null;
		if (perSlotTiers == null)
		{
			error = "服务端没发每格价阈值（ItemPerSlotTiers 为 null，多半是服务端半边版本旧）";
			return false;
		}
		if (perSlotTiers.Length != 5)
		{
			error = $"每格价阈值必须是 5 个，实际 {perSlotTiers.Length} 个";
			return false;
		}
		if (palette == null)
		{
			error = "服务端没发档位色板（TierColours 为 null）";
			return false;
		}
		if (palette.Length != 6)
		{
			error = $"档位色板必须是 6 个，实际 {palette.Length} 个";
			return false;
		}
		for (int i = 0; i < palette.Length; i++)
		{
			if (!IsHexColour(palette[i]))
			{
				error = $"档位色板第 {i} 项不是 hex 色（\"{palette[i]}\"）" + "——多半是没装 ColorConverterAPI，服务端把色板降级成了颜色名";
				return false;
			}
		}
		if (colourBanned && !IsHexColour(bannedColour))
		{
			error = "禁售底色不是 hex 色（\"" + bannedColour + "\"）——多半是没装 ColorConverterAPI";
			return false;
		}
		TierLadder ladder = new TierLadder(perSlotTiers, inclusive: false);
		result = new PerSlotColouring(new CategoryColouring(ladder, palette, enabled, colourBanned, bannedColour), ladder, palette, colourBanned, IsHexColour(bannedColour) ? bannedColour : null);
		error = null;
		return true;
	}

	public static bool IsHexColour(string colour)
	{
		if (string.IsNullOrEmpty(colour))
		{
			return false;
		}
		if (colour[0] != '#')
		{
			return false;
		}
		int num = colour.Length - 1;
		if (num != 3 && num != 6 && num != 8)
		{
			return false;
		}
		for (int i = 1; i < colour.Length; i++)
		{
			char c = colour[i];
			if ((c < '0' || c > '9') && (c < 'a' || c > 'f') && (c < 'A' || c > 'F'))
			{
				return false;
			}
		}
		return true;
	}

	public string Colour(double total, int width, int height, bool canSellOnFlea)
	{
		if (width <= 0 || height <= 0)
		{
			return null;
		}
		double value = ValuationMath.PerSlotPrice(total, height, width);
		return _inner.Colour(value, canSellOnFlea, null);
	}

	public string DensityColour(double total, int width, int height)
	{
		int num = Tier(total, width, height);
		if (num >= 0)
		{
			return _palette[num];
		}
		return null;
	}

	public int Tier(double total, int width, int height)
	{
		if (width <= 0 || height <= 0)
		{
			return -1;
		}
		return _ladder.Pick(ValuationMath.PerSlotPrice(total, height, width));
	}

	public bool UsesBannedColour(bool canSellOnFlea)
	{
		if (_colourBanned)
		{
			return !canSellOnFlea;
		}
		return false;
	}
}
