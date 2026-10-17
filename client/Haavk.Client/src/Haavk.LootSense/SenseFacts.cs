using System;
using System.Collections.Generic;
using System.Text;
using Comfort.Common;
using EFT;
using EFT.HandBook;
using EFT.InventoryLogic;
using Haavk.Core;
using Haavk.WeaponValue;
using Haavk.WeaponValue.Patches;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

using UnityEngine;

namespace Haavk.LootSense;

internal sealed class SenseFacts
{
	internal Color Colour;

	internal int Tier;

	internal bool Settled;

	internal string IconPath;

	internal string Text;

	private static Dictionary<string, string> _itemParent;

	private static Dictionary<string, KeyValuePair<string, string>> _category;

	internal static SenseFacts Build(Item item, Player player)
	{
		IReadOnlyList<string> palette = Palette();
		ResolveColour(item, palette, out var colour, out var tier);
		ResolveCategory(item, out var category, out var iconPath);
		return Finish(palette, colour, tier, iconPath, category, item, player, 0);
	}

	internal static SenseFacts BuildGroup(string header, List<Item> items, Player player)
	{
		if (items == null || items.Count == 0)
		{
			return null;
		}
		IReadOnlyList<string> palette = Palette();
		List<Color> list = new List<Color>(items.Count);
		List<int> list2 = new List<int>(items.Count);
		List<double> list3 = new List<double>(items.Count);
		foreach (Item item in items)
		{
			ResolveColour(item, palette, out var colour, out var tier);
			list.Add(colour);
			list2.Add(tier);
			list3.Add(SafeTotal(item));
		}
		int num = SenseRule.BestIndex((IReadOnlyList<int>)list2, (IReadOnlyList<double>)list3);
		if (num < 0)
		{
			return null;
		}
		ResolveCategory(items[num], out var _, out var iconPath);
		return Finish(palette, list[num], list2[num], iconPath, header, items[num], player, SenseRule.SameTierOthers((IReadOnlyList<int>)list2, num));
	}

	internal static string CategoryName(Item item)
	{
		ResolveCategory(item, out var category, out var _);
		return category;
	}

	private static IReadOnlyList<string> Palette()
	{
		if (WeaponValueFeature.Colouring != null)
		{
			return WeaponValueFeature.Colouring.Palette;
		}
		return null;
	}

	private static SenseFacts Finish(IReadOnlyList<string> palette, Color colour, int tier, string iconPath, string smallLine, Item shown, Player player, int sameTierOthers)
	{
		SenseRule.Vivid(colour.r, colour.g, colour.b, out var vr, out var vg, out var vb);
		SenseFacts senseFacts = new SenseFacts
		{
			Settled = (palette != null),
			Tier = tier,
			Colour = SenseMath.Rgba(vr, vg, vb, 1f),
			IconPath = iconPath
		};
		senseFacts.Text = ComposeText(shown, player, smallLine, senseFacts.Colour, sameTierOthers);
		return senseFacts;
	}

	private static void ResolveColour(Item item, IReadOnlyList<string> palette, out Color colour, out int tier)
	{
		if (GridBackgroundPatch.TryCellColour(item, out colour))
		{
			tier = SenseRule.PaletteIndex(ColorUtility.ToHtmlStringRGB(colour), palette);
			if (tier < 0)
			{
				tier = (WeaponValueFeature.TryTier(item, out var tier2) ? tier2 : (-1));
			}
		}
		else
		{
			tier = -1;
			colour = ((palette != null && palette.Count > 0 && ColorUtility.TryParseHtmlString(palette[0], out var color)) ? color : SenseMath.Rgba(0.85f, 0.85f, 0.85f, 1f));
			colour.a = 1f;
		}
	}

	private static double SafeTotal(Item item)
	{
		try
		{
			return WeaponTotalCalculator.Total(item);
		}
		catch (Exception)
		{
			return 0.0;
		}
	}

	private static void ResolveCategory(Item item, out string category, out string iconPath)
	{
		//IL_001e: Expected Obj, but got VT
		category = null;
		iconPath = null;
		if (!EnsureHandbookIndex() || !_itemParent.TryGetValue(((System.Object)item.TemplateId).ToString(), out var value) || value == null)
		{
			return;
		}
		try
		{
			category = value.Localized(EStringCase.None);
		}
		catch (Exception)
		{
			category = null;
		}
		string text = value;
		for (int i = 0; i < 8; i++)
		{
			if (text == null)
			{
				break;
			}
			if (!_category.TryGetValue(text, out var value2))
			{
				break;
			}
			string value3 = value2.Value;
			if (!string.IsNullOrEmpty(value3) && !value3.EndsWith("/", StringComparison.Ordinal))
			{
				iconPath = value3;
				break;
			}
			text = value2.Key;
		}
	}

	private static bool EnsureHandbookIndex()
	{
		if (_itemParent != null && _category != null)
		{
			return true;
		}
		if (!Singleton<Handbook>.Instantiated)
		{
			return false;
		}
		Handbook instance = Singleton<Handbook>.Instance;
		if (instance == null)
		{
			return false;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>((IEqualityComparer<string>)StringComparer.Ordinal);
		Dictionary<string, KeyValuePair<string, string>> dictionary2 = new Dictionary<string, KeyValuePair<string, string>>((IEqualityComparer<string>)StringComparer.Ordinal);
		HandbookData[] items = instance.Items;
		if (items != null)
		{
			foreach (HandbookData item in items)
			{
				if (!(item == null) && ((BaseHandbookData)item).Id != null)
				{
					dictionary[((BaseHandbookData)item).Id] = ((BaseHandbookData)item).ParentId;
				}
			}
		}
		HandbookData[] categories = instance.Categories;
		if (categories != null)
		{
			foreach (HandbookData item2 in categories)
			{
				if (!(item2 == null) && ((BaseHandbookData)item2).Id != null)
				{
					dictionary2[((BaseHandbookData)item2).Id] = new KeyValuePair<string, string>(((BaseHandbookData)item2).ParentId, item2.Icon);
				}
			}
		}
		if (dictionary.Count == 0 || dictionary2.Count == 0)
		{
			return false;
		}
		_itemParent = dictionary;
		_category = dictionary2;
		return true;
	}

	private static string ComposeText(Item item, Player player, string smallLine, Color colour, int sameTierOthers)
	{
		string value = ColorUtility.ToHtmlStringRGB(colour);
		bool flag = IsExamined(item, player);
		string text = (flag ? DisplayName(item) : "???");
		int stackObjectsCount = item.StackObjectsCount;
		if (flag && stackObjectsCount > 1)
		{
			text = text + " (" + stackObjectsCount + ")";
		}
		StringBuilder stringBuilder = new StringBuilder(160);
		if (!string.IsNullOrEmpty(smallLine))
		{
			stringBuilder.Append("<size=50%><color=#").Append(value).Append("><noparse>")
				.Append(smallLine)
				.Append("</noparse></color></size>\n");
		}
		stringBuilder.Append("<b><color=#").Append(value).Append("><noparse>")
			.Append(text)
			.Append("</noparse></color></b>");
		string text2 = null;
		if (flag && WeaponValueFeature.Display != null && WeaponValueFeature.Display.Value)
		{
			text2 = PriceText(item);
		}
		string text3 = ((sameTierOthers > 0) ? ("+" + sameTierOthers) : null);
		if (text2 != null || text3 != null)
		{
			stringBuilder.Append("\n<size=75%><color=#").Append(value).Append('>');
			if (text2 != null)
			{
				stringBuilder.Append(text2);
			}
			if (text2 != null && text3 != null)
			{
				stringBuilder.Append("  ");
			}
			if (text3 != null)
			{
				stringBuilder.Append(text3);
			}
			stringBuilder.Append("</color></size>");
		}
		return stringBuilder.ToString();
	}

	internal static string DisplayName(Item item)
	{
		//IL_003f: Expected Obj, but got VT
		try
		{
			string text = item.LocalizedName();
			if (!string.IsNullOrEmpty(text) && text.Length <= 12)
			{
				return text;
			}
			string text2 = item.LocalizedShortName();
			return string.IsNullOrEmpty(text2) ? text : text2;
		}
		catch (Exception)
		{
			return ((System.Object)item.TemplateId).ToString();
		}
	}

	private static string PriceText(Item item)
	{
		try
		{
			double num = WeaponTotalCalculator.Total(item);
			if (num <= 0.0)
			{
				return null;
			}
			return PriceBook.FormatRoubles(num, WeaponValueFeature.Style);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static bool IsExamined(Item item, Player player)
	{
		try
		{
			InventoryController inventoryController = player.InventoryController;
			return inventoryController == null || inventoryController.Examined(item);
		}
		catch (Exception)
		{
			return true;
		}
	}
}
