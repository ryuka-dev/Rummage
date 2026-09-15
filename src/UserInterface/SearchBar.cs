using System;
using System.Collections.Generic;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.UI.Inventory;
using Ryuka.Sulfur.Rummage.Config;
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
    /// The search box, clear button and category buttons, placed just above the backpack grid, with
    /// the results panel above them. Built on the first inventory open, and again if the game
    /// destroys it with its parent.
    /// </summary>
    internal sealed class SearchBar : IDisposable
    {
        private const float RowHeight = 32f;
        private const float InputAreaPadding = 1f;
        private const float InputFontSize = 18f;
        private const float MinimumInputFontSize = 12f;
        private const float ButtonRowHeight = 26f;
        private const float Gap = 4f;
        private const float MarginAboveGrid = 6f;
        private const float ClearButtonWidth = 72f;
        private const float CheckboxIconInset = 3f;
        private const float CheckboxFallbackInset = 7f;
        private const float CheckboxIconOffAlpha = 0.45f;
        private const float TooltipFontSize = 14f;
        private const float MinimumWidth = 360f;
        private const int CharacterLimit = 64;

        private static readonly Vector3[] Corners = new Vector3[4];

        private readonly InventoryFilter filter;
        private readonly DiagnosticsConfig diagnostics;
        private readonly DisplayConfig display;
        private readonly SearchResultsPanel results = new SearchResultsPanel();
        private readonly List<CategoryButton> categoryButtons = new List<CategoryButton>();
        private readonly List<TextMeshProUGUI> texts = new List<TextMeshProUGUI>();

        private GameObject root;
        private RectTransform rootRect;
        private TMP_InputField input;
        private RectTransform inputRect;
        private RectTransform clearRect;
        private TextMeshProUGUI placeholder;
        private TextMeshProUGUI clearLabel;
        private RectTransform checkboxRect;
        private Image checkboxBackground;
        private Image checkboxIcon;
        private RectTransform tooltipRect;
        private TextMeshProUGUI tooltipText;
        private float barWidth;

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

        public SearchBar(InventoryFilter filter, DiagnosticsConfig diagnostics, DisplayConfig display)
        {
            this.filter = filter;
            this.diagnostics = diagnostics;
            this.display = display;
            filter.Changed += RefreshSelection;
            display.ResultsPlacement.SettingChanged += OnResultsPlacementChanged;
        }

        /// <summary>A changed setting takes effect on the next frame the bar is placed.</summary>
        private void OnResultsPlacementChanged(object sender, EventArgs e)
        {
            placedFor = null;
        }

        public bool IsFocused
        {
            get { return input != null && input.isFocused; }
        }

        /// <summary>True while the bar is on screen, which is when the focus shortcut may act.</summary>
        public bool IsVisible
        {
            get { return root != null && root.activeInHierarchy && input != null; }
        }

        /// <summary>Puts the caret in the search box, as clicking it would.</summary>
        public void FocusInput()
        {
            if (!IsVisible)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(input.gameObject);
            }

            input.ActivateInputField();
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

            results.SetShown(true);

            if (fontStale)
            {
                // Until the new language's font is applied, nothing may measure text: the previous
                // font's material may already be released.
                TMP_FontAsset font = SampleFont(ui);
                if (font != null)
                {
                    fontStale = false;
                    ApplyFont(font);
                    LogFont(font);
                }
            }

            Place(gridRect);
        }

        /// <summary>Per-place match counts from the presenter; null while no filter is active.</summary>
        public void SetSummary(List<LocationCount> counts)
        {
            results.SetCounts(counts);
        }

        public void OnInventoryHidden()
        {
            Unfocus();
            if (root != null)
            {
                root.SetActive(false);
            }

            results.SetShown(false);
            ShowTooltip(false);
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
            results.SetFontStale();
            results.RefreshTexts();

            if (root == null)
            {
                return;
            }

            placeholder.text = RummageText.Get("search.placeholder", "Search name, oil or effect");
            clearLabel.text = RummageText.Get("search.clear", "Clear");
            tooltipText.text = RummageText.Get("search.appliedOils.tooltip", "Also find weapons by the oils applied to them");

            foreach (CategoryButton button in categoryButtons)
            {
                button.Label.text = CategoryLabel(button.Category);
            }
        }

        public void Dispose()
        {
            filter.Changed -= RefreshSelection;
            display.ResultsPlacement.SettingChanged -= OnResultsPlacementChanged;
            results.Destroy();

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

                Button clear = UiParts.CreateButton(rootRect, "Clear", font, texts, out clearLabel);
                clearRect = clear.GetComponent<RectTransform>();
                clear.onClick.AddListener(OnClearClicked);

                BuildCheckbox(font);
                BuildTooltip(font);

                foreach (ItemCategory category in ItemCategories.Selectable)
                {
                    TextMeshProUGUI label;
                    Button button = UiParts.CreateButton(rootRect, "Category_" + category, font, texts, out label);
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

                results.Destroy();
                results.Build((RectTransform)gridRect.parent, font);

                input.SetTextWithoutNotify(filter.Text);
                placedFor = null;
                RefreshTexts();
                fontStale = false;
                results.ApplyFont(font);
                RefreshSelection();
                return true;
            }
            catch (Exception ex)
            {
                buildFailed = true;
                RummagePlugin.Log.LogError("Could not build the search bar; searching and category buttons are unavailable this session. " + ex);

                results.Destroy();
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
            float height = RowHeight + Gap + ButtonRowHeight;
            rootRect.sizeDelta = new Vector2(width, height);
            rootRect.localPosition = new Vector3(topLeft.x, topLeft.y + MarginAboveGrid, 0f);
            rootRect.SetAsLastSibling();

            Layout(width);
            float barBottom = topLeft.y + MarginAboveGrid;
            results.Place(topLeft.x, barBottom, barBottom + height, width, display.ResultsPlacement.Value);

            if (diagnostics.LogSearchFilter.Value)
            {
                RummagePlugin.Log.LogInfo("[SearchBar] placed in '" + parent.name + "' above backpack grid "
                    + gridRect.rect.width.ToString("0") + "x" + gridRect.rect.height.ToString("0")
                    + " at (" + topLeft.x.ToString("0") + ", " + topLeft.y.ToString("0") + "), bar width " + width.ToString("0"));
            }
        }

        private void Layout(float width)
        {
            barWidth = width;

            // One row: search box, then the applied-oils checkbox (a square as tall as the row), then Clear.
            UiParts.SetTopLeft(inputRect, 0f, 0f, width - ClearButtonWidth - Gap - RowHeight - Gap, RowHeight);
            UiParts.SetTopLeft(checkboxRect, width - ClearButtonWidth - Gap - RowHeight, 0f, RowHeight, RowHeight);
            UiParts.SetTopLeft(clearRect, width - ClearButtonWidth, 0f, ClearButtonWidth, RowHeight);

            int count = categoryButtons.Count;
            float buttonWidth = (width - Gap * (count - 1)) / count;
            for (int i = 0; i < count; i++)
            {
                UiParts.SetTopLeft(categoryButtons[i].Rect, i * (buttonWidth + Gap), RowHeight + Gap, buttonWidth, ButtonRowHeight);
            }
        }

        private void RefreshSelection()
        {
            foreach (CategoryButton button in categoryButtons)
            {
                bool selected = button.Category == filter.Category;
                button.Background.color = selected ? UiTheme.ButtonSelected : UiTheme.Button;
                button.Label.color = selected ? UiTheme.Text : UiTheme.TextMuted;
            }

            if (checkboxIcon != null)
            {
                // Unticked: a faded gun on the plain button. Ticked: a solid gun on a filled amber
                // square, so the grey icon stands out clearly.
                bool ticked = filter.IncludeAppliedEnchantments;
                checkboxBackground.color = ticked ? UiTheme.CheckboxFilled : UiTheme.Button;

                if (checkboxIcon.sprite != null)
                {
                    checkboxIcon.color = new Color(1f, 1f, 1f, ticked ? 1f : CheckboxIconOffAlpha);
                }
                else
                {
                    // Without the icon, the filled square alone shows the ticked state.
                    checkboxIcon.enabled = false;
                }
            }

            if (input != null && input.text != filter.Text)
            {
                input.SetTextWithoutNotify(filter.Text);
            }
        }

        private void BuildCheckbox(TMP_FontAsset font)
        {
            TextMeshProUGUI label;
            Button checkbox = UiParts.CreateButton(rootRect, "IncludeAppliedOils", font, texts, out label);
            label.text = string.Empty;
            checkboxRect = checkbox.GetComponent<RectTransform>();
            checkboxBackground = checkbox.GetComponent<Image>();

            // The gun icon, scaled to fit the square and never rotated. Should it fail to load, a
            // plain filled square marks the ticked state instead.
            Sprite gun = UiIcons.AppliedOilsGun;
            float inset = gun != null ? CheckboxIconInset : CheckboxFallbackInset;

            GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.SetParent(checkboxRect, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(inset, inset);
            iconRect.offsetMax = new Vector2(-inset, -inset);

            checkboxIcon = icon.GetComponent<Image>();
            checkboxIcon.sprite = gun;
            checkboxIcon.preserveAspect = true;
            checkboxIcon.raycastTarget = false;

            checkbox.onClick.AddListener(OnIncludeAppliedClicked);
            checkbox.gameObject.AddComponent<HoverTip>().Hover = ShowTooltip;
        }

        private void BuildTooltip(TMP_FontAsset font)
        {
            GameObject tooltip = new GameObject("Tooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tooltipRect = tooltip.GetComponent<RectTransform>();
            tooltipRect.SetParent(rootRect, false);
            Image background = tooltip.GetComponent<Image>();
            background.color = UiTheme.Panel;
            background.raycastTarget = false;

            // One line, never wrapped: a second line would fall under the results line below the bar.
            tooltipText = UiParts.CreateText(tooltipRect, "Text", font, TooltipFontSize, UiTheme.Text, TextAlignmentOptions.MidlineLeft, texts, TextOverflowModes.Overflow);
            tooltipText.textWrappingMode = TextWrappingModes.NoWrap;
            tooltipText.rectTransform.offsetMin = new Vector2(8f, 4f);
            tooltipText.rectTransform.offsetMax = new Vector2(-8f, -4f);

            tooltip.SetActive(false);
        }

        /// <summary>Shows the checkbox's explanation just under it, right-aligned to it.</summary>
        private void ShowTooltip(bool show)
        {
            if (tooltipRect == null)
            {
                return;
            }

            // Measuring needs the current language's font in place.
            if (!show || fontStale || string.IsNullOrEmpty(tooltipText.text))
            {
                tooltipRect.gameObject.SetActive(false);
                return;
            }

            Vector2 preferred = tooltipText.GetPreferredValues(tooltipText.text, float.PositiveInfinity, 0f);
            float width = preferred.x + 16f;
            float height = preferred.y + 8f;
            float right = barWidth - ClearButtonWidth - Gap;

            UiParts.SetTopLeft(tooltipRect, Mathf.Max(0f, right - width), RowHeight + 2f, width, height);
            tooltipRect.SetAsLastSibling();
            tooltipRect.gameObject.SetActive(true);
        }

        private void OnIncludeAppliedClicked()
        {
            filter.SetIncludeAppliedEnchantments(!filter.IncludeAppliedEnchantments);
            UiParts.ClearSelection();
        }

        private void OnClearClicked()
        {
            filter.Clear();
            Unfocus();
            UiParts.ClearSelection();
        }

        private void OnCategoryClicked(ItemCategory category)
        {
            filter.SetCategory(category == filter.Category ? ItemCategory.All : category);
            UiParts.ClearSelection();
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
                    return GameTerms.Get(GameTerms.Attachments) ?? RummageText.Get("category.attachments", "Attachments");
                case ItemCategory.Consumables:
                    return GameTerms.Get(GameTerms.Consumable) ?? RummageText.Get("category.consumables", "Consumables");
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
            TMP_FontAsset font = GameFonts.Current();
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

            results.ApplyFont(font);
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
            background.color = UiTheme.Field;
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
            field.caretColor = UiTheme.Text;
            field.caretWidth = 2;
            field.selectionColor = new Color(UiTheme.Text.r, UiTheme.Text.g, UiTheme.Text.b, 0.25f);
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
            TextMeshProUGUI text = UiParts.CreateText(areaRect, "Text", font, size, UiTheme.Text, TextAlignmentOptions.MidlineLeft, texts, TextOverflowModes.Overflow);
            placeholder = UiParts.CreateText(areaRect, "Placeholder", font, size, UiTheme.TextMuted, TextAlignmentOptions.MidlineLeft, texts, TextOverflowModes.Overflow);

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
    }
}
