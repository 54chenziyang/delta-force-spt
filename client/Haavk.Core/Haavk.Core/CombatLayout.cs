using System;

namespace Haavk.Core;

public static class CombatLayout
{
	public const float BaseEmPx = 20.6f;

	public const float MinUiScale = 0.5f;

	public const float MaxUiScale = 2f;

	public const float MarkerRadiusEm = 1.94f;

	public const float IconOffsetEm = 4.63f;

	public const float BlockedW = 0.88f;

	public const float BlockedH = 1.17f;

	public const float BrokenW = 1.1f;

	public const float BrokenH = 1.28f;

	public const float HelmetBlockedW = 0.95f;

	public const float HelmetBlockedH = 1.02f;

	public const float HelmetBrokenW = 1.19f;

	public const float HelmetBrokenH = 1.12f;

	public const float KillSkullOffsetEm = 9.64f;

	public const float KillSlotEm = 1.92f;

	public const float SkullHeadHeightEm = 1.39f;

	public const float SkullHeightEm = 1.48f;

	public const float KillBoxTopEm = 1.26f;

	public const float KillBoxHeightEm = 1.25f;

	public const float KillBoxPadEm = 0.36f;

	public const float KillNameGapEm = 0.3f;

	public const float KillTextNudgeEm = 0.04f;

	public const float KillLine2CenterEm = 3.26f;

	public const float KillLine2FontEm = 0.93f;

	public const float KillRangeGapEm = 0.21f;

	public static float Em(float screenHeight, float uiScale)
	{
		if (screenHeight <= 0f || float.IsNaN(screenHeight))
		{
			screenHeight = 1080f;
		}
		if (float.IsNaN(uiScale))
		{
			uiScale = 1f;
		}
		float num = Math.Max(0.5f, Math.Min(2f, uiScale));
		return 20.6f * screenHeight / 1080f * num;
	}

	public static float MarkerRadiusPx(float em)
	{
		return 1.94f * em;
	}

	public static void IconBox(ArmorIcon icon, float em, out float w, out float h)
	{
		switch (icon)
		{
		case ArmorIcon.Blocked:
			w = 0.88f * em;
			h = 1.17f * em;
			break;
		case ArmorIcon.Broken:
			w = 1.1f * em;
			h = 1.28f * em;
			break;
		case ArmorIcon.HelmetBlocked:
			w = 0.95f * em;
			h = 1.02f * em;
			break;
		case ArmorIcon.HelmetBroken:
			w = 1.19f * em;
			h = 1.12f * em;
			break;
		default:
			w = 0f;
			h = 0f;
			break;
		}
	}

	public static float IconCenterY(float screenHeight, float em)
	{
		return screenHeight * 0.5f + 4.63f * em;
	}

	public static float KillSkullCenterY(float screenHeight, float em)
	{
		return screenHeight * 0.5f + 9.64f * em;
	}

	public static void SkullBox(bool head, float em, out float w, out float h)
	{
		h = (head ? 1.39f : 1.48f) * em;
		w = 2f * h;
	}
}
