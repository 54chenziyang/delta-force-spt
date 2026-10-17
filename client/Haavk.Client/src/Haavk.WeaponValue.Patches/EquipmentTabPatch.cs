using System;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;

namespace Haavk.WeaponValue.Patches;

/// <summary>
/// 挂 EquipmentTab.Show 的 Postfix：非局内（仓库 / 藏身处装备架）时，
/// 在个人面板上写一行装备总价值与常规/机密/绝密档位。
/// </summary>
internal static class EquipmentTabPatch
{
	internal static void Enable(Harmony harmony)
	{
		MethodInfo methodInfo = FindShow();
		if (methodInfo == (MethodInfo)null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 找不到 EquipmentTab.Show，仓库个人面板装备总价值不启用");
		}
		else
		{
			harmony.Patch((MethodBase)(object)methodInfo, (HarmonyMethod)null, new HarmonyMethod(typeof(EquipmentTabPatch).GetMethod("Postfix", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static MethodInfo FindShow()
	{
		MethodInfo[] methods = typeof(EquipmentTab).GetMethods((BindingFlags)20);
		foreach (MethodInfo methodInfo in methods)
		{
			if (methodInfo.Name == "Show" && methodInfo.GetParameters().Length == 6)
			{
				return methodInfo;
			}
		}
		return null;
	}

	private static void Postfix(EquipmentTab __instance, InventoryEquipment __1, bool __5)
	{
		try
		{
			if (__5)
			{
				return;
			}
			GearValuePanel.Attach(__instance, __1);
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 仓库个人面板写装备总价值失败: " + ex);
		}
	}
}
