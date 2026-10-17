using System;
using System.Reflection;
using EFT.UI;
using HarmonyLib;

using UnityEngine;

namespace Haavk.WeaponValue.Patches;

internal static class TooltipDelayPatch
{
	internal static void Enable(Harmony harmony)
	{
		MethodInfo val = AccessTools.Method(typeof(SimpleTooltip), "Show", new System.Type[4] {
			typeof(string),
			typeof(System.Nullable<Vector2>),
			typeof(float),
			typeof(System.Nullable<float>)}, (System.Type[])null);
		if (val == (MethodInfo)null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] SimpleTooltip.Show 解析失败，悬停延迟钳制不启用");
		}
		else
		{
			harmony.Patch((MethodBase)(object)val, new HarmonyMethod(typeof(TooltipDelayPatch).GetMethod("ClampDelay", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void ClampDelay(ref float delay)
	{
		try
		{
			float value = WeaponValueFeature.TooltipDelay.Value;
			if (delay > value)
			{
				delay = value;
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 钳制悬停延迟失败: " + ex);
		}
	}
}
