# 第三方组件与致谢

本项目是一个《逃离塔科夫》单人版（SPT 4.1.5）的玩法改造 mod 集合，**在已有第三方开源 mod 的基础上做了大量二次开发**。

本仓库**只包含自行编写与二次开发的源码**，不包含任何第三方 mod 的二进制文件（DLL）、游戏本体资源或 SPT 官方文件。下面逐项说明借用来源、原作者与移植范围。

---

## 一、代码衍生的第三方项目（本仓库包含衍生源码）

### 1. Item Valuation 2.0.1

- 作者：**acidphantasm**
- 来源：<https://hub.sp-tarkov.com/files/file/2789-item-valuation/>
- 许可：MIT（原文见 [licenses/ItemValuation-MIT.txt](licenses/ItemValuation-MIT.txt)）
- 涉及目录：`server/Haavk.Server/`
- 移植范围：
  - 服务端价格统计与估值的整体思路（`FleaOfferStats`、`WeaponTotalPriceStore`、`WeaponTotalPayload`、`WeaponTotalCallbacks`）
  - 客户端 `client/Haavk.Client/src/Haavk.WeaponValue/`（`PriceClient`、`WeaponTotalCalculator`、`GearValuePanel`、`TaxonomyColourMapping` 等）
  - 跳蚤市场品质筛选 `FleaQualityFilterPatch`、格子弹窗价格 `GridTooltipPatch`、装备页价格 `EquipmentTabPatch`
- 原创部分：`WeaponTotalStaticRouter`（武器总价静态路由）为本项目自行编写，非上游代码。

### 2. AutoCorpseSearch

- 作者：**maschine34675**
- 来源：<https://github.com/maschine34675/AutoCorpseSearch>
- 许可：MIT（原文见 [licenses/AutoSearch-MIT.txt](licenses/AutoSearch-MIT.txt)）
- 涉及文件：`client/Haavk.Client/src/Haavk.Looting/CorpseSearch.cs`、`SearchIndicator.cs`、`SearchTakeover.cs`
- 移植范围：尸体自动搜索的触发与接管逻辑（本项目在此基础上做了重写与交互改造）。

### 3. LootSearchSpeed

- 作者：**moon11yy**
- 来源：<https://github.com/moon11yy/LootSearchSpeed>
- 许可：MIT（原文见 [licenses/AutoSearch-MIT.txt](licenses/AutoSearch-MIT.txt)）
- 涉及文件：`client/Haavk.Client/src/Haavk.Looting.Patches/SearchInstantPatch.cs`
- 移植范围：容器初始搜索延时的 Transpiler 补丁。上游的 `ItemRevealDelayMultiplier`（逐件揭示延时）**未移植**。

### 4. QuickPrice V2.0.0（早期参考与移植来源）

- 作者：**只爱可爱的你**
- 服务端 GUID：`com.quickprice.spt`，路由前缀 `/showMeTheMoney/*`
- 本项目参考其价格查询、物品品质着色、搜索音效的实现思路，在 `Haavk.Client` / `Haavk.Core` 中**重新实现**：
  - 品质着色：`Haavk.Core/TaxonomyColourCodec.cs`、`CategoryColouring.cs`、`PerSlotColouring.cs`、`ArmourClassColouring.cs`
  - 价格查询与展示：`Haavk.WeaponValue/*`、`Haavk.Core/PriceBook.cs`、`MoneyFormat*.cs`
  - 搜索音效：`Haavk.Core/CombatSound*.cs`、`Haavk.Looting/Chime*.cs`
- 说明：本仓库**不包含 QuickPrice 的任何原始代码或二进制文件**；若需完整功能请自行安装 QuickPrice。
- 兼容性提示：QuickPrice 与本项目都会给物品标签着色，同时启用时可能出现配色冲突，建议只保留一个着色来源（本项目默认以 Haavk 为唯一着色权威）。

---

## 二、运行环境依赖的第三方 mod（不随本仓库分发）

以下 mod 由玩家自行安装，本仓库不含其代码，仅在此致谢（部分功能与它们共存/互补）：

| mod | 作者 |
| --- | --- |
| Tyfon.UIFixes（客户端 / 服务端） | Tyfon |
| RZDirectReload | RZ |
| AnanasCharles-StatDeltaPreview | AnanasCharles |
| Classic Reload | — |
| FromHell.TacticalSprint | FromHell |
| ModdingStatsHelper | Wara |
| HudBreakout | deadwolf |
| maschine 系列（QuestMarkers、Anvil-WebOverlay、AutoIFF、TraderSearch、CombatLog、MoreQuickSlots） | maschine34675 |
| QuestPresenceDetector | Lacyway |
| Breakout Outlines | harmonyzt |
| Armor Class Icon / Advanced Stash Sorting | slpf |
| flir.iof | flir063 |
| CactusPie.ContainerQuickLoot | hayk4700（移植） |
| QuestHandbook | — |
| mpstark-dynamicmaps | mpstark |
| InjectorInspect | — |
| QuickSell / QuickAim / QuickMeds / QuickLootF | — |
| LootNet / LootInVicinity | — |
| CabinetLooter / NestedQuickMove | — |
| SmajlecStamina | Smajlec |
| Manimal-QuickCancel | Manimal |
| 7Bpencil.BrighterInteriors | 7Bpencil |
| RaiRai.ColorConverterAPI | RaiRai |

---

## 三、本仓库原创部分

除上述明确标注的衍生代码外，以下均为本项目自行编写：

- `client/Delta*`（DeltaAutoHeal、DeltaBallistics、DeltaFallDamage、DeltaHealthPool、DeltaHitFeedback、DeltaInventory、DeltaMelee、DeltaMovement、DeltaQuickMove、DeltaStatusHud）
- `client/HideoutPrice`、`client/MapWaypoint`、`client/SptJsonFix`、`client/SptRaidTimeFix`、`client/TraderFill`
- `client/Haavk.Client/src/Haavk.Combat/*`（击杀播报、受击方向、准星）
- `client/Haavk.Client/src/Haavk.LootSense/*`（物资感知标记）
- `client/Haavk.Client/src/Haavk.Diagnostics/*`（自动选 PVE、调试指令）
- `server/DeltaTune`（近战 / 战利品 / 机器人参数调优）
- `server/TraderFillServer`（商人交换自动补全）
- `installer/*`（一键安装器）
- `client/Haavk.Client/_Scaffold.cs`（BepInEx 6 → 5 的 `BasePlugin` 桥接垫片）

---

## 四、许可

本项目自身代码以 MIT 许可发布，见 [LICENSE](LICENSE)。

被借用部分的版权归各自原作者所有，其原始 MIT 许可全文保留于 [licenses/](licenses/) 目录。二次修改部分版权归 **liujiaxing (Leo3306)** 所有。
