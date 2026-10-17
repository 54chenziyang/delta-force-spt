using System;

namespace Haavk.Core;

public sealed class CategoryColouring
{
	private readonly TierLadder _ladder;

	private readonly string[] _palette;

	private readonly bool _enabled;

	private readonly bool _colourBanned;

	private readonly string _bannedColour;

	public CategoryColouring(TierLadder ladder, string[] palette, bool enabled, bool colourBanned, string bannedColour)
	{
		if (ladder == null)
		{
			throw new ArgumentNullException("ladder");
		}
		if (palette == null || palette.Length != ladder.Count)
		{
			throw new ArgumentException($"色板长度必须等于档位数 {ladder.Count}，实际 {palette?.Length ?? 0}——配置错误要在启动期炸，不许静默错色", "palette");
		}
		_ladder = ladder;
		_palette = palette;
		_enabled = enabled;
		_colourBanned = colourBanned;
		_bannedColour = bannedColour;
	}

	public string Colour(double value, bool isFleaValid, string originalColour)
	{
		if (_colourBanned && !isFleaValid)
		{
			return _bannedColour;
		}
		if (!_enabled)
		{
			return originalColour;
		}
		return _palette[_ladder.Pick(value)];
	}

	public int Tier(double value)
	{
		return _ladder.Pick(value);
	}
}
