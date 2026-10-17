using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using EFT.HealthSystem;
using HarmonyLib;
using UnityEngine;

namespace DeltaFallDamage
{
    /// <summary>
    /// Replaces EFT's super-linear fall damage curve (damage ~ height^1.5) with a physically
    /// grounded model. Free-fall kinetic energy per unit mass is 0.5*v^2 = g*h, so the energy
    /// that must be absorbed on impact grows *linearly* with the fall height. Below a safe
    /// height the legs absorb everything; above it damage scales linearly up to a lethal dose.
    /// Applied to both the player and AI through the shared ActiveHealthController.
    /// </summary>
    [BepInPlugin("com.haavk.deltafalldamage", "Delta Fall Damage", "1.0.0")]
    public class DeltaFallDamagePlugin : BaseUnityPlugin
    {
        internal static DeltaFallDamagePlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> SafeHeightMeters;
        internal static ConfigEntry<float> LethalHeightMeters;
        internal static ConfigEntry<float> LethalDamagePerLeg;
        internal static ConfigEntry<bool> UseOverweightMultiplier;
        internal static ConfigEntry<bool> LogDiagnostics;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("Delta Fall Damage", "Enabled", true,
                "Master switch. When off, the vanilla height^1.5 fall damage curve is used.");
            SafeHeightMeters = Config.Bind("Delta Fall Damage", "Safe Height (m)", 3f,
                "Fall height in metres below which no damage is taken (legs absorb the impact).");
            LethalHeightMeters = Config.Bind("Delta Fall Damage", "Lethal Height (m)", 15f,
                "Fall height in metres that deals Lethal Damage Per Leg (roughly the real 50% fatality height).");
            LethalDamagePerLeg = Config.Bind("Delta Fall Damage", "Lethal Damage Per Leg", 65f,
                "Damage dealt to each leg at the lethal height. Damage scales linearly with height, so this is only one point on the curve.");
            UseOverweightMultiplier = Config.Bind("Delta Fall Damage", "Use Overweight Multiplier", true,
                "Keep the game's overweight / strength penalty (Player.Physical.FallDamageMultiplier).");
            LogDiagnostics = Config.Bind("Delta Fall Damage", "Log Diagnostics", false,
                "Log the computed fall damage to the BepInEx console.");

            _harmony = new Harmony("com.haavk.deltafalldamage");
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        /// <summary>
        /// Free fall: v = sqrt(2*g*h), energy per unit mass e = 0.5*v^2 = g*h  ->  linear in h.
        /// Model: 0 damage at or below the safe height, then a straight line up to
        /// LethalDamagePerLeg at the lethal height (no upper clamp, so the curve stays linear).
        /// Both legs take the same damage, mirroring the vanilla distribution.
        /// </summary>
        internal static float Apply(ActiveHealthController controller, float height)
        {
            if (controller == null || !controller.IsAlive)
            {
                return 0f;
            }

            float safe = SafeHeightMeters.Value;
            float lethal = Mathf.Max(LethalHeightMeters.Value, safe + 0.01f);
            if (height <= safe)
            {
                return 0f;
            }

            float t = (height - safe) / (lethal - safe);
            float damage = LethalDamagePerLeg.Value * t;

            if (UseOverweightMultiplier.Value)
            {
                try
                {
                    damage *= controller.Player.Physical.FallDamageMultiplier;
                }
                catch
                {
                    // Player / Physical not initialised yet; keep the base damage.
                }
            }

            if (damage <= 0f)
            {
                return 0f;
            }

            controller.ApplyDamage(EBodyPart.LeftLeg, damage, DamageHelper.FallDamage);
            controller.ApplyDamage(EBodyPart.RightLeg, damage, DamageHelper.FallDamage);

            if (LogDiagnostics.Value)
            {
                Log.LogInfo($"[FallDamage] height={height:F2} m -> {damage:F1} per leg");
            }

            return damage;
        }
    }

    [HarmonyPatch(typeof(ActiveHealthController), nameof(ActiveHealthController.HandleFall))]
    internal static class HandleFallPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ActiveHealthController __instance, float height, ref float __result)
        {
            if (__instance == null || !DeltaFallDamagePlugin.Enabled.Value)
            {
                return true;
            }

            __result = DeltaFallDamagePlugin.Apply(__instance, height);
            return false;
        }
    }
}
