using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using Comfort.Common;
using HarmonyLib;
using UnityEngine;

namespace DeltaMelee;

/// <summary>
/// 近战增强补丁（对照三角洲行动的近战手感）：
///   1) 攻速：服务端把「攻速倍率」写进刀具模板的 KnifeHitSlashRate / KnifeHitStabRate
///      （客户端原版不消费这两个字段），这里在读表时把它乘到挥砍动画速度上。
///   2) 连击：每帧直接读取「开火/瞄准」绑定键的真实物理按键状态，长按期间在 Idling
///      状态自动续接下一次挥砍，松开立即停止（不依赖原版只在松手时才会发出的 End* 命令）。
///   3) 命中兜底：原版 KnifeCollider 的 BoxCast 取样点沿视线反向偏移、结果缓冲仅 2 槽，
///      贴身时会被自身/护甲碰撞体占满导致永远打不到人；这里在「本次挥砍未命中」时
///      用前方锥形 Overlap 兜底补一次命中。
///   4) 头甲穿透分层：原版只看「命中哪个碰撞体」决定是否归零，导致低档刀打面罩与
///      高档刀打面罩结果一样、且高档甲对高档刀缺乏差异化。这里改为比较「刀穿透值
///      vs 甲实时防护值」，输出 5 级结果（完全格挡 / 大幅衰减 / 部分穿透 / 接近穿透 /
///      完全穿透），使高档刀能顶开低档面罩、低档刀被高档甲牢牢挡住。
///   5) 保底生命伤害：被完全格挡时原版会把生命伤害压到极低，这里给一个绝对下限
///      （默认 5 点），保证「磨」到最后总有反馈。
///   6) 卡死防护：原版 Kick 操作没有超时兜底，一旦「开火结束」事件丢失（例如在
///      animator 尚未回到待机时发起攻击）就会永久卡死。这里 (a) 只在 animator 允许
///      手部输入时才发起攻击（消除触发器），(b) 对 Kick 操作加超时监控，超时后走
///      原版 OnFireEnd 路径强制复位（消除结构性缺陷）。
///   注：近战对护甲耐久的损伤保持原版行为（ArmorDamage = 1），不做缩放。
/// </summary>
[BepInPlugin("com.delta.melee", "Delta Melee", "1.6.1")]
public class DeltaMeleePlugin : BaseUnityPlugin
{
    /// <summary>挥砍键是否处于按住状态（每帧从真实按键状态刷新）。</summary>
    internal static bool SlashHeld;

    /// <summary>刺击键是否处于按住状态（每帧从真实按键状态刷新）。</summary>
    internal static bool StabHeld;

    private static bool _firedThisIdle;
    private static float _nextAttemptTime;

    // ---- Kick 卡死防护 ----
    //  原版 Kick 操作只有 animator 事件一个出口；一旦该事件丢失就会永久卡死
    //  （无法交互 / 无法攻击）。这里记录「Kick 已持续多久」，超过阈值即强制复位。
    private static float _kickStuckSince = -1f;
    private static float _kickRecoveryTimeout = 3f;

    // ---- 按键按住状态来源 ----
    //  原版 KnifeInputTranslator 只消费按下瞬间的 ToggleShooting，不消费松开的 EndShooting，
    //  因此无法用命令事件跟踪「按住」状态（会导致松开后仍持续自动挥刀）。这里改为每帧直接
    //  读取「开火/瞄准」绑定键的真实物理按键状态（UnityEngine.Input.GetKey），做到「松开即停」。
    private static FieldInfo _presetField;
    private static FieldInfo _combosField;
    private static object _cachedPreset;
    private static KeyCode[] _slashKeys;
    private static KeyCode[] _stabKeys;

    // ---- 头甲穿透分层参数（ratio = 刀穿透值 / 甲实时防护值） ----
    //   ratio < T1        → 完全格挡（S0）
    //   T1 <= ratio < T2  → 大幅衰减（S1）
    //   T2 <= ratio < T3  → 部分穿透（S2）
    //   T3 <= ratio < T4  → 接近穿透（S3）
    //   ratio >= T4       → 完全穿透（S4）
    private static float _t1, _t2, _t3, _t4;
    private static float _s0, _s1, _s2, _s3, _s4;

    /// <summary>
    /// 保底生命伤害（点）：近战命中护甲时，即使被完全格挡也至少造成这么多伤害，
    /// 保证「磨」的过程中始终有反馈。
    /// </summary>
    internal static float MinHealthDamage = 5f;

    private void Awake()
    {
        var cfg = Config;
        _t1 = cfg.Bind("ArmorLayering", "Ratio_Block", 0.45f,
            "穿透比低于此值视为完全格挡（应用 Scale_Block）").Value;
        _t2 = cfg.Bind("ArmorLayering", "Ratio_HeavyLoss", 0.70f,
            "穿透比低于此值视为大幅衰减（应用 Scale_HeavyLoss）").Value;
        _t3 = cfg.Bind("ArmorLayering", "Ratio_Partial", 0.95f,
            "穿透比低于此值视为部分穿透（应用 Scale_Partial）").Value;
        _t4 = cfg.Bind("ArmorLayering", "Ratio_NearFull", 1.25f,
            "穿透比低于此值视为接近穿透，达到即完全穿透（应用 Scale_NearFull / Scale_Full）").Value;

        _s0 = cfg.Bind("ArmorLayering", "Scale_Block", 0.05f, "完全格挡时保留的伤害比例").Value;
        _s1 = cfg.Bind("ArmorLayering", "Scale_HeavyLoss", 0.30f, "大幅衰减时保留的伤害比例").Value;
        _s2 = cfg.Bind("ArmorLayering", "Scale_Partial", 0.55f, "部分穿透时保留的伤害比例").Value;
        _s3 = cfg.Bind("ArmorLayering", "Scale_NearFull", 0.80f, "接近穿透时保留的伤害比例").Value;
        _s4 = cfg.Bind("ArmorLayering", "Scale_Full", 1.00f, "完全穿透时保留的伤害比例").Value;

        MinHealthDamage = cfg.Bind("ArmorLayering", "MinHealthDamage", 5f,
            "近战命中护甲时的保底生命伤害（点）：即使被完全格挡也至少造成这么多伤害").Value;

        _kickRecoveryTimeout = cfg.Bind("Recovery", "KickStuckTimeout", 3f,
            "Kick 操作卡死超过此秒数则强制复位到待机（0 表示禁用该防护）").Value;

        Harmony harmony = new Harmony("com.delta.melee");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Logger.LogInfo("[DeltaMelee] 近战补丁已加载（攻速 / 连击 / 命中兜底 / 头甲穿透分层 / 保底伤害 / 卡死防护）");
    }

    /// <summary>按「穿透比」返回应保留的伤害比例（5 级分层）。</summary>
    internal static float ArmorLayeringScale(float ratio)
    {
        if (ratio < _t1) return _s0;
        if (ratio < _t2) return _s1;
        if (ratio < _t3) return _s2;
        if (ratio < _t4) return _s3;
        return _s4;
    }

    private void Update()
    {
        // 卡死兜底每帧都要跑（与是否按住攻击键无关）。
        MonitorKickStuck();

        RefreshBindingsIfNeeded();

        // 以每帧真实按键状态作为权威来源：按住才续击，松开立即停止。
        SlashHeld = IsHeld(_slashKeys, 0);
        StabHeld = IsHeld(_stabKeys, 1);

        if (!SlashHeld && !StabHeld)
        {
            _firedThisIdle = false;
            return;
        }

        try
        {
            var ctrl = GetLocalKnifeController();
            if (ctrl == null)
            {
                SlashHeld = false;
                StabHeld = false;
                _firedThisIdle = false;
                return;
            }

            // 只在「待机」状态续接，避免中途打断正在播放的挥砍动画。
            var op = ctrl.CurrentOperation;
            if (op == null || op.GetType().Name != "Idling")
            {
                _firedThisIdle = false;
                return;
            }

            // 关键守卫：仅当 animator 允许手部输入时才发起攻击。
            // 搜刮/上一刀结束后，操作状态可能已经回到 Idling，但 animator 仍在过渡中
            // （IsIdling 为 false）。此时发起攻击会让 SetFire 触发被吞掉 → Kick 永久卡死。
            if (!ctrl.CanInteract())
            {
                _firedThisIdle = false;
                return;
            }

            if (_firedThisIdle || Time.unscaledTime < _nextAttemptTime)
            {
                return;
            }

            _nextAttemptTime = Time.unscaledTime + 0.1f;
            bool ok = SlashHeld ? ctrl.MakeKnifeKick() : ctrl.MakeAlternativeKick();
            if (ok)
            {
                _firedThisIdle = true;
            }
            else
            {
                // 体力不足 / 状态不可用时降低重试频率，避免刷屏。
                _nextAttemptTime = Time.unscaledTime + 0.3f;
            }
        }
        catch
        {
            // 任何时候都不应影响原版近战流程。
        }
    }

    /// <summary>
    /// Kick 卡死兜底。原版 <c>Kick</c> 操作只有 animator 的「开火结束 / 待机开始」事件
    /// 一个出口，没有超时机制；一旦该事件丢失，操作就永久停在 Kick —— 表现为无法交互、
    /// 无法攻击、也不弹任何提示。
    ///
    /// 这里监控本地玩家的 <see cref="EFT.Player.KnifeController.CurrentOperation"/>：
    /// 一旦它处于 Kick 且持续超过 <see cref="_kickRecoveryTimeout"/> 秒，就调用
    /// <see cref="EFT.Player.KnifeController.OnFireEnd"/> 走原版的正常退出路径
    /// （清掉 Fire 标志并把操作切回 Idling），从而恢复。
    /// </summary>
    private void MonitorKickStuck()
    {
        try
        {
            if (_kickRecoveryTimeout <= 0f)
            {
                return;
            }

            var ctrl = GetLocalKnifeController();
            if (ctrl == null)
            {
                _kickStuckSince = -1f;
                return;
            }

            var op = ctrl.CurrentOperation;
            if (op == null || op.GetType().Name != "Kick")
            {
                _kickStuckSince = -1f;
                return;
            }

            // 刚进入 Kick：只记起始时刻。
            if (_kickStuckSince < 0f)
            {
                _kickStuckSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _kickStuckSince < _kickRecoveryTimeout)
            {
                return;
            }

            // 超时 → 强制复位。先清计数，避免复位过程中被重复触发。
            _kickStuckSince = -1f;
            ctrl.OnFireEnd();
            Logger.LogWarning("[DeltaMelee] 检测到近战 Kick 状态卡死，已强制复位到待机");
        }
        catch
        {
            _kickStuckSince = -1f;
        }
    }

    /// <summary>
    /// 从当前输入预设解析「开火(Shoot) / 瞄准(Aim)」实际绑定的物理按键。
    /// 预设或绑定发生变化时刷新；解析失败时置空，由 <see cref="IsHeld"/> 回退到鼠标键。
    /// </summary>
    private static void RefreshBindingsIfNeeded()
    {
        try
        {
            if (_presetField == null)
            {
                _presetField = typeof(EFT.InputSystem.InputManager).GetField(
                    "_currentPreset",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }

            object preset = _presetField != null ? _presetField.GetValue(null) : null;
            if (preset == null)
            {
                _cachedPreset = null;
                _slashKeys = null;
                _stabKeys = null;
                return;
            }

            // 预设未变且已成功解析过 → 无需重复反射。
            if (ReferenceEquals(preset, _cachedPreset) && (_slashKeys != null || _stabKeys != null))
            {
                return;
            }

            if (_combosField == null)
            {
                _combosField = preset.GetType().GetField(
                    "_workingKeyCombinations",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            var combos = _combosField != null ? _combosField.GetValue(preset) as System.Array : null;

            var slash = new List<KeyCode>(2);
            var stab = new List<KeyCode>(2);

            if (combos != null)
            {
                for (int i = 0; i < combos.Length; i++)
                {
                    if (!(combos.GetValue(i) is EFT.InputSystem.InputKeyCombination combo))
                    {
                        continue;
                    }

                    EFT.InputSystem.EGameKey gameKey = combo.GameKey;
                    bool isSlash = gameKey == EFT.InputSystem.EGameKey.Shoot;
                    bool isStab = gameKey == EFT.InputSystem.EGameKey.Aim;
                    if (!isSlash && !isStab)
                    {
                        continue;
                    }

                    var keys = combo.InputKeys;
                    if (keys == null)
                    {
                        continue;
                    }

                    for (int k = 0; k < keys.Length; k++)
                    {
                        if (keys[k] is EFT.InputSystem.InputKey key)
                        {
                            (isSlash ? slash : stab).Add(key.Key);
                        }
                    }
                }
            }

            _cachedPreset = preset;
            _slashKeys = slash.Count > 0 ? slash.ToArray() : null;
            _stabKeys = stab.Count > 0 ? stab.ToArray() : null;
        }
        catch
        {
            _cachedPreset = null;
            _slashKeys = null;
            _stabKeys = null;
        }
    }

    /// <summary>
    /// 绑定键中任意一个按住即视为按住；解析失败时回退到鼠标左/右键。
    /// 直接读取游戏每帧更新的池化 <see cref="EFT.InputSystem.InputKey"/> 状态（Down/Hold 视为按住），
    /// 与游戏自身的输入矩阵保持一致。
    /// </summary>
    private static bool IsHeld(KeyCode[] keys, int fallbackMouseButton)
    {
        if (keys != null && keys.Length > 0)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (EFT.InputSystem.InputKey.GetKey(keys[i]).GetValue() > 0.5f)
                {
                    return true;
                }
            }

            return false;
        }

        KeyCode fallback = fallbackMouseButton == 0 ? KeyCode.Mouse0 : KeyCode.Mouse1;
        return EFT.InputSystem.InputKey.GetKey(fallback).GetValue() > 0.5f;
    }

    private static EFT.Player.KnifeController GetLocalKnifeController()
    {
        if (!Singleton<EFT.GameWorld>.Instantiated)
        {
            return null;
        }

        var world = Singleton<EFT.GameWorld>.Instance;
        if (world == null)
        {
            return null;
        }

        var player = world.MainPlayer;
        if (player == null)
        {
            return null;
        }

        return player.HandsController as EFT.Player.KnifeController;
    }
}

[HarmonyPatch(typeof(EFT.Player.BaseKnifeController), "SetMeleeSpeed")]
internal static class PatchSetMeleeSpeed
{
    [HarmonyPrefix]
    private static void Prefix(EFT.Player.BaseKnifeController __instance, ref float speed)
    {
        try
        {
            if (__instance == null)
            {
                return;
            }

            var knife = __instance.Knife;
            if (knife == null)
            {
                return;
            }

            var tpl = knife.Template;
            if (tpl == null)
            {
                return;
            }

            bool slash = __instance.LastKickType == EFT.Player.EKickType.Slash;
            float mult = slash ? tpl.KnifeHitSlashRate : tpl.KnifeHitStabRate;

            // 原版字段通常为 0 或 1，只有在合理区间内才视为有效攻速倍率。
            if (mult > 0.05f && mult < 5f)
            {
                speed *= mult;
            }
        }
        catch
        {
            // 任何异常都不应影响原版近战流程。
        }
    }
}

/// <summary>
/// 贴身命中兜底。原版 ManualUpdate 仅在 OnHit != null（本次挥砍尚未命中）时调用本方法，
/// 而命中成功后 <c>ProcessOverlap</c> 会把自己从 OnHit 上摘掉 —— 因此本 Postfix
/// 天然「每次挥砍至多补一次」。
/// </summary>
[HarmonyPatch(typeof(EFT.KnifeCollider), "ManualUpdateClient")]
internal static class PatchKnifeHitFallback
{
    private static readonly Collider[] Buffer = new Collider[32];

    [HarmonyPostfix]
    private static void Postfix(EFT.KnifeCollider __instance)
    {
        try
        {
            // OnHit 为空 = 本次挥砍已经命中；原版 BoxCast 已找到目标则同样不会走到这里。
            if (__instance == null || __instance.OnHit == null)
            {
                return;
            }

            var orient = (__instance.GetPlayerOrientation != null)
                ? __instance.GetPlayerOrientation()
                : __instance.transform.forward;

            var origin = __instance.transform.position + orient * 0.25f;
            float radius = Mathf.Clamp(__instance.MaxDistance + 0.45f, 0.6f, 1.3f);

            int count = Physics.OverlapSphereNonAlloc(
                origin, radius, Buffer, __instance._hitMask, QueryTriggerInteraction.Collide);

            Collider best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = Buffer[i];
                if (col == null)
                {
                    continue;
                }

                // 排除自己身上的骨骼/护甲碰撞体。
                if (!__instance.AllCollidersNotYours(col))
                {
                    continue;
                }

                // 只取可承伤的碰撞体，避免补出一次「空刀」把本次挥砍的命中机会用掉。
                if (col.GetComponent<EFT.Ballistics.BallisticCollider>() == null)
                {
                    continue;
                }

                var to = col.transform.position - origin;
                if (to.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                // 只认前方（约 ±80°）目标，避免背后/侧后的敌人被平白砍到。
                if (Vector3.Dot(orient, to.normalized) < 0.15f)
                {
                    continue;
                }

                if (to.sqrMagnitude < bestSqr)
                {
                    bestSqr = to.sqrMagnitude;
                    best = col;
                }
            }

            if (best == null)
            {
                return;
            }

            var kh = default(EFT.Player.KnifeRaycastHit);
            kh.collider = best;
            kh.point = best.ClosestPoint(origin);
            kh.normal = -orient;
            __instance.OnHit(kh);
        }
        catch
        {
        }
    }
}

/// <summary>
/// 头甲伤害穿透分层。原版在 <see cref="EFT.Player.ProceedDamageThroughArmor"/> 内按
/// 「命中哪个碰撞体」决定是否归零：普通身体部位走 <see cref="EFT.InventoryLogic.ArmorComponent.ApplyDamage"/>
/// 的 CF（被 <c>Clamp(…,0.6,1)</c> 压得很平），面罩/护脸板则一律 <c>Damage = 0</c>。
/// 这里改为在结算后用「刀穿透值 / 甲实时防护值」重新分层覆盖生命伤害，使穿透结果
/// 与刀档、甲档强相关（详见 <see cref="DeltaMeleePlugin.ArmorLayeringScale"/>）。
///
/// 该方法是近战伤害进入护甲结算的唯一入口（全库仅 <see cref="EFT.Player.ApplyShot"/> 调用），
/// 且本地局 bot 为真实 <see cref="EFT.Player"/>，故此处改动同时作用于本地玩家与 bot 的受击方。
/// 只覆盖生命伤害（下游 <c>ApplyDamageInfo → ActiveHealthController.ApplyDamage</c>），
/// 护甲耐久损伤保持原版，不做缩放。
/// </summary>
[HarmonyPatch(typeof(EFT.Player), "ProceedDamageThroughArmor")]
internal static class PatchArmorPenetrationLayering
{
    private sealed class State
    {
        public bool IsMelee;
        public float Damage0;
        public float Pen0;
    }

    [HarmonyPrefix]
    private static void Prefix(ref EFT.Ballistics.DamageInfo damageInfo, out State __state)
    {
        __state = null;
        try
        {
            if (damageInfo.DamageType != EFT.EDamageType.Melee)
            {
                return;
            }

            __state = new State
            {
                IsMelee = true,
                Damage0 = damageInfo.Damage,
                Pen0 = damageInfo.PenetrationPower,
            };
        }
        catch
        {
            __state = null;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(
        ref EFT.Ballistics.DamageInfo damageInfo,
        System.Collections.Generic.List<EFT.InventoryLogic.ArmorComponent> __result,
        State __state)
    {
        try
        {
            if (__state == null || !__state.IsMelee)
            {
                return;
            }

            // 无护甲覆盖命中部位 → 保持全额刀伤。
            if (__result == null || __result.Count == 0)
            {
                return;
            }

            // 取防护值最高的那件甲作为比较基准（已含 ArmorClass 与当前耐久）。
            float bestResistance = 0f;
            for (int i = 0; i < __result.Count; i++)
            {
                var armor = __result[i];
                if (armor == null)
                {
                    continue;
                }

                var repairable = armor.Repairable;
                if (repairable == null)
                {
                    continue;
                }

                float templateDurability = repairable.TemplateDurability;
                if (templateDurability <= 0f)
                {
                    continue;
                }

                var data = ShotSharedMethods.RealResistance(
                    repairable.Durability, templateDurability, armor.ArmorClass, __state.Pen0);

                if (data.RealResistance > bestResistance)
                {
                    bestResistance = data.RealResistance;
                }
            }

            if (bestResistance <= 0.01f)
            {
                return;
            }

            float ratio = __state.Pen0 / bestResistance;
            float damage = __state.Damage0 * DeltaMeleePlugin.ArmorLayeringScale(ratio);

            // 保底生命伤害：被完全格挡时也至少造成这么多，保证「磨」的过程有反馈。
            if (damage < DeltaMeleePlugin.MinHealthDamage)
            {
                damage = DeltaMeleePlugin.MinHealthDamage;
            }

            damageInfo.Damage = damage;
        }
        catch
        {
            // 任何异常都不应影响原版结算流程。
        }
    }
}

