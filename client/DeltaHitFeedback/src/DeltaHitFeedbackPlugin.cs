using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using HarmonyLib;
using UnityEngine;

namespace DeltaHitFeedback
{
    /// <summary>
    /// Delta-style damage direction feedback: when the main player takes damage, a thick red arc
    /// appears around the crosshair pointing toward the source, plus a short red screen-edge flash.
    /// </summary>
    [BepInPlugin("com.haavk.deltahitfeedback", "Delta Hit Feedback", "1.0.0")]
    public class DeltaHitFeedbackPlugin : BaseUnityPlugin
    {
        internal static DeltaHitFeedbackPlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> IndicatorRadius;
        internal static ConfigEntry<float> IndicatorAngle;
        internal static ConfigEntry<float> IndicatorLife;
        internal static ConfigEntry<string> IndicatorColor;
        internal static ConfigEntry<bool> ScreenFlash;
        internal static ConfigEntry<float> FlashIntensity;
        internal static ConfigEntry<float> FlashDuration;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("Delta Hit Feedback", "Enabled", true,
                "Master switch for the Delta-style damage direction indicator.");
            IndicatorRadius = Config.Bind("Delta Hit Feedback", "Indicator Radius", 150f,
                "Distance (at 1080p) from screen center to the direction arc.");
            IndicatorAngle = Config.Bind("Delta Hit Feedback", "Indicator Half Angle", 46f,
                "Half angular width of the arc, in degrees.");
            IndicatorLife = Config.Bind("Delta Hit Feedback", "Indicator Life", 1.3f,
                "Seconds for an indicator to fade out.");
            IndicatorColor = Config.Bind("Delta Hit Feedback", "Indicator Color", "#E23B2E",
                "Arc color as hex (RRGGBB).");
            ScreenFlash = Config.Bind("Delta Hit Feedback", "Screen Flash", true,
                "Flash the screen edges red when damaged.");
            FlashIntensity = Config.Bind("Delta Hit Feedback", "Screen Flash Intensity", 0.22f,
                "Maximum opacity of the screen edge flash (0-1).");
            FlashDuration = Config.Bind("Delta Hit Feedback", "Screen Flash Duration", 0.40f,
                "Seconds for the screen flash to fade.");

            try
            {
                _harmony = new Harmony("com.haavk.deltahitfeedback");
                _harmony.PatchAll(typeof(DamagePatch));
                Log.LogInfo("DeltaHitFeedback: patched Player.ApplyDamageInfo");
            }
            catch (Exception e)
            {
                Log.LogError("DeltaHitFeedback: patch failed: " + e);
            }

            GameObject go = new GameObject("DeltaHitFeedbackRenderer");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<DeltaHitRenderer>();

            Log.LogInfo("Delta Hit Feedback v1.0.0 loaded");
        }

        internal static Player MainPlayer
        {
            get
            {
                try
                {
                    GameWorld gw = Singleton<GameWorld>.Instance;
                    return gw != null ? gw.MainPlayer : null;
                }
                catch { return null; }
            }
        }

        internal static void Report(Vector3 dir, float damage)
        {
            if (!Enabled.Value) return;
            DeltaHitRenderer r = DeltaHitRenderer.Instance;
            if (r == null) return;
            r.OnDamaged(dir, damage);
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(Player), "ApplyDamageInfo")]
    internal static class DamagePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Player __instance, out bool __state)
        {
            __state = __instance.HealthController != null && __instance.HealthController.IsAlive;
        }

        [HarmonyPostfix]
        private static void Postfix(Player __instance, DamageInfo damageInfo, EBodyPart bodyPartType, bool __state)
        {
            if (!__state) return;
            try
            {
                Player main = DeltaHitFeedbackPlugin.MainPlayer;
                if (main == null) return;
                if (!ReferenceEquals(__instance, main)) return;

                IObserverToPlayerBridge bridge = damageInfo.Player;
                IPlayer src = bridge != null ? bridge.iPlayer : null;

                Vector3 dir;
                if (src != null && src.ProfileId != main.ProfileId)
                    dir = src.Position - main.Position;
                else
                    dir = -damageInfo.Direction;

                DeltaHitFeedbackPlugin.Report(dir, damageInfo.Damage);
            }
            catch (Exception e)
            {
                DeltaHitFeedbackPlugin.Log.LogError("DeltaHitFeedback hit error: " + e);
            }
        }
    }

    internal class DeltaHitRenderer : MonoBehaviour
    {
        internal static DeltaHitRenderer Instance;

        private const int MaxIndicators = 6;

        private struct Indicator
        {
            public Vector3 Direction;
            public float Age;
            public float Life;
            public float Strength;
            public bool Active;
        }

        private readonly Indicator[] _indicators = new Indicator[MaxIndicators];
        private float _flashAge = 999f;

        private Texture2D _arc;
        private Texture2D _vignette;
        private int _arcAngleKey = -1;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        internal void OnDamaged(Vector3 dir, float damage)
        {
            dir.y = 0f;

            int slot = -1;
            for (int i = 0; i < MaxIndicators; i++)
            {
                if (!_indicators[i].Active) { slot = i; break; }
            }
            if (slot < 0)
            {
                float oldest = -1f;
                for (int i = 0; i < MaxIndicators; i++)
                {
                    if (_indicators[i].Age > oldest) { oldest = _indicators[i].Age; slot = i; }
                }
            }

            _indicators[slot].Direction = dir;
            _indicators[slot].Age = 0f;
            _indicators[slot].Life = Mathf.Max(0.2f, DeltaHitFeedbackPlugin.IndicatorLife.Value);
            _indicators[slot].Strength = Mathf.Clamp01(0.65f + damage / 60f);
            _indicators[slot].Active = true;

            if (DeltaHitFeedbackPlugin.ScreenFlash.Value)
                _flashAge = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < MaxIndicators; i++)
            {
                if (!_indicators[i].Active) continue;
                _indicators[i].Age += dt;
                if (_indicators[i].Age >= _indicators[i].Life) _indicators[i].Active = false;
            }
            if (_flashAge < 900f) _flashAge += dt;
        }

        private bool AnyIndicator()
        {
            for (int i = 0; i < MaxIndicators; i++)
                if (_indicators[i].Active) return true;
            return false;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!DeltaHitFeedbackPlugin.Enabled.Value) return;

            bool anyIndicator = AnyIndicator();
            bool flash = DeltaHitFeedbackPlugin.ScreenFlash.Value
                && _flashAge < DeltaHitFeedbackPlugin.FlashDuration.Value;
            if (!anyIndicator && !flash) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            float uiScale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            if (uiScale < 0.4f) uiScale = 0.4f;

            EnsureTextures();

            if (flash) DrawFlash();
            if (anyIndicator) DrawIndicators(cam, uiScale);
        }

        private void DrawIndicators(Camera cam, float uiScale)
        {
            Vector3 up = Vector3.up;
            Vector3 camFwd = Vector3.ProjectOnPlane(cam.transform.forward, up);
            Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, up);
            if (camFwd.sqrMagnitude < 0.0001f) return;
            camFwd.Normalize();
            if (camRight.sqrMagnitude < 0.0001f) camRight = Vector3.Cross(up, camFwd).normalized * -1f;
            else camRight.Normalize();

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float radius = DeltaHitFeedbackPlugin.IndicatorRadius.Value * uiScale;
            Color col = ParseColor(DeltaHitFeedbackPlugin.IndicatorColor.Value,
                new Color(0.886f, 0.231f, 0.18f, 1f));

            for (int i = 0; i < MaxIndicators; i++)
            {
                if (!_indicators[i].Active) continue;
                Indicator ind = _indicators[i];
                if (ind.Direction.sqrMagnitude < 0.0001f) continue;

                Vector3 dirH = Vector3.ProjectOnPlane(ind.Direction, up);
                if (dirH.sqrMagnitude < 0.0001f) continue;

                float x = Vector3.Dot(dirH, camRight);
                float z = Vector3.Dot(dirH, camFwd);
                float angle = Mathf.Atan2(x, z) * Mathf.Rad2Deg;

                float t = Mathf.Clamp01(ind.Age / ind.Life);
                float fadeIn = Mathf.Clamp01(ind.Age / 0.05f);
                float alpha = ind.Strength * fadeIn * (1f - t * t);
                if (alpha <= 0.002f) continue;

                float punch = 1f + 0.15f * Mathf.Clamp01(1f - ind.Age / 0.15f);
                float draw = radius / 0.49f * punch;

                Matrix4x4 old = GUI.matrix;
                GUIUtility.RotateAroundPivot(angle, center);
                GUI.color = new Color(col.r, col.g, col.b, alpha);
                GUI.DrawTexture(new Rect(center.x - draw * 0.5f, center.y - draw * 0.5f, draw, draw), _arc);
                GUI.matrix = old;
                GUI.color = Color.white;
            }
        }

        private void DrawFlash()
        {
            float dur = Mathf.Max(0.05f, DeltaHitFeedbackPlugin.FlashDuration.Value);
            float t = Mathf.Clamp01(_flashAge / dur);
            float a = DeltaHitFeedbackPlugin.FlashIntensity.Value * (1f - t) * (1f - t);
            if (a <= 0.002f) return;

            GUI.color = new Color(0.82f, 0.05f, 0.04f, a);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _vignette);
            GUI.color = Color.white;
        }

        private void EnsureTextures()
        {
            int key = Mathf.RoundToInt(DeltaHitFeedbackPlugin.IndicatorAngle.Value * 2f);
            if (_arc == null || _arcAngleKey != key)
            {
                if (_arc != null) Destroy(_arc);
                _arc = BuildArc(256, Mathf.Clamp(DeltaHitFeedbackPlugin.IndicatorAngle.Value, 8f, 80f));
                _arcAngleKey = key;
            }
            if (_vignette == null) _vignette = BuildVignette(256);
        }

        private static Texture2D BuildArc(int size, float halfAngleDeg)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] px = new Color[size * size];
            Color clear = new Color(1f, 1f, 1f, 0f);
            float c = size * 0.5f;
            float outer = c * 0.98f;
            float maxThick = c * 0.22f;
            float half = halfAngleDeg;

            for (int y = 0; y < size; y++)
            {
                float uy = (y + 0.5f) - c;
                for (int x = 0; x < size; x++)
                {
                    float ux = (x + 0.5f) - c;
                    float r = Mathf.Sqrt(ux * ux + uy * uy);
                    float ang = Mathf.Atan2(ux, uy) * Mathf.Rad2Deg;
                    float aa = Mathf.Abs(ang);

                    if (aa > half || r > outer)
                    {
                        px[y * size + x] = clear;
                        continue;
                    }

                    float taper = 1f - Mathf.Pow(aa / half, 1.4f);
                    float thick = maxThick * taper;
                    if (thick < 0.6f)
                    {
                        px[y * size + x] = clear;
                        continue;
                    }
                    float inner = outer - thick;

                    float radial = Mathf.Min(r - inner, outer - r);
                    float alpha = Mathf.Clamp01(radial);
                    float angEdge = (half - aa) * Mathf.Deg2Rad * r;
                    alpha = Mathf.Min(alpha, Mathf.Clamp01(angEdge));

                    px[y * size + x] = alpha <= 0f ? clear : new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D BuildVignette(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            float maxR = Mathf.Sqrt(c * c + c * c);

            for (int y = 0; y < size; y++)
            {
                float dy = y - c;
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / maxR;
                    float a = Mathf.Pow(Mathf.Clamp01((r - 0.30f) / 0.70f), 1.8f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            Color c;
            if (ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return fallback;
        }
    }
}
