using System;
using BepInEx.Configuration;
using Haavk.Core;
using Haavk.WeaponValue;
using UnityEngine;

namespace Haavk.LootSense;

internal static class LootSenseFeature
{
	internal const string Tag = "[Haavk|Sense]";

	internal static ConfigEntry<bool> Enabled;

	internal static ConfigEntry<SenseTrigger> Trigger;

	internal static ConfigEntry<KeyboardShortcut> Key;

	internal static ConfigEntry<bool> DoubleTap;

	internal static ConfigEntry<float> Radius;

	internal static ConfigEntry<float> Duration;

	internal static ConfigEntry<int> MinTier;

	internal static ConfigEntry<bool> ShowLoose;

	internal static ConfigEntry<bool> ShowContainers;

	internal static ConfigEntry<bool> ShowCorpses;

	internal static ConfigEntry<bool> ShowLight;

	internal static ConfigEntry<bool> ShowIcon;

	internal static ConfigEntry<bool> ShowText;

	internal static ConfigEntry<float> LightIntensity;

	internal static ConfigEntry<float> LightRange;

	internal static ConfigEntry<int> MaxLights;

	internal static ConfigEntry<float> MarkerSize;

	private static bool _warned;

	internal static int MinTierClamped => SenseRule.ClampMinTier(MinTier.Value);

	internal static void Init(ConfigFile config)
	{
		Enabled = config.Bind("局内感知 · 触发", "启用局内感知", defaultValue: true, new ConfigDescription("地上的物品 容器 尸体按档位发光，头上显示分类图标和名称价格，只在局内生效\n颜色与格子底色 拾取提示同一把尺，改完当场生效", null, new ConfigurationManagerAttributes
		{
			Order = 100
		}));
		Trigger = config.Bind("局内感知 · 触发", "触发方式", SenseTrigger.按键, new ConfigDescription("按键 = 按下触发键后半径内的物品由近到远亮起，持续一段时间后熄灭\n范围 = 自身半径内的物品一直亮着，走远了自动熄灭", null, new ConfigurationManagerAttributes
		{
			Order = 90,
			CustomDrawer = ConfigEnumDrawer.For<SenseTrigger>()
		}));
		Key = config.Bind("局内感知 · 触发", "触发键", new KeyboardShortcut(KeyCode.F), new ConfigDescription("只在按键模式下有用，F 同时是游戏的交互键，所以默认要双击", null, new ConfigurationManagerAttributes
		{
			Order = 80
		}));
		DoubleTap = config.Bind("局内感知 · 触发", "双击触发", defaultValue: true, new ConfigDescription("只在按键模式下有用，开着时半秒内按两下触发键才触发，单击留给开门和拾取\n两次触发之间至少隔 2 秒", null, new ConfigurationManagerAttributes
		{
			Order = 75
		}));
		Radius = config.Bind("局内感知 · 触发", "感知半径", 10f, new ConfigDescription("单位米，两种模式共用\n楼上楼下的东西不显示，只看脚下 1 米到头顶 3 米之间", new AcceptableValueRange<float>(3f, 30f), new ConfigurationManagerAttributes
		{
			Order = 70
		}));
		Duration = config.Bind("局内感知 · 触发", "持续秒数", 10f, new ConfigDescription("只在按键模式下有用，每件物品亮多久", new AcceptableValueRange<float>(2f, 60f), new ConfigurationManagerAttributes
		{
			Order = 60
		}));
		MinTier = config.Bind("局内感知 · 触发", "从第几档开始显示", 0, new ConfigDescription("档位序号 0 灰白 / 1 绿 / 2 蓝 / 3 紫 / 4 金 / 5 红，低于它的物品光 图标 浮字都不出\n按物品实际显示的颜色算，护甲看防级，弹药看穿深，禁售物品看每格价\n默认 0 全部显示", new AcceptableValueRange<int>(0, 5), new ConfigurationManagerAttributes
		{
			Order = 50
		}));
		ShowLoose = config.Bind("局内感知 · 触发", "散落物", defaultValue: true, new ConfigDescription("地上散落的物品，按它自己的档位发光", null, new ConfigurationManagerAttributes
		{
			Order = 46
		}));
		ShowContainers = config.Bind("局内感知 · 触发", "容器", defaultValue: true, new ConfigDescription("箱子 夹克 抽屉这类容器，按里面最值钱那件的档位发光，没搜过的也算\n名称和价格写的是那一件，后面的 +N 是还有几件同档\n东西被拿走后约 2 秒内跟着变，拿到低于起始档就熄灭", null, new ConfigurationManagerAttributes
		{
			Order = 45
		}));
		ShowCorpses = config.Bind("局内感知 · 触发", "尸体", defaultValue: true, new ConfigDescription("按身上和包里最值钱那件的档位发光，没搜过的也算，安全箱 狗牌 刀鞘 臂章不算\n第一行是阵营，名称和价格写的是那一件", null, new ConfigurationManagerAttributes
		{
			Order = 44
		}));
		ShowLight = config.Bind("局内感知 · 显示", "发光", defaultValue: true, new ConfigDescription("物品上方一盏档位色的点光源，照亮周围一小片地面\n戴着夜视仪时自动熄灭，图标和浮字照常", null, new ConfigurationManagerAttributes
		{
			Order = 50
		}));
		ShowIcon = config.Bind("局内感知 · 显示", "图标", defaultValue: true, new ConfigDescription("物品上方显示它在手册里的分类图标，染成档位色\n图标取自服务端，第一次出现某一类时要下载，可能晚一两帧", null, new ConfigurationManagerAttributes
		{
			Order = 40
		}));
		ShowText = config.Bind("局内感知 · 显示", "浮字", defaultValue: true, new ConfigDescription("图标右边三行字，分类 名称 总价，三行都是档位色\n没检视过的物品名称显示 ???，不显示价格，颜色照样按真实档位\n关掉启用价格显示时不写总价", null, new ConfigurationManagerAttributes
		{
			Order = 30
		}));
		LightIntensity = config.Bind("局内感知 · 显示", "光强度", 3f, new ConfigDescription("点光源亮度，室内偏亮就往下调", new AcceptableValueRange<float>(0f, 8f), new ConfigurationManagerAttributes
		{
			Order = 20
		}));
		MaxLights = config.Bind("局内感知 · 显示", "同时发光上限", 8, new ConfigDescription("只给离你最近的这么多件点灯，其余只显示图标和字\n灯多了会掉帧，范围模式半径开大时尤其明显，0 = 都不点灯", new AcceptableValueRange<int>(0, 40), new ConfigurationManagerAttributes
		{
			Order = 18
		}));
		LightRange = config.Bind("局内感知 · 显示", "光照范围", 3.5f, new ConfigDescription("单位米，点光源能照多远", new AcceptableValueRange<float>(0.5f, 10f), new ConfigurationManagerAttributes
		{
			Order = 15
		}));
		MarkerSize = config.Bind("局内感知 · 显示", "标记大小", 1f, new ConfigDescription("图标和浮字的整体缩放", new AcceptableValueRange<float>(0.5f, 3f), new ConfigurationManagerAttributes
		{
			Order = 10
		}));
		config.SettingChanged += OnSettingChanged;
		HaavkCoroutineHost.Run(SenseDriver.Loop());
	}

	private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
	{
		if (e != null)
		{
			ConfigEntryBase changedSetting = e.ChangedSetting;
			if (changedSetting == Trigger || changedSetting == MinTier || changedSetting == Enabled || changedSetting == ShowLoose || changedSetting == ShowContainers || changedSetting == ShowCorpses || changedSetting == WeaponValueFeature.Source || changedSetting == WeaponValueFeature.BannedColourEnabled || changedSetting == WeaponValueFeature.FleaBannedColour)
			{
				SenseDriver.RequestReset();
			}
		}
	}

	internal static void WarnOnce(string what, Exception e)
	{
		string data = "[Haavk|Sense] " + what + ((e == null) ? "" : (": " + e));
		if (_warned)
		{
			HaavkIl2CppPlugin.Instance.Log.LogDebug(data);
			return;
		}
		_warned = true;
		HaavkIl2CppPlugin.Instance.Log.LogError(data);
	}
}
