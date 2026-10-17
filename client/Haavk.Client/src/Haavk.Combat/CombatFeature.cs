using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Haavk.Combat.Patches;
using Haavk.Core;
using HarmonyLib;
using UnityEngine;

namespace Haavk.Combat;

internal static class CombatFeature
{
	internal const string Tag = "[Haavk|Combat]";

	private static ConfigEntry<bool> _marker;

	private static ConfigEntry<bool> _armorIcons;

	private static ConfigEntry<float> _uiScale;

	private static ConfigEntry<bool> _sounds;

	private static ConfigEntry<float> _volume;

	private static ConfigEntry<bool> _killNotice;

	private static ConfigEntry<int> _farMeters;

	private static ConfigEntry<bool> _crosshair;

	private static ConfigEntry<CrosshairPlace> _crosshairPlace;

	private static ConfigEntry<CrosshairColor> _crosshairColor;

	private static ConfigEntry<int> _customR;

	private static ConfigEntry<int> _customG;

	private static ConfigEntry<int> _customB;

	private static ConfigEntry<float> _crosshairWidth;

	internal const CrosshairColor DefaultCrosshairColor = CrosshairColor.白;

	internal const float MinCrosshairWidth = 0.3f;

	internal const float MaxCrosshairWidth = 3f;

	private static ConfigEntry<bool> _traceCombat;

	private static readonly HashSet<string> Warned = new HashSet<string>();

	internal const int DefaultFarMeters = 30;

	internal const float DefaultUiScale = 1.2f;

	internal const float MaxVolume = 2f;

	internal static bool CrosshairOn
	{
		get
		{
			if (_crosshair != null)
			{
				return _crosshair.Value;
			}
			return true;
		}
	}

	internal static CrosshairPlace CrosshairAt
	{
		get
		{
			if (_crosshairPlace != null)
			{
				return _crosshairPlace.Value;
			}
			return CrosshairPlace.枪口指向点;
		}
	}

	internal static Color CrosshairColorNow => ToColor((_crosshairColor == null) ? CrosshairColor.白 : _crosshairColor.Value);

	internal static float CrosshairWidth
	{
		get
		{
			if (_crosshairWidth != null)
			{
				return _crosshairWidth.Value;
			}
			return 1f;
		}
	}

	internal static bool TraceOn
	{
		get
		{
			if (_traceCombat != null)
			{
				return _traceCombat.Value;
			}
			return false;
		}
	}

	internal static CombatOptions Options => new CombatOptions
	{
		Marker = (_marker == null || _marker.Value),
		ArmorIcons = (_armorIcons == null || _armorIcons.Value),
		KillNotice = (_killNotice == null || _killNotice.Value),
		Sounds = (_sounds == null || _sounds.Value)
	};

	internal static float FarMeters => (_farMeters == null) ? 30 : _farMeters.Value;

	internal static float UiScale
	{
		get
		{
			if (_uiScale != null)
			{
				return _uiScale.Value;
			}
			return 1.2f;
		}
	}

	internal static float Volume
	{
		get
		{
			if (_volume != null)
			{
				return _volume.Value;
			}
			return 1f;
		}
	}

	internal static string Brief => "在位（挂点 " + DamagePatches.Installed + "/3）";

	private static Color ToColor(CrosshairColor c)
	{
		int num = 200;
		int num2 = Crosshair.Rgb(c, (_customR == null) ? num : _customR.Value, (_customG == null) ? num : _customG.Value, (_customB == null) ? num : _customB.Value);
		return new Color((float)((num2 >> 16) & 0xFF) / 255f, (float)((num2 >> 8) & 0xFF) / 255f, (float)(num2 & 0xFF) / 255f, 1f);
	}

	internal static void Init(ConfigFile config, Harmony harmony)
	{
		_marker = config.Bind("战斗 · 命中反馈", "命中标记", defaultValue: true, new ConfigDescription("打中敌人时在命中点闪一个白色 X，打头是每臂两条线，开镜时画在瞄具红点上\n致命一枪换成橙色 X，收缩后四臂飞散\n手雷或榴弹炸到人时 X 画在屏幕中心，一次爆炸只闪一次", null, new ConfigurationManagerAttributes
		{
			Order = 100
		}));
		_armorIcons = config.Bind("战斗 · 命中反馈", "护甲提示", defaultValue: true, new ConfigDescription("子弹被挡下时，屏幕中心下方出背心或头盔图标\n把护甲打碎出碎甲图标，把头盔打碎出碎头图标，打穿不出图标\n图标文件放在插件目录的 icons 里，没有文件就不画", null, new ConfigurationManagerAttributes
		{
			Order = 90
		}));
		_uiScale = config.Bind("战斗 · 命中反馈", "界面缩放", 1.2f, new ConfigDescription("命中标记、护甲图标、击败提示和准星一起缩放\n分辨率已经自动换算，1 是三角洲录像里的原大小", new AcceptableValueRange<float>(0.5f, 2f), new ConfigurationManagerAttributes
		{
			Order = 80
		}));
		_sounds = config.Bind("战斗 · 音效", "战斗音效", defaultValue: true, new ConfigDescription("命中 击杀 爆头击杀 碎甲 爆炸物命中 爆炸物击杀六种提示音，只有自己打中时响\n文件放在插件目录的 sounds 里，文件名 combat_hit combat_kill 等，缺哪个哪个不响", null, new ConfigurationManagerAttributes
		{
			Order = 100
		}));
		_volume = config.Bind("战斗 · 音效", "战斗音效音量", 1f, new ConfigDescription("与搜刮音效的音量分开调\n1 是音效文件原本的响度，超过 1 是放大，太大会破音", new AcceptableValueRange<float>(0f, 2f), new ConfigurationManagerAttributes
		{
			Order = 90
		}));
		_killNotice = config.Bind("战斗 · 击败提示", "击败提示", defaultValue: true, new ConfigDescription("打死敌人时在准星下方出骷髅和「击败 名字」，打头致死是橙色骷髅加一圈外扩的圆环\n第二行是远距离或近距离多少米，连杀时骷髅往左排成一排\n骷髅图标文件放在插件目录的 icons 里，没有文件就不画骷髅，文字和圆环照常", null, new ConfigurationManagerAttributes
		{
			Order = 100
		}));
		_farMeters = config.Bind("战斗 · 击败提示", "远距离分界", 30, new ConfigDescription("击杀距离达到这么多米显示远距离，不到显示近距离", new AcceptableValueRange<int>(1, 300), new ConfigurationManagerAttributes
		{
			Order = 90
		}));
		_crosshair = config.Bind("战斗 · 准星", "准星", defaultValue: true, new ConfigDescription("腰射时画三角洲式准星：四条短线加中心点，枪种不同大小不同\n蹲下变小、走动张开，冲刺、开镜、手里不是枪、打开背包或菜单时不画", null, new ConfigurationManagerAttributes
		{
			Order = 100
		}));
		_crosshairPlace = config.Bind("战斗 · 准星", "准星位置", CrosshairPlace.枪口指向点, new ConfigDescription("枪口指向点 = 枪管实际指着的地方，也就是子弹会去的地方，会跟着晃动和后坐走\n枪口在眼睛右下方，扫过近处物体的边缘时准星会滑一小段\n屏幕中心 = 三角洲原样，固定在屏幕正中", null, new ConfigurationManagerAttributes
		{
			Order = 90,
			CustomDrawer = ConfigEnumDrawer.For<CrosshairPlace>()
		}));
		_crosshairColor = config.Bind("战斗 · 准星", "准星颜色", CrosshairColor.白, new ConfigDescription("六种预设，或选自定义后用下面三条 RGB 滑条调\n这一行的字就是当前颜色，打开菜单时准星不画，看字就行", null, new ConfigurationManagerAttributes
		{
			Order = 80,
			CustomDrawer = ConfigEnumDrawer.For<CrosshairColor>(ToColor)
		}));
		_customR = BindChannel(config, "战斗 · 准星", "自定义颜色 R", 70);
		_customG = BindChannel(config, "战斗 · 准星", "自定义颜色 G", 69);
		_customB = BindChannel(config, "战斗 · 准星", "自定义颜色 B", 68);
		_crosshairWidth = config.Bind("战斗 · 准星", "准星粗细", 1f, new ConfigDescription("线和中心点一起变粗变细，1 是三角洲的粗细\n拖滑条调，数字框敲不进小数点", new AcceptableValueRange<float>(0.3f, 3f), new ConfigurationManagerAttributes
		{
			Order = 60
		}));
		_traceCombat = config.Bind("Spike", "TraceCombat", defaultValue: false, new ConfigDescription("战斗反馈的逐发诊断日志：每条命中事实一行（攻击者走的哪条路、部位、挡、致死、碎甲），每批反馈一行（画什么、落在哪、响什么），换局时一行各挂点计数与停止原因。验收用，平时别开。由 Scripts\\auto-login.ps1 -DebugCommands 写开", null, new ConfigurationManagerAttributes
		{
			Browsable = false,
			IsAdvanced = true
		}));
		CombatClips.Start();
		CombatIcons.Start();
		DamagePatches.Enable(harmony);
		HaavkCombatHud.Install();
	}

	private static ConfigEntry<int> BindChannel(ConfigFile config, string section, string key, int order)
	{
		return config.Bind(section, key, 200, new ConfigDescription("准星颜色选自定义时用，0 到 255\n三条都是 200 是三角洲录像里量出来的浅灰", new AcceptableValueRange<int>(0, 255), new ConfigurationManagerAttributes
		{
			Order = order
		}));
	}

	internal static void Trace(string msg)
	{
		if (TraceOn)
		{
			HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|Combat] " + msg);
		}
	}

	internal static void WarnOnce(string what, Exception e)
	{
		string data = "[Haavk|Combat] " + what + ((e == null) ? "" : (": " + e));
		if (!Warned.Add(what))
		{
			HaavkIl2CppPlugin.Instance.Log.LogDebug(data);
		}
		else
		{
			HaavkIl2CppPlugin.Instance.Log.LogError(data);
		}
	}

	internal static void ResetWarnings()
	{
		Warned.Clear();
	}
}
