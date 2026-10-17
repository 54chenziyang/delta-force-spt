using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using UnityEngine;

namespace DeltaStatusHud
{
    /// <summary>
    /// Delta-style status HUD: big health readout, per-body-part mini bars, energy/hydration,
    /// and a row of status effect badges (stim buffs, bleeding, fracture, ... debuffs).
    /// Rendered with IMGUI so it stays simple and independent of the game's UI framework.
    /// Only drawn while actually inside a raid (not in the hideout / menus).
    /// </summary>
    [BepInPlugin("com.haavk.deltastatushud", "Delta Status HUD", "1.1.0")]
    public class DeltaStatusHudPlugin : BaseUnityPlugin
    {
        internal static DeltaStatusHudPlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> Anchor;
        internal static ConfigEntry<float> Scale;
        internal static ConfigEntry<float> OffsetX;
        internal static ConfigEntry<float> OffsetY;
        internal static ConfigEntry<bool> ShowHpBar;
        internal static ConfigEntry<bool> ShowHpNumber;
        internal static ConfigEntry<bool> ShowBodyParts;
        internal static ConfigEntry<bool> ShowEnergyHydration;
        internal static ConfigEntry<bool> ShowEffects;
        internal static ConfigEntry<bool> ShowEffectTime;
        internal static ConfigEntry<bool> ShowOnlyInRaid;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("Delta Status HUD", "Enabled", true,
                "Master switch for the Delta-style status HUD.");
            Anchor = Config.Bind("Delta Status HUD", "Anchor", "BottomLeft",
                "Screen corner for the HUD: BottomLeft, BottomRight, TopLeft, TopRight.");
            Scale = Config.Bind("Delta Status HUD", "Scale", 1.0f,
                "Extra scale multiplier applied on top of the resolution scale.");
            OffsetX = Config.Bind("Delta Status HUD", "Offset X", 28f,
                "Horizontal padding from the anchored edge (at 1080p).");
            OffsetY = Config.Bind("Delta Status HUD", "Offset Y", 28f,
                "Vertical padding from the anchored edge (at 1080p). Raise this if the panel overlaps the vanilla bottom-left readout.");
            ShowHpBar = Config.Bind("Delta Status HUD", "Show HP Bar", true,
                "Draw the horizontal health bar.");
            ShowHpNumber = Config.Bind("Delta Status HUD", "Show HP Number", true,
                "Draw the current / max health numbers.");
            ShowBodyParts = Config.Bind("Delta Status HUD", "Show Body Parts", true,
                "Draw the 7 body-part mini bars.");
            ShowEnergyHydration = Config.Bind("Delta Status HUD", "Show Energy and Hydration", true,
                "Draw the energy and hydration bars.");
            ShowEffects = Config.Bind("Delta Status HUD", "Show Effects", true,
                "Draw the status effect badge row (buffs and debuffs).");
            ShowEffectTime = Config.Bind("Delta Status HUD", "Show Effect Time", true,
                "Show remaining seconds under each status effect badge.");
            ShowOnlyInRaid = Config.Bind("Delta Status HUD", "Only Show In Raid", true,
                "Hide the HUD outside of a raid (hideout, menus, loading).");

            GameObject go = new GameObject("DeltaStatusRenderer");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<DeltaStatusRenderer>();

            Log.LogInfo("Delta Status HUD v1.1.0 loaded");
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

        /// <summary>True only while the player is actually inside a raid.</summary>
        internal static bool IsInRaid()
        {
            try
            {
                AbstractGame game = Singleton<AbstractGame>.Instance;
                if (game != null && game.InRaid) return true;

                IBotGame bot = Singleton<IBotGame>.Instance;
                if (bot != null)
                {
                    int status = (int)bot.Status;
                    if (status != 0 && status != 5) return status != 6;
                }
            }
            catch { }
            return false;
        }
    }

    internal class DeltaStatusRenderer : MonoBehaviour
    {
        internal static DeltaStatusRenderer Instance;

        private static readonly Color ColGood = new Color(0.490f, 0.694f, 0.431f);
        private static readonly Color ColWarn = new Color(0.776f, 0.651f, 0.353f);
        private static readonly Color ColBad = new Color(0.765f, 0.294f, 0.247f);
        private static readonly Color ColText = new Color(0.800f, 0.812f, 0.784f);
        private static readonly Color ColDim = new Color(0.560f, 0.588f, 0.553f);
        private static readonly Color ColWater = new Color(0.42f, 0.60f, 0.78f);

        private const int PartCount = 7;
        private static readonly string[] PartLabels = { "头部", "胸部", "腹部", "左臂", "右臂", "左腿", "右腿" };

        private Texture2D _white;
        private readonly Dictionary<string, Texture2D> _icons = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> _iconMissing = new HashSet<string>();

        private GUIStyle _styleHpBig;
        private GUIStyle _styleHpSmall;
        private GUIStyle _stylePart;
        private GUIStyle _styleTiny;
        private GUIStyle _styleCaption;
        private float _styleScale = -1f;

        private bool _valid;
        private float _hpCur;
        private float _hpMax;
        private float _hpFrac;
        private float _ghostFrac;
        private float _energyFrac = 1f;
        private float _energyCur;
        private float _energyMax;
        private float _hydrationFrac = 1f;
        private float _hydrationCur;
        private float _hydrationMax;

        private readonly float[] _partFrac = new float[PartCount];
        private readonly bool[] _partDestroyed = new bool[PartCount];
        private readonly bool[] _partBroken = new bool[PartCount];

        private class EffectRow
        {
            public string Key;
            public string Label;
            public int Count;
            public Color Color;
            public float TimeLeft;
        }

        private readonly List<EffectRow> _effects = new List<EffectRow>();
        private readonly Dictionary<string, EffectRow> _effectLookup = new Dictionary<string, EffectRow>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!DeltaStatusHudPlugin.Enabled.Value)
            {
                _valid = false;
                return;
            }

            try
            {
                Snapshot();
            }
            catch (Exception e)
            {
                _valid = false;
                DeltaStatusHudPlugin.Log.LogError("DeltaStatusHud snapshot error: " + e);
            }

            if (_valid)
            {
                if (_ghostFrac < _hpFrac) _ghostFrac = _hpFrac;
                else _ghostFrac = Mathf.MoveTowards(_ghostFrac, _hpFrac, Time.unscaledDeltaTime * 0.45f);
            }
        }

        private void Snapshot()
        {
            if (DeltaStatusHudPlugin.ShowOnlyInRaid.Value && !DeltaStatusHudPlugin.IsInRaid())
            {
                _valid = false;
                return;
            }

            Player p = DeltaStatusHudPlugin.MainPlayer;
            if (p == null) { _valid = false; return; }

            IHealthController hc = p.HealthController;
            if (hc == null) { _valid = false; return; }

            ValueStruct total = hc.GetBodyPartHealth((EBodyPart)7, false);
            _hpCur = total.Current;
            _hpMax = total.Maximum;
            _hpFrac = total.Maximum > 0f ? Mathf.Clamp01(total.Current / total.Maximum) : 0f;

            ValueStruct en = hc.Energy;
            _energyCur = en.Current;
            _energyMax = en.Maximum;
            _energyFrac = en.Maximum > 0f ? Mathf.Clamp01(en.Current / en.Maximum) : 0f;
            ValueStruct hy = hc.Hydration;
            _hydrationCur = hy.Current;
            _hydrationMax = hy.Maximum;
            _hydrationFrac = hy.Maximum > 0f ? Mathf.Clamp01(hy.Current / hy.Maximum) : 0f;

            for (int i = 0; i < PartCount; i++)
            {
                EBodyPart bp = (EBodyPart)i;
                try
                {
                    ValueStruct v = hc.GetBodyPartHealth(bp, false);
                    _partFrac[i] = v.Maximum > 0f ? Mathf.Clamp01(v.Current / v.Maximum) : 0f;
                    _partDestroyed[i] = hc.IsBodyPartDestroyed(bp);
                    _partBroken[i] = hc.IsBodyPartBroken(bp);
                }
                catch
                {
                    _partFrac[i] = 0f;
                    _partDestroyed[i] = true;
                    _partBroken[i] = false;
                }
            }

            BuildEffects(hc);
            _valid = true;
        }

        private void BuildEffects(IHealthController hc)
        {
            _effects.Clear();
            _effectLookup.Clear();

            IEnumerable<IHealthEffect> active = hc.GetAllActiveEffects();
            if (active == null) return;

            foreach (IHealthEffect effect in active)
            {
                if (effect == null) continue;

                IStimulator stim = effect as IStimulator;
                if (stim != null)
                {
                    IStimulatorBuff[] buffs = stim.ActiveBuffs;
                    if (buffs == null) continue;
                    for (int i = 0; i < buffs.Length; i++)
                    {
                        IStimulatorBuff buff = buffs[i];
                        if (buff == null || !buff.Active) continue;

                        bool isBuff = buff.Settings != null && buff.Settings.IsBuff;
                        string name = string.IsNullOrEmpty(buff.BuffName)
                            ? (isBuff ? "Buff" : "Debuff")
                            : buff.BuffName;
                        AddEffect(isBuff ? "buff" : "debuff", name,
                            isBuff ? ColGood : ColBad, effect.TimeLeft);
                    }
                    continue;
                }

                string key;
                string label;
                Color color;
                if (!ClassifyEffect(effect, out key, out label, out color)) continue;
                AddEffect(key, label, color, effect.TimeLeft);
            }
        }

        private void AddEffect(string key, string label, Color color, float timeLeft)
        {
            string dedup = key + "|" + label;
            EffectRow existing;
            if (_effectLookup.TryGetValue(dedup, out existing))
            {
                existing.Count++;
                if (timeLeft > existing.TimeLeft) existing.TimeLeft = timeLeft;
                return;
            }

            EffectRow row = new EffectRow
            {
                Key = key,
                Label = label,
                Count = 1,
                Color = color,
                TimeLeft = timeLeft
            };
            _effectLookup[dedup] = row;
            _effects.Add(row);
        }

        private static bool ClassifyEffect(IHealthEffect effect, out string key, out string label, out Color color)
        {
            key = null;
            label = null;
            color = ColWarn;

            if (effect is IHeavyBleeding) { key = "bleed_heavy"; label = "重出血"; color = ColBad; }
            else if (effect is ILightBleeding) { key = "bleed_light"; label = "轻出血"; color = ColBad; }
            else if (effect is IWound) { key = "freshWound"; label = "新伤"; color = ColBad; }
            else if (effect is IFracture) { key = "fracture"; label = "骨折"; color = ColBad; }
            else if (effect is IPainKiller) { key = "painkillers"; label = "止痛"; color = ColGood; }
            else if (effect is IPain) { key = "pain"; label = "疼痛"; color = ColWarn; }
            else if (effect is ITremor) { key = "handsTremor"; label = "手抖"; color = ColWarn; }
            else if (effect is IMildMusclePain) { key = "mildMusclePain"; label = "肌肉痛"; color = ColWarn; }
            else if (effect is ISevereMusclePain) { key = "severeMusclePain"; label = "重度肌痛"; color = ColBad; }
            else if (effect is IContusion) { key = "concussion"; label = "脑震荡"; color = ColWarn; }
            else if (effect is ITunnelVision) { key = "tunnelVision"; label = "管视"; color = ColWarn; }
            else if (effect is IDehydration) { key = "dehydration"; label = "脱水"; color = ColBad; }
            else if (effect is IExhaustion) { key = "energyFatigue"; label = "体力竭"; color = ColWarn; }
            else if (effect is IOverEncumbered) { key = "criticalOverweight"; label = "严重超重"; color = ColBad; }
            else if (effect is IEncumbered) { key = "overweight"; label = "超重"; color = ColWarn; }
            else if (effect is IChronicStaminaFatigue) { key = "fatigue"; label = "疲劳"; color = ColWarn; }
            else if (effect is IStun) { key = "stun"; label = "眩晕"; color = ColBad; }
            else if (effect is IFlash) { key = "flash"; label = "闪光"; color = ColBad; }
            else if (effect is IStimulatorBuff) { return false; }
            else if (effect is IBerserk) { key = "berserk"; label = "狂暴"; color = ColWarn; }
            else if (effect is ILethalIntoxication) { key = "poisoning"; label = "中毒"; color = ColBad; }
            else if (effect is IIntoxication) { key = "poisoning"; label = "中毒"; color = ColBad; }
            else if (effect is IZombieInfection) { key = "poisoning"; label = "感染"; color = ColBad; }
            else if (effect is IPanicEffect) { key = "panicAttack"; label = "恐慌"; color = ColBad; }
            else return false;

            return true;
        }

        private static Color HealthColor(float frac)
        {
            Color c;
            if (frac > 0.5f) c = Color.Lerp(ColWarn, ColGood, (frac - 0.5f) * 2f);
            else c = Color.Lerp(ColBad, ColWarn, frac * 2f);

            if (frac < 0.3f && frac > 0f)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f);
                c.a = pulse;
            }
            return c;
        }

        private void EnsureTextures()
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
                _white.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private Texture2D GetIcon(string key)
        {
            Texture2D tex;
            if (_icons.TryGetValue(key, out tex)) return tex;
            if (_iconMissing.Contains(key)) return null;

            try
            {
                string path = Path.Combine(BepInEx.Paths.PluginPath, "DeltaStatusHud", "Icons", key + ".png");
                if (!File.Exists(path))
                {
                    _iconMissing.Add(key);
                    return null;
                }
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.hideFlags = HideFlags.HideAndDontSave;
                if (!t.LoadImage(bytes))
                {
                    Destroy(t);
                    _iconMissing.Add(key);
                    return null;
                }
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Bilinear;
                _icons[key] = t;
                return t;
            }
            catch (Exception e)
            {
                DeltaStatusHudPlugin.Log.LogError("DeltaStatusHud icon load failed (" + key + "): " + e.Message);
                _iconMissing.Add(key);
                return null;
            }
        }

        private void EnsureStyles(float s)
        {
            if (_styleHpBig != null && Mathf.Abs(_styleScale - s) < 0.0001f) return;
            _styleScale = s;

            _styleHpBig = new GUIStyle(GUI.skin.label);
            _styleHpBig.fontSize = Mathf.RoundToInt(34f * s);
            _styleHpBig.fontStyle = FontStyle.Bold;
            _styleHpBig.alignment = TextAnchor.LowerLeft;
            _styleHpBig.padding = new RectOffset(0, 0, 0, 0);

            _styleHpSmall = new GUIStyle(GUI.skin.label);
            _styleHpSmall.fontSize = Mathf.RoundToInt(16f * s);
            _styleHpSmall.alignment = TextAnchor.LowerLeft;
            _styleHpSmall.padding = new RectOffset(0, 0, 0, Mathf.RoundToInt(4f * s));

            _stylePart = new GUIStyle(GUI.skin.label);
            _stylePart.fontSize = Mathf.RoundToInt(12f * s);
            _stylePart.alignment = TextAnchor.MiddleCenter;

            _styleTiny = new GUIStyle(GUI.skin.label);
            _styleTiny.fontSize = Mathf.RoundToInt(11f * s);
            _styleTiny.alignment = TextAnchor.MiddleCenter;

            _styleCaption = new GUIStyle(GUI.skin.label);
            _styleCaption.fontSize = Mathf.RoundToInt(12f * s);
            _styleCaption.fontStyle = FontStyle.Bold;
            _styleCaption.alignment = TextAnchor.MiddleLeft;
            _styleCaption.normal.textColor = ColDim;
        }

        private void DrawRect(Rect r, Color c)
        {
            if (c.a <= 0.002f) return;
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!DeltaStatusHudPlugin.Enabled.Value) return;
            if (!_valid) return;

            float uiScale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            if (uiScale < 0.4f) uiScale = 0.4f;
            float s = uiScale * Mathf.Max(0.2f, DeltaStatusHudPlugin.Scale.Value);

            EnsureTextures();
            EnsureStyles(s);

            string anchor = DeltaStatusHudPlugin.Anchor.Value;
            bool left = anchor != "BottomRight" && anchor != "TopRight";
            bool top = anchor == "TopLeft" || anchor == "TopRight";

            float padX = DeltaStatusHudPlugin.OffsetX.Value * uiScale;
            float padY = DeltaStatusHudPlugin.OffsetY.Value * uiScale;
            float panelW = 320f * s;
            float x = left ? padX : Screen.width - padX - panelW;

            float gap = 6f * s;
            float hpH = 18f * s;
            float numH = 46f * s;
            float partsH = 34f * s;
            float enH = 44f * s;
            float fxH = 38f * s;

            float yBar, yNum, yParts, yEn, yFx;
            if (!top)
            {
                float cursor = Screen.height - padY;
                yBar = cursor - hpH; cursor = yBar - gap;
                yNum = cursor - numH; cursor = yNum - gap;
                yParts = cursor - partsH; cursor = yParts - gap;
                yEn = cursor - enH; cursor = yEn - gap;
                yFx = cursor - fxH;
            }
            else
            {
                float cursor = padY;
                yFx = cursor; cursor += fxH + gap;
                yEn = cursor; cursor += enH + gap;
                yParts = cursor; cursor += partsH + gap;
                yNum = cursor; cursor += numH + gap;
                yBar = cursor;
            }

            // background panel keeps the readout legible over busy terrain / other HUD elements
            float topY = Mathf.Min(yFx, yBar);
            float bottomY = Mathf.Max(yFx + fxH, yBar + hpH);
            Rect bg = new Rect(x - 8f * s, topY - 8f * s, panelW + 16f * s, (bottomY - topY) + 16f * s);
            DrawRect(bg, new Color(0f, 0f, 0f, 0.42f));

            if (DeltaStatusHudPlugin.ShowEffects.Value && _effects.Count > 0)
                DrawEffects(new Rect(x, yFx, panelW, fxH), s);

            if (DeltaStatusHudPlugin.ShowEnergyHydration.Value)
                DrawEnergyHydration(new Rect(x, yEn, panelW, enH), s);

            if (DeltaStatusHudPlugin.ShowBodyParts.Value)
                DrawBodyParts(new Rect(x, yParts, panelW, partsH), s);

            if (DeltaStatusHudPlugin.ShowHpNumber.Value)
                DrawHpNumber(new Rect(x, yNum, panelW, numH), s);

            if (DeltaStatusHudPlugin.ShowHpBar.Value)
                DrawHpBar(new Rect(x, yBar, panelW, hpH));
        }

        private void DrawHpBar(Rect r)
        {
            DrawRect(r, new Color(0f, 0f, 0f, 0.6f));

            float gw = r.width * _ghostFrac;
            if (gw > 1f) DrawRect(new Rect(r.x, r.y, gw, r.height), new Color(1f, 1f, 1f, 0.35f));

            float fw = r.width * _hpFrac;
            if (fw > 1f) DrawRect(new Rect(r.x, r.y, fw, r.height), HealthColor(_hpFrac));
        }

        private void DrawHpNumber(Rect r, float s)
        {
            float capH = 15f * s;
            Rect capRect = new Rect(r.x, r.y, r.width, capH);
            GUI.Label(capRect, "生命", _styleCaption);

            Rect numRect = new Rect(r.x, r.y + capH, r.width, r.height - capH);
            int cur = Mathf.CeilToInt(_hpCur);
            int max = Mathf.CeilToInt(_hpMax);

            Color c = HealthColor(_hpFrac);
            _styleHpBig.normal.textColor = c;
            GUI.Label(numRect, cur.ToString(), _styleHpBig);

            float w = _styleHpBig.CalcSize(new GUIContent(cur.ToString())).x;
            Rect small = new Rect(numRect.x + w + 5f * s, numRect.y, numRect.width - w, numRect.height);
            _styleHpSmall.normal.textColor = new Color(ColDim.r, ColDim.g, ColDim.b, 0.95f);
            GUI.Label(small, "/ " + max, _styleHpSmall);
        }

        private void DrawBodyParts(Rect r, float s)
        {
            float cellW = r.width / PartCount;
            float labelH = 15f * s;
            float barH = 10f * s;
            float barY = r.y + labelH + 3f * s;

            for (int i = 0; i < PartCount; i++)
            {
                float cx = r.x + i * cellW;
                Rect labelRect = new Rect(cx, r.y, cellW, labelH);
                Rect barRect = new Rect(cx + 2f * s, barY, cellW - 4f * s, barH);

                Color fill = HealthColor(_partFrac[i]);
                if (_partDestroyed[i])
                {
                    fill = new Color(0.30f, 0.10f, 0.09f, 1f);
                    _stylePart.normal.textColor = ColBad;
                }
                else if (_partBroken[i])
                {
                    _stylePart.normal.textColor = ColWarn;
                }
                else
                {
                    _stylePart.normal.textColor = ColText;
                }

                GUI.Label(labelRect, PartLabels[i], _stylePart);

                DrawRect(barRect, new Color(0f, 0f, 0f, 0.6f));
                float fw = barRect.width * _partFrac[i];
                if (_partDestroyed[i]) fw = barRect.width;
                if (fw > 1f) DrawRect(new Rect(barRect.x, barRect.y, fw, barRect.height), fill);
            }
        }

        private void DrawEnergyHydration(Rect r, float s)
        {
            float rowH = r.height * 0.5f;
            DrawBarRow(new Rect(r.x, r.y, r.width, rowH), "能量", _energyFrac, _energyCur, _energyMax, ColGood, s);
            DrawBarRow(new Rect(r.x, r.y + rowH, r.width, rowH), "水分", _hydrationFrac, _hydrationCur, _hydrationMax, ColWater, s);
        }

        private void DrawBarRow(Rect r, string label, float frac, float cur, float max, Color color, float s)
        {
            float labelW = 40f * s;
            float capH = 14f * s;
            float barH = 8f * s;

            Rect labelRect = new Rect(r.x, r.y, labelW, capH);
            _styleCaption.alignment = TextAnchor.MiddleLeft;
            GUI.Label(labelRect, label, _styleCaption);

            string value = Mathf.CeilToInt(cur) + " / " + Mathf.CeilToInt(max);
            Rect valueRect = new Rect(r.x + r.width - 120f * s, r.y, 120f * s, capH);
            _styleTiny.alignment = TextAnchor.MiddleRight;
            _styleTiny.normal.textColor = ColText;
            GUI.Label(valueRect, value, _styleTiny);
            _styleTiny.alignment = TextAnchor.MiddleCenter;

            Rect bar = new Rect(r.x, r.y + capH + 2f * s, r.width, barH);
            DrawRect(bar, new Color(0f, 0f, 0f, 0.6f));
            float fw = bar.width * frac;
            if (fw > 1f) DrawRect(new Rect(bar.x, bar.y, fw, bar.height), color);
        }

        private void DrawEffects(Rect r, float s)
        {
            float cellW = 46f * s;
            float icon = 26f * s;
            float yIcon = r.y;
            int maxCols = Mathf.Max(1, Mathf.FloorToInt(r.width / cellW));

            for (int i = 0; i < _effects.Count && i < maxCols; i++)
            {
                EffectRow row = _effects[i];
                float cx = r.x + i * cellW;

                Rect iconRect = new Rect(cx + (cellW - icon) * 0.5f, yIcon, icon, icon);
                Texture2D tex = GetIcon(row.Key);
                if (tex != null)
                {
                    Color old = GUI.color;
                    GUI.color = row.Color;
                    GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit, true);
                    GUI.color = old;
                }
                else
                {
                    DrawRect(iconRect, new Color(row.Color.r, row.Color.g, row.Color.b, 0.85f));
                }

                if (row.Count > 1)
                {
                    Rect cntRect = new Rect(iconRect.x + icon * 0.55f, yIcon - 2f * s, icon, 12f * s);
                    GUIStyle st = _styleTiny;
                    st.normal.textColor = Color.white;
                    st.alignment = TextAnchor.UpperRight;
                    GUI.Label(cntRect, "x" + row.Count, st);
                    st.alignment = TextAnchor.MiddleCenter;
                }

                if (DeltaStatusHudPlugin.ShowEffectTime.Value && row.TimeLeft > 0.05f)
                {
                    Rect tRect = new Rect(cx, yIcon + icon, cellW, 12f * s);
                    _styleTiny.normal.textColor = ColDim;
                    GUI.Label(tRect, Mathf.CeilToInt(row.TimeLeft).ToString(), _styleTiny);
                }
            }
        }
    }
}
