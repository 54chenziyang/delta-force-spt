using System;
using Haavk.Core;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Haavk.Combat;

internal static class CombatDraw
{
	private static readonly Color White = new Color(1f, 1f, 1f, 1f);

	private static readonly Color KillOrange = new Color(250f / 255f, 157f / 255f, 72f / 255f, 1f);

	private static readonly Color IconGray = new Color(142f / 255f, 142f / 255f, 144f / 255f, 1f);

	private const float Diag = 0.70710677f;

	private static readonly Texture2D[] Markers = new Texture2D[4];

	private static readonly float[] MarkerRadius = new float[4] { -1f, -1f, -1f, -1f };

	private static readonly Texture2D[] Icons = new Texture2D[5];

	private static float _iconEm = -1f;

	internal static int Draws;

	internal static bool Faulted;

	internal static void Frame(MarkerTrack marker, IconTrack icon, double now, float uiScale)
	{
		if (Faulted)
		{
			return;
		}
		Color color = GUI.color;
		try
		{
			float em = CombatLayout.Em(Screen.height, uiScale);
			DrawMarker(marker, now, em);
			DrawIcon(icon, now, em);
			Draws++;
		}
		catch (Exception e)
		{
			Faulted = true;
			CombatFeature.WarnOnce("画命中反馈失败，这一局不再画", e);
		}
		finally
		{
			GUI.color = color;
		}
	}

	internal static void Prewarm(float uiScale)
	{
		try
		{
			float em = CombatLayout.Em(Screen.height, uiScale);
			float r = CombatLayout.MarkerRadiusPx(em);
			MarkerTexture(MarkerShape.Single, lethal: false, r);
			MarkerTexture(MarkerShape.Double, lethal: false, r);
			MarkerTexture(MarkerShape.Single, lethal: true, r);
			MarkerTexture(MarkerShape.Double, lethal: true, r);
			for (int i = 1; i < Icons.Length; i++)
			{
				IconTexture((ArmorIcon)i, em);
			}
		}
		catch (Exception e)
		{
			CombatFeature.WarnOnce("预备命中反馈纹理失败", e);
		}
	}

	private static void DrawMarker(MarkerTrack marker, double now, float em)
	{
		AnimFrame animFrame = marker.Sample(now);
		if (!animFrame.Visible)
		{
			return;
		}
		float num = CombatLayout.MarkerRadiusPx(em);
		Texture2D texture2D = MarkerTexture(marker.Shape, marker.Lethal, num);
		if (texture2D == null)
		{
			return;
		}
		float num2 = (marker.AtCenter ? ((float)Screen.width * 0.5f) : marker.X);
		float num3 = (marker.AtCenter ? ((float)Screen.height * 0.5f) : marker.Y);
		Color color = (marker.Lethal ? KillOrange : White);
		color.a = animFrame.Alpha;
		GUI.color = color;
		float num4 = (float)texture2D.width * 0.5f * animFrame.Scale;
		if (animFrame.Shift <= 0f)
		{
			float num5 = num2 - num4;
			float num6 = num3 - num4;
			if (animFrame.Scale == 1f)
			{
				num5 = Mathf.Round(num5);
				num6 = Mathf.Round(num6);
			}
			GUI.DrawTexture(new Rect(num5, num6, 2f * num4, 2f * num4), texture2D);
		}
		else
		{
			float num7 = animFrame.Shift * num * 0.70710677f;
			GUI.DrawTextureWithTexCoords(new Rect(num2 - num4 - num7, num3 - num4 - num7, num4, num4), texture2D, new Rect(0f, 0.5f, 0.5f, 0.5f));
			GUI.DrawTextureWithTexCoords(new Rect(num2 + num7, num3 - num4 - num7, num4, num4), texture2D, new Rect(0.5f, 0.5f, 0.5f, 0.5f));
			GUI.DrawTextureWithTexCoords(new Rect(num2 - num4 - num7, num3 + num7, num4, num4), texture2D, new Rect(0f, 0f, 0.5f, 0.5f));
			GUI.DrawTextureWithTexCoords(new Rect(num2 + num7, num3 + num7, num4, num4), texture2D, new Rect(0.5f, 0f, 0.5f, 0.5f));
		}
	}

	private static void DrawIcon(IconTrack icon, double now, float em)
	{
		AnimFrame animFrame = icon.Sample(now);
		if (!animFrame.Visible)
		{
			return;
		}
		Texture2D texture2D = IconTexture(icon.Icon, em);
		if (!(texture2D == null))
		{
			float num = (float)texture2D.width * animFrame.Scale;
			float num2 = (float)texture2D.height * animFrame.Scale;
			float num3 = (float)Screen.width * 0.5f;
			float num4 = CombatLayout.IconCenterY(Screen.height, em);
			Color iconGray = IconGray;
			iconGray.a = animFrame.Alpha;
			GUI.color = iconGray;
			float num5 = num3 - num * 0.5f;
			float num6 = num4 - num2 * 0.5f;
			if (animFrame.Scale == 1f)
			{
				num5 = Mathf.Round(num5);
				num6 = Mathf.Round(num6);
			}
			GUI.DrawTexture(new Rect(num5, num6, num, num2), texture2D);
		}
	}

	private static Texture2D MarkerTexture(MarkerShape shape, bool lethal, float r)
	{
		if (shape == MarkerShape.None)
		{
			return null;
		}
		int num = ((shape == MarkerShape.Double) ? 1 : 0) + (lethal ? 2 : 0);
		Texture2D texture2D = Markers[num];
		if (texture2D != null && Math.Abs(MarkerRadius[num] - r) < 0.5f)
		{
			return texture2D;
		}
		byte[] topDown = MarkerGlyph.Render(shape, lethal, r, out var size);
		Replace(ref Markers[num], Upload(size, size, topDown));
		MarkerRadius[num] = r;
		return Markers[num];
	}

	private static Texture2D IconTexture(ArmorIcon icon, float em)
	{
		if (Math.Abs(_iconEm - em) >= 0.05f)
		{
			for (int i = 1; i < Icons.Length; i++)
			{
				Replace(ref Icons[i], null);
			}
			_iconEm = em;
		}
		if (icon <= ArmorIcon.None || (int)icon >= Icons.Length)
		{
			return null;
		}
		if (Icons[(int)icon] == null)
		{
			Icons[(int)icon] = BuildIcon(icon, em);
		}
		return Icons[(int)icon];
	}

	private static Texture2D BuildIcon(ArmorIcon icon, float em)
	{
		RgbaImage rgbaImage = CombatIcons.Source(icon, out var drawnAs);
		if (rgbaImage == null)
		{
			return null;
		}
		CombatLayout.IconBox(drawnAs, em, out var w, out var h);
		RgbaImage rgbaImage2 = IconRaster.Fit(rgbaImage, Math.Max(1, (int)Math.Round((double)w)), Math.Max(1, (int)Math.Round((double)h)), 1);
		return Upload(rgbaImage2.Width, rgbaImage2.Height, rgbaImage2.Pixels);
	}

	internal static Texture2D Upload(int w, int h, byte[] topDown)
	{
		Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
		texture2D.hideFlags = HideFlags.HideAndDontSave;
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.filterMode = FilterMode.Bilinear;
		texture2D.LoadRawTextureData(RgbaImage.FlipRows(topDown, w, h));
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		return texture2D;
	}

	internal static void Replace(ref Texture2D slot, Texture2D next)
	{
		Texture2D texture2D = slot;
		slot = next;
		if (texture2D == null)
		{
			return;
		}
		try
		{
			UnityEngine.Object.Destroy(texture2D);
		}
		catch (Exception)
		{
		}
	}
}
