using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Haavk.Combat;
using Haavk.Diagnostics;
using Haavk.Looting;
using Haavk.LootSense;
using Haavk.WeaponValue;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Haavk;

[BepInPlugin("com.haavk", "Haavk", "2.2.0")]
public class HaavkIl2CppPlugin : BasePlugin
{
	internal static HaavkIl2CppPlugin Instance;

	private ConfigEntry<string> _autoClickButton;

	private ConfigEntry<bool> _debugConsoleCommands;

	public override void Load()
	{
		Instance = this;
		_autoClickButton = Config.Bind("Spike", "AutoClickButton", "", new ConfigDescription("主菜单到位后自动点这个按钮（名字片段）。空 = 不点。主菜单上可用的名字：PlayButton / HideoutButton / TradeButton / CharacterButton / ExitButton。由 Scripts\\auto-login.ps1 -OpenScreen 写入。", null, new ConfigurationManagerAttributes
		{
			Browsable = false,
			IsAdvanced = true
		}));
		_debugConsoleCommands = Config.Bind("Spike", "DebugConsoleCommands", defaultValue: false, new ConfigDescription("往 SPT 自带的调试控制台再挂两条验收命令：haavk_hop（跳到下一个容器跟前并对准它）、haavk_cfg（当场改搜刮的开关：skip/graded/chime/trace/vol）。⚠\ufe0f 工装，会真的传送玩家；默认关，由 Scripts\\restart-game.ps1 -DebugCommands 写开。", null, new ConfigurationManagerAttributes
		{
			Browsable = false,
			IsAdvanced = true
		}));
		bool isEnabled;
		try
		{
			HaavkCoroutineHost.Install(this);
		}
		catch (Exception t)
		{
			ManualLogSource log = Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(10, 2, out isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 协程宿主装不上: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t);
			}
			log.LogError(bepInExErrorLogInterpolatedStringHandler);
		}
		try
		{
			((ModulePatch)new MainMenuReadyPatch()).Enable();
		}
		catch (Exception t2)
		{
			ManualLogSource log2 = Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(12, 2, out isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 主菜单挂点安装失败: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t2);
			}
			log2.LogError(bepInExErrorLogInterpolatedStringHandler);
		}
		try
		{
			if (AutoSelectPve.TriggerPresent())
			{
				AutoSelectPve.Enable();
			}
		}
		catch (Exception t3)
		{
			ManualLogSource log3 = Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(11, 2, out isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 自动选档挂载失败: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t3);
			}
			log3.LogError(bepInExErrorLogInterpolatedStringHandler);
		}
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		try
		{
			Harmony harmony = new Harmony("com.haavk");
			if (_debugConsoleCommands != null && _debugConsoleCommands.Value)
			{
				try
				{
					RaidDebugCommands.Enable(harmony);
				}
				catch (Exception t4)
				{
					ManualLogSource log4 = Log;
					BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(14, 2, out isEnabled);
					if (isEnabled)
					{
						bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
						bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 验收控制台命令挂载失败: ");
						bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t4);
					}
					log4.LogError(bepInExErrorLogInterpolatedStringHandler);
				}
			}
			WeaponValueFeature.Init(Config, harmony);
			flag = true;
			try
			{
				LootingFeature.Init(Config, harmony);
				flag2 = true;
			}
			catch (Exception t5)
			{
				ManualLogSource log5 = Log;
				BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(15, 2, out isEnabled);
				if (isEnabled)
				{
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" Looting 启动失败: ");
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t5);
				}
				log5.LogError(bepInExErrorLogInterpolatedStringHandler);
			}
			try
			{
				LootSenseFeature.Init(Config);
				flag3 = true;
			}
			catch (Exception t6)
			{
				ManualLogSource log6 = Log;
				BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(17, 2, out isEnabled);
				if (isEnabled)
				{
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" LootSense 启动失败: ");
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t6);
				}
				log6.LogError(bepInExErrorLogInterpolatedStringHandler);
			}
			try
			{
				CombatFeature.Init(Config, harmony);
				flag4 = true;
			}
			catch (Exception t7)
			{
				ManualLogSource log7 = Log;
				BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(14, 2, out isEnabled);
				if (isEnabled)
				{
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" Combat 启动失败: ");
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t7);
				}
				log7.LogError(bepInExErrorLogInterpolatedStringHandler);
			}
		}
		catch (Exception t8)
		{
			ManualLogSource log8 = Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(19, 2, out isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" WeaponValue 启动失败: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t8);
			}
			log8.LogError(bepInExErrorLogInterpolatedStringHandler);
		}
		ManualLogSource log9 = Log;
		BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(65, 6, out isEnabled);
		if (isEnabled)
		{
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" v");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted("2.2.0");
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" 已启动（SPT 4.1.5 / Mono 移植）：");
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("WeaponValue ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(flag ? "在位" : "失败");
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("、");
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("Looting ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(flag2 ? "在位" : "失败");
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("、");
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("LootSense ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(flag3 ? "在位" : "失败");
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("、");
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("Combat ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(flag4 ? CombatFeature.Brief : "失败");
		}
		log9.LogInfo(bepInExInfoLogInterpolatedStringHandler);
	}

	internal void AutoClickIfConfigured()
	{
		string text = _autoClickButton?.Value;
		if (!string.IsNullOrEmpty(text))
		{
			ManualLogSource log = Log;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(12, 2, out var isEnabled);
			if (isEnabled)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" 准备自动点击按钮 \"");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted(text);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("\"");
			}
			log.LogInfo(bepInExInfoLogInterpolatedStringHandler);
			AutoClickButton.Run(text, (string msg) =>
			{
				Log.LogInfo(msg);
			});
		}
	}
}
