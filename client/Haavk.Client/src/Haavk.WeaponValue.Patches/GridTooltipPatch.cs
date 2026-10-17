using System;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using Haavk.Core;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;

using UnityEngine;

namespace Haavk.WeaponValue.Patches;

internal static class GridTooltipPatch
{
	private static Item _hovered;

	private static string _hoveredName;

	internal static void Enable(Harmony harmony)
	{
		MethodInfo val = AccessTools.Method(typeof(GridItemView), "ShowTooltip", (System.Type[])null, (System.Type[])null);
		MethodInfo val2 = AccessTools.Method(typeof(SimpleTooltip), "Show", new System.Type[4] {
			typeof(string),
			typeof(System.Nullable<Vector2>),
			typeof(float),
			typeof(System.Nullable<float>)}, (System.Type[])null);
		if (val == (MethodInfo)null || val2 == (MethodInfo)null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] tooltip 挂点解析失败，悬停总价不启用");
			return;
		}
		harmony.Patch((MethodBase)(object)val, new HarmonyMethod(typeof(GridTooltipPatch).GetMethod("EnterView", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(typeof(GridTooltipPatch).GetMethod("LeaveView", (BindingFlags)40)), (HarmonyMethod)null);
		harmony.Patch((MethodBase)(object)val2, new HarmonyMethod(typeof(GridTooltipPatch).GetMethod("BeforeShow", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
	}

	private static void EnterView(GridItemView __instance)
	{
		try
		{
			_hovered = __instance.Item;
			_hoveredName = ExpectedName(__instance);
		}
		catch (Exception)
		{
			_hovered = null;
			_hoveredName = null;
		}
	}

	private static string ExpectedName(GridItemView view)
	{
		try
		{
			return view.GetItemName();
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static Exception LeaveView(Exception __exception)
	{
		_hovered = null;
		_hoveredName = null;
		return __exception;
	}

	private static void BeforeShow(ref string text)
	{
		try
		{
			Item hovered = _hovered;
			if (!(hovered == null))
			{
				if (WeaponValueFeature.NameColour.Value)
				{
					string hexColour = (GridBackgroundPatch.TryCellColour(hovered, out var colour) ? ("#" + ColorUtility.ToHtmlStringRGB(colour)) : null);
					text = TooltipText.ColourFirstLine(text, _hoveredName, hexColour);
				}
				string line = WeaponTotalCalculator.Line(hovered);
				text = TooltipText.InsertAfterFirstLine(text, line);
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] tooltip 写总价失败: " + ex);
		}
	}
}
