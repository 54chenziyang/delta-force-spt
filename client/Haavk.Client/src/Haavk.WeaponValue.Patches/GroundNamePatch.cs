using System;
using System.Reflection;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;

namespace Haavk.WeaponValue.Patches;

internal static class GroundNamePatch
{
	private const string StateHandler = "AvailableInteractionStateChangedHandler";

	private static bool _originalCaptured;

	private static Color _originalColour;

	private static bool _failWarned;

	internal static void Enable(Harmony harmony)
	{
		MethodInfo val = AccessTools.Method(typeof(ActionPanel), "AvailableInteractionStateChangedHandler", (Type[])null, (Type[])null);
		if (val == (MethodInfo)null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 找不到 ActionPanel.AvailableInteractionStateChangedHandler，地面物品名染色不启用");
		}
		else
		{
			harmony.Patch((MethodBase)(object)val, (HarmonyMethod)null, new HarmonyMethod(typeof(GroundNamePatch).GetMethod("Postfix", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Postfix(ActionPanel __instance)
	{
		TMP_Text tMP_Text = null;
		try
		{
			tMP_Text = __instance._itemName;
			if (!(tMP_Text == null))
			{
				if (!_originalCaptured)
				{
					_originalCaptured = true;
					_originalColour = tMP_Text.color;
				}
				tMP_Text.color = (TryGroundColour(__instance, out var colour) ? colour : _originalColour);
			}
		}
		catch (Exception ex)
		{
			try
			{
				if (tMP_Text != null && _originalCaptured)
				{
					tMP_Text.color = _originalColour;
				}
			}
			catch
			{
			}
			string data = "[Haavk|WpnValue] 地面物品名染色失败: " + ex;
			if (_failWarned)
			{
				HaavkIl2CppPlugin.Instance.Log.LogDebug(data);
				return;
			}
			_failWarned = true;
			HaavkIl2CppPlugin.Instance.Log.LogError(data);
		}
	}

	private static bool TryGroundColour(ActionPanel panel, out Color colour)
	{
		colour = default;
		if (!WeaponValueFeature.NameColourActive)
		{
			return false;
		}
		GamePlayerOwner owner = PrivAccess.ActionPanelOwner(panel);
		if ((UnityEngine.Object)(object)owner == null)
		{
			return false;
		}
		Player player = owner.Player;
		if (player == null)
		{
			return false;
		}
		LootItem lootItem = Il2CppCast.As<LootItem>(player.InteractableObject);
		if (lootItem == null)
		{
			return false;
		}
		ItemController itemOwner = lootItem.ItemOwner;
		if (itemOwner == null)
		{
			return false;
		}
		Item rootItem = itemOwner.RootItem;
		if (rootItem == null)
		{
			return false;
		}
		if (Il2CppCast.As<InventoryEquipment>(rootItem) != null)
		{
			return false;
		}
		if (!GridBackgroundPatch.TryCellColour(rootItem, out colour))
		{
			return false;
		}
		return true;
	}
}
