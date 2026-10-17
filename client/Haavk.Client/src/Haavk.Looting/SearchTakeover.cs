using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Diz.Binding;
using EFT;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using Haavk.Core;
using Haavk.Looting.Patches;
using Haavk.WeaponValue;
using Il2CppInterop.Runtime.InteropTypes;

using UnityEngine;

namespace Haavk.Looting;

internal static class SearchTakeover
{
	private sealed class Session
	{
		internal Coroutine Routine;

		internal ActiveSearchContentOperation Op;

		internal PlayerSearchController Psc;

		internal string ItemId;

		internal string Label;

		internal bool Parked;

		internal float Coefficient;
	}

	internal static readonly double[] DefaultSeconds = new double[6] { 1.03, 1.03, 1.12, 1.21, 2.08, 2.25 };

	private const int IndicatorMaxFrames = 30000;

	private static bool _bypass;

	private static readonly Dictionary<nint, Session> Sessions = new Dictionary<nint, Session>();

	private static readonly HashSet<string> RevealingIds = new HashSet<string>((IEqualityComparer<string>)StringComparer.Ordinal);

	private static bool _skipFailWarned;

	private static bool _threadWarned;

	private static bool _parkFailWarned;

	internal static bool Enabled => LootingFeature.TieredSearchTime;

	internal static bool AnyRevealing()
	{
		return RevealingIds.Count > 0;
	}

	internal static bool IsRevealing(string itemId)
	{
		if (itemId != null)
		{
			return RevealingIds.Contains(itemId);
		}
		return false;
	}

	internal static bool ShouldIntercept(ActiveSearchContentOperation op)
	{
		if (_bypass)
		{
			return false;
		}
		if (op == null)
		{
			return false;
		}
		return LootingFeature.TieredSearchTime;
	}

	internal static void Begin(ActiveSearchContentOperation op)
	{
		try
		{
			if (op == null)
			{
				return;
			}
			nint key = (op).Pointer();
			if (Sessions.ContainsKey(key))
			{
				return;
			}
			SearchableItem item = ((SearchContentOperation)op).Item;
			string id = ((item == null) ? null : item.Id);
			if (id != null && RevealingIds.Contains(id))
			{
				LootingFeature.Trace("接管：这一箱已在揭示中，不再起第二条协程");
				return;
			}
			op.Instant = false;
			string label = SafeName(item);
			if (!HaavkCoroutineHost.OnMainThread)
			{
				if (!_threadWarned)
				{
					_threadWarned = true;
					HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|Looting] 接管揭示：拦截点不在主线程上，改走主线程泵（这与 R4d 的判断不符，出现了就去核 SearchContent 的续体在哪跑）");
				}
				HaavkCoroutineHost.Post(() =>
				{
					Launch(op, key, id, label);
				});
			}
			else
			{
				Launch(op, key, id, label);
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 接管揭示起不来: " + ex);
		}
	}

	private static void Launch(ActiveSearchContentOperation op, nint key, string id, string label)
	{
		if (Sessions.ContainsKey(key))
		{
			return;
		}
		Session session = new Session
		{
			Op = op,
			Psc = ResolveController(op),
			ItemId = id,
			Label = label
		};
		if (id != null)
		{
			RevealingIds.Add(id);
		}
		Coroutine coroutine = HaavkCoroutineHost.Run(Reveal(session, key));
		if (coroutine == null)
		{
			if (id != null)
			{
				RevealingIds.Remove(id);
			}
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 接管揭示协程起不来，这一箱不会被揭示（关掉「按价值分级」可恢复原版行为）");
		}
		else
		{
			session.Routine = coroutine;
			Sessions[key] = session;
		}
	}

	private static IEnumerator Reveal(Session s, nint key)
	{
		int revealed = 0;
		yield return null;
		string why;
		while (true)
		{
			if (s.Op == null)
			{
				why = "操作没了";
				break;
			}
			Park(s);
			Item item = TakeNext(s.Op);
			if (item == null)
			{
				why = "取空";
				break;
			}
			float ms = DelayMsFor(item, s);
			if (SearchIndicator.Begin(item, ms))
			{
				float waited = 0f;
				int frames = 0;
				for (; waited < ms; waited += SearchIndicator.Advance())
				{
					if (frames >= 30000)
					{
						break;
					}
					yield return null;
					frames++;
				}
				SearchIndicator.Hide();
			}
			else if (ms > 0f)
			{
				yield return new WaitForSeconds(ms / 1000f);
			}
			else
			{
				yield return null;
			}
			if (!Discover(s.Op, item, ms))
			{
				why = "揭示失败";
				break;
			}
			revealed++;
		}
		Finish(key, s, revealed, why);
	}

	private static Item TakeNext(ActiveSearchContentOperation op)
	{
		_bypass = true;
		try
		{
			Item unknownItem;
			return op.TryFindUnknownItem(out unknownItem) ? unknownItem : null;
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 接管揭示取件失败: " + ex);
			return null;
		}
		finally
		{
			_bypass = false;
		}
	}

	private static float DelayMsFor(Item item, Session s)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			SearchTiming revealTiming = LootingFeature.RevealTiming;
			if (revealTiming == null)
			{
				return 0f;
			}
			float num = (WeaponValueFeature.TryTier(item, out var tier) ? revealTiming.DelayMs(tier) : revealTiming.FallbackMs);
			float num2 = 1f;
			if (LootingFeature.AttentionStillSpeedsUp)
			{
				num2 = SkillCoefficient(s);
				if (num2 > 0f)
				{
					num /= num2;
				}
			}
			if (LootingFeature.TraceSearchTiming)
			{
				object[] buffer = new object[4];
				buffer[0] = SafeName(item);
				buffer[1] = (object)tier;
				buffer[2] = (object)num;
				buffer[3] = ((num2 == 1f) ? "" : $"（技能系数 {num2:F3}）");
				SearchInstantPatch.Trace(string.Format("接管 {0} 档位 {1} → 等 {2:F0}ms{3}", buffer));
			}
			return num;
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 接管揭示算时长失败，按 0 处理: " + ex);
			return 0f;
		}
	}

	private static float SkillCoefficient(Session s)
	{
		if (s.Coefficient > 0f)
		{
			return s.Coefficient;
		}
		float num = 1f;
		try
		{
			Profile profile = s.Op._profile;
			IInventoryProfileSkillInfo inventoryProfileSkillInfo = ((profile == null) ? null : profile.SkillsInfo);
			if (inventoryProfileSkillInfo != null)
			{
				num = 1f + inventoryProfileSkillInfo.AttentionLootSpeedValue;
				if (IsOnCorpse(s.Op))
				{
					num += inventoryProfileSkillInfo.SearchBuffSpeedValue;
				}
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|Looting] 接管：取不到注意力技能系数，按不加速处理: " + ex.Message);
			num = 1f;
		}
		if (num <= 0f)
		{
			num = 1f;
		}
		s.Coefficient = num;
		return num;
	}

	private static bool IsOnCorpse(ActiveSearchContentOperation op)
	{
		try
		{
			SearchableItem item = ((SearchContentOperation)op).Item;
			if (item == null)
			{
				return false;
			}
			IItemOwner ownerOrNull = item.Parent.GetOwnerOrNull();
			if (ownerOrNull == null)
			{
				return false;
			}
			return Il2CppCast.As<InventoryEquipment>(ownerOrNull.RootItem) != null;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool Discover(ActiveSearchContentOperation op, Item item, float ms)
	{
		try
		{
			SinglePlayerSearchContentOperation singlePlayerSearchContentOperation = Il2CppCast.As<SinglePlayerSearchContentOperation>(op);
			if (singlePlayerSearchContentOperation != null)
			{
				singlePlayerSearchContentOperation.DiscoverItem(item);
			}
			else
			{
				op.DiscoverItem(item);
			}
			if (LootingFeature.TraceSearchTiming)
			{
				SearchInstantPatch.Trace($"接管揭示 {SafeName(item)}（等了 {ms:F0}ms）");
			}
			return true;
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 接管揭示失败，这一箱停在这里: " + ex);
			return false;
		}
	}

	private static void Park(Session s)
	{
		if (s.Parked)
		{
			return;
		}
		try
		{
			if (s.Psc == null || s.Op == null)
			{
				return;
			}
			BindableList<SearchContentOperation> searchOperationsList = s.Psc._searchOperations;
			if (searchOperationsList == null)
			{
				return;
			}
			SearchContentOperation searchContentOperation = Il2CppCast.As<SearchContentOperation>(s.Op);
			if (!(searchContentOperation == null))
			{
				if (!searchOperationsList.Contains(searchContentOperation))
				{
					searchOperationsList.Add(searchContentOperation);
				}
				s.Parked = true;
				LootingFeature.Trace("接管：操作已挂回 SearchOperations（转圈动画与计时靠它）");
			}
		}
		catch (Exception ex)
		{
			if (!_parkFailWarned)
			{
				_parkFailWarned = true;
				HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|Looting] 接管：操作挂不回 SearchOperations，搜索转圈动画会一闪而过（揭示本身不受影响）: " + ex.Message);
			}
		}
	}

	private static void Unpark(Session s)
	{
		if (s == null || !s.Parked)
		{
			return;
		}
		s.Parked = false;
		try
		{
			if (s.Psc == null || s.Op == null)
			{
				return;
			}
			BindableList<SearchContentOperation> searchOperationsList = s.Psc._searchOperations;
			if (!(searchOperationsList == null))
			{
				SearchContentOperation searchContentOperation = Il2CppCast.As<SearchContentOperation>(s.Op);
				if (!(searchContentOperation == null))
				{
					searchOperationsList.Remove(searchContentOperation);
				}
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 接管：操作摘不掉！这一局可能再也搜不了东西，关掉「按价值分级」并重开容器试试: " + ex);
		}
	}

	private static PlayerSearchController ResolveController(ActiveSearchContentOperation op)
	{
		try
		{
			return Il2CppCast.As<PlayerSearchController>(op._searchController);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void Finish(nint key, Session s, int revealed, string why)
	{
		try
		{
			Unpark(s);
			Sessions.Remove(key);
			if (s.ItemId != null)
			{
				RevealingIds.Remove(s.ItemId);
			}
			SearchIndicator.Clear();
			if (LootingFeature.TraceSearchTiming)
			{
				SearchInstantPatch.Trace($"接管结束【{s.Label}】共揭示 {revealed} 件（{why}）");
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 接管揭示收尾失败: " + ex);
		}
	}

	internal static void CancelAll()
	{
		int num = 0;
		try
		{
			foreach (Session value in Sessions.Values)
			{
				HaavkCoroutineHost.Stop(value.Routine);
				Unpark(value);
				num++;
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 停接管协程失败: " + ex);
		}
		finally
		{
			Sessions.Clear();
			RevealingIds.Clear();
			SearchIndicator.Clear();
		}
		if (num > 0 && LootingFeature.TraceSearchTiming)
		{
			SearchInstantPatch.Trace($"面板关闭，掐掉 {num} 条在跑的接管协程");
		}
	}

	internal static void NoticeOriginalRan(bool result)
	{
		if (!_bypass && result && !_skipFailWarned)
		{
			_skipFailWarned = true;
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 接管揭示：prefix 没能拦住引擎的循环（TryFindUnknownItem 的原方法仍然跑了）—— 引擎与接管会同时揭示，请关掉「按价值分级」");
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
