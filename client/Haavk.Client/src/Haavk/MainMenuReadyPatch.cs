using System;
using System.Reflection;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using EFT;
using Haavk.Diagnostics;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Haavk;

internal class MainMenuReadyPatch : ModulePatch
{
	private static bool _fired;

	internal static bool Reached => _fired;

	protected override MethodBase GetTargetMethod()
	{
		return (MethodBase)(object)AccessTools.Method(typeof(TarkovApplication), "MainMenu", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	private static void Postfix()
	{
		bool isEnabled;
		try
		{
			if (_fired)
			{
				return;
			}
			_fired = true;
			if (AutoSelectPve.Enabled)
			{
				ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
				BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(11, 1, out isEnabled);
				if (isEnabled)
				{
					bepInExInfoLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
					bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" 自动选档：已到主菜单");
				}
				log.LogInfo(bepInExInfoLogInterpolatedStringHandler);
			}
			HaavkIl2CppPlugin.Instance.AutoClickIfConfigured();
		}
		catch (Exception t)
		{
			ManualLogSource log2 = HaavkIl2CppPlugin.Instance.Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(19, 2, out isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 主菜单挂点 postfix 抛了: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t);
			}
			log2.LogError(bepInExErrorLogInterpolatedStringHandler);
		}
	}
}
