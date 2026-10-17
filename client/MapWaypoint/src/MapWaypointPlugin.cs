using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using UnityEngine;

namespace MapWaypoint
{
    /// <summary>
    /// Delta-style map waypoint: open the DynamicMaps map screen, click a spot on the map,
    /// and a waypoint is placed in the world. A HUD marker shows distance and, when off-screen,
    /// an edge arrow points toward it. Press Delete to clear.
    /// While the full screen map (or peek map) is open the mouse cursor is unlocked via the game's
    /// own cursor event, all player input (move / look / lean axes and fire / interact / weapon
    /// commands) is frozen so the map does not leak into character control, and Escape closes the
    /// map instead of opening the raid menu.
    /// </summary>
    [BepInPlugin("com.haavk.mapwaypoint", "MapWaypoint", "1.4.0")]
    public class MapWaypointPlugin : BaseUnityPlugin
    {
        internal static MapWaypointPlugin Instance;
        internal static ManualLogSource Log;

        /// <summary>Set every frame: true while the big map is open so all player input is frozen.</summary>
        internal static bool LockInput;

        private const string MapViewTypeName = "DynamicMaps.UI.Components.MapView";
        private const string PeekTypeName = "DynamicMaps.UI.Components.MapPeekComponent";
        private const string MapScreenTypeName = "DynamicMaps.UI.ModdedMapScreen";
        private const string DmPluginTypeName = "DynamicMaps.Plugin";
        private const string CursorControllerTypeName = "EFT.GlobalEvents.GlobalEventsController";
        private const string CursorEventTypeName = "EFT.GlobalEvents.ToggleShowInGameCursorEvent";

        private static readonly Color MarkerColor = new Color(0.20f, 0.95f, 1.00f, 1f);

        // --- MapView reflection (map zoom/rotation/map definition) ---
        private Type _mapViewType;
        private PropertyInfo _propRect;
        private PropertyInfo _propMapDef;

        // --- DynamicMaps singleton reflection (reliable component access) ---
        private bool _dmReady;
        private FieldInfo _dmPluginInstanceField;
        private FieldInfo _dmMapField;
        private PropertyInfo _dmMapViewProp;
        private FieldInfo _dmPeekField;
        private PropertyInfo _propIsPeeking;
        private PropertyInfo _propIsShowingScreen;

        // --- Cursor event reflection ---
        private bool _cursorReflReady;
        private PropertyInfo _gecInstanceProp;
        private MethodInfo _createCommonEventMethod;
        private Type _cursorEventType;
        private MethodInfo _cursorEventInvokeMethod;

        private Component _mapView;
        private Component _peek;
        private Component _mapScreen;
        private int _typeCooldown;
        private int _compCooldown;
        private int _lastCompState = -1;

        private bool _mouseDownOnMap;
        private Vector2 _mouseDownPos;

        private bool _hasWaypoint;
        private Vector2 _waypointMap;      // map coordinates (DynamicMaps map space == world x,z)
        private Vector3 _waypointWorld;    // world position used by the HUD marker

        private bool _cursorShown;
        private int _cursorReassert;

        private GameWorld _world;

        private Texture2D _arrow;
        private GUIStyle _labelStyle;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            try
            {
                new Harmony("com.haavk.mapwaypoint").PatchAll(typeof(MapWaypointPlugin).Assembly);
                Log.LogInfo("MapWaypoint v1.3.0 loaded (Delta-style map waypoint; cursor unlock + frozen look + Escape closes map)");
            }
            catch (Exception e)
            {
                Log.LogError("MapWaypoint: Harmony patch failed: " + e);
            }
        }

        private void Update()
        {
            try
            {
                ResetForNewRaid();

                // The DynamicMaps component tree only exists during a raid. Skipping all work
                // while in the menu/hideout avoids the costly Resources.FindObjectsOfTypeAll
                // scans (and their log spam) during main-menu loading.
                if (_world == null)
                {
                    LockInput = false;
                    SetMapCursor(false);
                    return;
                }

                if (_mapViewType == null)
                {
                    if (_typeCooldown-- > 0) return;
                    _typeCooldown = 120;
                    ResolveMapViewType();
                    if (_mapViewType == null) return;
                }

                if (_mapView == null || _mapScreen == null || _peek == null)
                {
                    if (_compCooldown-- > 0) return;
                    _compCooldown = 20;
                    FindComponents();
                }

                bool fullscreen = IsFullscreenMapVisible();

                LockInput = fullscreen;
                SetMapCursor(fullscreen);

                if (!fullscreen)
                {
                    _mouseDownOnMap = false;
                    return;
                }

                if (_mapView == null) return;

                if (Input.GetKeyDown(KeyCode.Delete))
                {
                    ClearWaypoint();
                }

                HandleMapClicks();
            }
            catch (Exception e)
            {
                Log.LogError("Update error: " + e);
            }
        }

        private void ResetForNewRaid()
        {
            GameWorld gw;
            try { gw = Singleton<GameWorld>.Instance; }
            catch { gw = null; }

            if (!ReferenceEquals(gw, _world))
            {
                _world = gw;
                _hasWaypoint = false;
                _mapView = null;
                _peek = null;
                _mapScreen = null;
                _compCooldown = 0;
                _lastCompState = -1;
                LockInput = false;
            }
        }

        private void ResolveMapViewType()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(MapViewTypeName, false); }
                catch { continue; }
                if (t == null) continue;

                _mapViewType = t;
                _propRect = t.GetProperty("RectTransform", BindingFlags.Public | BindingFlags.Instance);
                _propMapDef = t.GetProperty("CurrentMapDef", BindingFlags.Public | BindingFlags.Instance);

                Log.LogInfo("MapWaypoint: hooked DynamicMaps MapView (" + asm.GetName().Name + ")");
                return;
            }
        }

        /// <summary>
        /// Resolve the DynamicMaps singleton (Plugin.Instance.Map) and cache the reflection handles
        /// needed to reach the ModdedMapScreen's MapView and MapPeekComponent. This is far more
        /// reliable than scanning every loaded object, and survives the peek component being
        /// destroyed/recreated between raids.
        /// </summary>
        private void EnsureDynamicMapsReflection()
        {
            if (_dmReady) return;
            _dmReady = true;
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type pluginType = asm.GetType(DmPluginTypeName, false);
                    if (pluginType == null) continue;

                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    _dmPluginInstanceField = pluginType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                    _dmMapField = pluginType.GetField("Map", flags);

                    Type screenType = asm.GetType(MapScreenTypeName, false);
                    if (screenType != null)
                    {
                        _dmMapViewProp = screenType.GetProperty("MapView", flags);
                        _dmPeekField = screenType.GetField("_peekComponent", flags);
                        _propIsShowingScreen = screenType.GetProperty("IsShowingMapScreen", BindingFlags.Public | BindingFlags.Instance);
                    }

                    Type peekType = asm.GetType(PeekTypeName, false);
                    if (peekType != null)
                    {
                        _propIsPeeking = peekType.GetProperty("IsPeeking", BindingFlags.Public | BindingFlags.Instance);
                    }
                    return;
                }
            }
            catch (Exception e)
            {
                Log.LogError("MapWaypoint: DynamicMaps reflection error: " + e);
            }
        }

        private void FindComponents()
        {
            if (TryResolveViaPlugin())
            {
                ReportComponentState();
                return;
            }

            // Fallback: brute-force scan (only used if the DynamicMaps singleton is unavailable).
            if (_mapViewType == null) return;
            if (_mapView == null) _mapView = FindFirstActiveOrAny(_mapViewType);
            if (_mapScreen == null && _propIsShowingScreen != null) _mapScreen = FindFirstActiveOrAny(_propIsShowingScreen.DeclaringType);
            if (_peek == null && _propIsPeeking != null) _peek = FindFirstActiveOrAny(_propIsPeeking.DeclaringType);

            ReportComponentState();
        }

        private bool TryResolveViaPlugin()
        {
            EnsureDynamicMapsReflection();
            if (_dmPluginInstanceField == null || _dmMapField == null) return false;

            object plugin;
            try { plugin = _dmPluginInstanceField.GetValue(null); }
            catch { return false; }
            if (plugin == null) return false;

            object map;
            try { map = _dmMapField.GetValue(plugin); }
            catch { return false; }
            if (!(map is Component mapScreen)) return false;

            _mapScreen = mapScreen;

            if (_dmMapViewProp != null)
            {
                try
                {
                    if (_dmMapViewProp.GetValue(map) is Component mapView) _mapView = mapView;
                }
                catch { }
            }

            if (_dmPeekField != null)
            {
                try { _peek = _dmPeekField.GetValue(map) as Component; }
                catch { }
            }

            return _mapView != null;
        }

        private void ReportComponentState()
        {
            int state = (_mapView != null ? 1 : 0) | (_peek != null ? 2 : 0) | (_mapScreen != null ? 4 : 0);
            if (state == _lastCompState) return;
            _lastCompState = state;
            Log.LogInfo(string.Format("MapWaypoint: components mapView={0} peek={1} mapScreen={2}",
                _mapView != null, _peek != null, _mapScreen != null));
        }

        private static Component FindFirstActiveOrAny(Type t)
        {
            UnityEngine.Object[] objs;
            try { objs = Resources.FindObjectsOfTypeAll(t); }
            catch { return null; }
            if (objs == null) return null;

            Component fallback = null;
            foreach (UnityEngine.Object o in objs)
            {
                Component c = o as Component;
                if (c == null) continue;
                if (c.gameObject.activeInHierarchy) return c;
                if (fallback == null) fallback = c;
            }
            return fallback;
        }

        private static bool GetBool(PropertyInfo prop, object target)
        {
            if (prop == null || target == null) return false;
            try { return (bool)prop.GetValue(target); }
            catch { return false; }
        }

        /// <summary>
        /// True while the big map is on screen. The peek map sets IsPeeking; the map screen opened
        /// from the inventory sets IsShowingMapScreen. The mini-map keeps both false, so it is never
        /// treated as a full screen map.
        /// </summary>
        private bool IsFullscreenMapVisible()
        {
            if (GetBool(_propIsShowingScreen, _mapScreen)) return true;
            if (GetBool(_propIsPeeking, _peek)) return true;
            return false;
        }

        /// <summary>Show/hide the in-game cursor while the map is open (so the map can be clicked).</summary>
        private void SetMapCursor(bool mapOpen)
        {
            if (mapOpen)
            {
                // While the map is open, periodically re-raise the event. Anything that toggles the
                // cursor (closing another screen, etc.) would otherwise re-lock it mid-map.
                if (_cursorShown && --_cursorReassert > 0) return;
                _cursorReassert = 20;
                _cursorShown = true;
                RaiseCursorEvent(true);
                return;
            }

            if (!_cursorShown) return;
            _cursorShown = false;
            _cursorReassert = 0;
            RaiseCursorEvent(false);
        }

        private void EnsureCursorReflection()
        {
            if (_cursorReflReady) return;
            _cursorReflReady = true;
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type gec = asm.GetType(CursorControllerTypeName, false);
                    if (gec == null) continue;
                    Type evt = asm.GetType(CursorEventTypeName, false);
                    if (evt == null) continue;

                    _gecInstanceProp = gec.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    _createCommonEventMethod = gec.GetMethod("CreateCommonEvent", BindingFlags.Public | BindingFlags.Instance);
                    _cursorEventType = evt;
                    _cursorEventInvokeMethod = evt.GetMethod("Invoke", new[] { typeof(bool) });
                    return;
                }
            }
            catch (Exception e)
            {
                Log.LogError("MapWaypoint: cursor reflection error: " + e);
            }
        }

        /// <summary>
        /// Toggle the game cursor through the same event the game uses for the inventory screen.
        /// Writing Cursor.lockState/visible directly does not work because InputManager rewrites the
        /// cursor state every frame; this event flips its internal forceShowCursor flag instead.
        /// </summary>
        private void RaiseCursorEvent(bool show)
        {
            EnsureCursorReflection();
            if (_gecInstanceProp == null || _createCommonEventMethod == null || _cursorEventInvokeMethod == null)
            {
                Log.LogWarning("MapWaypoint: cursor event types unavailable, cursor not toggled");
                return;
            }

            try
            {
                object controller = _gecInstanceProp.GetValue(null, null);
                if (controller == null) return;

                MethodInfo generic = _createCommonEventMethod.MakeGenericMethod(_cursorEventType);
                object evt = generic.Invoke(controller, null);
                _cursorEventInvokeMethod.Invoke(evt, new object[] { show });
            }
            catch (Exception e)
            {
                Log.LogError("MapWaypoint: cursor event failed: " + e);
            }
        }

        private MethodInfo _endPeekMethod;
        private MethodInfo _closeMapScreenMethod;

        /// <summary>Close the currently open full screen map (or peek map). Returns true if something was closed.</summary>
        internal bool CloseOpenMap()
        {
            try
            {
                // The ESC patch can fire before Update() has resolved the components for this raid
                // (for example on the very first frame the map is open). Resolve on demand so ESC is
                // never missed.
                if (_mapScreen == null || _peek == null)
                {
                    FindComponents();
                }

                bool closed = false;

                if (_mapScreen != null && GetBool(_propIsShowingScreen, _mapScreen))
                {
                    if (_closeMapScreenMethod == null)
                    {
                        _closeMapScreenMethod = _mapScreen.GetType().GetMethod("OnMapScreenClose",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                    _closeMapScreenMethod?.Invoke(_mapScreen, null);
                    closed = true;
                }
                else if (_peek != null && GetBool(_propIsPeeking, _peek))
                {
                    if (_endPeekMethod == null)
                    {
                        _endPeekMethod = _peek.GetType().GetMethod("EndPeek",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                    _endPeekMethod?.Invoke(_peek, null);
                    closed = true;
                }

                if (closed)
                {
                    LockInput = false;
                    SetMapCursor(false);
                    Log.LogInfo("MapWaypoint: Escape closed the map");
                }
                return closed;
            }
            catch (Exception e)
            {
                Log.LogError("CloseOpenMap error: " + e);
                return false;
            }
        }

        private void HandleMapClicks()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _mouseDownOnMap = true;
                _mouseDownPos = Input.mousePosition;
            }

            if (!_mouseDownOnMap || !Input.GetMouseButtonUp(0)) return;

            _mouseDownOnMap = false;
            Vector2 up = Input.mousePosition;
            if ((up - _mouseDownPos).sqrMagnitude > 144f) return; // dragged, not a click

            TryPlaceWaypoint(up);
        }

        private void TryPlaceWaypoint(Vector2 screenPos)
        {
            RectTransform rect = _propRect?.GetValue(_mapView) as RectTransform;
            if (rect == null) return;

            Camera uiCam = GetUiCamera(rect);
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, uiCam, out local)) return;

            // The MapView rect's local space IS the map coordinate space: its markers are parented
            // under RectTransform with anchoredPosition == (world.x, world.z), and the rect itself
            // carries CoordinateRotation + zoom. So the local point is already a map coordinate and
            // must not be rotated again here.
            Vector2 mapCoord = local;

            if (!IsInsideMapBounds(mapCoord))
            {
                Log.LogInfo(string.Format("MapWaypoint: click outside map bounds map({0:F0},{1:F0})",
                    mapCoord.x, mapCoord.y));
                return;
            }

            _waypointMap = mapCoord;
            _waypointWorld = new Vector3(mapCoord.x, 0f, mapCoord.y);
            _hasWaypoint = true;
            Log.LogInfo(string.Format("MapWaypoint: waypoint map({0:F0},{1:F0}) -> world({2:F0},{3:F0})",
                mapCoord.x, mapCoord.y, _waypointWorld.x, _waypointWorld.z));
        }

        private bool IsInsideMapBounds(Vector2 mapCoord)
        {
            object mapDef = null;
            try { mapDef = _propMapDef?.GetValue(_mapView); }
            catch { }
            if (mapDef == null) return true;

            PropertyInfo propBounds = mapDef.GetType().GetProperty("Bounds", BindingFlags.Public | BindingFlags.Instance);

            object bounds = null;
            try { bounds = propBounds?.GetValue(mapDef); }
            catch { }
            if (bounds == null) return true;

            Type bt = bounds.GetType();
            object minObj, maxObj;
            try
            {
                minObj = bt.GetProperty("Min")?.GetValue(bounds);
                maxObj = bt.GetProperty("Max")?.GetValue(bounds);
            }
            catch { return true; }

            if (!(minObj is Vector2) || !(maxObj is Vector2)) return true;

            Vector2 min = (Vector2)minObj;
            Vector2 max = (Vector2)maxObj;
            const float pad = 10f;
            return mapCoord.x >= min.x - pad && mapCoord.x <= max.x + pad
                && mapCoord.y >= min.y - pad && mapCoord.y <= max.y + pad;
        }

        private static Camera GetUiCamera(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas.worldCamera;
        }

        private void ClearWaypoint()
        {
            if (!_hasWaypoint) return;
            _hasWaypoint = false;
            Log.LogInfo("MapWaypoint: waypoint cleared");
        }

        private void OnGUI()
        {
            if (!_hasWaypoint) return;

            try
            {
                GameWorld gw = Singleton<GameWorld>.Instance;
                Player player = gw != null ? gw.MainPlayer : null;
                if (player == null) return;

                Vector3 playerPos = ((IPlayer)player).Position;
                Vector3 delta = _waypointWorld - playerPos;
                delta.y = 0f;
                float dist = delta.magnitude;

                EnsureGui();

                if (IsFullscreenMapVisible())
                {
                    DrawMapOverlay();
                }

                Camera cam = Camera.main;
                if (cam == null) return;

                Vector3 sp = cam.WorldToScreenPoint(_waypointWorld);
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 gui = new Vector2(sp.x, Screen.height - sp.y);
                const float margin = 84f;

                string text = dist >= 1000f
                    ? (dist / 1000f).ToString("0.0") + "km"
                    : Mathf.RoundToInt(dist) + "m";

                bool behind = sp.z < 0f;
                bool onScreen = !behind
                    && gui.x >= margin && gui.x <= Screen.width - margin
                    && gui.y >= margin && gui.y <= Screen.height - margin;

                if (onScreen)
                {
                    DrawDiamond(gui, 16f);
                    DrawLabel(new Vector2(gui.x, gui.y + 24f), text);
                }
                else
                {
                    Vector2 d = gui - center;
                    if (behind) d = -d;
                    if (d.sqrMagnitude < 1f) d = new Vector2(0f, -1f);

                    float ang = Mathf.Atan2(d.y, d.x);
                    float rx = Screen.width * 0.5f - 60f;
                    float ry = Screen.height * 0.5f - 60f;
                    Vector2 edge = center + new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry);

                    Matrix4x4 old = GUI.matrix;
                    GUIUtility.RotateAroundPivot(ang * Mathf.Rad2Deg, edge);
                    float s = 40f;
                    GUI.color = MarkerColor;
                    GUI.DrawTexture(new Rect(edge.x - s * 0.5f, edge.y - s * 0.5f, s, s), _arrow);
                    GUI.matrix = old;
                    GUI.color = Color.white;

                    DrawLabel(new Vector2(edge.x, edge.y + 28f), text);
                }
            }
            catch (Exception e)
            {
                Log.LogError("OnGUI error: " + e);
            }
        }

        private void DrawMapOverlay()
        {
            if (_mapView == null) return;
            RectTransform rect = _propRect?.GetValue(_mapView) as RectTransform;
            if (rect == null) return;

            // The overlay diamond sits on the same map coordinate the click produced, so it uses the
            // MapView rect's local space directly (no extra rotation).
            Vector3 worldPoint = rect.TransformPoint(new Vector3(_waypointMap.x, _waypointMap.y, 0f));

            Camera uiCam = GetUiCamera(rect);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCam, worldPoint);

            DrawDiamond(new Vector2(screenPoint.x, Screen.height - screenPoint.y), 14f);
        }

        private static void DrawDiamond(Vector2 pos, float size)
        {
            float half = size * 0.5f;
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, pos);

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(pos.x - half - 2f, pos.y - half - 2f, size + 4f, size + 4f), Texture2D.whiteTexture);
            GUI.color = MarkerColor;
            GUI.DrawTexture(new Rect(pos.x - half, pos.y - half, size, size), Texture2D.whiteTexture);

            GUI.matrix = old;
            GUI.color = Color.white;
        }

        private void DrawLabel(Vector2 pos, string text)
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _labelStyle.normal.textColor = Color.white;
            }

            GUIContent content = new GUIContent(text);
            Vector2 size = _labelStyle.CalcSize(content);
            Rect bg = new Rect(
                pos.x - size.x * 0.5f - 6f,
                pos.y - size.y * 0.5f - 2f,
                size.x + 12f,
                size.y + 4f);

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(bg, text, _labelStyle);
        }

        private void EnsureGui()
        {
            if (_arrow == null) _arrow = BuildArrowTexture(40);
        }

        private static Texture2D BuildArrowTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            Color solid = Color.white;
            Color clear = new Color(1f, 1f, 1f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size; // 0 = left(base) .. 1 = right(tip)
                    float ny = (y + 0.5f) / size;
                    float halfHeight = (1f - nx) * 0.44f;
                    bool inside = nx >= 0.06f && nx <= 0.99f && Mathf.Abs(ny - 0.5f) <= halfHeight;
                    pixels[y * size + x] = inside ? solid : clear;
                }
            }

            tex.SetPixels(pixels);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return tex;
        }
    }

    /// <summary>
    /// In raid, pressing Escape closes the currently open DynamicMaps map screen instead of opening the
    /// game menu. If no map is open the patch does nothing and vanilla behaviour is preserved.
    /// </summary>
    [HarmonyPatch(typeof(EFT.EftGamePlayerOwner), "TranslateExitScreenInput")]
    internal static class PatchEscClosesMap
    {
        private static bool Prefix(ECommand command, ref bool __result)
        {
            if (!command.IsCommand(ECommand.Escape)) return true;

            MapWaypointPlugin plugin = MapWaypointPlugin.Instance;
            if (plugin == null) return true;
            if (!plugin.CloseOpenMap()) return true;

            __result = true; // consumed: the game will not open the raid menu
            return false;
        }
    }

    /// <summary>
    /// While the big map is open every input axis (move, rotate, look, lean) is zeroed and the
    /// original handler is skipped, so moving the mouse or pressing WASD does not move or turn the
    /// player. Skipping also covers the hands / player axis translators that hang off PlayerOwner.
    /// </summary>
    [HarmonyPatch(typeof(EFT.PlayerOwner), "TranslateAxes")]
    internal static class PatchFreezeAxes
    {
        private static bool Prefix(ref float[] axes)
        {
            if (!MapWaypointPlugin.LockInput) return true;
            if (axes != null)
            {
                for (int i = 0; i < axes.Length; i++)
                {
                    axes[i] = 0f;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// While the big map is open gameplay commands (fire, reload, switch weapon/mode, interact,
    /// lean, walk toggle, ...) are swallowed before they reach the move / hands / player input
    /// translators, so clicking the map cannot fire or interact in the world.
    /// Escape and Tab are handled by GamePlayerOwner before this method is reached, so the map can
    /// still be closed and the inventory still opens while the map is up.
    /// </summary>
    [HarmonyPatch(typeof(EFT.PlayerOwner), "TranslateCommand")]
    internal static class PatchBlockCommands
    {
        private static bool Prefix(ref InputNode.ETranslateResult __result)
        {
            if (!MapWaypointPlugin.LockInput) return true;
            __result = InputNode.ETranslateResult.Ignore;
            return false;
        }
    }
}
