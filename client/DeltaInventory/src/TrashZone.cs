using System;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeltaInventory
{
    /// <summary>
    /// Feature 4: drag-to-discard (Delta behaviour). While an item is being dragged a red lane
    /// appears on the right edge of the screen with a trash can glyph painted procedurally.
    /// Releasing the item over the lane discards it through the vanilla discard interaction.
    ///
    /// The lane is transparent to raycasts and is only used as a hit rectangle, so it never
    /// interferes with the underlying inventory UI.
    /// </summary>
    internal static class TrashZone
    {
        private static TrashZoneOverlay _overlay;

        internal static void Show(ItemUiContext ui)
        {
            if (ui == null) return;
            if (DeltaInventoryPlugin.Instance == null) return;
            if (!DeltaInventoryPlugin.Enabled.Value || !DeltaInventoryPlugin.DragDiscardEnabled.Value) return;

            try
            {
                // Unity's == overload reports destroyed objects as null, so a stale overlay
                // (canvas rebuilt after a scene change) is transparently recreated.
                if (_overlay == null) _overlay = TrashZoneOverlay.Create(ui);
                if (_overlay == null) return;
                _overlay.SetVisible(true);
            }
            catch (Exception e)
            {
                DeltaInventoryPlugin.Log.LogError("DeltaInventory: trash zone show failed: " + e);
            }
        }

        internal static void Hide()
        {
            if (_overlay == null) return;
            _overlay.SetVisible(false);
        }

        internal static bool Contains(Vector2 screenPosition)
        {
            TrashZoneOverlay overlay = _overlay;
            return overlay != null && overlay.Visible && overlay.Contains(screenPosition);
        }
    }

    internal sealed class TrashZoneOverlay : MonoBehaviour
    {
        private const float StripMargin = 8f;
        private const float EdgeWidth = 6f;
        private const float IconSize = 56f;
        private const int IconTextureSize = 64;

        private static Sprite _iconSprite;

        private RectTransform _strip;
        private Image _panel;
        private Image _edge;
        private Image _icon;
        private Canvas _canvas;
        private bool _visible;
        private bool _hover;

        internal bool Visible => _visible;

        internal static TrashZoneOverlay Create(ItemUiContext ui)
        {
            Transform dragLayer = ui.DragLayer;
            Canvas canvas = dragLayer != null ? dragLayer.GetComponentInParent<Canvas>() : null;
            if (canvas == null) canvas = ui.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                DeltaInventoryPlugin.Log.LogWarning("DeltaInventory: no canvas found for the trash zone");
                return null;
            }

            var root = new GameObject("DeltaTrashZone", typeof(RectTransform), typeof(TrashZoneOverlay));
            root.layer = canvas.gameObject.layer;

            var rect = (RectTransform)root.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var overlay = root.GetComponent<TrashZoneOverlay>();
            overlay._canvas = canvas;
            overlay.Build();
            return overlay;
        }

        private void Build()
        {
            float width = DeltaInventoryPlugin.TrashZoneWidth != null
                ? DeltaInventoryPlugin.TrashZoneWidth.Value
                : 110f;
            width = Mathf.Max(40f, width);

            _strip = NewRect("Strip", transform,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(width, 0f), new Vector2(-StripMargin, 0f));
            _panel = _strip.gameObject.AddComponent<Image>();
            _panel.sprite = null;
            _panel.raycastTarget = false;

            RectTransform edge = NewRect("Edge", _strip,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(EdgeWidth, 0f), new Vector2(-2f, 0f));
            _edge = edge.gameObject.AddComponent<Image>();
            _edge.sprite = null;
            _edge.raycastTarget = false;

            RectTransform icon = NewRect("Icon", _strip,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(IconSize, IconSize), Vector2.zero);
            _icon = icon.gameObject.AddComponent<Image>();
            _icon.sprite = GetIconSprite();
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;

            ApplyColors();
            gameObject.SetActive(false);
        }

        private static RectTransform NewRect(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;

            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        internal void SetVisible(bool value)
        {
            if (_visible == value && gameObject.activeSelf == value) return;
            _visible = value;
            if (value) SetHover(false);
            gameObject.SetActive(value);
        }

        internal bool Contains(Vector2 screenPosition)
        {
            if (_strip == null) return false;
            Camera camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(_strip, screenPosition, camera);
        }

        /// <summary>Fallback hover tracking in case the drag methods are not reached.</summary>
        private void Update()
        {
            if (!_visible) return;
            if (!Input.GetMouseButton(0))
            {
                SetVisible(false);
                return;
            }
            SetHover(TrashZone.Contains(Input.mousePosition));
        }

        private void SetHover(bool value)
        {
            if (_hover == value) return;
            _hover = value;
            ApplyColors();
        }

        private void ApplyColors()
        {
            if (_panel != null)
                _panel.color = _hover
                    ? new Color(0.95f, 0.15f, 0.15f, 0.30f)
                    : new Color(0.85f, 0.10f, 0.10f, 0.14f);
            if (_edge != null)
                _edge.color = _hover
                    ? new Color(1f, 0.35f, 0.35f, 0.95f)
                    : new Color(1f, 0.20f, 0.20f, 0.55f);
            if (_icon != null)
                _icon.color = _hover
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0.75f);
        }

        /// <summary>Paints a trash can glyph into a texture once and caches the sprite.</summary>
        private static Sprite GetIconSprite()
        {
            if (_iconSprite != null) return _iconSprite;

            const int size = IconTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DeltaTrashIcon",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var transparent = new Color32(255, 235, 235, 0);
            var ink = new Color32(255, 235, 235, 255);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = transparent;

            // Unity textures store row 0 at the bottom, so larger y is higher on screen.
            // Lid handle.
            Fill(pixels, size, ink, 26, 38, 55, 60);
            // Lid.
            Fill(pixels, size, ink, 10, 54, 48, 54);
            // Body (slightly tapered towards the bottom).
            for (int y = 12; y <= 45; y++)
            {
                float t = (45f - y) / 33f;
                float halfWidth = 20f - 3f * t;
                for (int x = 0; x < size; x++)
                    if (Mathf.Abs(x + 0.5f - 32f) <= halfWidth) pixels[y * size + x] = ink;
            }
            // Three vertical slits, carved back out of the body.
            int[] slits = { 24, 32, 40 };
            for (int y = 16; y <= 42; y++)
            {
                for (int s = 0; s < slits.Length; s++)
                {
                    for (float offset = -1.2f; offset <= 1.2f; offset += 0.4f)
                    {
                        int x = Mathf.RoundToInt(slits[s] + offset);
                        if (x < 0 || x >= size) continue;
                        pixels[y * size + x] = transparent;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            _iconSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _iconSprite.name = "DeltaTrashIcon";
            return _iconSprite;
        }

        private static void Fill(Color32[] pixels, int size, Color32 color,
            int xMin, int xMax, int yMin, int yMax)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                if (y < 0 || y >= size) continue;
                for (int x = xMin; x <= xMax; x++)
                {
                    if (x < 0 || x >= size) continue;
                    pixels[y * size + x] = color;
                }
            }
        }
    }

    /// <summary>Reveals the lane as soon as a drag starts.</summary>
    [HarmonyPatch(typeof(ItemView), nameof(ItemView.OnBeginDrag))]
    internal static class TrashZoneBeginDragPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemUiContext ___ItemUiContext)
        {
            TrashZone.Show(___ItemUiContext);
        }
    }

    /// <summary>Hides the lane when the drag ends, whatever the outcome is.</summary>
    [HarmonyPatch(typeof(ItemView), nameof(ItemView.OnEndDrag))]
    internal static class TrashZoneEndDragPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            TrashZone.Hide();
        }
    }

    /// <summary>
    /// Releasing over the lane discards the item. Priority First (800) so this runs before the
    /// drag-swap patch (VeryHigh, 700): dropping on the lane is never a swap.
    /// </summary>
    [HarmonyPatch(typeof(ItemView), nameof(ItemView.ProcessItem))]
    [HarmonyPriority(Priority.First)]
    internal static class TrashZoneProcessPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemView __instance, DragItemContext dragItemContext,
            PointerEventData eventData, ItemUiContext ___ItemUiContext)
        {
            try
            {
                if (DeltaInventoryPlugin.Instance == null) return true;
                if (!DeltaInventoryPlugin.Enabled.Value) return true;
                if (!DeltaInventoryPlugin.DragDiscardEnabled.Value) return true;
                if (dragItemContext == null || eventData == null) return true;
                if (!TrashZone.Contains(eventData.position)) return true;

                dragItemContext.DragCancelled();
                TrashZone.Hide();

                if (DiscardItem(___ItemUiContext, dragItemContext, __instance))
                {
                    DeltaInventoryPlugin.Verbose("DeltaInventory: discarded '" + dragItemContext.Item.Name + "' via trash lane");
                    return false;
                }

                DeltaInventoryPlugin.Verbose("DeltaInventory: trash lane drop refused, falling back to vanilla");
                return true;
            }
            catch (Exception e)
            {
                DeltaInventoryPlugin.Log.LogError("DeltaInventory: trash lane exception: " + e);
                return true;
            }
        }

        /// <summary>
        /// Uses the vanilla discard interaction on the source context (an inventory context, never
        /// the drag context: interactions for EItemViewType.Drag are not mapped and would throw).
        /// </summary>
        private static bool DiscardItem(ItemUiContext ui, DragItemContext dragItemContext, ItemView source)
        {
            if (ui == null) return false;

            ItemContext context = dragItemContext.Source;
            if (context == null && source != null) context = source.ItemContext;
            if (context == null) return false;

            ContextInteractions<EItemInfoButton> interactions = ui.GetItemContextInteractions(context, null);
            if (interactions == null) return false;

            if (!interactions.IsInteractionAvailable(EItemInfoButton.Discard).Succeed) return false;
            return interactions.ExecuteInteraction(EItemInfoButton.Discard);
        }
    }
}
