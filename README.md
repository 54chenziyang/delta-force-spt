# Delta Force SPT Mods（SPT 4.1.5）

把《逃离塔科夫》SPT 4.1.5 单人环境往《三角洲行动》的方向改的一组 mod 源码：单血量池、伤害方向反馈、状态 HUD、快速搜刮、物品估值与着色、商人交换自动补全、近战重做、地图路径点等。

本仓库只放**自己编写的源码**，不包含：

- 游戏本体、SPT 服务端文件、任何编译好的 `.dll`
- 第三方 mod 的二进制或源码
- 打包好的安装包（全量包约 1.5 GB，不在仓库内）
- 存档、`items.json` 等游戏数据文件

借用与移植来源逐条列在 [CREDITS.md](CREDITS.md)。

## 目录结构

```text
client/                 BepInEx 客户端插件（netstandard2.1）
  Haavk.Client/         Haavk 主插件 2.2.0（战斗 HUD / 搜刮 / 物品光柱 / 武器估值）
  Haavk.Core/           Haavk 共享库（颜色、价格、字体、图标光栅化等纯逻辑）
  DeltaAutoHeal/        战局结束自动治疗
  DeltaBallistics/      弹药伤害缩放与击退
  DeltaFallDamage/      线性势能跌落伤害
  DeltaHealthPool/      单血量池
  DeltaHitFeedback/     受击方向指示
  DeltaInventory/       双击装备 / 拖拽互换 / 拖到边缘丢弃
  DeltaMelee/           近战重做
  DeltaMovement/        姿态与跳跃手感
  DeltaQuickMove/       双击快速移动
  DeltaStatusHud/       状态 HUD
  HideoutPrice/         藏身处自动补全价格提示
  MapWaypoint/          地图路径点
  SptJsonFix/           JsonConverter 修复
  SptRaidTimeFix/       战局时间接口 Side 字段修复
  TraderFill/           商人以物易物自动补全（客户端）
server/                 SPT 服务端 mod（net10.0）
  Haavk.Server/         价格 / 估值 / 藏身处自动付款 / 武器总价路由
  TraderFillServer/     商人交换补全服务端
  DeltaTune/            数值调参（近战分层、战利品分层、Bot、幸运值）
installer/              v1.2 安装器源码（C# WinForms + csc 构建脚本）
licenses/               上游 MIT 许可原文
```

## 组件清单

| 组件 | 类型 | GUID / 元数据 | 版本 | 功能 |
|---|---|---|---|---|
| Haavk.Client | 客户端 | `com.haavk` | 2.2.0 | 战斗 HUD、击杀播报、搜刮增强、物品光柱、武器与装备估值、格子着色 |
| Haavk.Core | 共享库 | — | — | 上述功能的共享纯逻辑（价格档位、颜色编解码、PNG/图标光栅化、文本拼装） |
| DeltaAutoHeal | 客户端 | `com.haavk.deltaautoheal` | 1.0.0 | 战局结束后自动勾选全部治疗项目并付款，余额不足时把余额扣到 0 后放行 |
| DeltaBallistics | 客户端 | `com.haavk.deltaballistics` | 1.0.0 | 全局弹药伤害缩放 + 基于动量的击退 |
| DeltaFallDamage | 客户端 | `com.haavk.deltafalldamage` | 1.0.0 | 用线性势能 `g·h` 替换原版 `高度^1.5` 跌落伤害曲线，玩家与 AI 共用 |
| DeltaHealthPool | 客户端 | `com.haavk.deltahealthpool` | 1.0.0 | 所有部位共享一条总血量；部位伤害系数不同，部位血条只负责触发流血/骨折等异常 |
| DeltaHitFeedback | 客户端 | `com.haavk.deltahitfeedback` | 1.0.0 | 受击时准星周围出现指向伤害来源的红色弧形，并伴随屏幕边缘红闪 |
| DeltaInventory | 客户端 | `com.haavk.deltainventory` | 1.0.0 | 双击装备（可替换低价值物品）、拖拽交换格子位置（1 对 1 / 1 对多）、拖到右边缘丢弃 |
| DeltaMelee | 客户端 | `com.delta.melee` | 1.6.1 | 攻速、长按连击、贴身命中兜底、头甲穿透分层、保底伤害、踢击卡死防护 |
| DeltaMovement | 客户端 | `com.haavk.deltamovement` | 1.0.0 | 反射修改 `EFTHardSettings`：姿态切换速度、跳跃延迟、趴下对齐速度 |
| DeltaQuickMove | 客户端 | `com.haavk.deltaquickmove` | 1.1.0 | 双击把物品移入对应容器（仓库 / 弹挂 / 背包 / 出售栏） |
| DeltaStatusHud | 客户端 | `com.haavk.deltastatushud` | 1.1.0 | 总血量、部位小血条、能量/水分、状态效果徽章，仅战局内绘制 |
| HideoutPrice | 客户端 | `com.haavk.hideoutprice` | 1.0.0 | 藏身处建造面板显示「自动补全 ≈ X 卢布」 |
| MapWaypoint | 客户端 | `com.haavk.mapwaypoint` | 1.4.0 | 在地图上点选生成路径点，HUD 显示距离/边缘箭头，Delete 清除 |
| SptJsonFix | 客户端 | `com.haavk.jsonconvfix` | 1.0.0 | 保证 `JsonConvert.DefaultSettings` 携带 EFT 自己的转换器集合，修复物品反序列化失败导致战局卡死 |
| SptRaidTimeFix | 客户端 | `com.haavk.raidtimefix` | 1.0.0 | 把 `/singleplayer/settings/getRaidTime` 请求体里的整数 `Side` 改写为字符串 |
| TraderFill | 客户端 | `com.haavk.traderfill` | 1.0.0 | 商人以物易物界面一键补全所需物品 |
| Haavk.Server | 服务端 | `com.haavk.server` | 2.2.0 | 跳蚤价格与估值、物品着色、藏身处自动付款、武器总价路由（衍生自 Item Valuation） |
| TraderFillServer | 服务端 | `com.haavk.traderfill` | 1.0.0 | 商人交换补全的服务端实现 |
| DeltaTune | 服务端 | `com.delta.tune` | 1.0.0 | 近战武器分层、战利品按装备价值分层、Bot 血量/散布、战斗与幸运值参数 |

> 各插件具体的开关与数值都在游戏目录下 BepInEx / mod 的 `config.json` 或配置管理器中，源码里有默认值。

## 构建

前提：

- .NET SDK（客户端目标 `netstandard2.1`，服务端目标 `net10.0`）
- 一份可运行的 EFT + SPT 4.1.5 安装（提供 `Assembly-CSharp.dll` 等引用）
- BepInEx 5（`BepInEx\core` 下的 `BepInEx.dll`、`0Harmony.dll`）
- `BepInEx\plugins\spt` 下的 `spt-common.dll`、`spt-reflection.dll`

各 `.csproj` 里的路径是本机绝对路径，构建前请改成自己的安装位置：

```xml
<GameDir>D:\escape\EscapeFromTarkov</GameDir>
<ManagedDir>D:\escape\EscapeFromTarkov\EscapeFromTarkov_Data\Managed</ManagedDir>
<BepCore>D:\escape\EscapeFromTarkov\BepInEx\core</BepCore>
<SptPlug>D:\escape\EscapeFromTarkov\BepInEx\plugins\spt</SptPlug>
```

服务端工程还引用了 `SPT_Runtime` 下的 `SPTarkov.Server.Core.dll`、`SPTarkov.DI.dll`、`SPTarkov.Common.dll`、`SemanticVersioning.dll`、`0Harmony.dll`，同样需要按本机路径调整。

构建顺序：

1. `client/Haavk.Core`（`dotnet build -c Release`）
2. `client/Haavk.Client`（`Haavk.Client.csproj` 通过相对路径引用 `..\Haavk.Core\bin\Release\netstandard2.1\Haavk.Core.dll`）
3. 其余组件互相独立，可任意顺序构建

```powershell
dotnet build client/Haavk.Core/Haavk.Core.csproj -c Release
dotnet build client/Haavk.Client/Haavk.Client.csproj -c Release
```

## 安装

- 客户端插件：把编译出的 dll 放到 `BepInEx\plugins\<组件名>\`
  - `Haavk.Client.dll` 与 `Haavk.Core.dll` 必须放在同一目录
  - `Haavk.Client` 运行期会从插件目录读取 `icons\` 与 `sounds\`，这些二进制资源不在本仓库内
- 服务端 mod：把 dll 与 `config.json` 放到 `SPT_Runtime\user\mods\<组件名>\`，`Haavk.Server` 需要同时放入 `Haavk.Core.dll`

## 兼容性说明

- 目标版本：SPT 4.1.5（服务端元数据声明 `~4.1.0`），EFT 对应 Mono 运行时
- `Haavk.Client` 中的 `_Scaffold.cs` 是 BepInEx 6 → 5 的编译期桥接垫片（`BasePlugin`），不是反编译残留，构建时需要保留
- 早期 Haavk 与 QuickPrice 都会给物品着色，二者同时启用会互相覆盖；当前版本以 Haavk 为唯一着色权威
- 仓库不包含任何第三方 mod；CREDITS 中列出的 mod 仅说明运行环境与兼容性

## 许可

本项目以 MIT 发布，见 [LICENSE](LICENSE)。其中包含的上游衍生部分版权归原作者所有，原文见 [licenses/](licenses/) 与 [CREDITS.md](CREDITS.md)。

## 免责声明

仅供单人离线 mod 开发与学习使用。仓库不含任何游戏资源或商业素材，使用需自备正版《逃离塔科夫》与 SPT。请勿用于联机或任何违反游戏服务条款的场景。
