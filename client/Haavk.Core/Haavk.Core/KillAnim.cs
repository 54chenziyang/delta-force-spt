namespace Haavk.Core;

public static class KillAnim
{
	public const float Life = 3.1f;

	public const float ExitSeconds = 0.1f;

	public const float TitleShow = 0.19f;

	public const float DistanceExitDelay = 0.067f;

	public const float RingStartEm = 2.66f;

	public const float RingGrowEmPerSecond = 9.57f;

	public const float RingMaxEm = 5.22f;

	public const float RingLineEm = 0.085f;

	private const float SkullFadeIn = 0.25f;

	private const float SkullPop = 2.55f;

	private const float SkullHold = 0.1f;

	private const float SkullShrink = 0.167f;

	private const float TitleFadeIn = 0.11f;

	private const float TitlePop = 2.7f;

	private const float TitleShrinkStart = 0.2f;

	private const float TitleShrink = 0.15f;

	private const float BoxShow = 0.27f;

	private const float BoxPeakAt = 0.333f;

	private const float BoxPeak = 0.78f;

	private const float BoxRest = 0.25f;

	private const float BoxSettle = 0.333f;

	private const float DistanceShow = 0.07f;

	private const float DistanceFadeIn = 0.13f;

	private const float DistanceDropEm = 0.58f;

	private const float DistanceDropStart = 0.1f;

	private const float DistanceDropSeconds = 0.135f;

	private const float RingShow = 0.267f;

	private const float RingGrowStart = 0.3f;

	private const float RingFadeStart = 0.433f;

	private const float RingHide = 0.567f;

	public static KillPart Skull(float t, float end)
	{
		if (!(t >= 0f) || t >= end + 0.1f)
		{
			return KillPart.Hidden;
		}
		float num = EaseOutQuad(Clamp01(t / 0.25f));
		return Of(scale: (t < 0.1f) ? 2.55f : Lerp(2.55f, 1f, Clamp01((t - 0.1f) / 0.167f)), alpha: num * ExitFactor(t, end), squashY: 1f, dropEm: 0f);
	}

	public static KillPart Title(float t, float end)
	{
		if (!(t >= 0.19f) || t >= end + 0.1f)
		{
			return KillPart.Hidden;
		}
		float num = Clamp01((t - 0.19f) / 0.11f);
		float scale = Lerp(2.7f, 1f, EaseOutCubic(Clamp01((t - 0.2f) / 0.15f)));
		float num2 = ExitFactor(t, end);
		return Of(num * num2, scale, num2, 0f);
	}

	public static KillPart Box(float t, float end)
	{
		if (!(t >= 0.27f) || t >= end + 0.1f)
		{
			return KillPart.Hidden;
		}
		float num = ((t < 0.333f) ? (0.78f * (t - 0.27f) / 0.06299999f) : Lerp(0.78f, 0.25f, EaseOutCubic(Clamp01((t - 0.333f) / 0.333f))));
		float num2 = ExitFactor(t, end);
		return Of(num * num2, 1f, num2, 0f);
	}

	public static KillPart Distance(float t, float end)
	{
		float num = end + 0.067f;
		if (!(t >= 0.07f) || t >= num + 0.1f)
		{
			return KillPart.Hidden;
		}
		float num2 = Clamp01((t - 0.07f) / 0.13f);
		return Of(dropEm: -0.58f * (1f - Clamp01((t - 0.1f) / 0.135f)), alpha: num2 * ExitFactor(t, num), scale: 1f, squashY: 1f);
	}

	public static bool Ring(float t, out float radiusEm, out float alpha)
	{
		radiusEm = 0f;
		alpha = 0f;
		if (!(t >= 0.267f) || t >= 0.567f)
		{
			return false;
		}
		radiusEm = ((t < 0.3f) ? 2.66f : (2.66f + 9.57f * (t - 0.3f)));
		alpha = ((t < 0.433f) ? 1f : (1f - (t - 0.433f) / 0.13399997f));
		return true;
	}

	private static KillPart Of(float alpha, float scale, float squashY, float dropEm)
	{
		return new KillPart
		{
			Visible = true,
			Alpha = alpha,
			Scale = scale,
			SquashY = squashY,
			DropEm = dropEm
		};
	}

	private static float ExitFactor(float t, float end)
	{
		if (!(t >= end))
		{
			return 1f;
		}
		return Clamp01(1f - (t - end) / 0.1f);
	}

	private static float Lerp(float a, float b, float p)
	{
		return a + (b - a) * p;
	}

	private static float Clamp01(float v)
	{
		if (!(v < 0f))
		{
			if (!(v > 1f))
			{
				return v;
			}
			return 1f;
		}
		return 0f;
	}

	private static float EaseOutQuad(float p)
	{
		return 1f - (1f - p) * (1f - p);
	}

	private static float EaseOutCubic(float p)
	{
		return 1f - (1f - p) * (1f - p) * (1f - p);
	}
}
