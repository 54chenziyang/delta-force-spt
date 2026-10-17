using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;

namespace Haavk.Looting.Patches;

internal static class InventoryScreenPatch
{
	private static bool _failWarned;

	internal static void Enable(Harmony harmony)
	{
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		MethodInfo val = Enumerable.FirstOrDefault(typeof(InventoryScreen).GetMethods((BindingFlags)20), (MethodInfo m) => ((MemberInfo)m).Name == "Show" && Enumerable.Any(((MethodBase)m).GetParameters(), (ParameterInfo p) => p.ParameterType == typeof(CompoundItem)));
		MethodInfo val2 = AccessTools.Method(typeof(InventoryScreen), "Close", (Type[])null, (Type[])null);
		if (val == (MethodInfo)null || val2 == (MethodInfo)null)
		{
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			DefaultInterpolatedStringHandler val3 = new DefaultInterpolatedStringHandler(14, 2);
			val3.AppendLiteral("（Show=");
			val3.AppendFormatted((val == (MethodInfo)null) ? "null" : "ok");
			val3.AppendLiteral("，Close=");
			val3.AppendFormatted((val2 == (MethodInfo)null) ? "null" : "ok");
			val3.AppendLiteral("）");
			log.LogError("[Haavk|Looting] 物品面板挂点解析失败，尸体自动搜索/续搜/自动检视不启用" + val3.ToStringAndClear());
		}
		else
		{
			harmony.Patch((MethodBase)(object)val, (HarmonyMethod)null, new HarmonyMethod(typeof(InventoryScreenPatch).GetMethod("AfterShow", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			harmony.Patch((MethodBase)(object)val2, new HarmonyMethod(typeof(InventoryScreenPatch).GetMethod("BeforeClose", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterShow(InventoryController controller, CompoundItem lootItem)
	{
		try
		{
			if (controller == null || lootItem == null)
			{
				return;
			}
			PlayerSearchController playerSearchController = Il2CppCast.As<PlayerSearchController>(controller.SearchController);
			if (playerSearchController == null)
			{
				return;
			}
			InventoryEquipment inventoryEquipment = Il2CppCast.As<InventoryEquipment>(lootItem);
			bool flag = inventoryEquipment != null && IsOwnEquipment(controller, inventoryEquipment);
			if (inventoryEquipment != null)
			{
				if (!flag)
				{
					if (LootingFeature.CorpseSearchEnabled)
					{
						LootingFeature.Trace("命中尸体：root=InventoryEquipment，计划槽序 " + string.Join("/", LootingFeature.GetOrderedSlots()));
						CorpseSearch.Start(playerSearchController, inventoryEquipment);
					}
					AutoExamine.OnPanelOpened(controller, playerSearchController, inventoryEquipment);
				}
			}
			else
			{
				SearchableItem searchableItem = Il2CppCast.As<SearchableItem>(lootItem);
				if (LootingFeature.ResumeContainerSearch && searchableItem != null && !SearchTakeover.IsRevealing(searchableItem.Id) && playerSearchController.IsSearched(searchableItem) && playerSearchController.ContainsUnknownItems(searchableItem) && playerSearchController.CanStartNewSearchOperation())
				{
					LootingFeature.Trace("续搜场景容器");
					playerSearchController.SearchContents(searchableItem);
				}
				AutoExamine.OnPanelOpened(controller, playerSearchController, null);
			}
		}
		catch (Exception e)
		{
			Warn("物品面板 postfix 失败", e);
		}
	}

	private static void BeforeClose()
	{
		try
		{
			CorpseSearch.Cancel();
			AutoExamine.Stop();
			SearchTakeover.CancelAll();
		}
		catch (Exception e)
		{
			Warn("物品面板 Close prefix 失败", e);
		}
	}

	private static bool IsOwnEquipment(InventoryController controller, InventoryEquipment equipment)
	{
		try
		{
			Player.PlayerInventoryController playerInventoryController = Il2CppCast.As<Player.PlayerInventoryController>(controller);
			if (playerInventoryController == null || playerInventoryController.Inventory == null)
			{
				return false;
			}
			InventoryEquipment equipment2 = playerInventoryController.Inventory.Equipment;
			return equipment2 != null && equipment2.Id == equipment.Id;
		}
		catch (Exception)
		{
			return true;
		}
	}

	private static void Warn(string what, Exception e)
	{
		string data = "[Haavk|Looting] " + what + ": " + e;
		if (_failWarned)
		{
			HaavkIl2CppPlugin.Instance.Log.LogDebug(data);
			return;
		}
		_failWarned = true;
		HaavkIl2CppPlugin.Instance.Log.LogError(data);
	}
}
