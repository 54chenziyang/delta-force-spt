using System;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Haavk.Core;
using Il2CppInterop.Runtime.InteropTypes;
using System.Collections.Generic;
using JsonType;
using UnityEngine;

namespace Haavk.WeaponValue;

internal static class TaxonomyColourMapping
{
	private const int ServerAssumedNamedColourCount = 12;

	private static bool _registered;

	private static int _offset;

	internal static int Offset
	{
		get
		{
			if (_offset <= 0)
			{
				return Enum.GetValues(typeof(TaxonomyColor)).Length;
			}
			return _offset;
		}
	}

	internal static bool Registered => _registered;

	internal static void Register(string[] colours, int offsetFromServer, bool bakedByServer)
	{
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0393: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			int length = Enum.GetValues(typeof(TaxonomyColor)).Length;
			int num = ((offsetFromServer > 0) ? offsetFromServer : length);
			if (offsetFromServer > 0 && offsetFromServer != length)
			{
				ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
				BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(94, 4, out var isEnabled);
				if (isEnabled)
				{
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk|WpnValue]");
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" TaxonomyColor 具名色个数 = ");
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted(length);
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral("，");
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral("而服务端打包时按 ");
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted(offsetFromServer);
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 算。");
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral("本局按服务端的数补键（模板就是这么打的），但服务端的 ");
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral("TaxonomyNamedColourCount 该改成 ");
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted(length);
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 了");
				}
				log.LogError(bepInExErrorLogInterpolatedStringHandler);
			}
			else if (offsetFromServer <= 0 && length != 12)
			{
				ManualLogSource log2 = HaavkIl2CppPlugin.Instance.Log;
				DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(33, 3);
				val.AppendFormatted("[Haavk|WpnValue]");
				val.AppendLiteral(" 服务端没发打包偏移量（版本旧），");
				val.AppendLiteral("而运行期具名色个数 = ");
				val.AppendFormatted<int>(length);
				val.AppendLiteral(" ≠ ");
				val.AppendFormatted<int>(12);
				val.AppendLiteral("，");
				log2.LogError(val.ToStringAndClear() + "自定义底色多半会整体偏色");
			}
			if (colours == null || colours.Length == 0)
			{
				HaavkIl2CppPlugin.Instance.Log.LogDebug("[Haavk|WpnValue] 服务端没烘任何自定义底色，不补键（模板走引擎具名色板）");
				return;
			}
			Dictionary<TaxonomyColor, Color> colorMapping = TaxonomyColorExtension.ColorMapping;
			if (colorMapping == null)
			{
				HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] TaxonomyColorExtension.ColorMapping 取到 null，自定义底色补不了键 —— 服务端若已烘色，仓库会整片空白");
				return;
			}
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			foreach (string text in colours)
			{
				if (!TaxonomyColourCodec.TryEncodeCustomHex(text, num, out var taxonomyValue))
				{
					num3++;
					num4++;
					continue;
				}
				if (!ColorUtility.TryParseHtmlString(text, out var color))
				{
					HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 服务端烘进模板的 " + text + " 解析不出颜色，这一档的格子会空白");
					num3++;
					continue;
				}
				TaxonomyColor key = (TaxonomyColor)taxonomyValue;
				colorMapping[key] = color;
				if (!colorMapping.ContainsKey(key))
				{
					num5++;
				}
				else
				{
					num2++;
				}
			}
			if (num5 > 0)
			{
				ManualLogSource log3 = HaavkIl2CppPlugin.Instance.Log;
				DefaultInterpolatedStringHandler val2 = new DefaultInterpolatedStringHandler(32, 2);
				val2.AppendFormatted("[Haavk|WpnValue]");
				val2.AppendLiteral(" 有 ");
				val2.AppendFormatted<int>(num5);
				val2.AppendLiteral(" 个自定义底色写进 ColorMapping 后读不回来，");
				log3.LogError(val2.ToStringAndClear() + "对应档位的格子会空白");
			}
			if (bakedByServer && num4 > 0)
			{
				ManualLogSource log4 = HaavkIl2CppPlugin.Instance.Log;
				DefaultInterpolatedStringHandler val3 = new DefaultInterpolatedStringHandler(19, 3);
				val3.AppendFormatted("[Haavk|WpnValue]");
				val3.AppendLiteral(" 服务端烘进模板的 ");
				val3.AppendFormatted<int>(colours.Length);
				val3.AppendLiteral(" 种底色里有 ");
				val3.AppendFormatted<int>(num4);
				val3.AppendLiteral(" 种");
				log4.LogError(val3.ToStringAndClear() + "这边认不出是自定义色，没补键 —— 用到它们的格子会空白。客户端与服务端两半必须同版本一起装");
			}
			if (!_registered)
			{
				ManualLogSource log5 = HaavkIl2CppPlugin.Instance.Log;
				object[] buffer = new object[6];
				buffer[0] = "[Haavk|WpnValue]";
				buffer[1] = (object)num2;
				buffer[2] = (object)num3;
				buffer[3] = (object)num;
				buffer[4] = ((offsetFromServer > 0) ? "，服务端口径" : "，运行期口径（服务端版本旧）");
				buffer[5] = (object)colorMapping.Count;
				log5.LogDebug(string.Format("{0} 自定义底色已补键：{1} 个（跳过 {2}，偏移量 {3}{4}），ColorMapping 现有 {5} 项", buffer));
			}
			_registered = true;
			_offset = num;
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 自定义底色补键抛了，模板底色可能显示不正常: " + ex);
		}
	}
}
