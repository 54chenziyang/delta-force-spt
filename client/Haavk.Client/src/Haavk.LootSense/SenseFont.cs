using System;
using TMPro;
using UnityEngine;

namespace Haavk.LootSense;

internal static class SenseFont
{
	private const char Probe = '物';

	private static bool _resolved;

	private static bool _fontWasFound;

	private static TMP_FontAsset _font;

	private static Material _outlined;

	internal static void Apply(TextMeshPro text)
	{
		if (!_resolved || (_fontWasFound && _font == null))
		{
			Resolve();
		}
		if (_font != null)
		{
			text.font = _font;
		}
		if (_outlined != null)
		{
			text.fontSharedMaterial = _outlined;
		}
	}

	private static void Resolve()
	{
		_resolved = true;
		_font = null;
		_outlined = null;
		TMP_FontAsset tMP_FontAsset = null;
		try
		{
			tMP_FontAsset = TMP_Settings.defaultFontAsset;
		}
		catch (Exception)
		{
			tMP_FontAsset = null;
		}
		TMP_FontAsset tMP_FontAsset2 = (Covers(tMP_FontAsset) ? tMP_FontAsset : null);
		if (tMP_FontAsset2 == null)
		{
			try
			{
				foreach (TMP_FontAsset item in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
				{
					if (Covers(item))
					{
						tMP_FontAsset2 = item;
						break;
					}
				}
			}
			catch (Exception e)
			{
				LootSenseFeature.WarnOnce("枚举字体失败", e);
			}
			if (tMP_FontAsset2 == null)
			{
				LootSenseFeature.WarnOnce("没找到带中文字形的字体，浮字可能显示成方块", null);
				tMP_FontAsset2 = tMP_FontAsset;
			}
			else
			{
				HaavkIl2CppPlugin.Instance.Log.LogDebug("[Haavk|Sense] 默认字体没有中文，浮字改用 " + tMP_FontAsset2.name);
			}
		}
		_font = tMP_FontAsset2;
		_fontWasFound = tMP_FontAsset2 != null;
		if (tMP_FontAsset2 == null)
		{
			return;
		}
		try
		{
			Material material = ((TMP_Asset)tMP_FontAsset2).material;
			if (!(material == null))
			{
				Material material2 = new Material(material);
				material2.hideFlags = HideFlags.HideAndDontSave;
				material2.EnableKeyword("OUTLINE_ON");
				material2.SetFloat("_OutlineWidth", 0.12f);
				material2.SetColor("_OutlineColor", Color.black);
				_outlined = material2;
			}
		}
		catch (Exception e2)
		{
			LootSenseFeature.WarnOnce("描边材质建不出来，浮字不描边", e2);
			_outlined = null;
		}
	}

	private static bool Covers(TMP_FontAsset font)
	{
		if (font == null)
		{
			return false;
		}
		try
		{
			return font.HasCharacter('物', searchFallbacks: true, tryAddCharacter: true);
		}
		catch (Exception)
		{
			return false;
		}
	}
}
