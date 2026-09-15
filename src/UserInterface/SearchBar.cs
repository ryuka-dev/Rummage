using System;
using System.Collections.Generic;
using System.Text;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.UI.Inventory;
using Ryuka.Sulfur.Rummage.Diagnostics;
using Ryuka.Sulfur.Rummage.Filter;
using Ryuka.Sulfur.Rummage.Game;
using Ryuka.Sulfur.Rummage.Localization;
using Ryuka.Sulfur.Rummage.Search;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ryuka.Sulfur.Rummage.UserInterface
{
    /// <summary>
    /// The search box, clear button and category buttons, placed just above the backpack grid.
    /// Built on the first inventory open, and again if the game destroys it with its parent.
    /// </summary>
    internal sealed class SearchBar : IDisposable
    {
        private const float RowHeight = 32f;
        private const float InputAreaPadding = 1f;
        private const float InputFontSize = 18f;
        private const float MinimumInputFontSize = 12f;
        private const float ButtonRowHeight = 26f;
        private const float SummaryHeight = 40f;
        private const float SummaryFontSize = 14f;
        private const float Gap = 4f;
        private const float MarginAboveGrid = 6f;
        private const float ClearButtonWidth = 72f;
        private const float MinimumWidth = 360f;
        private const int CharacterLimit = 64;

        private static readonly Vector3[] Corners = new Vector3[4];

        private readonly InventoryFilter filter;
        private readonly DiagnosticsConfig diagnostics;
        private readonly List<CategoryButton> categoryButtons = new List<CategoryButton>();
        private readonly List<TextMeshProUGUI> texts = new List<TextMeshProUGUI>();

        private GameObject root;
        private RectTransform rootRect;
        private TMP_InputField input;
        private RectTransform inputRect;
        private RectTransform clearRect;
        private TextMeshProUGUI placeholder;
        private TextMeshProUGUI clearLabel;
        private TextMeshProUGUI summary;
        private List<LocationCount> summaryCounts;

        private RectTransform placedFor;
        private Rect placedForRect;
        private Vector3 placedForPosition;
        private bool fontStale;
        private bool buildFailed;

        private sealed class CategoryButton
        {
            public ItemCategory Category;
            public RectTransform Rect;
            public Image Background;
            public TextMeshProUGUI Label;
        }

        public SearchBar(InventoryFilter filter, DiagnosticsConfig diagnostics)
        {
            this.filter = filter;
            this.diagnostics = diagnostics;
            filter.Changed += RefreshSelection;
        }

        public bool IsFocused
        {
            get { return input != null && input.isFocused; }
        }

        public void Tick(InventoryUI ui)
        {
            ItemGrid backpack = ui.PlayerBackpackGrid;
            if (backpack == null)
            {
                return;
            }

            RectTransform gridRect = backpack.RectTransform != null ? backpack.RectTransform : backpack.GetComponent<RectTransform>();
            if (gridRect == null || !(gridRect.parent is RectTransform))
            {
                return;
            }

            if (root == null && !TryBuild(ui, gridRect))
            {
                return;
            }

            if (!root.activeSelf)
            {
                root.SetActive(true);
            }

            if (fontStale)
            {
                fontStale = false;
                TMP_FontAsset font = SampleFont(ui);
                ApplyFont(font);
                LogFont(font);
            }

            Place(gridRect);
        }

        public void OnInventoryHidden()
        {
            Unfocus();
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void Unfocus()
        {
            if (input == null)
            {
                return;
            }

            if (input.isFocused)
            {
                input.DeactivateInputField();
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == input.gameObject)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        /// <summary>Re-reads every label; the font is re-sampled on the next frame the bar shows.</summary>
        public void RefreshTexts()
        {
            fontStale = true;

            if (root == null)
            {
                return;
            }

            placeholder.text = RummageText.Get("search.placeholder", "Search name, oil or effect");
            clearLabel.text = RummageText.Get("search.clear", "Clear");

            foreach (CategoryButton button in categoryButtons)
            {
                button.Label.text = CategoryLabel(button.Category);
            }

            RenderSummary();
        }

        public void Dispose()
        {
            filter.Changed -= RefreshSelection;

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }

            root = null;
        }

        private bool TryBuild(InventoryUI ui, RectTransform gridRect)
        {
            if (buildFailed)
            {
                return false;
            }

            try
            {
                categoryButtons.Clear();
                texts.Clear();

                root = new GameObject("Rummage_SearchBar", typeof(RectTransform), typeof(LayoutElement));
                root.GetComponent<LayoutElement>().ignoreLayout = true;
                rootRect = root.GetComponent<RectTransform>();
                rootRect.SetParent(gridRect.parent, false);
                rootRect.anchorMin = new Vector2(0.5f, 0.5f);
                rootRect.anchorMax = new Vector2(0.5f, 0.5f);
                rootRect.pivot = Vector2.zero;

                TMP_FontAsset font = SampleFont(ui);
                if (font == null)
                {
                    RummagePlugin.Log.LogWarning("No game font found to sample for the search bar; its text uses the TextMeshPro default font and may not render.");
                }

                LogFont(font);

                input = CreateInput(rootRect, font);
                inputRect = input.GetComponent<RectTransform>();

                Button clear = CreateButton(rootRect, "Clear", font, out clearLabel);
                clearRect = clear.GetComponent<RectTransform>();
                clear.onClick.AddListener(OnClearClicked);

                foreach (ItemCategory category in ItemCategories.Selectable)
                {
                    TextMeshProUGUI label;
                    Button button = CreateButton(rootRect, "Category_" + category, font, out label);
                    ItemCategory captured = category;
                    button.onClick.AddListener(() => OnCategoryClicked(captured));

                    categoryButtons.Add(new CategoryButton
                    {
                        Category = category,
                        Rect = button.GetComponent<RectTransform>(),
                        Background = button.GetComponent<Image>(),
                        Label = label,
                    });
                }

                // Overflow with auto-sizing: the line may wrap to two rows, and TMP's Ellipsis mode
                // would blank it entirely once a CJK line is taller than the rect.
                summary = CreateText(rootRect, "Summary", font, SummaryFontSize, Theme.TextMuted, TextAlignmentOptions.TopLeft, TextOverflowModes.Overflow);
                summary.textWrappingMode = TextWrappingModes.Normal;
                summary.enableAutoSizing = true;
                summary.fontSizeMin = 10f;
                summary.fontSizeMax = SummaryFontSize;

                input.SetTextWithoutNotify(filter.Text);
                placedFor = null;
                RefreshTexts();
                fontStale = false;
                RefreshSelection();
                return true;
            }
            catch (Exception ex)
            {
                buildFailed = true;
                RummagePlugin.Log.LogError("Could not build the search bar; searching and category buttons are unavailable this session. " + ex);

                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                }

                root = null;
                return false;
            }
        }

        private void Place(RectTransform gridRect)
        {
            RectTransform parent = (RectTransform)gridRect.parent;
            if (rootRect.parent != parent)
            {
                rootRect.SetParent(parent, false);
                placedFor = null;
            }

            if (placedFor == gridRect && placedForRect == gridRect.rect && placedForPosition == gridRect.localPosition)
            {
                return;
            }

            placedFor = gridRect;
            placedForRect = gridRect.rect;
            placedForPosition = gridRect.localPosition;

            gridRect.GetWorldCorners(Corners);
            Vector3 topLeft = parent.InverseTransformPoint(Corners[1]);
            Vector3 topRight = parent.InverseTransformPoint(Corners[2]);

            float width = Mathf.Max(MinimumWidth, topRight.x - topLeft.x);
            rootRect.sizeDelta = new Vector2(width, RowHeight + Gap + ButtonRowHeight + Gap + SummaryHeight);
            rootRect.localPosition = new Vector3(topLeft.x, topLeft.y + MarginAboveGrid, 0f);
            rootRect.SetAsLastSibling();

            Layout(width);

            if (diagnostics.LogSearchFilter.Value)
            {
                RummagePlugin.Log.LogInfo("[SearchBar] placed in '" + parent.name + "' above backpack grid "
                    + gridRect.rect.width.ToString("0") + "x" + gridRect.rect.height.ToString("0")
                    + " at (" + topLeft.x.ToString("0") + ", " + topLeft.y.ToString("0") + "), bar width " + width.ToString("0"));
            }
        }

        private void Layout(float width)
        {
            SetTopLeft(inputRect, 0f, 0f, width - ClearButtonWidth - Gap, RowHeight);
            SetTopLeft(clearRect, width - ClearButtonWidth, 0f, ClearButtonWidth, RowHeight);

            int count = categoryButtons.Count;
            float buttonWidth = (width - Gap * (count - 1)) / count;
            for (int i = 0; i < count; i++)
            {
                SetTopLeft(categoryButtons[i].Rect, i * (buttonWidth + Gap), RowHeight + Gap, buttonWidth, ButtonRowHeight);
            }

            SetTopLeft(summary.rectTransform, 0f, RowHeight + Gap + ButtonRowHeight + Gap, width, SummaryHeight);
        }

        /// <summary>Shows where the matches are; null hides the line.</summary>
        public void SetSummary(List<LocationCount> counts)
        {
            summaryCounts = counts;
            RenderSummary();
        }

        private void RenderSummary()
        {
            if (summary == null)
            {
                return;
            }

            if (summaryCounts == null)
            {
                summary.text = string.Empty;
                return;
            }

            if (summaryCounts.Count == 0)
            {
                summary.text = RummageText.Get("summary.none", "No matches");
                return;
            }

            StringBuilder text = new StringBuilder();
            foreach (LocationCount count in summaryCounts)
            {
                if (text.Length > 0)
                {
                    text.Append("  ·  ");
                }

                text.Append(count.Label).Append(" ×").Append(count.Quantity);
            }

            summary.text = text.ToString();
        }

        private void RefreshSelection()
        {
            foreach (CategoryButton button in categoryButtons)
            {
                bool selected = button.Category == filter.Category;
                button.Background.color = selected ? Theme.ButtonSelected : Theme.Button;
                button.Label.color = selected ? Theme.Text : Theme.TextMuted;
            }

            if (input != null && input.text != filter.Text)
            {
                input.SetTextWithoutNotify(filter.Text);
            }
        }

        private void OnClearClicked()
        {
            filter.Clear();
            Unfocus();
            ClearSelection();
        }

        private void OnCategoryClicked(ItemCategory category)
        {
            filter.SetCategory(category == filter.Category ? ItemCategory.All : category);
            ClearSelection();
        }

        /// <summary>
        /// A clicked button stays selected in the event system otherwise, and a later Space or Enter
        /// would press it again.
        /// </summary>
        private static void ClearSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        private static string CategoryLabel(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Oils:
                    return RummageText.Get("category.oils", "Oils");
                case ItemCategory.Scrolls:
                    return RummageText.Get("category.scrolls", "Scrolls");
                case ItemCategory.Weapons:
                    return RummageText.Get("category.weapons", "Weapons");
                case ItemCategory.Attachments:
                    return RummageText.Get("category.attachments", "Attachments");
                case ItemCategory.Consumables:
                    return RummageText.Get("category.consumables", "Consumables");
                default:
                    return RummageText.Get("category.all", "All");
            }
        }

        /// <summary>
        /// The game's font for the current language, from the same per-language cache its
        /// FontLocalizer components apply (I2 term "Font/&lt;style&gt;"). A font sampled from an
        /// arbitrary inventory text is not enough: some keep a Latin-only font in every language.
        /// </summary>
        private static TMP_FontAsset SampleFont(InventoryUI ui)
        {
            AsyncAssetLoading loading = StaticInstance<AsyncAssetLoading>.Instance;
            TMP_FontAsset[] cache = loading != null ? loading.fontCache : null;
            int index = (int)PerfectRandom.Sulfur.Core.FontStyle.RoundRegular;

            if (cache != null && index < cache.Length && cache[index] == null)
            {
                FontLocalizer.LoadFontCache();
            }

            TMP_FontAsset font = cache != null && index < cache.Length ? cache[index] : null;
            if (font != null)
            {
                return font;
            }

            TextMeshProUGUI sample = ui.sacrificeFeedbackText;
            return sample != null ? sample.font : null;
        }

        private void LogFont(TMP_FontAsset font)
        {
            if (diagnostics.LogSearchFilter.Value)
            {
                RummagePlugin.Log.LogInfo("[SearchBar] font: " + (font != null ? font.name : "<none>"));
            }
        }

        private void ApplyFont(TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }

            foreach (TextMeshProUGUI text in texts)
            {
                if (text != null)
                {
                    text.font = font;
                }
            }

            if (input != null)
            {
                // Through the field, so it re-measures its text and caret for the new font.
                float size = FitInputFontSize(font);
                input.fontAsset = font;
                input.pointSize = size;
            }
        }

        private TMP_InputField CreateInput(RectTransform parent, TMP_FontAsset font)
        {
            GameObject go = new GameObject("SearchInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            // TMP_InputField creates its caret and hooks its text component in OnEnable, and only if
            // the text component is already assigned by then. Wire everything while inactive.
            go.SetActive(false);

            Image background = go.GetComponent<Image>();
            background.color = Theme.Field;
            background.raycastTarget = true;

            SearchInputField field = go.AddComponent<SearchInputField>();
            field.targetGraphic = background;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = CharacterLimit;
            field.shouldHideMobileInput = true;
            field.shouldHideSoftKeyboard = true;
            field.onFocusSelectAll = false;
            field.resetOnDeActivation = false;
            field.restoreOriginalTextOnEscape = false;
            field.customCaretColor = true;
            field.caretColor = Theme.Text;
            field.caretWidth = 2;
            field.selectionColor = new Color(Theme.Text.r, Theme.Text.g, Theme.Text.b, 0.25f);
            field.navigation = new Navigation { mode = Navigation.Mode.None };

            GameObject area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            RectTransform areaRect = area.GetComponent<RectTransform>();
            areaRect.SetParent(rect, false);
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(8f, InputAreaPadding);
            areaRect.offsetMax = new Vector2(-8f, -InputAreaPadding);

            // Overflow, not Ellipsis: TMP draws nothing at all when the first line is taller than
            // the rect in the Ellipsis and Truncate modes, and CJK fonts have tall lines. The
            // viewport's RectMask2D clips the text instead.
            float size = FitInputFontSize(font);
            TextMeshProUGUI text = CreateText(areaRect, "Text", font, size, Theme.Text, TextAlignmentOptions.MidlineLeft, TextOverflowModes.Overflow);
            placeholder = CreateText(areaRect, "Placeholder", font, size, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, TextOverflowModes.Overflow);

            field.textViewport = areaRect;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.pointSize = size;
            field.onValueChanged.AddListener(filter.SetText);

            go.SetActive(true);
            return field;
        }

        /// <summary>
        /// The largest size up to <see cref="InputFontSize"/> whose line height fits the input area,
        /// from the font's own metrics. CJK fonts have much taller lines than the Latin one.
        /// </summary>
        private static float FitInputFontSize(TMP_FontAsset font)
        {
            float available = RowHeight - InputAreaPadding * 2f;
            if (font == null || font.faceInfo.pointSize <= 0f || font.faceInfo.lineHeight <= 0f)
            {
                return InputFontSize;
            }

            float lineHeightPerPoint = font.faceInfo.lineHeight / font.faceInfo.pointSize;
            return Mathf.Clamp(Mathf.Floor(available / lineHeightPerPoint), MinimumInputFontSize, InputFontSize);
        }

        private Button CreateButton(RectTransform parent, string name, TMP_FontAsset font, out TextMeshProUGUI label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Image background = go.GetComponent<Image>();
            background.color = Theme.Button;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.8f, 1f);
            colors.pressedColor = new Color(0.75f, 0.7f, 0.62f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            label = CreateText(rect, "Label", font, 15f, Theme.TextMuted, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = 15f;
            label.rectTransform.offsetMin = new Vector2(3f, 1f);
            label.rectTransform.offsetMax = new Vector2(-3f, -1f);
            return button;
        }

        private TextMeshProUGUI CreateText(
            RectTransform parent,
            string name,
            TMP_FontAsset font,
            float size,
            Color color,
            TextAlignmentOptions alignment,
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

            texts.Add(text);
            return text;
        }

        private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private static class Theme
        {
            public static readonly Color Field = new Color(0.07f, 0.05f, 0.03f, 0.9f);
            public static readonly Color Button = new Color(0.13f, 0.08f, 0.03f, 0.91f);
            public static readonly Color ButtonSelected = new Color(0.29f, 0.19f, 0.07f, 0.95f);
            public static readonly Color Text = new Color(0.96f, 0.77f, 0.42f, 1f);
            public static readonly Color TextMuted = new Color(0.72f, 0.52f, 0.21f, 0.9f);
        }
    }
}
