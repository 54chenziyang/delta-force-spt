using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Comfort.Common;
using EFT.HandBook;
using Haavk.Core;
using SPT.Common.Http;
using UnityEngine;

namespace Haavk.WeaponValue;

internal static class PriceClient
{
	private class Payload
	{
		public Dictionary<string, double> Prices;

		public string[] TierColours;

		public double[] ItemPerSlotTiers;

		public string FleaBannedBackgroundColour;

		public bool ColourWeaponBackground;

		public bool ColourFleaBannedWeaponBackground;

		public double[] ArmourClassTiers;

		public Dictionary<string, double> FleaMaxPrices;

		public Dictionary<string, double> FleaMinPrices;

		public Dictionary<string, double> TraderPrices;

		public Dictionary<string, double> TraderAvgPrices;

		public Dictionary<string, double> TraderMinPrices;

		public double[] TraderItemPerSlotTiers;

		public bool ColourItemBackground = true;

		public bool ColourKeyBackground = true;

		public bool ColourArmourBackground;

		public string[] BackgroundColours;

		public int BackgroundColourOffset;

		public Dictionary<string, int> ColourPins;
	}

	[CompilerGenerated]
	private sealed class __c__DisplayClass4_0
	{
		public string[] toRegister;

		public int offset;

		public bool baked;

		internal void _FetchFireAndForget_b__0()
		{
			TaxonomyColourMapping.Register(toRegister, offset, baked);
		}
	}

	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	private struct _FetchFireAndForget_d__4 : IAsyncStateMachine
	{
		public int __1__state;

		public AsyncVoidMethodBuilder __t__builder;

		private __c__DisplayClass4_0 __8__1;

		private TaskAwaiter<string> __u__1;

		public void MoveNext()
		{
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			int num = __1__state;
			try
			{
				try
				{
					TaskAwaiter<string> val;
					if (num != 0)
					{
						__8__1 = new __c__DisplayClass4_0();
						val = RequestHandler.PostJsonAsync("/itemvaluation/weapontotal/prices", "{}").GetAwaiter();
						if (!val.IsCompleted)
						{
							num = (__1__state = 0);
							__u__1 = val;
							__t__builder.AwaitUnsafeOnCompleted<TaskAwaiter<string>, _FetchFireAndForget_d__4>(ref val, ref this);
							return;
						}
					}
					else
					{
						val = __u__1;
						__u__1 = default;
						num = (__1__state = -1);
					}
					Payload payload = JsonConvert.DeserializeObject<Payload>(val.GetResult());
					if (payload == null || payload.Prices == null)
					{
						HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 价格表响应为空，按只有手册价降级");
					}
					else
					{
						__8__1.toRegister = BackgroundColoursToRegister(payload);
						__8__1.offset = payload.BackgroundColourOffset;
						__8__1.baked = payload.BackgroundColours != null && payload.BackgroundColours.Length != 0;
						__c__DisplayClass4_0 __hc = __8__1;
						HaavkCoroutineHost.Post(() =>
						{
							TaxonomyColourMapping.Register(__hc.toRegister, __hc.offset, __hc.baked);
						});
						WeaponValueFeature.SetColourItemBackground(payload.ColourItemBackground);
						WeaponValueFeature.SetColourKeyBackground(payload.ColourKeyBackground);
						WeaponValueFeature.SetColourPins(payload.ColourPins);
						WeaponValueFeature.LoadPrices(PriceSource.跳蚤最高价, payload.FleaMaxPrices);
						WeaponValueFeature.LoadPrices(PriceSource.跳蚤平均价, payload.Prices);
						WeaponValueFeature.LoadPrices(PriceSource.跳蚤最低价, payload.FleaMinPrices);
						WeaponValueFeature.LoadPrices(PriceSource.商人最高价, payload.TraderPrices);
						WeaponValueFeature.LoadPrices(PriceSource.商人平均价, payload.TraderAvgPrices);
						WeaponValueFeature.LoadPrices(PriceSource.商人最低价, payload.TraderMinPrices);
						if (PerSlotColouring.TryCreate(payload.ItemPerSlotTiers, payload.TierColours, payload.FleaBannedBackgroundColour, payload.ColourWeaponBackground, payload.ColourFleaBannedWeaponBackground, out var result, out var error))
						{
							WeaponValueFeature.SetFleaColouring(result);
						}
						else
						{
							HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 每格价取色不可用，底色与数字色都按服务端口径降级: " + error);
						}
						if (ArmourClassColouring.TryCreate(payload.ArmourClassTiers, payload.TierColours, out var result2, out var error2))
						{
							WeaponValueFeature.SetArmourColouring(result2);
							WeaponValueFeature.SetColourArmourBackground(payload.ColourArmourBackground);
						}
						else
						{
							HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 防级取色不可用，关禁售色后禁售护甲按服务端禁售色降级: " + error2);
						}
						string error3 = null;
						if (TiersUsable(payload.TraderItemPerSlotTiers) && PerSlotColouring.TryCreate(payload.TraderItemPerSlotTiers, payload.TierColours, null, payload.ColourWeaponBackground, colourBanned: false, out var result3, out error3))
						{
							WeaponValueFeature.SetTraderColouring(result3);
						}
						else
						{
							HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 商人价模式不可用（F12 开关将无效，留在跳蚤模式）: " + (TiersUsable(payload.TraderItemPerSlotTiers) ? error3 : "服务端没发商人价阈值（TraderItemPerSlotTiers 缺失或全为 0，多半是服务端半边版本旧）"));
						}
						HaavkIl2CppPlugin.Instance.Log.LogInfo(string.Format("{0} 价格表已载入：跳蚤 {1} 件，商人 {2} 件", "[Haavk|WpnValue]", TableCounts(payload.FleaMaxPrices, payload.Prices, payload.FleaMinPrices), TableCounts(payload.TraderPrices, payload.TraderAvgPrices, payload.TraderMinPrices)));
						__8__1 = null;
					}
				}
				catch (Exception ex)
				{
					HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 取价格表失败，按只有手册价降级: " + ex.Message);
				}
			}
			catch (Exception exception)
			{
				__1__state = -2;
				__t__builder.SetException(exception);
				return;
			}
			__1__state = -2;
			__t__builder.SetResult();
		}

		[DebuggerHidden]
		public void SetStateMachine(IAsyncStateMachine stateMachine)
		{
			__t__builder.SetStateMachine(stateMachine);
		}
	}

	private const string Route = "/itemvaluation/weapontotal/prices";

	private static bool _started;

	internal static IEnumerator FetchWhenReady()
	{
		if (!_started)
		{
			_started = true;
			while (!Singleton<Handbook>.Instantiated)
			{
				yield return new WaitForSeconds(1f);
			}
			FetchFireAndForget();
		}
	}

	[AsyncStateMachine(typeof(_FetchFireAndForget_d__4))]
	private static void FetchFireAndForget()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		_FetchFireAndForget_d__4 obj = default;
		obj.__t__builder = AsyncVoidMethodBuilder.Create();
		obj.__1__state = -1;
		obj.__t__builder.Start<_FetchFireAndForget_d__4>(ref obj);
	}

	private static string[] BackgroundColoursToRegister(Payload payload)
	{
		if (payload.BackgroundColours != null && payload.BackgroundColours.Length != 0)
		{
			return payload.BackgroundColours;
		}
		List<string> list = new List<string>();
		if (payload.TierColours != null)
		{
			list.AddRange((IEnumerable<string>)payload.TierColours);
		}
		if (!string.IsNullOrEmpty(payload.FleaBannedBackgroundColour))
		{
			list.Add(payload.FleaBannedBackgroundColour);
		}
		return list.ToArray();
	}

	private static int Count(Dictionary<string, double> table)
	{
		return table?.Count ?? 0;
	}

	private static string TableCounts(Dictionary<string, double> max, Dictionary<string, double> avg, Dictionary<string, double> min)
	{
		int num = Count(max);
		int num2 = Count(avg);
		int num3 = Count(min);
		if (num != num2 || num2 != num3)
		{
			return $"高{num}/平{num2}/低{num3}";
		}
		return num.ToString();
	}

	private static bool TiersUsable(double[] tiers)
	{
		if (tiers == null || tiers.Length != 5)
		{
			return false;
		}
		for (int i = 0; i < tiers.Length; i++)
		{
			if (tiers[i] > 0.0)
			{
				return true;
			}
		}
		return false;
	}
}
