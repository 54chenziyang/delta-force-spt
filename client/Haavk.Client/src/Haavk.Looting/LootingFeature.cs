using System;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using EFT.InventoryLogic;
using Haavk.Core;
using Haavk.Looting.Patches;
using HarmonyLib;

namespace Haavk.Looting;

internal static class LootingFeature
{
	internal const string Tag = "[Haavk|Looting]";

	private static ConfigFile _configFile;

	private static ConfigEntry<bool> _skipInitialDelay;

	private static ConfigEntry<bool> _tieredSearchTime;

	private static ConfigEntry<float>[] _tierSeconds;

	private static ConfigEntry<bool> _attentionStillSpeedsUp;

	private static ConfigEntry<bool> _traceSearchTiming;

	private static ConfigEntry<bool> _cellIndicator;

	private static ConfigEntry<float> _indicatorSpin;

	private static ConfigEntry<float> _indicatorOrbit;

	private static ConfigEntry<float> _indicatorScale;

	private static ConfigEntry<bool> _corpseSearchEnabled;

	private static ConfigEntry<bool> _autoExamineEnabled;

	private static ConfigEntry<int> _tacticalVestOrder;

	private static ConfigEntry<int> _pocketsOrder;

	private static ConfigEntry<int> _backpackOrder;

	private static ConfigEntry<bool> _resumePartialSearch;

	private static ConfigEntry<bool> _resumeContainerSearch;

	private static ConfigEntry<bool> _rareItemChime;

	private static ConfigEntry<int> _chimeMinTier;

	private static ConfigEntry<float> _chimeVolume;

	private static ConfigEntry<bool> _verboseLog;

	internal static bool SkipInitialDelay
	{
		get
		{
			if (_skipInitialDelay != null)
			{
				return _skipInitialDelay.Value;
			}
			return false;
		}
	}

	internal static bool TieredSearchTime
	{
		get
		{
			if (_tieredSearchTime != null)
			{
				return _tieredSearchTime.Value;
			}
			return false;
		}
	}

	internal static bool TraceSearchTiming
	{
		get
		{
			if (_traceSearchTiming != null)
			{
				return _traceSearchTiming.Value;
			}
			return false;
		}
	}

	internal static bool CellIndicator
	{
		get
		{
			if (_cellIndicator != null)
			{
				return _cellIndicator.Value;
			}
			return false;
		}
	}

	internal static float IndicatorSpinSpeed
	{
		get
		{
			if (_indicatorSpin != null)
			{
				return _indicatorSpin.Value;
			}
			return 320f;
		}
	}

	internal static float IndicatorOrbit
	{
		get
		{
			if (_indicatorOrbit != null)
			{
				return _indicatorOrbit.Value;
			}
			return 0.09f;
		}
	}

	internal static float IndicatorScale
	{
		get
		{
			if (_indicatorScale != null)
			{
				return _indicatorScale.Value;
			}
			return 0.55f;
		}
	}

	internal static SearchTiming RevealTiming { get; private set; }

	internal static bool AttentionStillSpeedsUp
	{
		get
		{
			if (_attentionStillSpeedsUp != null)
			{
				return _attentionStillSpeedsUp.Value;
			}
			return false;
		}
	}

	internal static bool CorpseSearchEnabled
	{
		get
		{
			if (_corpseSearchEnabled != null)
			{
				return _corpseSearchEnabled.Value;
			}
			return false;
		}
	}

	internal static bool AutoExamineEnabled
	{
		get
		{
			if (_autoExamineEnabled != null)
			{
				return _autoExamineEnabled.Value;
			}
			return false;
		}
	}

	internal static bool ResumePartialSearch
	{
		get
		{
			if (_resumePartialSearch != null)
			{
				return _resumePartialSearch.Value;
			}
			return false;
		}
	}

	internal static bool ResumeContainerSearch
	{
		get
		{
			if (_resumeContainerSearch != null)
			{
				return _resumeContainerSearch.Value;
			}
			return false;
		}
	}

	internal static bool RareItemChime
	{
		get
		{
			if (_rareItemChime != null)
			{
				return _rareItemChime.Value;
			}
			return false;
		}
	}

	internal static float ChimeVolume
	{
		get
		{
			if (_chimeVolume != null)
			{
				return _chimeVolume.Value;
			}
			return 0.5f;
		}
	}

	internal static int ChimeMinTier
	{
		get
		{
			if (_chimeMinTier != null)
			{
				return ChimeRule.ClampMinTier(_chimeMinTier.Value, 6);
			}
			return 6;
		}
	}

	internal static bool VerboseLog
	{
		get
		{
			if (_verboseLog != null)
			{
				return _verboseLog.Value;
			}
			return false;
		}
	}

	internal static void Init(ConfigFile config, Harmony harmony)
	{
		_configFile = config;
		_corpseSearchEnabled = config.Bind("搜刮 · 功能开关", "尸体自动搜索", defaultValue: true, new ConfigDescription("打开尸体面板后自动按顺序搜索胸挂/口袋/背包，不用逐个点击", null, new ConfigurationManagerAttributes
		{
			Order = 30
		}));
		_skipInitialDelay = config.Bind("搜刮 · 功能开关", "容器免等待", defaultValue: true, new ConfigDescription("跳过开箱时约 2 秒的等待，直接开始揭示物品，逐件揭示的速度不受影响", null, new ConfigurationManagerAttributes
		{
			Order = 20
		}));
		_autoExamineEnabled = config.Bind("搜刮 · 功能开关", "自动检视", defaultValue: true, new ConfigDescription("搜出来的物品和尸体上直接可见的装备自动检视，耗时和经验与手动检视完全一致", null, new ConfigurationManagerAttributes
		{
			Order = 10
		}));
		_tacticalVestOrder = config.Bind("搜刮 · 尸体搜索细节", "搜索顺序：胸挂", 1, new ConfigDescription("数字小的先搜", null, new ConfigurationManagerAttributes
		{
			Order = 30
		}));
		_pocketsOrder = config.Bind("搜刮 · 尸体搜索细节", "搜索顺序：口袋", 2, new ConfigDescription("数字小的先搜", null, new ConfigurationManagerAttributes
		{
			Order = 29
		}));
		_backpackOrder = config.Bind("搜刮 · 尸体搜索细节", "搜索顺序：背包", 3, new ConfigDescription("数字小的先搜", null, new ConfigurationManagerAttributes
		{
			Order = 28
		}));
		_resumePartialSearch = config.Bind("搜刮 · 尸体搜索细节", "续搜未搜完的槽位", defaultValue: true, new ConfigDescription("搜到一半关掉面板，重开后接着搜剩下的", null, new ConfigurationManagerAttributes
		{
			Order = 20
		}));
		_resumeContainerSearch = config.Bind("搜刮 · 尸体搜索细节", "重开容器时续搜", defaultValue: true, new ConfigDescription("重新打开搜到一半的容器时自动接着搜", null, new ConfigurationManagerAttributes
		{
			Order = 10
		}));
		_verboseLog = config.Bind("搜刮 · 诊断", "详细日志", defaultValue: false, new ConfigDescription("把触发/喂入/跳过写进 LogOutput.log，排查问题时再开", null, new ConfigurationManagerAttributes
		{
			IsAdvanced = true
		}));
		_tieredSearchTime = config.Bind("搜刮 · 分级搜索时间", "按价值分级", defaultValue: true, new ConfigDescription("揭示物品的等待时间按价值档位定长，越贵重翻得越慢，关掉回到原版的 1~2 秒随机\n档位与格子底色 悬停价同源，颜色和耗时永远对得上", null, new ConfigurationManagerAttributes
		{
			Order = 60
		}));
		string[] array = new string[6] { "灰白", "绿", "蓝", "紫", "金", "红" };
		_tierSeconds = new ConfigEntry<float>[6];
		for (int i = 0; i < _tierSeconds.Length; i++)
		{
			_tierSeconds[i] = config.Bind("搜刮 · 分级搜索时间", $"{array[i]}档秒数", (float)SearchTakeover.DefaultSeconds[i], new ConfigDescription($"第 {i} 档（{array[i]}）物品揭示前等待的秒数，0 = 立刻出现", new AcceptableValueRange<float>(0f, 30f), new ConfigurationManagerAttributes
			{
				Order = 50 - i
			}));
		}
		_attentionStillSpeedsUp = config.Bind("搜刮 · 分级搜索时间", "注意力技能仍加速搜索", defaultValue: false, new ConfigDescription("开启后六档秒数只是零技能基准，注意力等级越高翻得越快\n关闭则六档秒数就是实际耗时，不随角色成长变化\n两种情况下搜索技能经验都照发", null, new ConfigurationManagerAttributes
		{
			IsAdvanced = true,
			Order = 40
		}));
		RebuildTiming();
		ConfigEntry<float>[] tierSeconds = _tierSeconds;
		for (int j = 0; j < tierSeconds.Length; j++)
		{
			tierSeconds[j].SettingChanged += (object s, EventArgs e) =>
			{
				RebuildTiming();
			};
		}
		_rareItemChime = config.Bind("搜刮 · 稀有音效", "稀有物品音效", defaultValue: true, new ConfigDescription("翻出高档位物品时额外叮一声，原版材质音照响", null, new ConfigurationManagerAttributes
		{
			Order = 30
		}));
		_chimeMinTier = config.Bind("搜刮 · 稀有音效", "从第几档开始响", 0, new ConfigDescription("档位序号 0 灰白 / 1 绿 / 2 蓝 / 3 紫 / 4 金 / 5 红，默认 0 每档都响，嫌低档吵就调高\n六档各一个音效文件，放在 plugins/Haavk/sounds/\n⚠\ufe0f 老版本 cfg 存的是 4，升级后想全响要手动改回 0", new AcceptableValueRange<int>(0, 5), new ConfigurationManagerAttributes
		{
			Order = 20
		}));
		_chimeVolume = config.Bind("搜刮 · 稀有音效", "音效音量", 0.5f, new ConfigDescription("搜刮音效的音量，自定义文件和内置回落音都吃，EFT 原生局内 UI 音约相当于 0.15，改完当场生效", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes
		{
			Order = 10
		}));
		_cellIndicator = config.Bind("搜刮 · 逐格指示器", "逐格搜索指示器", defaultValue: true, new ConfigDescription("下一个将被揭示的格子上显示一个旋转的放大镜\n⚠\ufe0f 依赖上面的「按价值分级」：关掉它就没有逐件等待，指示器也无从显示\n容器面板没打开（例如尸体在后台自动搜索）时不显示，也不消耗任何东西", null, new ConfigurationManagerAttributes
		{
			Order = 40
		}));
		_indicatorSpin = config.Bind("搜刮 · 逐格指示器", "指示器旋转速度", 320f, new ConfigDescription("放大镜每秒绕小圈顺时针走多少度，0 = 不动\n图标本身的角度不变，只有位置在绕圈", new AcceptableValueRange<float>(0f, 720f), new ConfigurationManagerAttributes
		{
			Order = 30
		}));
		_indicatorOrbit = config.Bind("搜刮 · 逐格指示器", "指示器绕圈半径", 0.09f, new ConfigDescription("那个小圈的半径相对一个格子的倍数，0 = 原地不动", new AcceptableValueRange<float>(0f, 0.5f), new ConfigurationManagerAttributes
		{
			Order = 25
		}));
		_indicatorScale = config.Bind("搜刮 · 逐格指示器", "指示器大小", 0.55f, new ConfigDescription("放大镜边长相对一个格子的倍数", new AcceptableValueRange<float>(0.3f, 1.5f), new ConfigurationManagerAttributes
		{
			Order = 20
		}));
		_traceSearchTiming = config.Bind("Spike", "TraceSearchTiming", defaultValue: false, new ConfigDescription("把每一次搜刮挂点命中打进日志，带距上次命中的毫秒数。验收判据（同一箱里便宜的连着蹦、值钱的顿一下）靠它，平时别开。进图后也能用调试控制台的 haavk_cfg trace=on 当场打开。", null, new ConfigurationManagerAttributes
		{
			Browsable = false,
			IsAdvanced = true
		}));
		SearchInstantPatch.Enable(harmony);
		InventoryScreenPatch.Enable(harmony);
		DiscoverSoundPatch.Enable(harmony);
		ChimeClips.Start();
		if (!SkipInitialDelay && !TieredSearchTime)
		{
			HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|Looting] 两个时长开关都关着：搜刮完全走原版（挂点仍然装着，开关在挂点里现判，F12 改完下一个箱子生效）");
		}
	}

	private static void RebuildTiming()
	{
		if (_tierSeconds != null)
		{
			double[] array = new double[_tierSeconds.Length];
			for (int i = 0; i < _tierSeconds.Length; i++)
			{
				array[i] = _tierSeconds[i].Value;
			}
			if (SearchTiming.TryCreate(array, out var result, out var error))
			{
				RevealTiming = result;
				return;
			}
			RevealTiming = null;
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 六档秒数配置非法，按价值分级退化成「不等待」: " + error);
		}
	}

	private static void SetVolatile(Action apply)
	{
		ConfigFile configFile = _configFile;
		if (configFile == null)
		{
			apply();
			return;
		}
		bool saveOnConfigSet = configFile.SaveOnConfigSet;
		configFile.SaveOnConfigSet = false;
		try
		{
			apply();
		}
		finally
		{
			configFile.SaveOnConfigSet = saveOnConfigSet;
		}
	}

	internal static void SetSkipInitialDelay(bool on)
	{
		if (_skipInitialDelay != null)
		{
			SetVolatile(() =>
			{
				_skipInitialDelay.Value = on;
			});
		}
	}

	internal static void SetTieredSearchTime(bool on)
	{
		if (_tieredSearchTime != null)
		{
			SetVolatile(() =>
			{
				_tieredSearchTime.Value = on;
			});
		}
	}

	internal static void SetTraceSearchTiming(bool on)
	{
		if (_traceSearchTiming != null)
		{
			SetVolatile(() =>
			{
				_traceSearchTiming.Value = on;
			});
		}
	}

	internal static void SetChimeMinTier(int raw)
	{
		if (_chimeMinTier != null)
		{
			int clamped = ChimeRule.ClampMinTier(raw, 6);
			SetVolatile(() =>
			{
				_chimeMinTier.Value = clamped;
			});
		}
	}

	internal static EquipmentSlot[] GetOrderedSlots()
	{
		return Enumerable.ToArray(Enumerable.Select(Enumerable.OrderBy(new[]
		{
			new
			{
				Slot = EquipmentSlot.TacticalVest,
				Order = (_tacticalVestOrder?.Value ?? 1)
			},
			new
			{
				Slot = EquipmentSlot.Pockets,
				Order = (_pocketsOrder?.Value ?? 2)
			},
			new
			{
				Slot = EquipmentSlot.Backpack,
				Order = (_backpackOrder?.Value ?? 3)
			}
		}, s => s.Order), s => s.Slot));
	}

	internal static void Trace(string msg)
	{
		if (_verboseLog != null && _verboseLog.Value)
		{
			HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|Looting] " + msg);
		}
	}

	internal static void SetChimeVolume(float v)
	{
		if (_chimeVolume != null)
		{
			float clamped;
			if (v < 0f)
			{
				clamped = 0f;
			}
			else
			{
				clamped = ((v > 1f) ? 1f : v);
			}
			SetVolatile(() =>
			{
				_chimeVolume.Value = clamped;
			});
		}
	}

	internal static void SetCellIndicator(bool on)
	{
		if (_cellIndicator != null)
		{
			SetVolatile(() =>
			{
				_cellIndicator.Value = on;
			});
		}
	}

	internal static void SetIndicatorSpin(float degreesPerSecond)
	{
		if (_indicatorSpin != null)
		{
			float v;
			if (degreesPerSecond < 0f)
			{
				v = 0f;
			}
			else
			{
				v = ((degreesPerSecond > 720f) ? 720f : degreesPerSecond);
			}
			SetVolatile(() =>
			{
				_indicatorSpin.Value = v;
			});
		}
	}

	internal static void SetIndicatorOrbit(float cells)
	{
		if (_indicatorOrbit != null)
		{
			float v;
			if (cells < 0f)
			{
				v = 0f;
			}
			else
			{
				v = ((cells > 0.5f) ? 0.5f : cells);
			}
			SetVolatile(() =>
			{
				_indicatorOrbit.Value = v;
			});
		}
	}

	internal static void SetAllTierSeconds(float seconds)
	{
		if (_tierSeconds == null)
		{
			return;
		}
		float v;
		if (seconds < 0f)
		{
			v = 0f;
		}
		else
		{
			v = ((seconds > 30f) ? 30f : seconds);
		}
		SetVolatile(() =>
		{
			ConfigEntry<float>[] tierSeconds = _tierSeconds;
			for (int i = 0; i < tierSeconds.Length; i++)
			{
				tierSeconds[i].Value = v;
			}
		});
		RebuildTiming();
	}

	internal static string DescribeState()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(56, 9);
		val.AppendLiteral("skip=");
		val.AppendFormatted(SkipInitialDelay ? "on" : "off");
		val.AppendLiteral(" graded=");
		val.AppendFormatted(TieredSearchTime ? "on" : "off");
		val.AppendLiteral(" attn=");
		val.AppendFormatted(AttentionStillSpeedsUp ? "on" : "off");
		val.AppendLiteral(" chime=");
		val.AppendFormatted<int>(ChimeMinTier);
		val.AppendLiteral(" trace=");
		val.AppendFormatted(TraceSearchTiming ? "on" : "off");
		val.AppendLiteral(" vol=");
		val.AppendFormatted<float>(ChimeVolume, "F2");
		val.AppendLiteral(" ind=");
		val.AppendFormatted(CellIndicator ? "on" : "off");
		val.AppendLiteral(" spin=");
		val.AppendFormatted<float>(IndicatorSpinSpeed, "F0");
		val.AppendLiteral(" orbit=");
		val.AppendFormatted<float>(IndicatorOrbit, "F2");
		return val.ToStringAndClear();
	}
}
