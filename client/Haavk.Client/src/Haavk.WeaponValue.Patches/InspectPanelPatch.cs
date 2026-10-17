using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;

namespace Haavk.WeaponValue.Patches;

internal static class InspectPanelPatch
{
	private const string RefreshMethod = "method_1";

	private const int RefreshFrameInterval = 6;

	private static readonly Dictionary<nint, Coroutine> Watchers = new Dictionary<nint, Coroutine>();

	internal static void Enable(Harmony harmony)
	{
		MethodInfo val = AccessTools.Method(typeof(ItemSpecificationPanel), "method_1", (Type[])null, (Type[])null);
		if (val == (MethodInfo)null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 找不到 ItemSpecificationPanel.UpdateInfoWindowParameters，检视窗口总价不启用");
		}
		else
		{
			harmony.Patch((MethodBase)(object)val, (HarmonyMethod)null, new HarmonyMethod(typeof(InspectPanelPatch).GetMethod("Postfix", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Postfix(ItemSpecificationPanel __instance)
	{
		try
		{
			Item item = PrivAccess.SpecPanelItem(__instance);
			if (item == null)
			{
				return;
			}
			ItemInfoWindowLabels itemLabels = __instance._itemLabels;
			if ((UnityEngine.Object)(object)itemLabels == null)
			{
				return;
			}
			TMP_Text description = itemLabels._description;
			if (!(description == null))
			{
				string text = description.text;
				string text2 = WeaponTotalCalculator.Line(item);
				if (text2 != null)
				{
					description.text = (string.IsNullOrEmpty(text) ? text2 : (text2 + "\n" + text));
				}
				StartWatcher(__instance, item, description, text, text2);
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 检视窗口写总价失败: " + ex);
		}
	}

	private static void StartWatcher(ItemSpecificationPanel panel, Item item, TMP_Text desc, string original, string lastLine)
	{
		nint pointer = (panel).Pointer();
		if (Watchers.TryGetValue(pointer, out var value) && value != null)
		{
			HaavkCoroutineHost.Stop(value);
		}
		Coroutine coroutine = HaavkCoroutineHost.Run(RefreshLoop(panel, item, desc, original, lastLine));
		if (coroutine != null)
		{
			Watchers[pointer] = coroutine;
		}
		else
		{
			Watchers.Remove(pointer);
		}
	}

	private static IEnumerator RefreshLoop(ItemSpecificationPanel panel, Item item, TMP_Text desc, string original, string lastLine)
	{
		nint key = (panel).Pointer();
		while (true)
		{
			for (int i = 0; i < 6; i++)
			{
				yield return null;
			}
			if ((UnityEngine.Object)(object)panel == null || desc == null)
			{
				break;
			}
			string text;
			try
			{
				text = WeaponTotalCalculator.Line(item);
			}
			catch (Exception ex)
			{
				HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 检视窗口总价刷新失败: " + ex);
				break;
			}
			if (!(text == lastLine))
			{
				lastLine = text;
				TMP_Text tMP_Text = desc;
				string text2;
				if (text == null)
				{
					text2 = original;
				}
				else
				{
					text2 = (string.IsNullOrEmpty(original) ? text : (text + "\n" + original));
				}
				tMP_Text.text = text2;
			}
		}
		Watchers.Remove(key);
	}
}
