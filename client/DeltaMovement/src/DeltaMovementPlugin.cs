using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace DeltaMovement;

/// <summary>
/// 位移手感补丁（对照三角洲行动）——消除「跳跃 / 下蹲 / 趴下」的动作延迟，让按键立即生效。
///
/// 机制（全部走 EFTHardSettings 这个全局 ScriptableObject，改动只影响对应动作）：
///   1) 下蹲 / 趴下：MovementContext.SmoothPoseLevel 用
///      Mathf.Lerp(SmoothedPoseLevel, 目标, deltaTime * POSE_CHANGING_SPEED * 技能速度)
///      逐帧逼近目标姿势。原版 POSE_CHANGING_SPEED=3 太黏，按住蹲键要拖一段才到位；
///      适度调大（默认 15）后每帧过渡比例更高、几乎立刻进入蹲姿，且仍是逐帧平滑过渡，
///      不会像调得过大（如 500）那样一帧吸附、看起来像跳帧。体力消耗依旧累积，不破坏平衡。
///   2) 跳跃前摇：JumpPlayerState.Enter 读取 _liftDelay = JUMP_DELAY_BY_SPEED.Evaluate(初速)，
///      起跳后 _stateTime &lt; _liftDelay 期间升力为 0（这就是「按了跳却要等一下才离地」）。
///      把 JUMP_DELAY_BY_SPEED 换成常量 0 后 _liftDelay = 0，按键当帧即产生升力。
///      起跳速度曲线 LIFT_VELOCITY_BY_SPEED 保持不变，跳跃高度/手感不受影响。
///   3) 趴下贴地：MovementContext 用 Vector3.Lerp(..., deltaTime * PRONE_ALIGN_SPEED) 把身体
///      对齐地面，适当调大可以更快「落地成趴」。
///
/// 不写任何 Harmony 补丁：EFTHardSettings.Instance 是所有读取方的唯一入口，直接改字段即可。
/// </summary>
[BepInPlugin("com.haavk.deltamovement", "DeltaMovement", "1.0.0")]
public class DeltaMovementPlugin : BaseUnityPlugin
{
    private static readonly FieldInfo InstanceField =
        typeof(EFTHardSettings).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>已写入参数的 EFTHardSettings 实例，用于避免每帧重复写。</summary>
    private static EFTHardSettings _applied;

    private float _poseChangingSpeed;
    private float _proneAlignSpeed;
    private bool _removeJumpDelay;

    private void Awake()
    {
        ConfigEntry<float> pose = Config.Bind(
            "Movement", "PoseChangingSpeed", 15f,
            "下蹲/趴下姿势切换速度（原版 3）。适当调大后姿势切换更快但仍逐帧平滑过渡；" +
            "调得过大（如 500）会让姿势瞬间吸附、看起来像跳帧，甚至会干扰趴下判定。");
        ConfigEntry<float> prone = Config.Bind(
            "Movement", "ProneAlignSpeed", 10f,
            "趴下时身体贴地速度（原版 5）。适当调大后趴下贴地更快；过大反而会抖动。");
        ConfigEntry<bool> jump = Config.Bind(
            "Movement", "RemoveJumpDelay", true,
            "移除跳跃前摇延迟：置为 true 后按下跳跃键当帧即离地（原版会按移动速度延迟起跳）。");

        _poseChangingSpeed = pose.Value;
        _proneAlignSpeed = prone.Value;
        _removeJumpDelay = jump.Value;

        Logger.LogInfo("[DeltaMovement] 位移补丁已加载（跳跃/下蹲/趴下 无延迟）");
    }

    private void Update()
    {
        try
        {
            EFTHardSettings settings = GetLoadedInstance();
            if (settings == null || ReferenceEquals(settings, _applied))
            {
                return;
            }

            _applied = settings;
            Apply(settings);
        }
        catch (Exception e)
        {
            // 失败就重置，下一帧继续尝试；绝不影响原版移动。
            _applied = null;
            Logger.LogError("[DeltaMovement] 应用位移参数失败: " + e);
        }
    }

    /// <summary>
    /// 只在 EFTHardSettings 真正加载完成后才取实例：直接访问 EFTHardSettings.Instance 会在
    /// 尚未加载时触发一次同步加载并打出 CPU spike 警告，这里读它的私有静态缓存 _instance 代替。
    /// </summary>
    private static EFTHardSettings GetLoadedInstance()
    {
        if (InstanceField == null)
        {
            return null;
        }

        return InstanceField.GetValue(null) as EFTHardSettings;
    }

    private void Apply(EFTHardSettings settings)
    {
        settings.POSE_CHANGING_SPEED = _poseChangingSpeed;
        settings.PRONE_ALIGN_SPEED = _proneAlignSpeed;

        if (_removeJumpDelay)
        {
            settings.JUMP_DELAY_BY_SPEED = AnimationCurve.Constant(0f, 1f, 0f);
        }

        Logger.LogInfo(string.Format(
            "[DeltaMovement] 已应用: POSE_CHANGING_SPEED={0} PRONE_ALIGN_SPEED={1} 跳跃前摇={2}",
            settings.POSE_CHANGING_SPEED,
            settings.PRONE_ALIGN_SPEED,
            _removeJumpDelay ? "已移除" : "保留原版"));
    }
}
