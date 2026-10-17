using System;
using Haavk.Core;
using UnityEngine;

namespace Haavk.Combat;

internal static class KillFeedDraw
{
	private static readonly Color White = new Color(1f, 1f, 1f, 1f);

	private static readonly Color Orange = new Color(253f / 255f, 184f / 255f, 48f / 255f, 1f);

	private static readonly Color BoxGray = new Color(184f / 255f, 188f / 255f, 200f / 255f, 1f);

	private static readonly Texture2D[] Skulls = new Texture2D[2];

	private static float _skullEm = -1f;

	private const int RingSteps = 8;

	private static readonly Texture2D[] Rings = new Texture2D[8];

	private static float[] _ringRadii;

	private static float _ringEm = -1f;

	internal static int Draws;

	internal static bool Faulted;

	private static bool _measureReported;

	internal static void ResetRaid()
	{
		Draws = 0;
		Faulted = false;
		_measureReported = false;
	}

	internal static void Frame(KillFeed feed, double now, float uiScale, float farMeters)
	{
		if (Faulted)
		{
			return;
		}
		Color color = GUI.color;
		Matrix4x4 matrix = GUI.matrix;
		try
		{
			float em = CombatLayout.Em(Screen.height, uiScale);
			float cx = (float)Screen.width * 0.5f;
			float num = CombatLayout.KillSkullCenterY(Screen.height, em);
			for (int i = 0; i < feed.Texts.Count; i++)
			{
				DrawText(feed.Texts[i], now, em, cx, num, farMeters, matrix);
			}
			for (int j = 0; j < feed.Skulls.Count; j++)
			{
				DrawRing(feed.Skulls[j], now, em, cx, num);
			}
			for (int k = 0; k < feed.Skulls.Count; k++)
			{
				DrawSkull(feed.Skulls[k], now, em, cx, num);
			}
			Draws++;
		}
		catch (Exception e)
		{
			Faulted = true;
			CombatFeature.WarnOnce("画击败提示失败，这一局不再画", e);
		}
		finally
		{
			GUI.matrix = matrix;
			GUI.color = color;
		}
	}

	internal static void Prewarm(float uiScale)
	{
		try
		{
			float em = CombatLayout.Em(Screen.height, uiScale);
			SkullTexture(head: false, em);
			SkullTexture(head: true, em);
			for (int i = 0; i < 8; i++)
			{
				RingTexture(i, em);
			}
		}
		catch (Exception e)
		{
			CombatFeature.WarnOnce("预备击败提示纹理失败", e);
		}
	}

	private static void DrawSkull(KillSkull s, double now, float em, float cx, float cy)
	{
		KillPart killPart = KillAnim.Skull((float)(now - s.Start), 3.1f);
		if (!killPart.Visible || killPart.Alpha <= 0f)
		{
			return;
		}
		Texture2D texture2D = SkullTexture(s.Head, em);
		if (!(texture2D == null))
		{
			float num = cx + s.SlotAt(now) * 1.92f * em;
			float num2 = (float)texture2D.width * killPart.Scale;
			float num3 = (float)texture2D.height * killPart.Scale;
			Color color = (s.Head ? Orange : White);
			color.a = killPart.Alpha;
			GUI.color = color;
			float num4 = num - num2 * 0.5f;
			float num5 = cy - num3 * 0.5f;
			if (killPart.Scale == 1f)
			{
				num4 = Mathf.Round(num4);
				num5 = Mathf.Round(num5);
			}
			GUI.DrawTexture(new Rect(num4, num5, num2, num3), texture2D);
		}
	}

	private static void DrawRing(KillSkull s, double now, float em, float cx, float cy)
	{
		if (s.Head && KillAnim.Ring((float)(now - s.Start), out var radiusEm, out var alpha) && !(alpha <= 0f))
		{
			float num = radiusEm * em;
			EnsureRingSteps(em);
			int num2 = RingGlyph.Nearest(_ringRadii, num);
			Texture2D texture2D = RingTexture(num2, em);
			float num3 = (float)texture2D.width * 0.5f * (num / _ringRadii[num2]);
			float num4 = cx + s.SlotAt(now) * 1.92f * em;
			Color orange = Orange;
			orange.a = alpha;
			GUI.color = orange;
			GUI.DrawTexture(new Rect(num4 - num3, cy - num3, 2f * num3, 2f * num3), texture2D);
		}
	}

	private static Texture2D SkullTexture(bool head, float em)
	{
		if (Math.Abs(_skullEm - em) >= 0.5f)
		{
			CombatDraw.Replace(ref Skulls[0], null);
			CombatDraw.Replace(ref Skulls[1], null);
			_skullEm = em;
		}
		if (Skulls[head ? 1u : 0u] != null)
		{
			return Skulls[head ? 1u : 0u];
		}
		RgbaImage rgbaImage = CombatIcons.Skull(head, out var drawnAsHead);
		if (rgbaImage == null)
		{
			return null;
		}
		CombatLayout.SkullBox(drawnAsHead, em, out var w, out var h);
		RgbaImage rgbaImage2 = IconRaster.Fit(rgbaImage, Math.Max(1, (int)Math.Round((double)w)), Math.Max(1, (int)Math.Round((double)h)), 1);
		Skulls[head ? 1u : 0u] = CombatDraw.Upload(rgbaImage2.Width, rgbaImage2.Height, rgbaImage2.Pixels);
		return Skulls[head ? 1u : 0u];
	}

	private static void EnsureRingSteps(float em)
	{
		if (_ringRadii == null || !(Math.Abs(_ringEm - em) < 0.5f))
		{
			for (int i = 0; i < Rings.Length; i++)
			{
				CombatDraw.Replace(ref Rings[i], null);
			}
			_ringRadii = RingGlyph.Steps(2.66f * em, 5.22f * em, 8);
			_ringEm = em;
		}
	}

	private static Texture2D RingTexture(int i, float em)
	{
		EnsureRingSteps(em);
		if (Rings[i] != null)
		{
			return Rings[i];
		}
		byte[] topDown = RingGlyph.Render(_ringRadii[i], 0.085f * em, out var size);
		Rings[i] = CombatDraw.Upload(size, size, topDown);
		return Rings[i];
	}

	private static void DrawText(KillText k, double now, float em, float cx, float skullY, float farMeters, Matrix4x4 baseMatrix)
	{
		float t = (float)(now - k.Start);
		float end = (double.IsPositiveInfinity(k.End) ? float.PositiveInfinity : ((float)(k.End - k.Start)));
		KillPart killPart = KillAnim.Box(t, end);
		KillPart killPart2 = KillAnim.Title(t, end);
		KillPart f = KillAnim.Distance(t, end);
		if (!killPart.Visible && !killPart2.Visible && !f.Visible)
		{
			return;
		}
		string text = KillWords.Action(k.Head);
		string text2 = (string.IsNullOrEmpty(k.Name) ? "" : k.Name);
		GUIStyle gUIStyle = Style(Math.Max(8, Mathf.RoundToInt(em)), TextAnchor.MiddleLeft);
		float x = gUIStyle.CalcSize(new GUIContent(text)).x;
		float num = ((text2.Length == 0) ? 0f : gUIStyle.CalcSize(new GUIContent(text2)).x);
		float num2 = ((text2.Length == 0) ? 0f : Mathf.Round(0.3f * em));
		float num3 = Mathf.Round(0.36f * em);
		float num4 = Mathf.Round(x + num2 + num + 2f * num3);
		float num5 = Mathf.Round(1.25f * em);
		float num6 = Mathf.Round(skullY + 1.26f * em);
		float py = num6 + num5 * 0.5f;
		float num7 = Mathf.Round(cx - num4 * 0.5f);
		if (killPart.Visible && killPart.Alpha > 0f)
		{
			GUI.matrix = baseMatrix * AroundPivot(cx, py, 1f, killPart.SquashY);
			Color boxGray = BoxGray;
			boxGray.a = killPart.Alpha;
			GUI.color = boxGray;
			GUI.DrawTexture(new Rect(num7, num6, num4, num5), Texture2D.whiteTexture);
		}
		if (killPart2.Visible && killPart2.Alpha > 0f)
		{
			GUI.matrix = baseMatrix * AroundPivot(cx, py, killPart2.Scale, killPart2.Scale * killPart2.SquashY);
			GUI.color = White;
			float y = num6 + Mathf.Round(0.04f * em);
			Label(new Rect(num7 + num3, y, x, num5), text, gUIStyle, White, killPart2.Alpha);
			if (text2.Length > 0)
			{
				Label(new Rect(num7 + num3 + x + num2, y, num, num5), text2, gUIStyle, Orange, killPart2.Alpha);
			}
		}
		GUI.matrix = baseMatrix;
		if (f.Visible && f.Alpha > 0f)
		{
			DrawDistance(k, f, em, cx, skullY, farMeters);
		}
		if (!_measureReported && CombatFeature.TraceOn)
		{
			_measureReported = true;
			CombatFeature.Trace("击败提示首帧：字号 " + gUIStyle.fontSize + " 动作词宽 " + x.ToString("0") + " 名字宽 " + num.ToString("0") + " 底框 " + num4.ToString("0") + "×" + num5.ToString("0") + " em " + em.ToString("0.0") + " 屏" + Screen.width + "×" + Screen.height + " label内边距=" + gUIStyle.padding.left + "/" + gUIStyle.padding.right + " 名字=" + text2);
		}
	}

	private static void DrawDistance(KillText k, KillPart f, float em, float cx, float skullY, float farMeters)
	{
		string text = KillWords.Range(k.Meters, farMeters);
		string text2 = KillWords.Meters(k.Meters) + "米";
		GUIStyle gUIStyle = Style(Math.Max(8, Mathf.RoundToInt(em * 0.93f)), TextAnchor.MiddleLeft);
		float x = gUIStyle.CalcSize(new GUIContent(text)).x;
		float x2 = gUIStyle.CalcSize(new GUIContent(text2)).x;
		float num = Mathf.Round(0.21f * em);
		float num2 = Mathf.Round(cx - (x + num + x2) * 0.5f);
		float num3 = Mathf.Round(2f * em);
		float y = Mathf.Round(skullY + (3.26f + f.DropEm) * em - num3 * 0.5f);
		GUI.color = White;
		Label(new Rect(num2, y, x, num3), text, gUIStyle, White, f.Alpha);
		Label(new Rect(num2 + x + num, y, x2, num3), text2, gUIStyle, Orange, f.Alpha);
	}

	private static GUIStyle Style(int fontSize, TextAnchor anchor)
	{
		return new GUIStyle(GUI.skin.label)
		{
			fontSize = fontSize,
			alignment = anchor,
			richText = false,
			wordWrap = false
		};
	}

	private static void Label(Rect r, string text, GUIStyle st, Color c, float alpha)
	{
		c.a = alpha;
		st.normal.textColor = c;
		GUI.Label(r, text, st);
	}

	private static Matrix4x4 AroundPivot(float px, float py, float sx, float sy)
	{
		if (sy < 0.02f)
		{
			sy = 0.02f;
		}
		return new Matrix4x4
		{
			m00 = sx,
			m03 = px - sx * px,
			m11 = sy,
			m13 = py - sy * py,
			m22 = 1f,
			m33 = 1f
		};
	}
}
