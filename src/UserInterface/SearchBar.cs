using System;
using System.Collections.Generic;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.UI.Inventory;
using Ryuka.Sulfur.Rummage.Diagnostics;
using Ryuka.Sulfur.Rummage.Filter;
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
        private const float RowHeight = 30f;
        private const float ButtonRowHeight = 26f;
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
                ApplyFont(SampleFont(ui));
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
            rootRect.sizeDelta = new Vector2(width, RowHeight + Gap + ButtonRowHeight);
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
        /// The game swaps its font asset per language; an inventory text it manages carries the
        /// one for the current language.
        /// </summary>
        private static TMP_FontAsset SampleFont(InventoryUI ui)
        {
            TextMeshProUGUI sample = ui.sacrificeFeedbackText;
            return sample != null ? sample.font : null;
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
        }

        private TMP_InputField CreateInput(RectTransform parent, TMP_FontAsset font)
        {
            GameObject go = new GameObject("SearchInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Image background = go.GetComponent<Image>();
            background.color = Theme.Field;
            background.raycastTarget = true;

            TMP_InputField field = go.AddComponent<TMP_InputField>();
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
            areaRect.offsetMin = new Vector2(8f, 2f);
            areaRect.offsetMax = new Vector2(-8f, -2f);

            TextMeshProUGUI text = CreateText(areaRect, "Text", font, 18f, Theme.Text, TextAlignmentOptions.MidlineLeft);
            placeholder = CreateText(areaRect, "Placeholder", font, 18f, Theme.TextMuted, TextAlignmentOptions.MidlineLeft);

            field.textViewport = areaRect;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.onValueChanged.AddListener(filter.SetText);
            return field;
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

        private TextMeshProUGUI CreateText(RectTransform parent, string name, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
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
            text.overflowMode = TextOverflowModes.Ellipsis;

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
