namespace Haavk.Core;

public static class CombatAnim
{
	public const float MarkerHold = 0.18f;

	public const float MarkerFade = 0.13f;

	public const float KillShrinkEnd = 0.1f;

	public const float KillHoldEnd = 0.16f;

	public const float KillFlySeconds = 0.1f;

	public const float KillShrinkTo = 0.7f;

	public const float KillFlyFrom = 0.53f;

	public const float KillFlyTo = 2.4f;

	public const float PlainPopSeconds = 0.13f;

	public const float PlainPopAmount = 0.3f;

	public const float PlainHold = 0.567f;

	public const float BrokenHold = 0.467f;

	public const float IconFade = 0.1f;

	public static AnimFrame WhiteMarker(float t)
	{
		if (t < 0f || t >= 0.31f)
		{
			return AnimFrame.Hidden;
		}
		return AnimFrame.Of((t < 0.18f) ? 1f : (1f - (t - 0.18f) / 0.13f), 1f, 0f);
	}

	public static AnimFrame KillMarker(float t)
	{
		if (t < 0f)
		{
			return AnimFrame.Hidden;
		}
		if (t < 0.1f)
		{
			return AnimFrame.Of(1f, Lerp(1f, 0.7f, t / 0.1f), 0f);
		}
		if (t < 0.16f)
		{
			return AnimFrame.Of(1f, 0.7f, 0f);
		}
		float num = (t - 0.16f) / 0.1f;
		if (num >= 1f)
		{
			return AnimFrame.Hidden;
		}
		float shift = 1.8700001f * (1f - (1f - num) * (1f - num));
		return AnimFrame.Of((num < 0.6f) ? 1f : (1f - (num - 0.6f) / 0.4f), 0.7f, shift);
	}

	public static AnimFrame Armor(ArmorIcon icon, float t)
	{
		if (t < 0f || icon == ArmorIcon.None)
		{
			return AnimFrame.Hidden;
		}
		bool flag = ArmorIcons.IsBroken(icon);
		float num = (flag ? 0.467f : 0.567f);
		if (t >= num + 0.1f)
		{
			return AnimFrame.Hidden;
		}
		float scale = 1f;
		if (!flag && t < 0.13f)
		{
			float num2 = 1f - t / 0.13f;
			scale = 1f + 0.3f * num2 * num2;
		}
		return AnimFrame.Of((t < num) ? 1f : (1f - (t - num) / 0.1f), scale, 0f);
	}

	private static float Lerp(float a, float b, float p)
	{
		return a + (b - a) * p;
	}
}
