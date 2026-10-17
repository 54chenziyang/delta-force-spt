using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SPTarkov.Server.Core.Helpers.Ragfair;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Ragfair;

namespace Haavk.Server;

/// <summary>
/// 跳蚤市场「品质标签」的服务端半边。
/// 客户端点品质标签时会把 FilterRule.HandbookId 设成一个哨兵 MongoId（见 <see cref="Sentinel" />），
/// 这里在原生分类筛选之后，把结果整个替换成「该品质档的全部模板」，于是报价列表只会出这一档的物品。
/// 非哨兵（正常分类、名字搜索等）原样放行。
/// </summary>
internal static class RagfairTierFilterPatch
{
	private static Harmony _harmony;

	/// <summary>
	/// 哨兵 HandbookId：'a' + 档位数字 + 22 个 '0'。严格 24 位十六进制，能通过 MongoId 的解析与 IsEmpty 判断。
	/// </summary>
	internal static string Sentinel(int tier)
	{
		return "a" + (char)(48 + tier) + new string('0', 22);
	}

	/// <summary>
	/// 命中哨兵则返回品质档（0..5），否则返回 null。
	/// </summary>
	internal static int? ParseSentinel(string id)
	{
		if (string.IsNullOrEmpty(id) || id.Length != 24 || id[0] != 'a' || id[1] < '0' || id[1] > '5')
		{
			return null;
		}
		for (int i = 2; i < 24; i++)
		{
			if (id[i] != '0')
			{
				return null;
			}
		}
		return id[1] - 48;
	}

	internal static void Enable()
	{
		MethodInfo target = AccessTools.Method(typeof(RagfairHelper), "FilterCategories", new Type[2]
		{
			typeof(MongoId),
			typeof(SearchRequestData)
		});
		if ((object)target == null)
		{
			throw new InvalidOperationException("找不到 RagfairHelper.FilterCategories");
		}
		_harmony = new Harmony("com.haavk.server.ragfairtier");
		_harmony.Patch(target, (HarmonyMethod)null, new HarmonyMethod(AccessTools.Method(typeof(RagfairTierFilterPatch), "Postfix", (Type[])null, (Type[])null)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
	}

	private static void Postfix(SearchRequestData request, ref List<MongoId> __result)
	{
		try
		{
			MongoId? handbookId = request?.HandbookId;
			if (!handbookId.HasValue)
			{
				return;
			}
			int? tier = ParseSentinel(handbookId.Value.ToString());
			if (!tier.HasValue)
			{
				return;
			}
			List<MongoId> list = new List<MongoId>();
			foreach (KeyValuePair<string, int> templateTier in HaavkServerMod.TemplateTiers)
			{
				if (templateTier.Value == tier.Value)
				{
					list.Add(new MongoId(templateTier.Key));
				}
			}
			__result = list;
		}
		catch (Exception)
		{
			// 出任何问题都放行原生结果，别把跳蚤搜索打崩。
		}
	}
}
