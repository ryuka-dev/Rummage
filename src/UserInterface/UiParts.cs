using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ryuka.Sulfur.Rummage.UserInterface
{
    /// <summary>Colours shared by Rummage's inventory UI, close to the game's own amber palette.</summary>
    internal static class UiTheme
    {
        public static readonly Color Field = new Color(0.07f, 0.05f, 0.03f, 0.9f);
        public static readonly Color Panel = new Color(0.03f, 0.02f, 0.01f, 0.92f);
        public static readonly Color Button = new Color(0.13f, 0.08f, 0.03f, 0.91f);
        public static readonly Color ButtonSelected = new Color(0.29f, 0.19f, 0.07f, 0.95f);
        public static readonly Color Text = new Color(0.96f, 0.77f, 0.42f, 1f);
        public static readonly Color TextMuted = new Color(0.72f, 0.52f, 0.21f, 0.9f);

        public const string TextHex = "#F4C46A";
    }

    /// <summary>Builds the small UI pieces Rummage uses, in code, without any prefab.</summary>
    internal static class UiParts
    {
        /// <summary>
        /// A text stretched over its parent. Every created text is added to
        /// <paramref name="registry"/> so its owner can swap the font on a language change.
        /// </summary>
        public static TextMeshProUGUI CreateText(
            RectTransform parent,
            string name,
            TMP_FontAsset font,
            float size,
            Color color,
            TextAlignmentOptions alignment,
            List<TextMeshProUGUI> registry,
            TextOverflowModes overflow = TextOverflowModes.Ellipsis)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = overflow;

            registry.Add(text);
            return text;
        }

        public static Button CreateButton(RectTransform parent, string name, TMP_FontAsset font, List<TextMeshProUGUI> registry, out TextMeshProUGUI label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Image background = go.GetComponent<Image>();
            background.color = UiTheme.Button;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.8f, 1f);
            colors.pressedColor = new Color(0.75f, 0.7f, 0.62f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            label = CreateText(rect, "Label", font, 15f, UiTheme.TextMuted, TextAlignmentOptions.Center, registry);
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = 15f;
            label.rectTransform.offsetMin = new Vector2(3f, 1f);
            label.rectTransform.offsetMax = new Vector2(-3f, -1f);
            return button;
        }

        public static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        public static void SetBottomLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>
        /// A clicked button stays selected in the event system otherwise, and a later Space or Enter
        /// would press it again.
        /// </summary>
        public static void ClearSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }
    }
}
