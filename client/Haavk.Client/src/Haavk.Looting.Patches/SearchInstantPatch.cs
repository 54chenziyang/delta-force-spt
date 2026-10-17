using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using Haavk.WeaponValue;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;


namespace Haavk.Looting.Patches;

internal static class SearchInstantPatch
{
	private static bool _failWarned;

	private static readonly Stopwatch Clock = Stopwatch.StartNew();

	private static long _lastTraceMs = -1L;

	private static int _traceSeq;

	internal static void Enable(Harmony harmony)
	{
		MethodInfo val = AccessTools.Method(typeof(ActiveSearchContentOperation), "ExecuteInternal", (System.Type[])null, (System.Type[])null);
		MethodInfo val2 = AccessTools.Method(typeof(ActiveSearchContentOperation), "TryFindUnknownItem", (System.Type[])null, (System.Type[])null);
		if (val == (MethodInfo)null || val2 == (MethodInfo)null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 搜刮挂点解析失败，分级搜刮时长不启用（ExecuteInternal=" + ((val == (MethodInfo)null) ? "null" : "ok") + "，TryFindUnknownItem=" + ((val2 == (MethodInfo)null) ? "null" : "ok") + "）");
		}
		else
		{
			harmony.Patch((MethodBase)(object)val, new HarmonyMethod(typeof(SearchInstantPatch).GetMethod("BeforeExecuteInternal", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			harmony.Patch((MethodBase)(object)val2, new HarmonyMethod(typeof(SearchInstantPatch).GetMethod("BeforeTryFindUnknownItem", (BindingFlags)40)), new HarmonyMethod(typeof(SearchInstantPatch).GetMethod("AfterTryFindUnknownItem", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void BeforeExecuteInternal(ActiveSearchContentOperation __instance)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!(__instance == null))
			{
				bool flag = (__instance.Instant = LootingFeature.SkipInitialDelay);
				if (LootingFeature.TraceSearchTiming)
				{
					DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(21, 4);
					val.AppendLiteral("── 开箱【");
					val.AppendFormatted(DescribeTarget(__instance));
					val.AppendLiteral("】Instant=");
					val.AppendFormatted<bool>(flag);
					val.AppendLiteral("（");
					val.AppendFormatted(flag ? "免等待" : "原版 2 秒");
					val.AppendLiteral("）");
					val.AppendLiteral("，当前 ");
					val.AppendFormatted(LootingFeature.DescribeState());
					Trace(val.ToStringAndClear());
				}
			}
		}
		catch (Exception e)
		{
			Warn("写开箱延时失败", e);
		}
	}

	private static bool BeforeTryFindUnknownItem(ActiveSearchContentOperation __instance, ref bool __result)
	{
		try
		{
			if (!SearchTakeover.ShouldIntercept(__instance))
			{
				return true;
			}
			SearchTakeover.Begin(__instance);
			__result = false;
			return false;
		}
		catch (Exception e)
		{
			Warn("接管拦截失败，放行原方法", e);
			return true;
		}
	}

	private static void AfterTryFindUnknownItem(ActiveSearchContentOperation __instance, ref Item unknownItem, bool __result)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__instance == null)
			{
				return;
			}
			if (SearchTakeover.Enabled)
			{
				SearchTakeover.NoticeOriginalRan(__result);
				return;
			}
			bool traceSearchTiming = LootingFeature.TraceSearchTiming;
			if (!__result)
			{
				if (traceSearchTiming)
				{
					Trace("取空，本箱到此为止");
				}
				ChimeSound.ContainerFinished();
				return;
			}
			__instance.Instant = false;
			int tier = -1;
			if (unknownItem == null || !WeaponValueFeature.TryTier(unknownItem, out tier))
			{
				tier = -1;
			}
			ChimeSound.Fallback(unknownItem, tier);
			if (traceSearchTiming)
			{
				DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(21, 2);
				val.AppendFormatted(SafeName(unknownItem));
				val.AppendLiteral(" 档位 ");
				val.AppendFormatted<int>(tier);
				val.AppendLiteral(" → 原版速度（分级关着或未接管）");
				Trace(val.ToStringAndClear());
			}
		}
		catch (Exception e)
		{
			Warn("逐件 postfix 失败", e);
		}
	}

	private static string DescribeTarget(ActiveSearchContentOperation op)
	{
		//IL_0032: Expected Obj, but got VT
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			SearchableItem item = ((SearchContentOperation)op).Item;
			string text = ((item == null) ? "<null>" : SafeName(item));
			string text2 = ((item == null) ? "?" : ((System.Object)item.TemplateId).ToString());
			bool flag = Il2CppCast.As<SinglePlayerSearchContentOperation>(op) != null;
			DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(11, 4);
			val.AppendFormatted(text);
			val.AppendLiteral(" ");
			val.AppendFormatted(text2);
			val.AppendLiteral(" 单机子类=");
			val.AppendFormatted<bool>(flag);
			val.AppendLiteral(" 首次=");
			val.AppendFormatted<bool>(op._firstSearch);
			return val.ToStringAndClear();
		}
		catch (Exception ex)
		{
			return "取不到被搜对象: " + ex.Message;
		}
	}

	internal static void Trace(string what)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			long elapsedMilliseconds = Clock.ElapsedMilliseconds;
			long num = ((_lastTraceMs < 0) ? 0 : (elapsedMilliseconds - _lastTraceMs));
			_lastTraceMs = elapsedMilliseconds;
			_traceSeq++;
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			object[] buffer = new object[4];
			buffer[0] = "[Haavk|Looting]";
			buffer[1] = (object)_traceSeq;
			buffer[2] = (object)num;
			buffer[3] = what;
			log.LogInfo(string.Format("{0} 追踪#{1} +{2}ms {3}", buffer));
		}
		catch (Exception)
		{
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
