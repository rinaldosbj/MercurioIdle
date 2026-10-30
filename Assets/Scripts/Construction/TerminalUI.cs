using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MercurioIdle.Construction
{
    public static class TerminalUI
    {
        public static readonly Color Foreground = new Color(0.91f, 0.91f, 0.91f, 1f);
        public static readonly Color Dim = new Color(0.565f, 0.565f, 0.565f, 1f);
        public static readonly Color Faint = new Color(0.235f, 0.235f, 0.235f, 1f);
        public static readonly Color PanelFill = new Color(0f, 0f, 0f, 0.94f);

        private static Font defaultFont;

        public static Font DefaultFont
        {
            get
            {
                if (defaultFont == null)
                {
                    defaultFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "IBM Plex Mono", "JetBrains Mono", "Consolas", "Menlo", "Courier New", "DejaVu Sans Mono" }, 16);
                    if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return defaultFont;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            EnsureEventSystem();

            var canvasObject = new GameObject(name);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            var eventSystem = EventSystem.current != null ? EventSystem.current : Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();

            var legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                legacy.enabled = false;
                Object.Destroy(legacy);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                var module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static void Stretch(RectTransform rect, float inset)
        {
            Anchor(rect, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));
        }

        public static Image CreateBox(Transform parent, string name, Color border, Color fill, out Image inner)
        {
            var outerRect = CreateRect(name, parent);
            var outer = outerRect.gameObject.AddComponent<Image>();
            outer.color = border;

            var innerRect = CreateRect("Fill", outerRect);
            Stretch(innerRect, 1f);
            inner = innerRect.gameObject.AddComponent<Image>();
            inner.color = fill;
            return outer;
        }

        public static Text CreateLabel(Transform parent, string name, string text, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var rect = CreateRect(name, parent);
            Stretch(rect, 0f);

            var label = rect.gameObject.AddComponent<Text>();
            label.font = DefaultFont;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = anchor;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static TerminalButton CreateButton(Transform parent, string name, string text, int size)
        {
            var border = CreateBox(parent, name, Foreground, Color.black, out var fill);
            var label = CreateLabel(border.transform, "Label", text, size, Foreground, TextAnchor.MiddleCenter, FontStyle.Bold);
            var button = border.gameObject.AddComponent<TerminalButton>();
            button.Setup(border, fill, label);
            return button;
        }
    }
}
