using System;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.HealthSystem;
using HarmonyLib;
using UnityEngine;

namespace DeltaHealthPool
{
    /// <summary>
    /// Delta-style single health pool: every body part of a character shares one global HP bar.
    /// Different body parts apply different damage multipliers, and the vanilla per-part health
    /// bars are kept only to trigger negative effects (bleeding / fracture / blacked-out limbs),
    /// never to decide life or death. Only the global pool reaching zero kills the character.
    /// Applies to both the player and AI.
    /// </summary>
    [BepInPlugin("com.haavk.deltahealthpool", "Delta Health Pool", "1.0.0")]
    public class DeltaHealthPoolPlugin : BaseUnityPlugin
    {
        internal static DeltaHealthPoolPlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> BasePool;
        internal static ConfigEntry<float> HeadMultiplier;
        internal static ConfigEntry<float> ChestMultiplier;
        internal static ConfigEntry<float> StomachMultiplier;
        internal static ConfigEntry<float> LimbMultiplier;
        internal static ConfigEntry<bool> ShowHud;
        internal static ConfigEntry<float> HudX;
        internal static ConfigEntry<float> HudY;
        internal static ConfigEntry<float> HudWidth;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("Delta Health Pool", "Enabled", true,
                "Master switch. When off, vanilla per-body-part health behaves normally.");
            BasePool = Config.Bind("Delta Health Pool", "Base Pool", 100f,
                "Size of the shared character health pool.");
            HeadMultiplier = Config.Bind("Delta Health Pool", "Head Multiplier", 2.5f,
                "Damage multiplier applied to the shared pool for hits on the head.");
            ChestMultiplier = Config.Bind("Delta Health Pool", "Chest Multiplier", 1.0f,
                "Damage multiplier applied to the shared pool for hits on the chest.");
            StomachMultiplier = Config.Bind("Delta Health Pool", "Stomach Multiplier", 1.0f,
                "Damage multiplier applied to the shared pool for hits on the stomach.");
            LimbMultiplier = Config.Bind("Delta Health Pool", "Limb Multiplier", 0.8f,
                "Damage multiplier applied to the shared pool for hits on arms and legs.");
            ShowHud = Config.Bind("Delta Health Pool", "Show HUD", true,
                "Draw a compact health-pool bar on screen.");
            HudX = Config.Bind("Delta Health Pool", "HUD X", 0.5f,
                "HUD horizontal center as a fraction of screen width (0 = left, 1 = right).");
            HudY = Config.Bind("Delta Health Pool", "HUD Y", 0.93f,
                "HUD vertical center as a fraction of screen height (0 = top, 1 = bottom).");
            HudWidth = Config.Bind("Delta Health Pool", "HUD Width", 320f,
                "Bar width in pixels at 1080p (scales with resolution).");

            try
            {
                _harmony = new Harmony("com.haavk.deltahealthpool");
                _harmony.PatchAll(typeof(ApplyDamagePatch));
                _harmony.PatchAll(typeof(ChangeHealthPatch));
                _harmony.PatchAll(typeof(DestroyPartPatch));
                Log.LogInfo("DeltaHealthPool: patched ActiveHealthController (ApplyDamage/ChangeHealth/TryToKillAfterDestroyPart)");
            }
            catch (Exception e)
            {
                Log.LogError("DeltaHealthPool: patch failed: " + e);
            }

            GameObject go = new GameObject("DeltaHealthPoolHud");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<HealthPoolHud>();

            Log.LogInfo("Delta Health Pool v1.0.0 loaded");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }

    /// <summary>
    /// Stores one shared HP pool per health controller. Uses a weak table so the pool is
    /// discarded automatically once the controller (and thus the raid) is gone.
    /// </summary>
    internal static class HealthPool
    {
        internal sealed class PoolState
        {
            public float Current;
            public float Max;
            public bool Initialized;
        }

        private static readonly ConditionalWeakTable<ActiveHealthController, PoolState> Table =
            new ConditionalWeakTable<ActiveHealthController, PoolState>();

        internal static float Base
        {
            get { return Mathf.Max(1f, DeltaHealthPoolPlugin.BasePool.Value); }
        }

        /// <summary>Returns the pool for a controller, initializing it from current body health.</summary>
        internal static PoolState Get(ActiveHealthController ctrl)
        {
            PoolState st = Table.GetValue(ctrl, k => new PoolState());
            if (!st.Initialized)
            {
                float max = Base;
                float current = max;
                try
                {
                    ValueStruct common = ctrl.GetBodyPartHealth(EBodyPart.Common);
                    if (common.Maximum > 0.0001f)
                        current = max * Mathf.Clamp01(common.Current / common.Maximum);
                }
                catch { }
                st.Max = max;
                st.Current = Mathf.Clamp(current, 0f, max);
                st.Initialized = true;
            }
            return st;
        }

        internal static float Multiplier(EBodyPart part)
        {
            switch (part)
            {
                case EBodyPart.Head:
                    return DeltaHealthPoolPlugin.HeadMultiplier.Value;
                case EBodyPart.Chest:
                    return DeltaHealthPoolPlugin.ChestMultiplier.Value;
                case EBodyPart.Stomach:
                    return DeltaHealthPoolPlugin.StomachMultiplier.Value;
                case EBodyPart.LeftArm:
                case EBodyPart.RightArm:
                case EBodyPart.LeftLeg:
                case EBodyPart.RightLeg:
                    return DeltaHealthPoolPlugin.LimbMultiplier.Value;
                default:
                    return 1f;
            }
        }

        internal static bool TryGetMain(out float current, out float max)
        {
            current = 0f;
            max = 0f;
            try
            {
                GameWorld gw = Singleton<GameWorld>.Instance;
                Player p = gw != null ? gw.MainPlayer : null;
                ActiveHealthController ctrl = p != null ? p.ActiveHealthController : null;
                if (ctrl == null) return false;
                if (!IsInRaid(gw, p)) return false;
                PoolState st = Get(ctrl);
                current = st.Current;
                max = st.Max;
                return true;
            }
            catch { return false; }
        }

        /// <summary>只在正式局内显示：藏身处、菜单等场景一律不画血条。</summary>
        private static bool IsInRaid(GameWorld gw, Player p)
        {
            if (gw == null || p == null || !p.IsYourPlayer) return false;
            if (string.Equals(gw.LocationId, "hideout", StringComparison.OrdinalIgnoreCase)) return false;
            AbstractGame game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            return game != null && game.Status == GameStatus.Started;
        }
    }

    [HarmonyPatch(typeof(ActiveHealthController), nameof(ActiveHealthController.ApplyDamage))]
    internal static class ApplyDamagePatch
    {
        // Initialize the pool before the body runs so the starting value reflects pre-hit state.
        [HarmonyPrefix]
        private static void Prefix(ActiveHealthController __instance)
        {
            if (!DeltaHealthPoolPlugin.Enabled.Value) return;
            HealthPool.Get(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(ActiveHealthController __instance, EBodyPart bodyPart, DamageInfo damageInfo, float __result)
        {
            if (!DeltaHealthPoolPlugin.Enabled.Value) return;
            if (__result <= 0.0001f) return;   // friendly-fire ignore / already dead / no health loss
            if (!__instance.IsAlive) return;

            try
            {
                HealthPool.PoolState st = HealthPool.Get(__instance);
                st.Current -= __result * HealthPool.Multiplier(bodyPart);

                if (st.Current <= 0f)
                {
                    st.Current = 0f;
                    __instance.Kill(damageInfo.DamageType);
                }
            }
            catch (Exception e)
            {
                DeltaHealthPoolPlugin.Log.LogError("DeltaHealthPool ApplyDamage: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(ActiveHealthController), nameof(ActiveHealthController.ChangeHealth))]
    internal static class ChangeHealthPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ActiveHealthController __instance)
        {
            if (!DeltaHealthPoolPlugin.Enabled.Value) return;
            HealthPool.Get(__instance);
        }

        // Positive values are healing: restore the shared pool 1:1 with the health actually gained.
        [HarmonyPostfix]
        private static void Postfix(ActiveHealthController __instance, EBodyPart bodyPart, float value)
        {
            if (!DeltaHealthPoolPlugin.Enabled.Value) return;
            if (value <= 0f) return;
            if (!__instance.IsAlive) return;

            try
            {
                if (!__instance.BodyState.ContainsKey(bodyPart)) return;
                var part = __instance.BodyState[bodyPart];
                if (part == null || part.IsDestroyed) return;

                float healed = part.Health.LastDiff;
                if (healed <= 0f) return;

                HealthPool.PoolState st = HealthPool.Get(__instance);
                st.Current = Mathf.Min(st.Max, st.Current + healed);
            }
            catch (Exception e)
            {
                DeltaHealthPoolPlugin.Log.LogError("DeltaHealthPool ChangeHealth: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(ActiveHealthController), nameof(ActiveHealthController.TryToKillAfterDestroyPart))]
    internal static class DestroyPartPatch
    {
        // Blacking out the head or chest no longer kills; only the shared pool hitting zero does.
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !DeltaHealthPoolPlugin.Enabled.Value;
        }
    }

    internal class HealthPoolHud : MonoBehaviour
    {
        private Texture2D _white;
        private GUIStyle _label;

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!DeltaHealthPoolPlugin.Enabled.Value) return;
            if (!DeltaHealthPoolPlugin.ShowHud.Value) return;

            float cur, max;
            if (!HealthPool.TryGetMain(out cur, out max) || max <= 0f) return;

            EnsureAssets();

            float uiScale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.6f, 2f);
            float w = DeltaHealthPoolPlugin.HudWidth.Value * uiScale;
            float h = 14f * uiScale;
            float x = Mathf.Clamp01(DeltaHealthPoolPlugin.HudX.Value) * Screen.width - w * 0.5f;
            float y = Mathf.Clamp01(DeltaHealthPoolPlugin.HudY.Value) * Screen.height - h * 0.5f;

            float frac = Mathf.Clamp01(cur / max);
            Color fill = frac > 0.6f
                ? new Color(0.30f, 0.78f, 0.32f, 1f)
                : (frac > 0.3f ? new Color(0.92f, 0.75f, 0.22f, 1f) : new Color(0.88f, 0.24f, 0.20f, 1f));

            // Outer border / background.
            GUI.color = new Color(0f, 0f, 0f, 0.60f);
            GUI.DrawTexture(new Rect(x - 2f, y - 2f, w + 4f, h + 4f), _white);

            // Empty track.
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(x, y, w, h), _white);

            // Filled portion.
            GUI.color = fill;
            GUI.DrawTexture(new Rect(x, y, w * frac, h), _white);

            GUI.color = Color.white;

            _label.fontSize = Mathf.RoundToInt(13f * uiScale);
            string text = string.Format("HP  {0:0} / {1:0}", Mathf.Ceil(cur), Mathf.Ceil(max));
            Rect labelRect = new Rect(x, y - 19f * uiScale, w, 18f * uiScale);

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(labelRect.x + 1f, labelRect.y + 1f, labelRect.width, labelRect.height), text, _label);
            GUI.color = old;
            GUI.Label(labelRect, text, _label);
        }

        private void EnsureAssets()
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label);
                _label.alignment = TextAnchor.MiddleCenter;
                _label.fontStyle = FontStyle.Bold;
                _label.richText = false;
            }
        }

        private void OnDestroy()
        {
            if (_white != null) Destroy(_white);
        }
    }
}
