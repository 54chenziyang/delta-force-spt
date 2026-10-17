using System;
using Haavk.Core;
using UnityEngine;

namespace Haavk.Combat;

internal static class CrosshairDraw
{
	internal static int Draws;

	internal static bool Faulted;

	internal static void ResetRaid()
	{
		Draws = 0;
		Faulted = false;
	}

	internal static void Frame(CrosshairFrame f, bool atCenter, float x, float y, float uiScale)
	{
		if (Faulted || !f.Visible || f.Alpha <= 0f)
		{
			return;
		}
		Color color = GUI.color;
		try
		{
			float num = CombatLayout.Em(Screen.height, uiScale);
			float num2 = (atCenter ? Mathf.Round((float)Screen.width * 0.5f) : x);
			float num3 = (atCenter ? Mathf.Round((float)Screen.height * 0.5f) : y);
			float num4 = Mathf.Max(1f, Mathf.Round(0.115f * num * CombatFeature.CrosshairWidth));
			float num5 = Mathf.Round(f.InnerEm * num);
			float num6 = Mathf.Max(1f, Mathf.Round(f.LineEm * num));
			float num7 = Mathf.Floor(num4 * 0.5f);
			float num8 = num4 - 2f * num7;
			Color crosshairColorNow = CombatFeature.CrosshairColorNow;
			crosshairColorNow.a = f.Alpha;
			GUI.color = crosshairColorNow;
			Texture2D whiteTexture = Texture2D.whiteTexture;
			GUI.DrawTexture(new Rect(num2 - num7, num3 - num5 - num6, num4, num6), whiteTexture);
			GUI.DrawTexture(new Rect(num2 - num7, num3 + num5 + num8, num4, num6), whiteTexture);
			GUI.DrawTexture(new Rect(num2 - num5 - num6, num3 - num7, num6, num4), whiteTexture);
			GUI.DrawTexture(new Rect(num2 + num5 + num8, num3 - num7, num6, num4), whiteTexture);
			GUI.DrawTexture(new Rect(num2 - num7, num3 - num7, num4, num4), whiteTexture);
			Draws++;
		}
		catch (Exception e)
		{
			Faulted = true;
			CombatFeature.WarnOnce("画准星失败，这一局不再画", e);
		}
		finally
		{
			GUI.color = color;
		}
	}
}
