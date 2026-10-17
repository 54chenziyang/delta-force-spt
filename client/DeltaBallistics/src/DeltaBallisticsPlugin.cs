using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace DeltaBallistics
{
    /// <summary>
    /// Delta-style ballistics:
    ///  1) Global ammo damage reduction (Delta Force feel) by scaling every AmmoTemplate.Damage.
    ///  2) Physically grounded knockback: the bullet's momentum is transferred to the target and
    ///     applied as a short, decaying displacement via the character controller.
    /// </summary>
    [BepInPlugin("com.haavk.deltaballistics", "Delta Ballistics", "1.0.0")]
    public class DeltaBallisticsPlugin : BaseUnityPlugin
    {
        internal static DeltaBallisticsPlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> DamageMultiplier;
        internal static ConfigEntry<float> KnockbackScale;
        internal static ConfigEntry<float> CharacterMassKg;
        internal static ConfigEntry<float> KnockbackDuration;
        internal static ConfigEntry<float> MaxKnockbackSpeed;
        internal static ConfigEntry<float> VerticalFactor;
        internal static ConfigEntry<bool> ApplyKnockbackToAI;
        internal static ConfigEntry<bool> LogDiagnostics;

        private Harmony _harmony;

        // Damage scaling bookkeeping. Keyed by template object reference so re-applying is
        // idempotent even if the game reloads templates.
        private ItemFactory _scaledFactory;
        private float _rescanTimer;
        private readonly Dictionary<AmmoTemplate, int> _originalDamage = new Dictionary<AmmoTemplate, int>();

        // Ammo lookup for knockback: template id (24-char) -> template.
        internal static readonly Dictionary<string, AmmoTemplate> AmmoById =
            new Dictionary<string, AmmoTemplate>(StringComparer.Ordinal);

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("Delta Ballistics", "Enabled", true,
                "Master switch for the Delta ballistics changes (damage scaling + knockback).");
            DamageMultiplier = Config.Bind("Delta Ballistics", "Damage Multiplier", 0.30f,
                "Multiplier applied to every ammo template's Damage. 0.30 = Delta-style harsh reduction.");
            KnockbackScale = Config.Bind("Delta Ballistics", "Knockback Scale", 25f,
                "Scientific amplification factor. Raw momentum transfer (bullet momentum / body mass) is tiny; "
                + "this scales it up to a visible impulse.");
            CharacterMassKg = Config.Bind("Delta Ballistics", "Character Mass (kg)", 80f,
                "Assumed mass of the hit character used in the momentum equation.");
            KnockbackDuration = Config.Bind("Delta Ballistics", "Knockback Duration (s)", 0.12f,
                "Seconds over which the knockback velocity decays linearly to zero.");
            MaxKnockbackSpeed = Config.Bind("Delta Ballistics", "Max Knockback Speed (m/s)", 6f,
                "Speed cap for accumulated knockback on a single character.");
            VerticalFactor = Config.Bind("Delta Ballistics", "Vertical Knockback Factor", 0.35f,
                "How much of the bullet direction's vertical component is kept (0 = purely horizontal).");
            ApplyKnockbackToAI = Config.Bind("Delta Ballistics", "Apply Knockback To AI", true,
                "Also push AI characters when they are hit.");
            LogDiagnostics = Config.Bind("Delta Ballistics", "Log Diagnostics", false,
                "Log a line for every knockback impulse (developer use).");

            try
            {
                _harmony = new Harmony("com.haavk.deltaballistics");
                _harmony.PatchAll(typeof(DamagePatch));
                Log.LogInfo("DeltaBallistics: patched Player.ApplyDamageInfo");
            }
            catch (Exception e)
            {
                Log.LogError("DeltaBallistics: patch failed: " + e);
            }

            GameObject go = new GameObject("DeltaBallisticsKnockback");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<KnockbackManager>();

            Log.LogInfo("Delta Ballistics v1.0.0 loaded");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        private void Update()
        {
            if (!Enabled.Value) return;

            try
            {
                ApplyDamageScaling();
            }
            catch (Exception e)
            {
                Log.LogError("DeltaBallistics scaling error: " + e);
            }
        }

        private void ApplyDamageScaling()
        {
            if (!Singleton<ItemFactory>.Instantiated) return;
            ItemFactory factory = Singleton<ItemFactory>.Instance;
            if (factory == null || factory.ItemTemplates == null) return;

            bool factoryChanged = !ReferenceEquals(factory, _scaledFactory);
            if (!factoryChanged)
            {
                _rescanTimer -= Time.unscaledDeltaTime;
                if (_rescanTimer > 0f) return;
            }
            _rescanTimer = 2f;
            _scaledFactory = factory;

            float mult = Mathf.Clamp(DamageMultiplier.Value, 0.01f, 5f);
            AmmoById.Clear();

            int count = 0;
            foreach (KeyValuePair<MongoID, ItemTemplate> kv in factory.ItemTemplates)
            {
                if (!(kv.Value is AmmoTemplate ammo)) continue;

                if (!_originalDamage.TryGetValue(ammo, out int original))
                {
                    original = ammo.Damage;
                    _originalDamage[ammo] = original;
                }

                int scaled = Mathf.Max(1, Mathf.RoundToInt(original * mult));
                if (ammo.Damage != scaled) ammo.Damage = scaled;

                AmmoById[kv.Key.ToString()] = ammo;
                count++;
            }

            if (count > 0 && LogDiagnostics.Value)
                Log.LogInfo($"DeltaBallistics: scaled {count} ammo templates (x{mult:F2})");
        }
    }

    [HarmonyPatch(typeof(Player), "ApplyDamageInfo")]
    internal static class DamagePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, DamageInfo damageInfo)
        {
            if (!DeltaBallisticsPlugin.Enabled.Value) return;

            try
            {
                if (__instance == null) return;
                if (__instance.IsAI && !DeltaBallisticsPlugin.ApplyKnockbackToAI.Value) return;
                if (__instance.HealthController != null && !__instance.HealthController.IsAlive) return;

                string sourceId = damageInfo.SourceId;
                if (string.IsNullOrEmpty(sourceId) || sourceId.Length != 24) return;

                AmmoTemplate ammo;
                if (!DeltaBallisticsPlugin.AmmoById.TryGetValue(sourceId, out ammo) || ammo == null) return;

                float massKg = Mathf.Max(0f, ammo.BulletMassGram) / 1000f;
                float speed = Mathf.Max(0f, ammo.InitialSpeed);
                if (massKg <= 0f || speed <= 0f) return;

                float bodyMass = Mathf.Max(20f, DeltaBallisticsPlugin.CharacterMassKg.Value);
                float scale = DeltaBallisticsPlugin.KnockbackScale.Value;

                // Momentum conservation: dv = (m_bullet * v_muzzle) / M_body, then amplified.
                float dv = massKg * speed / bodyMass * scale;

                Vector3 dir = damageInfo.Direction;
                if (dir.sqrMagnitude < 1e-6f) return;
                dir.Normalize();
                dir = new Vector3(dir.x, dir.y * Mathf.Clamp01(DeltaBallisticsPlugin.VerticalFactor.Value), dir.z);
                if (dir.sqrMagnitude < 1e-6f) return;
                dir.Normalize();

                KnockbackManager mgr = KnockbackManager.Instance;
                if (mgr != null) mgr.AddImpulse(__instance, dir * dv);

                if (DeltaBallisticsPlugin.LogDiagnostics.Value)
                    DeltaBallisticsPlugin.Log.LogInfo(
                        $"DeltaBallistics knockback: {(__instance.IsAI ? "AI" : "Player")} " +
                        $"dv={dv:F3} m/s (mass={massKg * 1000f:F1}g speed={speed:F0}m/s)");
            }
            catch (Exception e)
            {
                DeltaBallisticsPlugin.Log.LogError("DeltaBallistics knockback error: " + e);
            }
        }
    }

    /// <summary>
    /// Applies accumulated knockback velocity to characters over a short, linearly decaying window
    /// by driving their ICharacterController directly (works for the local player and AI impostors).
    /// </summary>
    internal class KnockbackManager : MonoBehaviour
    {
        internal static KnockbackManager Instance;

        private sealed class State
        {
            internal Vector3 Velocity;
            internal float Elapsed;
            internal float Duration;
        }

        private readonly Dictionary<Player, State> _states = new Dictionary<Player, State>();
        private readonly List<Player> _finished = new List<Player>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        internal void AddImpulse(Player player, Vector3 velocity)
        {
            if (player == null) return;

            State state;
            if (!_states.TryGetValue(player, out state))
            {
                state = new State();
                _states[player] = state;
            }

            state.Velocity += velocity;

            float max = Mathf.Max(0.5f, DeltaBallisticsPlugin.MaxKnockbackSpeed.Value);
            if (state.Velocity.sqrMagnitude > max * max)
                state.Velocity = state.Velocity.normalized * max;

            state.Elapsed = 0f;
            state.Duration = Mathf.Max(0.02f, DeltaBallisticsPlugin.KnockbackDuration.Value);
        }

        private void Update()
        {
            if (_states.Count == 0) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _finished.Clear();

            foreach (KeyValuePair<Player, State> kv in _states)
            {
                Player player = kv.Key;
                State state = kv.Value;

                if (player == null || state.Elapsed >= state.Duration)
                {
                    _finished.Add(player);
                    continue;
                }

                ICharacterController cc;
                try { cc = player.CharacterController; }
                catch { cc = null; }
                if (cc == null)
                {
                    _finished.Add(player);
                    continue;
                }

                float factor = 1f - state.Elapsed / state.Duration;
                try { cc.Move(state.Velocity * factor * dt, dt); }
                catch { }

                state.Elapsed += dt;
            }

            for (int i = 0; i < _finished.Count; i++)
                _states.Remove(_finished[i]);
        }
    }
}
