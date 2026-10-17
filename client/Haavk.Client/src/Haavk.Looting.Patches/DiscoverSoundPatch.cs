using System;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using HarmonyLib;

namespace Haavk.Looting.Patches;

internal static class DiscoverSoundPatch
{
	internal static void Enable(Harmony harmony)
	{
		MethodInfo val = AccessTools.DeclaredMethod(typeof(SinglePlayerSearchContentOperation), "DiscoverItem", (Type[])null, (Type[])null);
		if (val == (MethodInfo)null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 稀有音效挂点① DiscoverItem 没找到，只剩挂点②兜底");
		}
		else
		{
			harmony.Patch((MethodBase)(object)val, (HarmonyMethod)null, new HarmonyMethod(typeof(DiscoverSoundPatch).GetMethod("AfterDiscoverItem", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterDiscoverItem(Item item)
	{
		try
		{
			ChimeSound.OnDiscovered(item);
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 稀有音效挂点① 抛了: " + ex);
		}
	}
}
