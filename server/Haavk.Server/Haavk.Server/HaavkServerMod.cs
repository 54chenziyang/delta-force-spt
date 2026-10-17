using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Haavk.Core;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Ragfair;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Json;

namespace Haavk.Server;

[Injectable(InjectionType.Transient, int.MaxValue, TypePriority = 1000100)]
public class HaavkServerMod : IOnLoad
{
	private const string Tag = "[Haavk.Server]";

	private const string DefaultFleaBannedTextColour = "#c03a3a";

	private static readonly string[] DefaultTargetLocales = new string[2] { "ch", "en" };

	private ModConfig _cfg;

	private MoneyStyle _moneyStyle;

	private bool _colourConverterInstalled;

	private bool _liveFleaPricesInstalled;

	private int _itemsUpdated;

	private int _missingDescriptions;

	private string _firstMissingDescription;

	private Dictionary<MongoId, double> _originalPrices;

	private Dictionary<string, Dictionary<string, string>> _originalLocales;

	private readonly RagfairConfig _ragfairConfig;

	private bool _haavkClientInstalled;

	private readonly HashSet<string> _bakedBackgroundColours;

	private Dictionary<string, (TemplateItem Template, int Tier)> _colourPins;

	private List<string> _colourPinSources;

	private CategoryColouring _itemColouring;

	private CategoryColouring _keyColouring;

	private CategoryColouring _weaponColouring;

	private CategoryColouring _ammoColouring;

	private CategoryColouring _armourColouring;

	private CategoryColouring _backpackColouring;

	private CategoryColouring _vestColouring;

	private CategoryColouring _containerColouring;

	private TierLadder _ammoPenGrade;

	private TierLadder _ammoDamageGrade;

	private readonly List<MongoId> _bannedBaseClasses;

	private readonly List<string> _validArmourSlots;

	// 跳蚤「品质标签」筛选用：模板 id（24 位小写 hex）→ 品质档（0=劣质 … 5=极品）。
	// 供 RagfairTierFilterPatch 在收到哨兵 HandbookId 时，把该档的全部模板作为筛选结果返回。
	// 注意：每小时价格刷新会跳过未变价的物品，所以这里只累积、绝不清空（清空会丢掉没被刷新到的物品）。
	internal static readonly Dictionary<string, int> TemplateTiers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	private const int TaxonomyNamedColourCount = 12;

	// SPT 4.1.5 移植版：客户端是 Mono 构建，文件名不再带 Il2Cpp 后缀。
	// 原版（SPT 5.0 / IL2CPP）为 "Haavk.Client.Il2Cpp.dll"。
	private const string HaavkClientAssemblyName = "Haavk.Client.dll";

	// 容量型装备的基类模板 id（items.json 里 Node 节点的真实 id）。
	// 这些件数多、单格价与实战性能严重脱节，品质改按总容量格数定级。
	private static readonly MongoId BackpackBaseClass = new MongoId("5448e53e4bdc2d60728b4567");

	private static readonly MongoId VestBaseClass = new MongoId("5448e5284bdc2dcb718b4567");

	private static readonly MongoId SimpleContainerBaseClass = new MongoId("5795f317245977243854e041");

	// 护目镜（眼镜/护目镜）也归入「按防护等级着色」这一路。
	private static readonly MongoId VisorsBaseClass = new MongoId("5448e5724bdc2ddf718b4568");

	// 反编译产物：下面这些是 C# 12 主构造函数捕获的服务字段。ILSpy 没能还原主构造声明，
	// 只留下了对合成字段的引用，这里按原样补齐声明（原名是 <logger>P 这类，转义后即此形态）。
	private ISptLogger<HaavkServerMod> _003Clogger_003EP;

	private TemplateTable _003CtemplateTable_003EP;

	private TradersTable _003CtradersTable_003EP;

	private LocaleTable _003ClocaleTable_003EP;

	private LocaleService _003ClocaleService_003EP;

	private ItemHelper _003CitemHelper_003EP;

	private RagfairServerHelper _003CragfairServerHelper_003EP;

	private RagfairOfferService _003CragfairOfferService_003EP;

	private ModHelper _003CmodHelper_003EP;

	private FileUtil _003CfileUtil_003EP;

	private IReadOnlyList<SptMod> _003CinstalledMods_003EP;

	private WeaponTotalPriceStore _003CpriceStore_003EP;

	private ICloner _003Ccloner_003EP;

	private Dictionary<MongoId, TemplateItem> ItemTemplates => _003CtemplateTable_003EP.Items;

	private Dictionary<MongoId, double> PriceTable => _003CtemplateTable_003EP.Prices;

	private HandbookBase HandbookTable => _003CtemplateTable_003EP.Handbook;

	private Dictionary<MongoId, Trader> TraderTable => _003CtradersTable_003EP;

	private Dictionary<string, LazyLoad<GlobalLocaleDictionary>> LocalesGlobal => _003ClocaleTable_003EP.Global;

	public HaavkServerMod(ISptLogger<HaavkServerMod> logger, TemplateTable templateTable, TradersTable tradersTable, LocaleTable localeTable, LocaleService localeService, ItemHelper itemHelper, RagfairConfig ragfairConfig, RagfairServerHelper ragfairServerHelper, RagfairOfferService ragfairOfferService, ModHelper modHelper, FileUtil fileUtil, IReadOnlyList<SptMod> installedMods, WeaponTotalPriceStore priceStore, ICloner cloner)
	{
		_003Clogger_003EP = logger;
		_003CtemplateTable_003EP = templateTable;
		_003CtradersTable_003EP = tradersTable;
		_003ClocaleTable_003EP = localeTable;
		_003ClocaleService_003EP = localeService;
		_003CitemHelper_003EP = itemHelper;
		_003CragfairServerHelper_003EP = ragfairServerHelper;
		_003CragfairOfferService_003EP = ragfairOfferService;
		_003CmodHelper_003EP = modHelper;
		_003CfileUtil_003EP = fileUtil;
		_003CinstalledMods_003EP = installedMods;
		_003CpriceStore_003EP = priceStore;
		_003Ccloner_003EP = cloner;
		_originalLocales = new Dictionary<string, Dictionary<string, string>>();
		_ragfairConfig = ragfairConfig;
		_bakedBackgroundColours = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		_colourPins = new Dictionary<string, (TemplateItem, int)>(StringComparer.OrdinalIgnoreCase);
		_colourPinSources = new List<string>();
		int num = 6;
		List<MongoId> list = new List<MongoId>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<MongoId> span = CollectionsMarshal.AsSpan(list);
		span[0] = BaseClasses.LOOT_CONTAINER;
		span[1] = BaseClasses.STASH;
		span[2] = BaseClasses.POCKETS;
		span[3] = BaseClasses.RANDOM_LOOT_CONTAINER;
		span[4] = BaseClasses.BUILT_IN_INSERTS;
		span[5] = BaseClasses.HIDEOUT_AREA_CONTAINER;
		_bannedBaseClasses = list;
		num = 16;
		List<string> list2 = new List<string>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<string> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = "helmet_top";
		span2[1] = "helmet_back";
		span2[2] = "helmet_ears";
		span2[3] = "helmet_eyes";
		span2[4] = "helmet_jaw";
		span2[5] = "front_plate";
		span2[6] = "back_plate";
		span2[7] = "soft_armor_front";
		span2[8] = "soft_armor_back";
		span2[9] = "soft_armor_left";
		span2[10] = "soft_armor_right";
		span2[11] = "collar";
		span2[12] = "groin";
		span2[13] = "groin_back";
		span2[14] = "shoulder_l";
		span2[15] = "shoulder_r";
		_validArmourSlots = list2;
	}

	public Task OnLoadAsync(CancellationToken cancellationToken)
	{
		return OnLoad();
	}

	private Task OnLoad()
	{
		string absolutePathToModFolder = _003CmodHelper_003EP.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
		_cfg = _003CmodHelper_003EP.GetJsonDataFromFile<ModConfig>(absolutePathToModFolder, "config.json");
		if (!MoneyFormats.TryParse(_cfg.MoneyFormat, out var format))
		{
			_003Clogger_003EP.Warning($"{"[Haavk.Server]"} config.json 的 MoneyFormat = \"{_cfg.MoneyFormat}\" 认不出，按 {"plain"} 处理。合法值：{string.Join(" / ", MoneyFormats.AllTokens)}");
		}
		_moneyStyle = new MoneyStyle(format, _cfg.MoneySpaceAfterSymbol);
		_colourConverterInstalled = IsColourConverterInstalled();
		_liveFleaPricesInstalled = IsLiveFleaPricesInstalled();
		_haavkClientInstalled = IsHaavkClientInstalled();
		if (!_haavkClientInstalled)
		{
			MakeAdjustmentsForColourConverterMissing();
			_003Clogger_003EP.Warning("[Haavk.Server] 没找到客户端半边（BepInEx\\plugins 下的 Haavk.Client.dll），自定义底色退回引擎具名色板。只装服务端的话这是正常的");
		}
		BuildColourings();
		LoadColourPins(absolutePathToModFolder);
		StoreOriginalTables();
		SetColours(firstRun: true);
		try
		{
			RagfairTierFilterPatch.Enable();
		}
		catch (Exception value)
		{
			_003Clogger_003EP.Error($"{"[Haavk.Server]"} 跳蚤品质标签筛选启用失败: {value}");
		}
		if (_liveFleaPricesInstalled)
		{
			Task.Run(async () =>
			{
				while (true)
				{
					await Task.Delay(TimeSpan.FromHours(1));
					try
					{
						SetColours();
					}
					catch (Exception value)
					{
						_003Clogger_003EP.Error($"{"[Haavk.Server]"} 每小时价格刷新失败，下一小时重试: {value}");
					}
				}
			});
		}
		return Task.CompletedTask;
	}

	private void BuildColourings()
	{
		string[] palette = new string[6] { _cfg.BadColour, _cfg.PoorColour, _cfg.FairColour, _cfg.GoodColour, _cfg.VeryGoodColour, _cfg.ExceptionalColour };
		_itemColouring = new CategoryColouring(new TierLadder(new double[5] { _cfg.BadItemPerSlotMaxValue, _cfg.PoorItemPerSlotMaxValue, _cfg.FairItemPerSlotMaxValue, _cfg.GoodItemPerSlotMaxValue, _cfg.VeryGoodItemPerSlotMaxValue }, inclusive: false), palette, _cfg.ColourNormalItems, _cfg.ColourFleaBannedItems, _cfg.FleaBannedColour);
		_keyColouring = new CategoryColouring(new TierLadder(new double[5] { _cfg.BadKeyMaxValue, _cfg.PoorKeyMaxValue, _cfg.FairKeyMaxValue, _cfg.GoodKeyMaxValue, _cfg.VeryGoodKeyMaxValue }, inclusive: false), palette, _cfg.ColourKeys, _cfg.ColourFleaBannedKeys, _cfg.FleaBannedColour);
		_weaponColouring = new CategoryColouring(new TierLadder(new double[5] { _cfg.BadWeaponMaxValue, _cfg.PoorWeaponMaxValue, _cfg.FairWeaponMaxValue, _cfg.GoodWeaponMaxValue, _cfg.VeryGoodWeaponMaxValue }, inclusive: false), palette, _cfg.ColourWeapons, _cfg.ColourFleaBannedWeapons, _cfg.FleaBannedColour);
		_ammoColouring = new CategoryColouring(new TierLadder(new double[5] { _cfg.BadAmmoMaxScore, _cfg.PoorAmmoMaxScore, _cfg.FairAmmoMaxScore, _cfg.GoodAmmoMaxScore, _cfg.VeryGoodAmmoMaxScore }, inclusive: true), palette, _cfg.ColourAmmo, _cfg.ColourFleaBannedAmmo, _cfg.FleaBannedColour);
		_armourColouring = new CategoryColouring(new TierLadder(new double[5] { _cfg.BadArmourMaxPlateClass, _cfg.PoorArmourMaxPlateClass, _cfg.FairArmourMaxPlateClass, _cfg.GoodArmourMaxPlateClass, _cfg.VeryGoodArmourMaxPlateClass }, inclusive: true), palette, _cfg.ColourArmours, _cfg.ColourFleaBannedArmour, _cfg.FleaBannedColour);
		_backpackColouring = new CategoryColouring(new TierLadder(new double[5] { _cfg.BadBackpackCapacityMax, _cfg.PoorBackpackCapacityMax, _cfg.FairBackpackCapacityMax, _cfg.GoodBackpackCapacityMax, _cfg.VeryGoodBackpackCapacityMax }, inclusive: true), palette, _cfg.ColourBackpacks, _cfg.ColourFleaBannedBackpacks, _cfg.FleaBannedColour);
		_vestColouring = new CategoryColouring(new TierLadder(new double[5] { _cfg.BadVestCapacityMax, _cfg.PoorVestCapacityMax, _cfg.FairVestCapacityMax, _cfg.GoodVestCapacityMax, _cfg.VeryGoodVestCapacityMax }, inclusive: true), palette, _cfg.ColourVests, _cfg.ColourFleaBannedVests, _cfg.FleaBannedColour);
		_containerColouring = new CategoryColouring(new TierLadder(new double[5] { _cfg.BadContainerCapacityMax, _cfg.PoorContainerCapacityMax, _cfg.FairContainerCapacityMax, _cfg.GoodContainerCapacityMax, _cfg.VeryGoodContainerCapacityMax }, inclusive: true), palette, _cfg.ColourContainers, _cfg.ColourFleaBannedContainers, _cfg.FleaBannedColour);
		_ammoPenGrade = new TierLadder(new double[5] { _cfg.AmmoPenGrade1Max, _cfg.AmmoPenGrade2Max, _cfg.AmmoPenGrade3Max, _cfg.AmmoPenGrade4Max, _cfg.AmmoPenGrade5Max }, inclusive: true);
		_ammoDamageGrade = new TierLadder(new double[5] { _cfg.AmmoDamageGrade1Max * DamageDisplayMultiplier, _cfg.AmmoDamageGrade2Max * DamageDisplayMultiplier, _cfg.AmmoDamageGrade3Max * DamageDisplayMultiplier, _cfg.AmmoDamageGrade4Max * DamageDisplayMultiplier, _cfg.AmmoDamageGrade5Max * DamageDisplayMultiplier }, inclusive: true);
	}

	// 性能指标：一件容器类装备的总容量格数 = 所有子网格 cellsH×cellsV 之和
	// （含内部武器槽/弹匣槽等全部子网格）。背包/胸挂/容器按此值定品质。
	private static double Capacity(TemplateItem templateItem)
	{
		IEnumerable<Grid> grids = templateItem.Properties?.Grids;
		if (grids == null)
		{
			return 0.0;
		}
		double num = 0.0;
		foreach (Grid grid in grids)
		{
			int cellsH = grid.Properties?.CellsH ?? 0;
			int cellsV = grid.Properties?.CellsV ?? 0;
			if (cellsH > 0 && cellsV > 0)
			{
				num += cellsH * cellsV;
			}
		}
		return num;
	}

	private void LoadColourPins(string pathToMod)
	{
		_colourPins = new Dictionary<string, (TemplateItem, int)>(StringComparer.OrdinalIgnoreCase);
		_colourPinSources = new List<string>();
		string directoryName;
		try
		{
			directoryName = System.IO.Path.GetDirectoryName(System.IO.Path.TrimEndingDirectorySeparator(pathToMod));
			if (directoryName == null || !Directory.Exists(directoryName))
			{
				return;
			}
		}
		catch (Exception ex)
		{
			_003Clogger_003EP.Warning($"{"[Haavk.Server]"} 定位 user\\mods 失败，不读 {"haavk-colours.json"}: {ex.Message}");
			return;
		}
		JsonSerializerOptions options = new JsonSerializerOptions
		{
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		};
		List<ColourPinFile> list = new List<ColourPinFile>();
		foreach (string item3 in Directory.GetDirectories(directoryName).OrderBy((string d) => d, StringComparer.Ordinal))
		{
			string text = System.IO.Path.Combine(item3, "haavk-colours.json");
			if (File.Exists(text))
			{
				string fileName = System.IO.Path.GetFileName(item3);
				try
				{
					Dictionary<string, string> entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(text), options);
					list.Add(new ColourPinFile(fileName, entries));
					_colourPinSources.Add(fileName);
				}
				catch (Exception ex2)
				{
					_003Clogger_003EP.Warning($"{"[Haavk.Server]"} {text} 读不了，整个文件不生效: {ex2.Message}");
				}
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		Dictionary<string, (string, TemplateItem)> dictionary = new Dictionary<string, (string, TemplateItem)>(StringComparer.OrdinalIgnoreCase);
		foreach (var (mongoId2, item) in ItemTemplates)
		{
			string text2 = mongoId2.ToString();
			dictionary[text2] = (text2, item);
		}
		ColourPinSet colourPinSet = ColourPins.Merge(list, dictionary.ContainsKey);
		foreach (string warning in colourPinSet.Warnings)
		{
			_003Clogger_003EP.Warning($"{"[Haavk.Server]"} {"haavk-colours.json"} {warning}");
		}
		foreach (var (key, item2) in colourPinSet.Tiers)
		{
			var (text4, templateItem2) = dictionary[key];
			if ((object)templateItem2.Properties == null)
			{
				_003Clogger_003EP.Warning($"{"[Haavk.Server]"} {"haavk-colours.json"}：模板 {text4} 没有 Properties，写不了底色，跳过");
			}
			else
			{
				_colourPins[text4] = (templateItem2, item2);
			}
		}
	}

	private void StoreOriginalTables()
	{
		_originalPrices = _003Ccloner_003EP.Clone(PriceTable);
		Dictionary<string, LazyLoad<GlobalLocaleDictionary>> localesGlobal = LocalesGlobal;
		string[] array = _cfg?.TargetLocales;
		string[] array2 = ((array != null && array.Length > 0) ? array : DefaultTargetLocales);
		_originalLocales = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
		string[] array3 = array2;
		foreach (string text in array3)
		{
			if (!localesGlobal.ContainsKey(text))
			{
				_003Clogger_003EP.Warning("[Haavk.Server] locale \"" + text + "\" 不在数据库中，跳过");
			}
			else
			{
				_originalLocales[text] = _003Ccloner_003EP.Clone(_003ClocaleService_003EP.GetLocaleDb(text));
			}
		}
		if (_originalLocales.Count == 0)
		{
			_003Clogger_003EP.Error("[Haavk.Server] 没有任何可用 locale，物品描述将不会被修改");
		}
	}

	private const double AmmoDamageWeight = 0.3;

	// 显示用伤害倍率，<=0 时退回 1.0，避免算出全 0 伤害。
	private double DamageDisplayMultiplier
	{
		get
		{
			double value = _cfg.AmmoDamageDisplayMultiplier;
			return (value > 0.0) ? value : 1.0;
		}
	}

	// 把弹药模板的原始伤害换算成游戏内实际生效值（卡面「威力」），用于名字后缀、伤害等级与综合分。
	private int EffectiveDamage(double? rawDamage)
	{
		int num = (int)Math.Round(rawDamage.GetValueOrDefault() * DamageDisplayMultiplier, MidpointRounding.AwayFromZero);
		return (num < 0) ? 0 : num;
	}

	private static double AmmoScore(int? penetration, int? damage)
	{
		return penetration.GetValueOrDefault() + (double)damage.GetValueOrDefault() * AmmoDamageWeight;
	}

	private string AmmoTierColour(int tier)
	{
		string[] array = new string[6] { _cfg.BadColour, _cfg.PoorColour, _cfg.FairColour, _cfg.GoodColour, _cfg.VeryGoodColour, _cfg.ExceptionalColour };
		if (tier < 0 || tier >= array.Length)
		{
			return "#808080";
		}
		return array[tier];
	}

	private string BakeBackgroundColour(string colour)
	{
		if (!_haavkClientInstalled)
		{
			return colour;
		}
		if (!TaxonomyColourCodec.TryEncodeCustomHex(colour, 12, out var taxonomyValue))
		{
			return colour;
		}
		_bakedBackgroundColours.Add(colour);
		return taxonomyValue.ToString(CultureInfo.InvariantCulture);
	}

	private void MakeAdjustmentsForColourConverterMissing()
	{
		_cfg.BadColour = "grey";
		_cfg.PoorColour = "default";
		_cfg.FairColour = "green";
		_cfg.GoodColour = "blue";
		_cfg.VeryGoodColour = "violet";
		_cfg.ExceptionalColour = "yellow";
		_cfg.FleaBannedColour = "tracerRed";
	}

	private void SetColours(bool firstRun = false)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		Dictionary<MongoId, TemplateItem> itemTemplates = ItemTemplates;
		Dictionary<MongoId, double> priceTable = PriceTable;
		HandbookBase handbookTable = HandbookTable;
		IEnumerable<MongoId> source = (from x in typeof(BaseClasses).GetFields(BindingFlags.Static | BindingFlags.Public)
			select x.GetValue(null)).Cast<MongoId>();
		List<(List<MongoId>, double)> list = (from b in (from t in TraderTable
				where t.Key != Traders.FENCE
				select t.Value.Base).Where((TraderBase b) =>
			{
				HashSet<MongoId> hashSet = b?.ItemsBuy?.Category;
				return hashSet != null && hashSet.Count > 0;
			}).Where((TraderBase b) =>
			{
				double? num5 = b.LoyaltyLevels?.FirstOrDefault()?.BuyPriceCoefficient;
				return num5.HasValue && num5.GetValueOrDefault() > 0.0;
			})
			select (Categories: b.ItemsBuy.Category.ToList(), Coefficient: b.LoyaltyLevels[0].BuyPriceCoefficient.Value)).ToList();
		Dictionary<MongoId, PriceSpread> dictionary = FleaOfferStats.Build(_003CragfairOfferService_003EP.GetOffers());
		foreach (KeyValuePair<MongoId, TemplateItem> item in itemTemplates)
		{
			item.Deconstruct(out var key, out var value);
			MongoId mongoId = key;
			TemplateItem templateItem = value;
			if (_003CitemHelper_003EP.IsOfBaseclasses(mongoId, _bannedBaseClasses))
			{
				continue;
			}
			key = templateItem.Parent;
			if (key.IsEmpty || source.Contains(mongoId) || (!firstRun && (!_originalPrices.TryGetValue(mongoId, out var value2) || !priceTable.TryGetValue(mongoId, out var value3) || (int)value2 == (int)value3)))
			{
				continue;
			}
			double num = handbookTable.Items.Find((HandbookItem x) => x.Id == mongoId)?.Price ?? 1.0;
			double num2 = (priceTable.TryGetValue(mongoId, out var value4) ? value4 : num);
			foreach (KeyValuePair<MongoId, UnreasonableModPrices> unreasonableModPrice in _ragfairConfig.Dynamic.UnreasonableModPrices)
			{
				unreasonableModPrice.Deconstruct(out key, out var value5);
				MongoId baseClassTpl = key;
				UnreasonableModPrices unreasonableModPrices = value5;
				if (_003CitemHelper_003EP.IsOfBaseclass(mongoId, baseClassTpl) && num2 > num * (double)unreasonableModPrices.HandbookPriceOverMultiplier)
				{
					num2 = num * (double)unreasonableModPrices.NewPriceHandbookMultiplier;
				}
			}
			string key2 = mongoId.ToString();
			PriceSpread priceSpread = ((dictionary.TryGetValue(mongoId, out var value6) && value6.Count > 0) ? value6 : new PriceSpread
			{
				Max = num2,
				Avg = num2,
				Min = num2,
				Count = 0
			});
			_003CpriceStore_003EP.Payload.FleaMaxPrices[key2] = priceSpread.Max;
			_003CpriceStore_003EP.Payload.Prices[key2] = priceSpread.Avg;
			_003CpriceStore_003EP.Payload.FleaMinPrices[key2] = priceSpread.Min;
			List<double> list2 = new List<double>(list.Count);
			foreach (var (baseClassTpls, buyPriceCoefficient) in list)
			{
				if (_003CitemHelper_003EP.IsOfBaseclasses(mongoId, baseClassTpls))
				{
					list2.Add(TraderPricing.SellToTrader(num, buyPriceCoefficient));
				}
			}
			PriceSpread priceSpread2 = PriceStats.Summarise(list2);
			if (priceSpread2.Count > 0)
			{
				_003CpriceStore_003EP.Payload.TraderPrices[key2] = priceSpread2.Max;
				_003CpriceStore_003EP.Payload.TraderAvgPrices[key2] = priceSpread2.Avg;
				_003CpriceStore_003EP.Payload.TraderMinPrices[key2] = priceSpread2.Min;
			}
			int? heightValue = templateItem.Properties.Height;
			int? widthValue = templateItem.Properties.Width;
			if (!heightValue.HasValue || !widthValue.HasValue)
			{
				continue;
			}
			int height = heightValue.Value;
			int width = widthValue.Value;
			if (height <= 0 || width <= 0)
			{
				continue;
			}
			double num3 = ValuationMath.PerSlotPrice(num2, height, width);
			bool flag = _003CragfairServerHelper_003EP.IsItemValidRagfairItem(_003CitemHelper_003EP.GetItem(mongoId));
			string backgroundColor = templateItem.Properties.BackgroundColor;
			double price = 0.0;
			bool flag2 = false;
			bool perSlot = false;
			int? qualityTier = null;
			string text;
			if (_003CitemHelper_003EP.IsOfBaseclass(mongoId, BaseClasses.WEAPON))
			{
				text = _weaponColouring.Colour(num2, flag, backgroundColor);
				qualityTier = _weaponColouring.Tier(num2);
				flag2 = false;
			}
			else if (_003CitemHelper_003EP.IsOfBaseclass(mongoId, BaseClasses.AMMO))
			{
				double ammoScore = AmmoScore(templateItem.Properties.PenetrationPower, EffectiveDamage(templateItem.Properties.Damage));
				text = _ammoColouring.Colour(ammoScore, flag, backgroundColor);
				price = num2;
				flag2 = true;
				if (_cfg.ColourAmmo && !(_cfg.ColourFleaBannedAmmo && !flag) && !_colourPins.ContainsKey(key2))
				{
					_colourPins[key2] = (templateItem, _ammoColouring.Tier(ammoScore));
				}
			}
			else if (_003CitemHelper_003EP.IsOfBaseclass(mongoId, BaseClasses.KEY))
			{
				text = _keyColouring.Colour(num2, flag, backgroundColor);
				qualityTier = _keyColouring.Tier(num2);
				price = num2;
				flag2 = true;
			}
			else if (_003CitemHelper_003EP.IsOfBaseclass(mongoId, BackpackBaseClass))
			{
				double backpackCapacity = Capacity(templateItem);
				text = _backpackColouring.Colour(backpackCapacity, flag, backgroundColor);
				qualityTier = _backpackColouring.Tier(backpackCapacity);
				price = num3;
				flag2 = true;
				perSlot = true;
				if (_cfg.ColourBackpacks && !(_cfg.ColourFleaBannedBackpacks && !flag) && !_colourPins.ContainsKey(key2))
				{
					_colourPins[key2] = (templateItem, _backpackColouring.Tier(backpackCapacity));
				}
			}
			else if (_003CitemHelper_003EP.IsOfBaseclass(mongoId, VestBaseClass))
			{
				double vestCapacity = Capacity(templateItem);
				text = _vestColouring.Colour(vestCapacity, flag, backgroundColor);
				qualityTier = _vestColouring.Tier(vestCapacity);
				price = num3;
				flag2 = true;
				perSlot = true;
				if (_cfg.ColourVests && !(_cfg.ColourFleaBannedVests && !flag) && !_colourPins.ContainsKey(key2))
				{
					_colourPins[key2] = (templateItem, _vestColouring.Tier(vestCapacity));
				}
			}
			else if (_003CitemHelper_003EP.IsOfBaseclasses(mongoId, new _003C_003Ez__ReadOnlyArray<MongoId>(new MongoId[2]
			{
				BaseClasses.ARMORED_EQUIPMENT,
				BaseClasses.VEST
			})))
			{
				int valueOrDefault = (templateItem.Properties?.ArmorClass).GetValueOrDefault();
				bool isFaceCover = _003CitemHelper_003EP.IsOfBaseclass(mongoId, new MongoId("5a341c4686f77469e155819e")) || _003CitemHelper_003EP.IsOfBaseclass(mongoId, VisorsBaseClass);
				// 品级取「该物品自身护甲等级」与「可装插板护甲等级」的较高者，而非只看插板等级。
				int maxClass = valueOrDefault;
				IEnumerable<Slot> enumerable = templateItem.Properties?.Slots;
				if (enumerable != null)
				{
					List<int> list3 = (from r in (from s in enumerable
							where _validArmourSlots.Contains(s.Name.ToLowerInvariant())
							select s.Properties?.Filters?.FirstOrDefault((SlotFilter f) => f.Plate.HasValue)?.Plate into p
							where p.HasValue
							select p.Value).Select(_003CitemHelper_003EP.GetItem)
						where r.Key
						select r.Value into t
						select t.Properties.ArmorClass into ac
						where ac.HasValue
						select ac.Value).ToList();
					if (list3.Count > 0)
					{
						maxClass = Math.Max(maxClass, list3.Max());
					}
				}
				if (maxClass > 0 || isFaceCover)
				{
					text = _armourColouring.Colour(maxClass, flag, backgroundColor);
					qualityTier = _armourColouring.Tier(maxClass);
					// 按「该物品最高护甲等级」定品质时必须写入 pin，
					// 否则客户端会用「已装插板等级」重新计算（跳蚤上的裸甲未装板 = 0 档），把这层着色覆盖成灰色。
					if (_cfg.ColourArmours && !(_cfg.ColourFleaBannedArmour && !flag) && !_colourPins.ContainsKey(key2))
					{
						_colourPins[key2] = (templateItem, _armourColouring.Tier(maxClass));
					}
				}
				else
				{
					// 自身与插板均无护甲等级（如纯胸挂/携行具）时，回落到按价格上色并烘焙，避免无底色。
					text = _itemColouring.Colour(num3, flag, backgroundColor);
					TemplateTiers[key2] = _itemColouring.Tier(num3);
					FinalizeItemAndLocales(num3, flag, mongoId, text, perSlot: true, qualityTier: _itemColouring.Tier(num3));
					if (!_colourPins.ContainsKey(key2))
					{
						templateItem.Properties.BackgroundColor = BakeBackgroundColour(text);
					}
					_itemsUpdated++;
					continue;
				}
				price = num2;
				flag2 = true;
			}
			else if (_003CitemHelper_003EP.IsOfBaseclass(mongoId, BaseClasses.MONEY))
			{
				text = "black";
			}
			else if (_003CitemHelper_003EP.IsOfBaseclass(mongoId, SimpleContainerBaseClass))
			{
				double containerCapacity = Capacity(templateItem);
				text = _containerColouring.Colour(containerCapacity, flag, backgroundColor);
				qualityTier = _containerColouring.Tier(containerCapacity);
				price = num3;
				flag2 = true;
				perSlot = true;
				if (_cfg.ColourContainers && !(_cfg.ColourFleaBannedContainers && !flag) && !_colourPins.ContainsKey(key2))
				{
					_colourPins[key2] = (templateItem, _containerColouring.Tier(containerCapacity));
				}
			}
			else
			{
				text = _itemColouring.Colour(num3, flag, backgroundColor);
				qualityTier = _itemColouring.Tier(num3);
				price = num3;
				flag2 = true;
				perSlot = true;
			}
			// 品质标签映射：优先用本次判定出的品质档；弹药等分支只写了 pin、没写 qualityTier，用 pin 兜底。
			int? mapTier = qualityTier;
			if (!mapTier.HasValue && _colourPins.TryGetValue(key2, out var pinEntry))
			{
				mapTier = pinEntry.Tier;
			}
			if (mapTier.HasValue)
			{
				TemplateTiers[key2] = mapTier.Value;
			}
			if (flag2 || qualityTier.HasValue)
			{
				FinalizeItemAndLocales(price, flag, mongoId, text, perSlot, qualityTier);
			}
			_itemsUpdated++;
			if (!string.IsNullOrEmpty(text) && !_colourPins.ContainsKey(key2))
			{
				templateItem.Properties.BackgroundColor = BakeBackgroundColour(text);
			}
		}
		string[] array = new string[6] { _cfg.BadColour, _cfg.PoorColour, _cfg.FairColour, _cfg.GoodColour, _cfg.VeryGoodColour, _cfg.ExceptionalColour };
		foreach (KeyValuePair<string, (TemplateItem, int)> colourPin in _colourPins)
		{
			colourPin.Deconstruct(out var _, out var value7);
			var (templateItem2, num4) = value7;
			if (!string.IsNullOrEmpty(array[num4]))
			{
				templateItem2.Properties.BackgroundColor = BakeBackgroundColour(array[num4]);
			}
		}
		_003CpriceStore_003EP.Payload.ColourPins = _colourPins.ToDictionary((KeyValuePair<string, (TemplateItem Template, int Tier)> p) => p.Key, (KeyValuePair<string, (TemplateItem Template, int Tier)> p) => p.Value.Tier);
		_003CpriceStore_003EP.Payload.TierColours = new string[6] { _cfg.BadColour, _cfg.PoorColour, _cfg.FairColour, _cfg.GoodColour, _cfg.VeryGoodColour, _cfg.ExceptionalColour };
		_003CpriceStore_003EP.Payload.BackgroundColours = _bakedBackgroundColours.ToArray();
		_003CpriceStore_003EP.Payload.BackgroundColourOffset = 12;
		_003CpriceStore_003EP.Payload.FleaBannedTextColour = (string.IsNullOrEmpty(_cfg.FleaBannedTextColour) ? "#c03a3a" : _cfg.FleaBannedTextColour);
		_003CpriceStore_003EP.Payload.ItemPerSlotTiers = new double[5] { _cfg.BadItemPerSlotMaxValue, _cfg.PoorItemPerSlotMaxValue, _cfg.FairItemPerSlotMaxValue, _cfg.GoodItemPerSlotMaxValue, _cfg.VeryGoodItemPerSlotMaxValue };
		_003CpriceStore_003EP.Payload.FleaBannedBackgroundColour = _cfg.FleaBannedColour;
		_003CpriceStore_003EP.Payload.ColourWeaponBackground = _cfg.ColourWeapons;
		_003CpriceStore_003EP.Payload.ColourFleaBannedWeaponBackground = _cfg.ColourFleaBannedWeapons;
		_003CpriceStore_003EP.Payload.ArmourClassTiers = new double[5] { _cfg.BadArmourMaxPlateClass, _cfg.PoorArmourMaxPlateClass, _cfg.FairArmourMaxPlateClass, _cfg.GoodArmourMaxPlateClass, _cfg.VeryGoodArmourMaxPlateClass };
		_003CpriceStore_003EP.Payload.ColourArmourBackground = _cfg.ColourArmours;
		_003CpriceStore_003EP.Payload.ColourItemBackground = _cfg.ColourNormalItems;
		_003CpriceStore_003EP.Payload.ColourKeyBackground = _cfg.ColourKeys;
		_003CpriceStore_003EP.Payload.TraderItemPerSlotTiers = new double[5] { _cfg.BadTraderItemPerSlotMaxValue, _cfg.PoorTraderItemPerSlotMaxValue, _cfg.FairTraderItemPerSlotMaxValue, _cfg.GoodTraderItemPerSlotMaxValue, _cfg.VeryGoodTraderItemPerSlotMaxValue };
		long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
		stopwatch.Stop();
		_originalPrices = _003Ccloner_003EP.Clone(PriceTable);
		if (firstRun)
		{
			_003Clogger_003EP.Info($"{"[Haavk.Server]"} 已就绪：{_itemsUpdated} 件物品，{elapsedMilliseconds} ms（商人 {list.Count} 个，跳蚤裸件挂单 {dictionary.Count} 个模板）" + BackgroundSummary() + ColourPinSummary() + (_cfg.SuppressDescriptionPrice ? "；描述价行由客户端写" : "；描述价行由服务端写（装了客户端插件的话检视窗口会出现两行）"));
		}
		else
		{
			_003Clogger_003EP.Info($"{"[Haavk.Server]"} 每小时价格刷新：{_itemsUpdated} 件变价，{elapsedMilliseconds} ms（跳蚤裸件挂单 {dictionary.Count} 个模板）" + BackgroundSummary());
		}
		if (_missingDescriptions > 0)
		{
			_003Clogger_003EP.Warning($"{"[Haavk.Server]"} {_missingDescriptions} 件物品找不到原描述，没写描述价行（首个 {_firstMissingDescription}；多半是别的 mod 加的物品没带描述文本）");
		}
		_itemsUpdated = 0;
		_missingDescriptions = 0;
		_firstMissingDescription = null;
	}

	private string BackgroundSummary()
	{
		if (!_haavkClientInstalled)
		{
			return "；底色走引擎具名色板";
		}
		return $"；自定义底色 {_bakedBackgroundColours.Count} 种（偏移量 {12}）";
	}

	private string ColourPinSummary()
	{
		if (_colourPinSources.Count == 0)
		{
			return "";
		}
		return $"；指定底色 {_colourPins.Count} 件（{_colourPinSources.Count} 个 {"haavk-colours.json"}：{string.Join("、", _colourPinSources)}）";
	}

	// 品级词：与 Bad/Poor/Fair/Good/VeryGood/Exceptional 六档一一对应（底色 白/绿/蓝/紫/金/红）。
	private static readonly string[] QualityTierWords = new string[6] { "劣质", "普通", "良好", "优秀", "精良", "极品" };

	// 把品级写进物品名开头（如「【极品】AK-74M」）。跳蚤搜索框匹配物品全名，输入品级词即可筛出。
	private static string QualityTaggedName(string originalName, int tier)
	{
		if (string.IsNullOrEmpty(originalName) || tier < 0 || tier >= QualityTierWords.Length)
		{
			return originalName;
		}
		return $"【{QualityTierWords[tier]}】{originalName}";
	}

	private void FinalizeItemAndLocales(double price, bool availableOnFlea, MongoId itemId, string valueColour, bool perSlot, int? qualityTier = null)
	{
		string descriptionKey = $"{itemId} Description";
		string nameKey = $"{itemId} Name";
		// price 为 0 的（如武器）没有价格可写，不因想写品级标签而误写一条「0 ₽」描述行。
		bool flag = !_cfg.SuppressDescriptionPrice && price > 0.0;
		int? damage = null;
		int? penetration = null;
		int? ammoLevel = null;
		string ammoTierColour = null;
		string ammoGradeLine = null;
		bool isAmmo = _003CitemHelper_003EP.IsOfBaseclass(itemId, BaseClasses.AMMO);
		bool wantNameTag = _cfg.DamageAndPenStatsInName && isAmmo;
		bool wantGrades = _cfg.AmmoGradesInDescription && isAmmo;
		if (wantNameTag || wantGrades)
		{
			KeyValuePair<bool, TemplateItem> item = _003CitemHelper_003EP.GetItem(itemId);
			if (item.Key)
			{
				damage = EffectiveDamage(item.Value.Properties.Damage);
				penetration = item.Value.Properties.PenetrationPower;
				int tier = _ammoColouring.Tier(AmmoScore(penetration, damage));
				ammoLevel = tier + 1;
				ammoTierColour = AmmoTierColour(tier);
				if (wantGrades)
				{
					int penLevel = _ammoPenGrade.Pick(penetration.GetValueOrDefault()) + 1;
					int dmgLevel = _ammoDamageGrade.Pick(damage.GetValueOrDefault()) + 1;
					ammoGradeLine = DescriptionLine.AmmoGrades(penLevel, dmgLevel, ammoTierColour);
				}
			}
		}
		bool flag2 = wantNameTag && (damage.HasValue || penetration.HasValue);
		bool wantQualityTag = _cfg.QualityTagInName && qualityTier.HasValue;
		if (!flag && ammoGradeLine == null && !flag2 && !wantQualityTag)
		{
			return;
		}
		string formattedPrice = PriceBook.FormatRoubles(price, _moneyStyle);
		string textColour = (string.IsNullOrEmpty(_cfg.FleaBannedTextColour) ? "#c03a3a" : _cfg.FleaBannedTextColour);
		Dictionary<string, LazyLoad<GlobalLocaleDictionary>> localesGlobal = LocalesGlobal;
		bool flag3 = false;
		foreach (var (text2, dictionary2) in _originalLocales)
		{
			if (!localesGlobal.TryGetValue(text2, out var value))
			{
				continue;
			}
			string newDescription = null;
			if (flag)
			{
				string value2 = null;
				if (!dictionary2.TryGetValue(descriptionKey, out value2))
				{
					dictionary2.TryGetValue($"{itemId} description", out value2);
				}
				if (value2 != null)
				{
					(string, string, string) tuple = ValuationLabels.For(text2);
					string text3;
					if (availableOnFlea)
					{
						string label;
						if (!perSlot)
						{
							(label, _, _) = tuple;
						}
						else
						{
							label = tuple.Item2;
						}
						text3 = DescriptionLine.Price(label, formattedPrice, valueColour);
					}
					else
					{
						text3 = DescriptionLine.Banned(tuple.Item3, textColour);
					}
					string text4 = text3;
					newDescription = text4 + "\n" + value2;
					flag3 = true;
				}
			}
			if (ammoGradeLine != null)
			{
				string baseDesc = null;
				if (!dictionary2.TryGetValue(descriptionKey, out baseDesc))
				{
					dictionary2.TryGetValue($"{itemId} description", out baseDesc);
				}
				if (baseDesc != null)
				{
					newDescription = ((newDescription == null) ? (ammoGradeLine + "\n" + baseDesc) : (ammoGradeLine + "\n" + newDescription));
				}
			}
			string newName = null;
			if (flag2 && dictionary2.TryGetValue(nameKey, out var value3))
			{
				newName = DescriptionLine.AmmoTaggedName(value3, damage, penetration, ammoLevel, ammoTierColour);
			}
			else if (wantQualityTag && dictionary2.TryGetValue(nameKey, out var value4))
			{
				newName = QualityTaggedName(value4, qualityTier.Value);
			}
			if (newDescription == null && newName == null)
			{
				continue;
			}
			value.AddTransformer((GlobalLocaleDictionary? transformer) =>
			{
				if (newDescription != null)
				{
					transformer[descriptionKey] = newDescription;
				}
				if (newName != null)
				{
					transformer[nameKey] = newName;
				}
				return transformer;
			});
		}
		if (flag && !flag3)
		{
			_missingDescriptions++;
			if (_firstMissingDescription == null)
			{
				_firstMissingDescription = itemId.ToString();
			}
		}
	}

	private bool IsHaavkClientInstalled()
	{
		try
		{
			// SPT 4.1.5 的服务端工作目录与 5.0.0 不同，不能再用 Directory.GetCurrentDirectory().Parent
			// 推游戏根。改为从本 mod 自己的程序集位置逐级向上找带 BepInEx\plugins 的那一层；
			// 再用当前工作目录做同样上溯兜底。文件名统一走 HaavkClientAssemblyName 常量。
			if (ClientHalfUnder(Assembly.GetExecutingAssembly().Location))
			{
				return true;
			}
			return ClientHalfUnder(Directory.GetCurrentDirectory());
		}
		catch (Exception value)
		{
			_003Clogger_003EP.Error($"{"[Haavk.Server]"} 探测客户端半边失败，按未安装处理: {value}");
			return false;
		}
	}

	private bool ClientHalfUnder(string startPath)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(startPath);
		while (directoryInfo != null)
		{
			string path = System.IO.Path.Combine(directoryInfo.FullName, "BepInEx", "plugins");
			if (Directory.Exists(path) && Directory.EnumerateFiles(path, HaavkClientAssemblyName, SearchOption.AllDirectories).Any())
			{
				return true;
			}
			directoryInfo = directoryInfo.Parent;
		}
		return false;
	}

	private bool IsColourConverterInstalled()
	{
		string path = "rairai.colorconverterapi.dll";
		try
		{
			DirectoryInfo parent = new DirectoryInfo(Directory.GetCurrentDirectory()).Parent;
			if (parent == null)
			{
				return false;
			}
			return _003CfileUtil_003EP.FileExists(System.IO.Path.Combine(parent.FullName, "BepInEx", "plugins", path));
		}
		catch (Exception value)
		{
			_003Clogger_003EP.Error($"{"[Haavk.Server]"} 探测 ColorConverterAPI 失败: {value}");
			return false;
		}
	}

	private bool IsLiveFleaPricesInstalled()
	{
		return _003CinstalledMods_003EP.Any((SptMod x) => x.ModMetadata.ModGuid == "xyz.drakia.livefleaprices");
	}
}
