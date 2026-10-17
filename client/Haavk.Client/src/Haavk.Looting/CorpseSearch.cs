using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Haavk.Looting;

internal static class CorpseSearch
{
	private static Coroutine _active;

	internal static void Start(PlayerSearchController psc, InventoryEquipment equipment)
	{
		Cancel();
		if (!(psc == null) && !(equipment == null))
		{
			_active = HaavkCoroutineHost.Run(Run(psc, equipment));
		}
	}

	internal static void Cancel()
	{
		if (!(_active == null))
		{
			HaavkCoroutineHost.Stop(_active);
			_active = null;
		}
	}

	private static IEnumerator Run(PlayerSearchController psc, InventoryEquipment equipment)
	{
		List<SearchableItem> fed = new List<SearchableItem>();
		EquipmentSlot[] orderedSlots = LootingFeature.GetOrderedSlots();
		foreach (EquipmentSlot slotType in orderedSlots)
		{
			SearchableItem item = SlotContent(equipment, slotType);
			if (item == null)
			{
				continue;
			}
			if (!NeedsSearch(psc, item))
			{
				DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(8, 1);
				val.AppendLiteral("跳过 ");
				val.AppendFormatted<EquipmentSlot>(slotType);
				val.AppendLiteral("：无需搜索");
				LootingFeature.Trace(val.ToStringAndClear());
				continue;
			}
			while (!SafeCanStart(psc))
			{
				if (CancelledByPlayer(psc, fed))
				{
					LootingFeature.Trace("检测到手动取消，停止后续喂入");
					yield break;
				}
				yield return null;
			}
			if (CancelledByPlayer(psc, fed))
			{
				LootingFeature.Trace("检测到手动取消，停止后续喂入");
				yield break;
			}
			if (!NeedsSearch(psc, item))
			{
				DefaultInterpolatedStringHandler val2 = new DefaultInterpolatedStringHandler(12, 1);
				val2.AppendLiteral("跳过 ");
				val2.AppendFormatted<EquipmentSlot>(slotType);
				val2.AppendLiteral("：等待期间状态已变");
				LootingFeature.Trace(val2.ToStringAndClear());
			}
			else
			{
				if (!Feed(psc, item, slotType))
				{
					yield break;
				}
				fed.Add(item);
			}
		}
		_active = null;
	}

	private static bool Feed(PlayerSearchController psc, SearchableItem item, EquipmentSlot slotType)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			psc.SearchContents(item);
			if (LootingFeature.VerboseLog)
			{
				DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(9, 2);
				val.AppendLiteral("喂入 ");
				val.AppendFormatted<EquipmentSlot>(slotType);
				val.AppendLiteral("，当前并发 ");
				val.AppendFormatted<int>(Il2CppEnum.Count(psc.SearchOperations));
				LootingFeature.Trace(val.ToStringAndClear());
			}
			return true;
		}
		catch (Exception t)
		{
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(16, 3, out var isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk|Looting]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 喂入 ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(slotType);
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 失败，停止自动搜索: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t);
			}
			log.LogError(bepInExErrorLogInterpolatedStringHandler);
			return false;
		}
	}

	private static SearchableItem SlotContent(InventoryEquipment equipment, EquipmentSlot slotType)
	{
		try
		{
			Slot slot = equipment.GetSlot(slotType);
			if (slot == null)
			{
				return null;
			}
			return Il2CppCast.As<SearchableItem>(slot.ContainedItem);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static bool SafeCanStart(PlayerSearchController psc)
	{
		try
		{
			return psc.CanStartNewSearchOperation() && !SearchTakeover.AnyRevealing();
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool NeedsSearch(PlayerSearchController psc, SearchableItem item)
	{
		try
		{
			if (IsBeingSearched(psc, item))
			{
				return false;
			}
			if (!psc.IsSearched(item))
			{
				return true;
			}
			return LootingFeature.ResumePartialSearch && psc.ContainsUnknownItems(item);
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool IsBeingSearched(PlayerSearchController psc, SearchableItem item)
	{
		if (item != null && SearchTakeover.IsRevealing(item.Id))
		{
			return true;
		}
		foreach (SearchContentOperation item2 in Il2CppEnum.ToList<SearchContentOperation>(psc.SearchOperations))
		{
			if (!(item2 == null) && !(item2.Item == null) && item2.Item.Id == item.Id)
			{
				return true;
			}
		}
		return false;
	}

	private static bool CancelledByPlayer(PlayerSearchController psc, List<SearchableItem> fed)
	{
		try
		{
			for (int i = 0; i < fed.Count; i++)
			{
				SearchableItem item = fed[i];
				if (!IsBeingSearched(psc, item) && psc.ContainsUnknownItems(item))
				{
					return true;
				}
			}
		}
		catch (Exception)
		{
		}
		return false;
	}
}
