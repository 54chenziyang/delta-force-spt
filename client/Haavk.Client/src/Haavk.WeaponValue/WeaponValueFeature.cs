using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using EFT.InventoryLogic;
using Haavk.Core;
using Haavk.WeaponValue.Patches;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;

using UnityEngine;

namespace Haavk.WeaponValue;

internal static class WeaponValueFeature
{
	internal const string Tag = "[Haavk|WpnValue]";

	private static readonly PriceBook[] Books = CreateBooks();

	private static PerSlotColouring _fleaColouring;

	private static PerSlotColouring _traderColouring;

	private static PriceSource _degradeWarnedFor = (PriceSource)(-1);

	private static Dictionary<string, int> _colourPins = new Dictionary<string, int>();

	internal const string DefaultFleaBannedHex = "#7A0000FF";

	internal static ConfigEntry<Color> FleaBannedColour;

	internal static ConfigEntry<float> TooltipDelay;

	internal static ConfigEntry<PriceLineMode> PriceLine;

	internal static ConfigEntry<bool> BannedColourEnabled;

	internal static ConfigEntry<PriceSource> Source;

	internal static ConfigEntry<bool> Display;

	internal static ConfigEntry<bool> EnableInspect;

	internal static ConfigEntry<bool> EnableTooltip;

	internal static ConfigEntry<bool> EnableBackground;

	internal static ConfigEntry<string> LabelTotal;

	internal static ConfigEntry<string> LabelPerSlot;

	internal static ConfigEntry<string> LabelBanned;

	internal static ConfigEntry<bool> NameColour;

	internal static ConfigEntry<MoneyFormat> Money;

	internal static ConfigEntry<bool> MoneySpace;

	internal static PriceSource EffectiveSource
	{
		get
		{
			if (Source == null)
			{
				return PriceSource.跳蚤平均价;
			}
			PriceSource value = Source.Value;
			if (Books[(int)value].Loaded && (!PriceSources.IsTrader(value) || _traderColouring != null))
			{
				return value;
			}
			if (value != PriceSource.跳蚤平均价 && Books[1].Loaded)
			{
				WarnDegradeOnce(value);
			}
			return PriceSource.跳蚤平均价;
		}
	}

	internal static bool IsTraderSource => PriceSources.IsTrader(EffectiveSource);

	internal static PriceBook Book => Books[(int)EffectiveSource];

	internal static PerSlotColouring Colouring
	{
		get
		{
			if (!IsTraderSource)
			{
				return _fleaColouring;
			}
			return _traderColouring;
		}
	}

	internal static ArmourClassColouring ArmourColouring { get; private set; }

	internal static bool ColourArmourBackground { get; private set; }

	internal static bool ColourItemBackground { get; private set; } = true;

	internal static bool ColourKeyBackground { get; private set; } = true;

	internal static bool NameColourActive
	{
		get
		{
			if (Display.Value)
			{
				return NameColour.Value;
			}
			return false;
		}
	}

	internal static MoneyStyle Style => new MoneyStyle(Money.Value, MoneySpace.Value);

	private static PriceBook[] CreateBooks()
	{
		PriceBook[] array = new PriceBook[6];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = new PriceBook(!PriceSources.IsTrader((PriceSource)i));
		}
		return array;
	}

	internal static void LoadPrices(PriceSource source, Dictionary<string, double> prices)
	{
		if (prices != null)
		{
			Books[(int)source].Load(prices);
		}
	}

	private static void WarnDegradeOnce(PriceSource wanted)
	{
		if (_degradeWarnedFor != wanted)
		{
			_degradeWarnedFor = wanted;
			HaavkIl2CppPlugin.Instance.Log.LogWarning(string.Format("{0} 价格来源「{1}」不可用（服务端没发这张表，或商人尺没构造出来），已回落「跳蚤平均价」。客户端与服务端两半必须同版本一起装。", "[Haavk|WpnValue]", wanted));
		}
	}

	internal static void SetFleaColouring(PerSlotColouring value)
	{
		_fleaColouring = value;
	}

	internal static void SetTraderColouring(PerSlotColouring value)
	{
		_traderColouring = value;
	}

	internal static void SetArmourColouring(ArmourClassColouring value)
	{
		ArmourColouring = value;
	}

	internal static void SetColourArmourBackground(bool value)
	{
		ColourArmourBackground = value;
	}

	internal static void SetColourItemBackground(bool value)
	{
		ColourItemBackground = value;
	}

	internal static void SetColourKeyBackground(bool value)
	{
		ColourKeyBackground = value;
	}

	internal static void SetColourPins(Dictionary<string, int> pins)
	{
		_colourPins = ((pins == null) ? new Dictionary<string, int>() : new Dictionary<string, int>((IDictionary<string, int>)pins, (IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase));
	}

	internal static bool TryPinnedTier(Item item, out int tier)
	{
		//IL_0028: Expected Obj, but got VT
		tier = -1;
		Dictionary<string, int> colourPins = _colourPins;
		if (colourPins.Count == 0 || item == null)
		{
			return false;
		}
		return colourPins.TryGetValue(((System.Object)item.TemplateId).ToString(), out tier);
	}

	internal static bool TryTier(Item item, out int tier)
	{
		tier = -1;
		if (item == null)
		{
			return false;
		}
		if (TryPinnedTier(item, out tier))
		{
			return true;
		}
		PerSlotColouring colouring = Colouring;
		if (colouring == null)
		{
			return false;
		}
		IntVec2 intVec = item.CalculateCellSize();
		tier = colouring.Tier(WeaponTotalCalculator.Total(item), intVec.X, intVec.Y);
		return tier >= 0;
	}

	internal static void Init(ConfigFile config, Harmony harmony)
	{
		EnsureColourConverter();
		TooltipDelay = config.Bind("悬停提示", "悬停提示延迟秒数", 0f, new ConfigDescription("指针悬停到提示框弹出的等待秒数上限，0 = 立即显示，原版约 0.6 秒\n对全游戏的悬停提示生效，物品 交易行 藏身处 任务奖励 buff 图标都算\n只钳低不抬高，改完当场生效", new AcceptableValueRange<float>(0f, 0.6f)));
		PriceLine = config.Bind("悬停提示", "价格行显示模式", PriceLineMode.总价加每格, new ConfigDescription("悬停提示与检视窗口里价格行显示什么，仅每格只看单格价值\n占地 1 格的物品两个数相同，算不出格数时回落总价，改完当场生效", null, new ConfigurationManagerAttributes
		{
			CustomDrawer = ConfigEnumDrawer.For<PriceLineMode>()
		}));
		Display = config.Bind("物品价值 · 显示位置", "启用价格显示", defaultValue: true, "所有价格与档位色的总开关，关掉后不再往检视窗口 悬停提示 格子底色 物品名这四处写东西，下面四项一并失效，方便让别的估价 mod 接管\n⚠\ufe0f 搜刮不受影响，分级搜刮时长和金红物品的叮照常工作\n关掉后枪械的检视窗口没有任何价格，武器那一行只由客户端写，接管方要自己补\n改这一项要重启游戏");
		EnableInspect = config.Bind("物品价值 · 显示位置", "检视窗口显示价格", defaultValue: true, "检视窗口描述区顶部显示含全部配件的实时总价");
		EnableTooltip = config.Bind("物品价值 · 显示位置", "悬停提示显示价格", defaultValue: true, "悬停任意物品时提示框里显示实时总价，枪械含全部配件不含子弹，护甲/胸挂含槽内组件不含格内物品，可堆叠物按堆叠数计");
		EnableBackground = config.Bind("物品价值 · 显示位置", "格子底色按实例总价", defaultValue: true, "枪械格子的底色按实例总价除以当前占格数上色，与普通物品的每格价共用一套阈值\n护甲/弹挂/头盔按实例里最高防级上色，换插板当场跟着变\n非武器的禁售格改用下面的跳蚤禁售颜色\n关掉则整个回到服务端写的模板级底色，改这一项要重启游戏");
		NameColour = config.Bind("物品价值 · 显示位置", "物品名按档位染色", defaultValue: true, "物品名跟着它的格子底色变色，两处 ① 准星指着地面散落物时屏幕中下方的拾取提示 ② 悬停提示框首行\n尺子与格子底色相同，枪械按实例总价，护甲按最高防级，弹药按穿深，其余按每格价，禁售物品跟随跳蚤禁售颜色\n未检视的物品照样染色，尸体和错误类提示不染，算不出档位时保持原色\n改完当场生效，② 还要求悬停提示显示价格是开着的");
		LabelTotal = config.Bind("物品价值 · 文案", "总价标签", "总价", "总价那一行的前缀，与服务端 config.json 里的文案保持一致即可");
		LabelPerSlot = config.Bind("物品价值 · 文案", "每格标签", "每格", "每格那一段的前缀，英文用户可改成 Per Slot，占地 1 格的物品不显示这一段");
		LabelBanned = config.Bind("物品价值 · 文案", "禁售标签", "禁售", "枪械跳蚤禁售时额外加的标记");
		Money = config.Bind("物品价值 · 文案", "金额格式", MoneyFormat.原样, new ConfigDescription("价格数字的写法，悬停提示与检视窗口同时生效，改完当场生效\n原样 ₽172,058 ｜ 万w ₽ 17.2w ｜ 万汉字 ₽ 17.2万 ｜ K与M ₽ 172.1K\n万和 K 只在数够大时才换单位，万 ≥ 10000，K ≥ 1000，不够就用逗号分组\n所以一行里一半带单位一半不带是正常的", null, new ConfigurationManagerAttributes
		{
			CustomDrawer = ConfigEnumDrawer.For<MoneyFormat>()
		}));
		MoneySpace = config.Bind("物品价值 · 文案", "符号后加空格", defaultValue: true, "₽ 与数字之间加一个空格，四种金额格式都生效，改完当场生效");
		FleaBannedColour = config.Bind("物品价值 · 颜色", "跳蚤禁售颜色", ParseHex("#7A0000FF"), "跳蚤禁售物品的标记色，右边的重置按钮一键还原出厂色 #7A0000FF\n作用范围是禁售物品的格子底色和悬停/检视里的禁售二字，改完当场生效\n格子底色那半边要求格子底色按实例总价是开着的，非武器格子还要求服务端本来就把它画成了禁售色\n服务端写进物品详情行的禁售二字管不到，那个由 config.json 的 FleaBannedTextColour 决定\nA 通道不生效，格子底色浓淡由引擎钉死 0.30");
		BannedColourEnabled = config.Bind("物品价值 · 颜色", "启用跳蚤禁售色", defaultValue: true, "关掉后禁售物品的格子底色改按本来的档位显色，护甲/插板/头盔按防级取最高 6 级红，其余物品按每格价\n悬停/检视里的禁售二字照旧显示，改完当场生效");
		Source = config.Bind("物品价值 · 价格来源", "价格来源", PriceSource.跳蚤平均价, new ConfigDescription("整把尺按选中的口径换轨，悬停价 检视价 格子底色 搜刮时长 稀有音效档位全部跟着变\n跳蚤三档来自当前挂单的单件价，取最高/平均/最低\n商人三档是能收这件东西的商人的报价，模板级满耐久价按 1 级忠诚度，不含 Fence\n选商人口径时跳蚤禁售整体失效，不画禁售底色也不显禁售二字，启用跳蚤禁售色开关无效\n弹药穿深色与护甲防级色不受影响\n服务端没发对应表时自动回落跳蚤平均价，改完当场生效", null, new ConfigurationManagerAttributes
		{
			CustomDrawer = ConfigEnumDrawer.For<PriceSource>()
		}));
		TooltipDelayPatch.Enable(harmony);
		if (!Display.Value)
		{
			HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|WpnValue] 价格显示总开关已关闭：不写检视/悬停/格子底色/物品名，价格表仍照常拉取（搜刮分级与稀有音效不受影响）");
		}
		if (Display.Value && EnableInspect.Value)
		{
			InspectPanelPatch.Enable(harmony);
		}
		if (Display.Value && EnableTooltip.Value)
		{
			GridTooltipPatch.Enable(harmony);
		}
		if (Display.Value && EnableBackground.Value)
		{
			GridBackgroundPatch.Enable(harmony);
		}
		GroundNamePatch.Enable(harmony);
		FleaQualityFilterPatch.Enable(harmony);
		WeaponModdingQuickBuildPatch.Enable(harmony);
		if (Display.Value)
		{
			EquipmentTabPatch.Enable(harmony);
		}
		config.SettingChanged += OnSettingChanged;
		HaavkCoroutineHost.Run(PriceClient.FetchWhenReady());
	}

	private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
	{
		if (e != null && (e.ChangedSetting == FleaBannedColour || e.ChangedSetting == BannedColourEnabled || e.ChangedSetting == Source))
		{
			GridBackgroundPatch.RequestRepaint();
		}
	}

	private static void EnsureColourConverter()
	{
		if (!TomlTypeConverter.CanConvert(typeof(Color)))
		{
			TomlTypeConverter.AddConverter(typeof(Color), new TypeConverter
			{
				ConvertToString = (object obj, System.Type type) => "#" + ColorUtility.ToHtmlStringRGBA((Color)obj),
				ConvertToObject = (string str, System.Type type) => ColorUtility.TryParseHtmlString(str, out var color) ? color : ParseHex("#7A0000FF")
			});
		}
	}

	private static Color ParseHex(string hex)
	{
		ColorUtility.TryParseHtmlString(hex, out var color);
		return color;
	}
}
