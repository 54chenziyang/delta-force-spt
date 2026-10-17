using System;
using System.Collections;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using Haavk.Core;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

using UnityEngine;

namespace Haavk.Looting;

internal static class AutoExamine
{
	private static readonly EquipmentSlot[] VisibleSlots = new EquipmentSlot[13]
	{
		EquipmentSlot.FirstPrimaryWeapon,
		EquipmentSlot.SecondPrimaryWeapon,
		EquipmentSlot.Holster,
		EquipmentSlot.ArmorVest,
		EquipmentSlot.Headwear,
		EquipmentSlot.TacticalVest,
		EquipmentSlot.Backpack,
		EquipmentSlot.Earpiece,
		EquipmentSlot.Eyewear,
		EquipmentSlot.FaceCover,
		EquipmentSlot.Scabbard,
		EquipmentSlot.ArmBand,
		EquipmentSlot.Dogtag
	};

	private const int StallWarnFrames = 600;

	private static readonly ExamineQueue<Item> Queue = new ExamineQueue<Item>();

	private static InventoryController _controller;

	private static PlayerSearchController _searchController;

	private static System.Action<Item> _onItemFound;

	private static Coroutine _consumer;

	private static bool _stallWarned;

	internal static void OnPanelOpened(InventoryController controller, PlayerSearchController searchController, InventoryEquipment corpseEquipment)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		if (!LootingFeature.AutoExamineEnabled || controller == null || searchController == null)
		{
			return;
		}
		Stop();
		try
		{
			_controller = controller;
			_searchController = searchController;
			_stallWarned = false;
			_onItemFound = (new System.Action<Item>(OnItemFound));
			((SearchController)_searchController).OnItemFound += _onItemFound;
			if (corpseEquipment != null)
			{
				EnqueueVisibleEquipment(corpseEquipment);
			}
			_consumer = HaavkCoroutineHost.Run(Consume());
			DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(15, 1);
			val.AppendLiteral("自动检视启动，T2 入队 ");
			val.AppendFormatted<int>(Queue.Count);
			val.AppendLiteral(" 件");
			LootingFeature.Trace(val.ToStringAndClear());
		}
		catch (Exception t)
		{
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(11, 2, out var isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk|Looting]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 自动检视启动失败: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t);
			}
			log.LogError(bepInExErrorLogInterpolatedStringHandler);
			Stop();
		}
	}

	internal static void Stop()
	{
		try
		{
			if (_searchController != null && (System.Delegate)(object)_onItemFound != (System.Delegate)null)
			{
				((SearchController)_searchController).OnItemFound -= _onItemFound;
			}
		}
		catch (Exception ex)
		{
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(20, 2, out var isEnabled);
			if (isEnabled)
			{
				bepInExWarningLogInterpolatedStringHandler.AppendFormatted("[Haavk|Looting]");
				bepInExWarningLogInterpolatedStringHandler.AppendLiteral(" 退订 OnItemFound 失败: ");
				bepInExWarningLogInterpolatedStringHandler.AppendFormatted(ex.Message);
			}
			log.LogWarning(bepInExWarningLogInterpolatedStringHandler);
		}
		_onItemFound = null;
		_searchController = null;
		HaavkCoroutineHost.Stop(_consumer);
		_consumer = null;
		Queue.Clear();
		_controller = null;
	}

	private static void EnqueueVisibleEquipment(InventoryEquipment equipment)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		for (int i = 0; i < VisibleSlots.Length; i++)
		{
			try
			{
				Slot slot = equipment.GetSlot(VisibleSlots[i]);
				if (!(slot == null))
				{
					Item containedItem = slot.ContainedItem;
					DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(4, 1);
					val.AppendLiteral("装备槽 ");
					val.AppendFormatted<EquipmentSlot>(VisibleSlots[i]);
					TryEnqueue(containedItem, val.ToStringAndClear());
				}
			}
			catch (Exception)
			{
			}
		}
	}

	private static void OnItemFound(Item item)
	{
		if (!(_controller == null))
		{
			TryEnqueue(item, "搜索揭示");
		}
	}

	private static void TryEnqueue(Item item, string source)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		if (item == null)
		{
			return;
		}
		try
		{
			if (_controller.Examined(item))
			{
				DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(12, 2);
				val.AppendLiteral("检视跳过（");
				val.AppendFormatted(source);
				val.AppendLiteral("）：");
				val.AppendFormatted(SafeName(item));
				val.AppendLiteral(" 已检视过");
				LootingFeature.Trace(val.ToStringAndClear());
			}
			else if (Queue.Enqueue(item.Id, item))
			{
				LootingFeature.Trace("检视入队（" + source + "）：" + SafeName(item));
			}
		}
		catch (Exception ex)
		{
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(9, 2, out var isEnabled);
			if (isEnabled)
			{
				bepInExWarningLogInterpolatedStringHandler.AppendFormatted("[Haavk|Looting]");
				bepInExWarningLogInterpolatedStringHandler.AppendLiteral(" 检视入队失败: ");
				bepInExWarningLogInterpolatedStringHandler.AppendFormatted(ex.Message);
			}
			log.LogWarning(bepInExWarningLogInterpolatedStringHandler);
		}
	}

	private static IEnumerator Consume()
	{
		while (true)
		{
			if (!Queue.TryDequeue(out var item))
			{
				yield return null;
				continue;
			}
			if (_controller == null)
			{
				break;
			}
			if (SafeExamined(item))
			{
				continue;
			}
			int waited = 0;
			while (SafeExamining())
			{
				waited++;
				if (waited == 600 && !_stallWarned)
				{
					_stallWarned = true;
					ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
					DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(24, 2);
					val.AppendFormatted("[Haavk|Looting]");
					val.AppendLiteral(" 等待 _examining 复位已超过 ");
					val.AppendFormatted<int>(600);
					val.AppendLiteral(" 帧。");
					log.LogWarning(val.ToStringAndClear() + "原生失败路径不复位该字段，本局检视可能已卡死；关闭面板即可停止等待。");
				}
				yield return null;
			}
			if (_controller == null)
			{
				break;
			}
			if (!SafeExamined(item))
			{
				if (!FeedExamine(item))
				{
					break;
				}
				item = null;
			}
		}
	}

	private static bool FeedExamine(Item item)
	{
		try
		{
			ItemUiContext instance = ItemUiContext.Instance;
			if ((UnityEngine.Object)(object)instance == null)
			{
				ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
				BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(35, 1, out var isEnabled);
				if (isEnabled)
				{
					bepInExWarningLogInterpolatedStringHandler.AppendFormatted("[Haavk|Looting]");
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral(" ItemUiContext.Instance 为空，自动检视本轮停止");
				}
				log.LogWarning(bepInExWarningLogInterpolatedStringHandler);
				return false;
			}
			instance.Examine(item, (System.Action)null);
			LootingFeature.Trace("检视喂入：" + SafeName(item));
			return true;
		}
		catch (Exception t)
		{
			ManualLogSource log2 = HaavkIl2CppPlugin.Instance.Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(16, 2, out var isEnabled2);
			if (isEnabled2)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk|Looting]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 自动检视喂入失败，本轮停止: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t);
			}
			log2.LogError(bepInExErrorLogInterpolatedStringHandler);
			return false;
		}
	}

	private static bool SafeExamined(Item item)
	{
		try
		{
			return _controller == null || _controller.Examined(item);
		}
		catch (Exception)
		{
			return true;
		}
	}

	private static bool SafeExamining()
	{
		try
		{
			return _controller != null && _controller._examining;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static string SafeName(Item item)
	{
		//IL_002e: Expected Obj, but got VT
		try
		{
			return (item == null) ? "<null>" : item.LocalizedShortName();
		}
		catch (Exception)
		{
			return (item == null) ? "<null>" : ((System.Object)item.TemplateId).ToString();
		}
	}
}
